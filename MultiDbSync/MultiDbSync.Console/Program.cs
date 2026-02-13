using MediatR;
using Microsoft.EntityFrameworkCore; // Required for EnsureCreatedAsync
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MultiDbSync.Application;
using MultiDbSync.Application.Commands;
using MultiDbSync.Application.Queries;
using MultiDbSync.Domain.Entities;
using MultiDbSync.Domain.Interfaces;
using MultiDbSync.Domain.ValueObjects;
using MultiDbSync.Infrastructure;
using MultiDbSync.Infrastructure.Data;
using Spectre.Console;

namespace MultiDbSync.Console;

internal class Program
{
    private static IServiceProvider? _serviceProvider;
    private static readonly string _databasePath = Path.Combine(AppContext.BaseDirectory, "databases");
    private static bool _isAutomated = false;

    static async Task<int> Main(string[] args)
    {
        _isAutomated = args.Any(a => a is "--demo" or "--automated" or "--ci");

        System.Console.Title = "MultiDbSync Demo";
        AnsiConsole.Write(new FigletText("MultiDbSync").Color(Color.Cyan1));

        try
        {
            if (!Directory.Exists(_databasePath))
            {
                Directory.CreateDirectory(_databasePath);
            }

            // 1. Setup Dependency Injection
            var services = new ServiceCollection();

            // Add Logging
            services.AddLogging(configure => configure.AddConsole().SetMinimumLevel(LogLevel.Warning));

            // Add Layers (These extension methods are in your Infrastructure/Application projects)
            services.AddInfrastructure();
            services.AddApplication();

            _serviceProvider = services.BuildServiceProvider();

            // 2. Initialize Databases
            await InitializeDatabaseAsync();

            // 3. Run Demo
            if (_isAutomated)
            {
                await RunCrudDemoAsync();
            }
            else
            {
                await RunCrudDemoAsync(); // Running just one demo for simplicity in this fix
            }

            return 0;
        }
        catch (Exception ex)
        {
            AnsiConsole.WriteException(ex);
            return 1;
        }
    }

    private static async Task InitializeDatabaseAsync()
    {
        using var scope = _serviceProvider!.CreateScope();

        // FIX: Request the concrete MultiDbContextFactory, not the interface
        // The interface IDbContextFactory doesn't have 'SetCurrentNodeId', but the concrete class does.
        var factory = scope.ServiceProvider.GetRequiredService<MultiDbContextFactory>();

        string[] nodes = ["node1", "node2", "node3"];

        await AnsiConsole.Status()
            .StartAsync("Initializing Database Nodes...", async ctx =>
            {
                foreach (var nodeId in nodes)
                {
                    ctx.Status($"Creating [bold]{nodeId}[/]...");

                    // FIX: This method exists on MultiDbContextFactory, not the interface
                    factory.SetCurrentNodeId(nodeId);

                    // We create a context *after* setting the node ID
                    var context = factory.CreateDbContext();
                    await context.Database.EnsureCreatedAsync();

                    if (!context.DatabaseNodes.Any())
                    {
                        var isPrimary = nodeId == "node1";
                        var connectionString = $"Data Source={Path.Combine(_databasePath, $"{nodeId}.db")}";

                        // FIX: Use the Constructor, not property setters (properties are private set)
                        var node = new DatabaseNode(
                            nodeId,
                            isPrimary,
                            connectionString,
                            priority: isPrimary ? 100 : 50
                        );

                        context.DatabaseNodes.Add(node);
                        await context.SaveChangesAsync();
                    }
                }
            });

        AnsiConsole.MarkupLine("[green]Database nodes initialized successfully![/]");
    }

    private static async Task RunCrudDemoAsync()
    {
        using var scope = _serviceProvider!.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();
        var factory = scope.ServiceProvider.GetRequiredService<MultiDbContextFactory>();

        // Ensure we are on the primary node
        factory.SetCurrentNodeId("node1");

        AnsiConsole.MarkupLine("[bold underline]CQRS Create & Read Demo[/]");

        // 1. Create Product
        var productName = "High-End Gaming Laptop";

        // FIX: Use the 'Money' Value Object defined in your Domain, not raw decimals
        var price = new Money(1500.00m, "USD");

        // FIX: Match the constructor signature of CreateProductCommand
        // (Name, Description, Sku, Price, WarehouseId)
        var createCommand = new CreateProductCommand(
            productName,
            "Powerful Laptop",
            "GAMING-001",
            price,
            "WH-NY-01"
        );

        AnsiConsole.MarkupLine($"Sending [cyan]CreateProductCommand[/]...");
        var result = await sender.Send(createCommand);

        if (result.IsSuccess)
        {
            AnsiConsole.MarkupLine($"[green]Success![/] Product ID: {result.Value}");
        }
        else
        {
            AnsiConsole.MarkupLine($"[red]Failed:[/] {result.Error}");
            return;
        }

        // 2. Update Stock
        // FIX: Ensure UpdateStockCommand parameters match (Guid, int, string)
        await sender.Send(new UpdateStockCommand(result.Value, 50, "Initial Stock"));

        // 3. Read (Query)
        var products = await sender.Send(new GetAllProductsQuery());

        var table = new Table();
        table.AddColumn("Name");
        table.AddColumn("Price");
        table.AddColumn("Stock");

        foreach (var p in products)
        {
            table.AddRow(
                p.Name,
                $"{p.Price.Amount} {p.Price.Currency}", // Access Money properties
                p.StockQuantity.ToString()
            );
        }

        AnsiConsole.Write(table);
    }
}
