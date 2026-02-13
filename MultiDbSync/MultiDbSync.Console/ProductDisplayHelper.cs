using MultiDbSync.Application.Queries;
using Spectre.Console;

namespace MultiDbSync.Console;

internal static class ProductDisplayHelper
{
    public static async Task DisplayProductsAsync(GetAllProductsQueryHandler handler)
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
