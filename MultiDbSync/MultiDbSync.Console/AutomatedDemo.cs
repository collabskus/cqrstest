using Microsoft.Extensions.DependencyInjection;
using MultiDbSync.Application.Commands;
using MultiDbSync.Application.Queries;
using MultiDbSync.Domain.Entities;
using Spectre.Console;

namespace MultiDbSync.Console;

internal sealed class AutomatedDemo(IServiceProvider serviceProvider, DemoSettings settings)
{
    private readonly IServiceProvider _serviceProvider = serviceProvider;
    private readonly DemoSettings _settings = settings;

    public async Task RunAsync()
    {
        using var scope = _serviceProvider.CreateScope();
        var random = new Random(_settings.RandomSeed);

        AnsiConsole.MarkupLine("[bold underline cyan1]Automated CI/CD Demo - High Volume Data Operations[/]\n");

        // Phase 1: Bulk Product Creation
        await CreateProductsAsync(scope, random);

        // Phase 2: Query and Display Statistics
        var allProducts = await AnalyzeDataAsync(scope);
        if (allProducts is null) return;

        // Phase 3: Bulk Stock Updates
        await UpdateStockLevelsAsync(scope, allProducts, random);

        // Phase 4: Price Adjustments
        await UpdatePricesAsync(scope, allProducts, random);

        // Phase 5: Sample Deletions
        await RemoveDiscontinuedProductsAsync(scope, allProducts);

        // Final Statistics
        await DisplayFinalStatisticsAsync(scope, allProducts);

        AnsiConsole.MarkupLine("\n[bold green]✓ Automated demo completed successfully![/]");
        AnsiConsole.MarkupLine("[dim]All operations logged and synchronized across nodes.[/]");
    }

