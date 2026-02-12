using Microsoft.EntityFrameworkCore;
using MultiDbSync.Domain.Entities;
using MultiDbSync.Domain.Interfaces;
using MultiDbSync.Infrastructure.Data;

namespace MultiDbSync.Infrastructure.Repositories;

public class Repository<T>(MultiDbContext context) : IRepository<T> where T : class
{
    protected readonly MultiDbContext _context = context;
    protected readonly DbSet<T> _dbSet = context.Set<T>();

    public virtual async Task<T?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _dbSet.FindAsync([id], cancellationToken);
    }

    public virtual async Task<IReadOnlyList<T>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _dbSet.ToListAsync(cancellationToken);
    }

    public virtual async Task<T> AddAsync(T entity, CancellationToken cancellationToken = default)
    {
        await _dbSet.AddAsync(entity, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
        return entity;
    }

    public virtual async Task UpdateAsync(T entity, CancellationToken cancellationToken = default)
    {
        _dbSet.Update(entity);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public virtual async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await GetByIdAsync(id, cancellationToken);
        if (entity is not null)
        {
            _dbSet.Remove(entity);
            await _context.SaveChangesAsync(cancellationToken);
        }
    }

    public virtual async Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await GetByIdAsync(id, cancellationToken) is not null;
    }
}

public class ProductRepository(MultiDbContext context)
    : Repository<Product>(context), IProductRepository
{
    public async Task<IReadOnlyList<Product>> GetByCategoryAsync(
        string category,
        CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Where(p => p.Category == category)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Product>> GetLowStockProductsAsync(
        int threshold,
        CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Where(p => p.StockQuantity <= threshold)
            .ToListAsync(cancellationToken);
    }

    public async Task<Product?> GetByNameAsync(
        string name,
        CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .FirstOrDefaultAsync(p => p.Name == name, cancellationToken);
    }
}

public class OrderRepository(MultiDbContext context)
    : Repository<Order>(context), IOrderRepository
{
    public async Task<IReadOnlyList<Order>> GetByCustomerIdAsync(
        Guid customerId,
        CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Where(o => o.CustomerId == customerId)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Order>> GetByStatusAsync(
        OrderStatus status,
        CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Where(o => o.Status == status)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Order>> GetRecentOrdersAsync(
        int count,
        CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .OrderByDescending(o => o.CreatedAt)
            .Take(count)
            .ToListAsync(cancellationToken);
    }
}

public class CustomerRepository(MultiDbContext context)
    : Repository<Customer>(context), ICustomerRepository
{
    public async Task<Customer?> GetByEmailAsync(
        string email,
        CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .FirstOrDefaultAsync(c => c.Email.Value == email, cancellationToken);
    }

    public async Task<IReadOnlyList<Customer>> SearchByNameAsync(
        string namePattern,
        CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Where(c => c.Name.Contains(namePattern))
            .ToListAsync(cancellationToken);
    }
}

public class DatabaseNodeRepository(MultiDbContext context)
    : Repository<DatabaseNode>(context), IDatabaseNodeRepository
{
    public async Task<DatabaseNode?> GetByIdAsync(
        string nodeId,
        CancellationToken cancellationToken = default)
    {
        return await _dbSet.FindAsync([nodeId], cancellationToken);
    }

    public override async Task<IReadOnlyList<DatabaseNode>> GetAllAsync(
        CancellationToken cancellationToken = default)
    {
        return await _dbSet.ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<DatabaseNode>> GetHealthyNodesAsync(
        CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Where(n => n.Status == NodeStatus.Healthy)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<DatabaseNode>> GetPrimaryNodesAsync(
        CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Where(n => n.IsPrimary)
            .ToListAsync(cancellationToken);
    }

    public new async Task AddAsync(DatabaseNode node, CancellationToken cancellationToken = default)
    {
        await _dbSet.AddAsync(node, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public new async Task UpdateAsync(DatabaseNode node, CancellationToken cancellationToken = default)
    {
        _dbSet.Update(node);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(string nodeId, CancellationToken cancellationToken = default)
    {
        var node = await GetByIdAsync(nodeId, cancellationToken);
        if (node is not null)
        {
            _dbSet.Remove(node);
            await _context.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task<bool> ExistsAsync(string nodeId, CancellationToken cancellationToken = default)
    {
        return await _dbSet.FindAsync([nodeId], cancellationToken) is not null;
    }
}

public class SyncOperationRepository(MultiDbContext context)
    : ISyncOperationRepository
{
    private readonly MultiDbContext _context = context;

    public async Task<SyncOperation?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        return await _context.SyncOperations.FindAsync([id], cancellationToken);
    }

    public async Task<IReadOnlyList<SyncOperation>> GetPendingOperationsAsync(
        CancellationToken cancellationToken = default)
    {
        return await _context.SyncOperations
            .Where(o => o.Status == SyncStatus.Pending || o.Status == SyncStatus.Failed)
            .OrderBy(o => o.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<SyncOperation>> GetOperationsByNodeIdAsync(
        string nodeId,
        CancellationToken cancellationToken = default)
    {
        return await _context.SyncOperations
            .Where(o => o.NodeId == nodeId)
            .OrderByDescending(o => o.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task AddAsync(
        SyncOperation operation,
        CancellationToken cancellationToken = default)
    {
        await _context.SyncOperations.AddAsync(operation, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(
        SyncOperation operation,
        CancellationToken cancellationToken = default)
    {
        _context.SyncOperations.Update(operation);
        await _context.SaveChangesAsync(cancellationToken);
    }
}
