using MultiDbSync.Domain.Entities;
using Spectre.Console;

namespace MultiDbSync.Console;

internal static class StatisticsDisplayHelper
{
    public static void DisplayDatabaseStatistics(IReadOnlyList<Product> products)
    {
        var statsTable = new Table()
            .Border(TableBorder.Rounded)
            .BorderColor(Color.Grey);

        statsTable.AddColumn(new TableColumn("[bold]Metric[/]").Centered());
        statsTable.AddColumn(new TableColumn("[bold]Value[/]").Centered());

        statsTable.AddRow("Total Products", $"[cyan]{products.Count:N0}[/]");
        statsTable.AddRow("Total Stock Units", $"[cyan]{products.Sum(p => p.StockQuantity):N0}[/]");
        statsTable.AddRow("Avg Price", $"[green]${products.Average(p => p.Price.Amount):N2}[/]");
        statsTable.AddRow("Total Inventory Value", $"[green]${products.Sum(p => p.Price.Amount * p.StockQuantity):N2}[/]");
        statsTable.AddRow("Categories", $"[yellow]{products.Select(p => p.Category).Distinct().Count()}[/]");

        AnsiConsole.Write(
            new Panel(statsTable)
                .Header("[bold cyan1]Database Statistics[/]")
                .BorderColor(Color.Cyan1)
        );
    }

    public static void DisplayCategoryBreakdown(IReadOnlyList<Product> products)
    {
        AnsiConsole.MarkupLine("\n[bold underline]Products by Category:[/]");

        var categoryTable = new Table();
        categoryTable.AddColumn("Category");
        categoryTable.AddColumn("Count");
        categoryTable.AddColumn("Total Value");
        categoryTable.AddColumn("Avg Stock");

        var byCategory = products
            .GroupBy(p => p.Category)
            .OrderByDescending(g => g.Count());

        foreach (var group in byCategory)
        {
            categoryTable.AddRow(
                group.Key,
                $"[cyan]{group.Count():N0}[/]",
                $"[green]${group.Sum(p => p.Price.Amount * p.StockQuantity):N2}[/]",
                $"{group.Average(p => p.StockQuantity):N0}"
            );
        }

        AnsiConsole.Write(categoryTable);
    }

    public static void DisplayComparison(IReadOnlyList<Product> before, IReadOnlyList<Product> after)
    {
        var comparison = new Table()
            .Border(TableBorder.Rounded)
            .BorderColor(Color.Green);

        comparison.AddColumn("[bold]Metric[/]");
        comparison.AddColumn("[bold]Before[/]");
        comparison.AddColumn("[bold]After[/]");
        comparison.AddColumn("[bold]Change[/]");

        comparison.AddRow(
            "Total Products",
            $"{before.Count:N0}",
            $"[cyan]{after.Count:N0}[/]",
            $"[red]{after.Count - before.Count:+0;-#}[/]"
        );

        comparison.AddRow(
            "Total Stock",
            $"{before.Sum(p => p.StockQuantity):N0}",
            $"[cyan]{after.Sum(p => p.StockQuantity):N0}[/]",
            $"[green]{after.Sum(p => p.StockQuantity) - before.Sum(p => p.StockQuantity):+#,0;-#,0}[/]"
        );

        AnsiConsole.Write(
            new Panel(comparison)
                .Header("[bold green]Before & After Comparison[/]")
                .BorderColor(Color.Green)
        );
    }

    public static void DisplayTopProducts(IReadOnlyList<Product> products)
    {
        AnsiConsole.MarkupLine("\n[bold underline]Sample Products (Top 10 by Value):[/]");

        var sampleTable = new Table();
        sampleTable.AddColumn("Name");
        sampleTable.AddColumn("Category");
        sampleTable.AddColumn("Price");
        sampleTable.AddColumn("Stock");
        sampleTable.AddColumn("Value");

        foreach (var p in products.OrderByDescending(p => p.Price.Amount * p.StockQuantity).Take(10))
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
}
