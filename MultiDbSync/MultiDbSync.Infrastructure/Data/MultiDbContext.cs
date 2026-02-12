using Microsoft.EntityFrameworkCore;
using MultiDbSync.Domain.Entities;
using MultiDbSync.Domain.ValueObjects;

namespace MultiDbSync.Infrastructure.Data;

public class MultiDbContext(string connectionString) : DbContext
{
    public DbSet<Product> Products => Set<Product>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderItem> OrderItems => Set<OrderItem>();
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<DatabaseNode> DatabaseNodes => Set<DatabaseNode>();
    public DbSet<SyncOperation> SyncOperations => Set<SyncOperation>();

    private readonly string _connectionString = connectionString;

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.UseSqlite(_connectionString);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        foreach (var e in modelBuilder.Model.GetEntityTypes())
        {
            Console.WriteLine($"EF ENTITY: {e.Name}");
        }

        modelBuilder.Entity<Product>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
            entity.Property(e => e.Description).HasMaxLength(1000);
            entity.Property(e => e.Category).HasMaxLength(100);
            entity.Property(e => e.Price).HasConversion(
                v => $"{v.Amount}|{v.Currency}",
                v => new Money(decimal.Parse(v.Split('|')[0]), v.Split('|')[1]))
                .HasMaxLength(50);
        });

        modelBuilder.Entity<Order>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.TotalAmount).HasConversion(
                v => $"{v.Amount}|{v.Currency}",
                v => new Money(decimal.Parse(v.Split('|')[0]), v.Split('|')[1]))
                .HasMaxLength(50);
            entity.Property(e => e.ShippingAddress).HasConversion(
                v => $"{v.Street}|{v.City}|{v.State}|{v.PostalCode}|{v.Country}",
                v => new Address(v.Split('|')[0], v.Split('|')[1], v.Split('|')[2], v.Split('|')[3], v.Split('|')[4]))
                .HasMaxLength(200);
            entity.HasMany(e => e.Items)
                  .WithOne()
                  .HasForeignKey(e => e.OrderId);
        });

        modelBuilder.Entity<OrderItem>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.UnitPrice).HasConversion(
                v => $"{v.Amount}|{v.Currency}",
                v => new Money(decimal.Parse(v.Split('|')[0]), v.Split('|')[1]))
                .HasMaxLength(50);
        });

        modelBuilder.Entity<Customer>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
            entity.Property(e => e.Email).HasConversion(
                v => v.Value,
                v => new EmailAddress(v))
                .HasMaxLength(255);
        });

        modelBuilder.Entity<DatabaseNode>(entity =>
        {
            entity.HasKey(e => e.NodeId);
            entity.Property(e => e.ConnectionString).IsRequired();
            entity.Property(e => e.Status).HasConversion<string>();
        });

        modelBuilder.Entity<SyncOperation>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Type).HasConversion<string>();
            entity.Property(e => e.Status).HasConversion<string>();
        });
    }
}

public class MultiDbContextFactory
{
    private readonly string _baseConnectionString;
    private readonly string _databasePath;

    public MultiDbContextFactory(string baseConnectionString, string databasePath)
    {
        _baseConnectionString = baseConnectionString;
        _databasePath = databasePath;
    }

    public MultiDbContext CreateDbContext(string nodeId)
    {
        var dbPath = Path.Combine(_databasePath, $"{nodeId}.db");
        var connectionString = $"Data Source={dbPath}";
        return new MultiDbContext(connectionString);
    }
}
