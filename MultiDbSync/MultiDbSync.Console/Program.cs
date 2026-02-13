using Microsoft.EntityFrameworkCore;
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
    internal static readonly string[] categoriesArray = ["Electronics", "Accessories", "Components", "Peripherals", "Software"];
    internal static readonly string[] adjectivesArray = ["Premium", "Budget", "Professional", "Gaming", "Wireless", "RGB", "Compact", "Ultra"];
    internal static readonly string[] productsArray = ["Laptop", "Monitor", "Keyboard", "Mouse", "Headset", "Webcam", "Microphone", "Cable"];

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

            // Add Layers
            services.AddInfrastructureServices(DatabasePath);
            services.AddApplicationServices();

            var serviceProvider = services.BuildServiceProvider();

            // 2. Initialize Databases
            await InitializeDatabaseAsync(serviceProvider);

            // 3. Run Demo
            if (_isAutomated)
            {
                await RunAutomatedDemoAsync(serviceProvider);
            }
            else
            {
                await RunInteractiveDemoAsync(serviceProvider);
            }

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
        var factory = scope.ServiceProvider.GetRequiredService<MultiDbContextFactory>();

        string[] nodes = ["node1", "node2", "node3"];

        await AnsiConsole.Status()
            .StartAsync("Initializing Database Nodes...", async ctx =>
            {
                foreach (var nodeId in nodes)
                {
                    ctx.Status($"Creating [bold]{nodeId}[/]...");

                    await using var context = factory.CreateDbContext(nodeId);

                    // Drop and recreate to ensure clean state
                    await context.Database.EnsureDeletedAsync();
                    await context.Database.EnsureCreatedAsync();

                    if (!context.DatabaseNodes.Any())
                    {
                        var isPrimary = nodeId == "node1";
                        var connectionString = $"Data Source={Path.Combine(DatabasePath, $"{nodeId}.db")}";

                        var node = new DatabaseNode(
                            nodeId,
                            connectionString,
                            isPrimary ? 100 : 50,
                            isPrimary
                        );

                        context.DatabaseNodes.Add(node);
                        await context.SaveChangesAsync();
                    }
                }
            });

        AnsiConsole.MarkupLine("[green]✓ Database nodes initialized successfully![/]\n");
    }

    private static async Task RunInteractiveDemoAsync(IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();
        var createProductHandler = scope.ServiceProvider.GetRequiredService<CreateProductCommandHandler>();
        var updateStockHandler = scope.ServiceProvider.GetRequiredService<UpdateProductStockCommandHandler>();
        var getAllProductsHandler = scope.ServiceProvider.GetRequiredService<GetAllProductsQueryHandler>();

        AnsiConsole.MarkupLine("[bold underline cyan1]Interactive Demo[/]\n");

        // Create a few products
        AnsiConsole.MarkupLine("[yellow]Creating sample products...[/]");

        var products = new[]
        {
            ("Gaming Laptop", "High-performance gaming laptop with RTX 4090", 2499.99m, "Electronics"),
            ("Wireless Mouse", "Ergonomic wireless mouse", 49.99m, "Accessories"),
            ("Mechanical Keyboard", "RGB mechanical keyboard", 149.99m, "Accessories"),
        };

        foreach (var (name, desc, price, category) in products)
        {
            var cmd = new CreateProductCommand(name, desc, price, "USD", 100, category);
            var result = await createProductHandler.HandleAsync(cmd);

            if (result.IsSuccess)
            {
                AnsiConsole.MarkupLine($"  [green]✓[/] Created: {name}");
            }
        }

        // Display results
        AnsiConsole.MarkupLine("\n[bold underline]Product Catalog:[/]");
        await DisplayProductsAsync(getAllProductsHandler);
    }

    private static async Task RunAutomatedDemoAsync(IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();
        var factory = scope.ServiceProvider.GetRequiredService<MultiDbContextFactory>();
        var createProductHandler = scope.ServiceProvider.GetRequiredService<CreateProductCommandHandler>();
        var updateStockHandler = scope.ServiceProvider.GetRequiredService<UpdateProductStockCommandHandler>();
        var updatePriceHandler = scope.ServiceProvider.GetRequiredService<UpdateProductPriceCommandHandler>();
        var getAllProductsHandler = scope.ServiceProvider.GetRequiredService<GetAllProductsQueryHandler>();
        var deleteProductHandler = scope.ServiceProvider.GetRequiredService<DeleteProductCommandHandler>();

        AnsiConsole.MarkupLine("[bold underline cyan1]Automated CI/CD Demo - High Volume Data Operations[/]\n");

        var random = new Random(42); // Fixed seed for reproducibility

        // Phase 1: Bulk Product Creation
        await AnsiConsole.Progress()
            .AutoClear(false)
            .Columns(
                new TaskDescriptionColumn(),
                new ProgressBarColumn(),
                new PercentageColumn(),
                new RemainingTimeColumn(),
                new SpinnerColumn())
            .StartAsync(async ctx =>
            {
                var categories = categoriesArray;
                var adjectives = adjectivesArray;
                var products = productsArray;

                var createTask = ctx.AddTask("[yellow]Creating 100 products[/]", maxValue: 100);

                for (int i = 0; i < 100; i++)
                {
                    var adjective = adjectives[random.Next(adjectives.Length)];
                    var product = products[random.Next(products.Length)];
                    var category = categories[random.Next(categories.Length)];
                    var name = $"{adjective} {product} {i + 1}";
                    var price = Math.Round((decimal)(random.NextDouble() * 2000 + 10), 2);
                    var stock = random.Next(0, 500);

                    var cmd = new CreateProductCommand(
                        name,
                        $"High-quality {product.ToLower()} for professional use",
                        price,
                        "USD",
                        stock,
                        category
                    );

                    await createProductHandler.HandleAsync(cmd);
                    createTask.Increment(1);
                }
            });

        AnsiConsole.MarkupLine("[green]✓ Created 100 products[/]\n");

        // Phase 2: Statistics
        var productsResult = await getAllProductsHandler.HandleAsync(new GetAllProductsQuery());

        if (productsResult.IsSuccess && productsResult.Data is not null)
        {
            var allProducts = productsResult.Data;

            var statsTable = new Table()
                .Border(TableBorder.Rounded)
                .BorderColor(Color.Grey);

            statsTable.AddColumn(new TableColumn("[bold]Metric[/]").Centered());
            statsTable.AddColumn(new TableColumn("[bold]Value[/]").Centered());

            statsTable.AddRow("Total Products", $"[cyan]{allProducts.Count}[/]");
            statsTable.AddRow("Total Stock Units", $"[cyan]{allProducts.Sum(p => p.StockQuantity):N0}[/]");
            statsTable.AddRow("Avg Price", $"[green]${allProducts.Average(p => p.Price.Amount):N2}[/]");
            statsTable.AddRow("Total Inventory Value", $"[green]${allProducts.Sum(p => p.Price.Amount * p.StockQuantity):N2}[/]");
            statsTable.AddRow("Categories", $"[yellow]{allProducts.Select(p => p.Category).Distinct().Count()}[/]");

            AnsiConsole.Write(
                new Panel(statsTable)
                    .Header("[bold cyan1]Database Statistics[/]")
                    .BorderColor(Color.Cyan1)
            );

            // Category breakdown
            AnsiConsole.MarkupLine("\n[bold underline]Products by Category:[/]");
            var categoryTable = new Table();
            categoryTable.AddColumn("Category");
            categoryTable.AddColumn("Count");
            categoryTable.AddColumn("Total Value");
            categoryTable.AddColumn("Avg Stock");

            var byCategory = allProducts
                .GroupBy(p => p.Category)
                .OrderByDescending(g => g.Count());

            foreach (var group in byCategory)
            {
                categoryTable.AddRow(
                    group.Key,
                    $"[cyan]{group.Count()}[/]",
                    $"[green]${group.Sum(p => p.Price.Amount * p.StockQuantity):N2}[/]",
                    $"{group.Average(p => p.StockQuantity):N0}"
                );
            }

            AnsiConsole.Write(categoryTable);

            // Phase 3: Bulk Updates
            AnsiConsole.MarkupLine("\n[bold yellow]Phase 3: Performing bulk stock updates...[/]");

            await AnsiConsole.Progress()
                .AutoClear(false)
                .StartAsync(async ctx =>
                {
                    var updateTask = ctx.AddTask("[yellow]Updating stock levels[/]", maxValue: 50);

                    foreach (var product in allProducts.Take(50))
                    {
                        var newStock = random.Next(50, 200);
                        var updateCmd = new UpdateProductStockCommand(product.Id, newStock);
                        await updateStockHandler.HandleAsync(updateCmd);
                        updateTask.Increment(1);
                    }
                });

            AnsiConsole.MarkupLine("[green]✓ Updated 50 product stock levels[/]\n");

            // Phase 4: Price adjustments
            AnsiConsole.MarkupLine("[bold yellow]Phase 4: Adjusting prices...[/]");

            await AnsiConsole.Progress()
                .AutoClear(false)
                .StartAsync(async ctx =>
                {
                    var priceTask = ctx.AddTask("[yellow]Applying price changes[/]", maxValue: 30);

                    foreach (var product in allProducts.Take(30))
                    {
                        var newPrice = Math.Round(product.Price.Amount * (decimal)(random.NextDouble() * 0.4 + 0.8), 2);
                        var updateCmd = new UpdateProductPriceCommand(product.Id, newPrice, "USD");
                        await updatePriceHandler.HandleAsync(updateCmd);
                        priceTask.Increment(1);
                    }
                });

            AnsiConsole.MarkupLine("[green]✓ Updated 30 product prices[/]\n");

            // Phase 5: Sample deletions
            AnsiConsole.MarkupLine("[bold yellow]Phase 5: Removing discontinued products...[/]");

            var toDelete = allProducts
                .Where(p => p.StockQuantity == 0)
                .Take(5)
                .ToList();

            foreach (var product in toDelete)
            {
                var deleteCmd = new DeleteProductCommand(product.Id);
                await deleteProductHandler.HandleAsync(deleteCmd);
            }

            AnsiConsole.MarkupLine($"[green]✓ Removed {toDelete.Count} discontinued products[/]\n");

            // Final Statistics
            var finalResult = await getAllProductsHandler.HandleAsync(new GetAllProductsQuery());

            if (finalResult.IsSuccess && finalResult.Data is not null)
            {
                var finalProducts = finalResult.Data;

                var comparison = new Table()
                    .Border(TableBorder.Rounded)
                    .BorderColor(Color.Green);

                comparison.AddColumn("[bold]Metric[/]");
                comparison.AddColumn("[bold]Before[/]");
                comparison.AddColumn("[bold]After[/]");
                comparison.AddColumn("[bold]Change[/]");

                comparison.AddRow(
                    "Total Products",
                    $"{allProducts.Count}",
                    $"[cyan]{finalProducts.Count}[/]",
                    $"[red]{finalProducts.Count - allProducts.Count:+0;-#}[/]"
                );

                comparison.AddRow(
                    "Total Stock",
                    $"{allProducts.Sum(p => p.StockQuantity):N0}",
                    $"[cyan]{finalProducts.Sum(p => p.StockQuantity):N0}[/]",
                    $"[green]{finalProducts.Sum(p => p.StockQuantity) - allProducts.Sum(p => p.StockQuantity):+#,0;-#,0}[/]"
                );

                AnsiConsole.Write(
                    new Panel(comparison)
                        .Header("[bold green]Before & After Comparison[/]")
                        .BorderColor(Color.Green)
                );
            }

            // Sample data display
            AnsiConsole.MarkupLine("\n[bold underline]Sample Products (Top 10 by Value):[/]");
            var sampleTable = new Table();
            sampleTable.AddColumn("Name");
            sampleTable.AddColumn("Category");
            sampleTable.AddColumn("Price");
            sampleTable.AddColumn("Stock");
            sampleTable.AddColumn("Value");

            foreach (var p in finalResult.Data!.OrderByDescending(p => p.Price.Amount * p.StockQuantity).Take(10))
            {
                sampleTable.AddRow(
                    p.Name.Length > 30 ? p.Name[..27] + "..." : p.Name,
                    p.Category,
                    $"${p.Price.Amount:N2}",
                    p.StockQuantity.ToString(),
                    $"[green]${p.Price.Amount * p.StockQuantity:N2}[/]"
                );
            }

            AnsiConsole.Write(sampleTable);
        }

        AnsiConsole.MarkupLine("\n[bold green]✓ Automated demo completed successfully![/]");
        AnsiConsole.MarkupLine("[dim]All operations logged and synchronized across nodes.[/]");
    }

    private static async Task DisplayProductsAsync(GetAllProductsQueryHandler handler)
    {
        var result = await handler.HandleAsync(new GetAllProductsQuery());

        if (result.IsSuccess && result.Data is not null)
        {
            var table = new Table();
            table.AddColumn("Name");
            table.AddColumn("Category");
            table.AddColumn("Price");
            table.AddColumn("Stock");

            foreach (var p in result.Data)
            {
                table.AddRow(
                    p.Name,
                    p.Category,
                    $"${p.Price.Amount} {p.Price.Currency}",
                    p.StockQuantity.ToString()
                );
            }

            AnsiConsole.Write(table);
        }
        else
        {
            AnsiConsole.MarkupLine($"[red]Failed to get products:[/] {result.ErrorMessage}");
        }
    }
}