    private async Task CreateProductsAsync(IServiceScope scope, Random random)
    {
        var createProductHandler = scope.ServiceProvider.GetRequiredService<CreateProductCommandHandler>();

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
                var createTask = ctx.AddTask($"[yellow]Creating {_settings.ProductCount:N0} products[/]", maxValue: 100);

                for (var i = 0; i < _settings.ProductCount; i++)
                {
                    var adjective = ProductDataGenerator.AdjectivesArray[random.Next(ProductDataGenerator.AdjectivesArray.Length)];
                    var product = ProductDataGenerator.ProductsArray[random.Next(ProductDataGenerator.ProductsArray.Length)];
                    var category = ProductDataGenerator.CategoriesArray[random.Next(ProductDataGenerator.CategoriesArray.Length)];
                    var name = $"{adjective} {product} {i + 1}";
                    var price = Math.Round((decimal)(random.NextDouble() * 2000 + 10), 2);
                    var stock = random.Next(0, 500);

                    var cmd = new CreateProductCommand(
                        name,
                        $"High-quality {product.ToLower()} with advanced features",
                        price,
                        "USD",
                        stock,
                        category
                    );

                    await createProductHandler.HandleAsync(cmd);

                    if ((i + 1) % (_settings.ProductCount / 100) == 0 || i == _settings.ProductCount - 1)
                    {
                        var progress = (double)(i + 1) / _settings.ProductCount * 100;
                        createTask.Value = progress;
                    }
                }
            });

        AnsiConsole.MarkupLine($"[green]✓ Created {_settings.ProductCount:N0} products[/]\n");
    }

    private async Task<IReadOnlyList<Product>?> AnalyzeDataAsync(IServiceScope scope)
    {
        var getAllProductsHandler = scope.ServiceProvider.GetRequiredService<GetAllProductsQueryHandler>();

        AnsiConsole.MarkupLine("[bold yellow]Phase 2: Analyzing data...[/]");

        var result = await getAllProductsHandler.HandleAsync(new GetAllProductsQuery());

        if (!result.IsSuccess || result.Data is null)
        {
            AnsiConsole.MarkupLine($"[red]Failed to retrieve products: {result.ErrorMessage}[/]");
            return null;
        }

        var allProducts = result.Data;

        // Display statistics
        StatisticsDisplayHelper.DisplayDatabaseStatistics(allProducts);
        StatisticsDisplayHelper.DisplayCategoryBreakdown(allProducts);

        return allProducts;
    }

    private async Task UpdateStockLevelsAsync(IServiceScope scope, IReadOnlyList<Product> allProducts, Random random)
    {
        var updateStockHandler = scope.ServiceProvider.GetRequiredService<UpdateProductStockCommandHandler>();

        AnsiConsole.MarkupLine("\n[bold yellow]Phase 3: Performing bulk stock updates...[/]");

        await AnsiConsole.Progress()
            .AutoClear(false)
            .StartAsync(async ctx =>
            {
                var updateTask = ctx.AddTask("[yellow]Updating stock levels[/]", maxValue: _settings.StockUpdateCount);

                foreach (var product in allProducts.Take(_settings.StockUpdateCount))
                {
                    var newStock = random.Next(50, 200);
                    var updateCmd = new UpdateProductStockCommand(product.Id, newStock);
                    await updateStockHandler.HandleAsync(updateCmd);
                    updateTask.Increment(1);
                }
            });

        AnsiConsole.MarkupLine($"[green]✓ Updated {_settings.StockUpdateCount:N0} product stock levels[/]\n");
    }

    private async Task UpdatePricesAsync(IServiceScope scope, IReadOnlyList<Product> allProducts, Random random)
    {
        var updatePriceHandler = scope.ServiceProvider.GetRequiredService<UpdateProductPriceCommandHandler>();

        AnsiConsole.MarkupLine("[bold yellow]Phase 4: Adjusting prices...[/]");

        await AnsiConsole.Progress()
            .AutoClear(false)
            .StartAsync(async ctx =>
            {
                var priceTask = ctx.AddTask("[yellow]Applying price changes[/]", maxValue: _settings.PriceUpdateCount);

                foreach (var product in allProducts.Take(_settings.PriceUpdateCount))
                {
                    var newPrice = Math.Round(product.Price.Amount * (decimal)(random.NextDouble() * 0.4 + 0.8), 2);
                    var updateCmd = new UpdateProductPriceCommand(product.Id, newPrice, "USD");
                    await updatePriceHandler.HandleAsync(updateCmd);
                    priceTask.Increment(1);
                }
            });

        AnsiConsole.MarkupLine($"[green]✓ Updated {_settings.PriceUpdateCount:N0} product prices[/]\n");
    }

    private async Task RemoveDiscontinuedProductsAsync(IServiceScope scope, IReadOnlyList<Product> allProducts)
    {
        var deleteProductHandler = scope.ServiceProvider.GetRequiredService<DeleteProductCommandHandler>();

        AnsiConsole.MarkupLine("[bold yellow]Phase 5: Removing discontinued products...[/]");

        var toDelete = allProducts
            .Where(p => p.StockQuantity == 0)
            .Take(_settings.DeleteCount)
            .ToList();

        foreach (var product in toDelete)
        {
            var deleteCmd = new DeleteProductCommand(product.Id);
            await deleteProductHandler.HandleAsync(deleteCmd);
        }

        AnsiConsole.MarkupLine($"[green]✓ Removed {toDelete.Count} discontinued products[/]\n");
    }

    private async Task DisplayFinalStatisticsAsync(IServiceScope scope, IReadOnlyList<Product> allProducts)
    {
        var getAllProductsHandler = scope.ServiceProvider.GetRequiredService<GetAllProductsQueryHandler>();
        var finalResult = await getAllProductsHandler.HandleAsync(new GetAllProductsQuery());

        if (finalResult.IsSuccess && finalResult.Data is not null)
        {
            var finalProducts = finalResult.Data;

            // Display comparison
            StatisticsDisplayHelper.DisplayComparison(allProducts, finalProducts);

            // Display sample data
            StatisticsDisplayHelper.DisplayTopProducts(finalProducts);
        }
    }
}
