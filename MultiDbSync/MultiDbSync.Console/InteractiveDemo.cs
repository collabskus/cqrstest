using Microsoft.Extensions.DependencyInjection;
using MultiDbSync.Application.Commands;
using MultiDbSync.Application.Queries;
using Spectre.Console;

namespace MultiDbSync.Console;

internal sealed class InteractiveDemo(IServiceProvider serviceProvider)
{
    private readonly IServiceProvider _serviceProvider = serviceProvider;

    public async Task RunAsync()
    {
        using var scope = _serviceProvider.CreateScope();
        var createProductHandler = scope.ServiceProvider.GetRequiredService<CreateProductCommandHandler>();
        var getAllProductsHandler = scope.ServiceProvider.GetRequiredService<GetAllProductsQueryHandler>();

        AnsiConsole.MarkupLine("[bold underline cyan1]Interactive Demo[/]\n");

        // Create sample products
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
        await ProductDisplayHelper.DisplayProductsAsync(getAllProductsHandler);
    }
}
