using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MultiDbSync.Application;
using MultiDbSync.Application.Commands;
using MultiDbSync.Application.Queries;
using MultiDbSync.Domain.Entities;
using MultiDbSync.Infrastructure;
using MultiDbSync.Infrastructure.Data;
using Spectre.Console;

namespace MultiDbSync.Console;

internal sealed class Program(string[] args)
{
    private static readonly string DatabasePath = Path.Combine(AppContext.BaseDirectory, "databases");
    private readonly bool _isAutomated = args.Any(a => a is "--demo" or "--automated" or "--ci");

    static async Task<int> Main(string[] args)
    {
        var program = new Program(args);
        return await program.RunAsync();
    }

    private async Task<int> RunAsync()
    {
        System.Console.Title = "MultiDbSync Demo";
        AnsiConsole.Write(new FigletText("MultiDbSync").Color(Color.Cyan1));

        try
        {
            if (!Directory.Exists(DatabasePath))
            {
                Directory.CreateDirectory(DatabasePath);
            }

            // 1. Setup Dependency Injection
            var services = new ServiceCollection();

            // Add Logging
            services.AddLogging(configure => configure.AddConsole().SetMinimumLevel(LogLevel.Warning));

            // Add Layers (using the actual extension methods from your Infrastructure/Application projects)
            services.AddInfrastructureServices(DatabasePath);
            services.AddApplicationServices();

            var serviceProvider = services.BuildServiceProvider();

            // 2. Initialize Databases
            await InitializeDatabaseAsync(serviceProvider);

            // 3. Run Demo
            await RunCrudDemoAsync(serviceProvider);

            return 0;
        }
        catch (Exception ex)
        {
            AnsiConsole.WriteException(ex);
            return 1;
        }
    }

    private static async Task InitializeDatabaseAsync(IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();

        // Get the factory
        var factory = scope.ServiceProvider.GetRequiredService<MultiDbContextFactory>();

        string[] nodes = ["node1", "node2", "node3"];

        await AnsiConsole.Status()
            .StartAsync("Initializing Database Nodes...", async ctx =>
            {
                foreach (var nodeId in nodes)
                {
                    ctx.Status($"Creating [bold]{nodeId}[/]...");

                    // Create a context for this specific node
                    var context = factory.CreateDbContext(nodeId);
                    await context.Database.EnsureCreatedAsync();

                    if (!context.DatabaseNodes.Any())
                    {
                        var isPrimary = nodeId == "node1";
                        var connectionString = $"Data Source={Path.Combine(DatabasePath, $"{nodeId}.db")}";

                        // Use the correct constructor signature: (nodeId, connectionString, priority, isPrimary)
                        var node = new DatabaseNode(
                            nodeId,
                            connectionString,
                            isPrimary ? 100 : 50,
                            isPrimary
                        );

                        context.DatabaseNodes.Add(node);
                        await context.SaveChangesAsync();
                    }

                    await context.DisposeAsync();
                }
            });

        AnsiConsole.MarkupLine("[green]Database nodes initialized successfully![/]");
    }

    private async Task RunCrudDemoAsync(IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();
        var factory = scope.ServiceProvider.GetRequiredService<MultiDbContextFactory>();

        // Get the handler instances directly
        var createProductHandler = scope.ServiceProvider.GetRequiredService<CreateProductCommandHandler>();
        var updateStockHandler = scope.ServiceProvider.GetRequiredService<UpdateProductStockCommandHandler>();
        var getAllProductsHandler = scope.ServiceProvider.GetRequiredService<GetAllProductsQueryHandler>();

        // Use the primary node
        await using var context = factory.CreateDbContext("node1");

        AnsiConsole.MarkupLine("[bold underline]CQRS Create & Read Demo[/]");

        // 1. Create Product
        var productName = "High-End Gaming Laptop";

        // Match the constructor signature: (Name, Description, Price, Currency, StockQuantity, Category)
        var createCommand = new CreateProductCommand(
            productName,
            "Powerful Laptop",
            1500.00m,
            "USD",
            50,
            "Electronics"
        );

        AnsiConsole.MarkupLine($"Sending [cyan]CreateProductCommand[/]...");
        var result = await createProductHandler.HandleAsync(createCommand);

        if (result.IsSuccess && result.Data is not null)
        {
            AnsiConsole.MarkupLine($"[green]Success![/] Product ID: {result.Data.Id}");

            // 2. Update Stock
            var updateStockCommand = new UpdateProductStockCommand(
                result.Data.Id,
                75
            );
            await updateStockHandler.HandleAsync(updateStockCommand);

            // 3. Read (Query)
            var productsResult = await getAllProductsHandler.HandleAsync(new GetAllProductsQuery());

            if (productsResult.IsSuccess && productsResult.Data is not null)
            {
                var table = new Table();
                table.AddColumn("Name");
                table.AddColumn("Price");
                table.AddColumn("Stock");
                table.AddColumn("Category");

                foreach (var p in productsResult.Data)
                {
                    table.AddRow(
                        p.Name,
                        $"{p.Price.Amount} {p.Price.Currency}",
                        p.StockQuantity.ToString(),
                        p.Category
                    );
                }

                AnsiConsole.Write(table);
            }
            else
            {
                AnsiConsole.MarkupLine($"[red]Failed to get products:[/] {productsResult.ErrorMessage}");
            }
        }
        else
        {
            AnsiConsole.MarkupLine($"[red]Failed:[/] {result.ErrorMessage}");
        }
    }
}
