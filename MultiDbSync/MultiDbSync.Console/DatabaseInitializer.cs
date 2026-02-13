using Microsoft.Extensions.DependencyInjection;
using MultiDbSync.Domain.Entities;
using MultiDbSync.Infrastructure.Data;
using Spectre.Console;

namespace MultiDbSync.Console;

internal sealed class DatabaseInitializer
{
    private static readonly string[] Nodes = ["node1", "node2", "node3"];

    public async Task InitializeAsync(IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();
        var factory = scope.ServiceProvider.GetRequiredService<MultiDbContextFactory>();

        await AnsiConsole.Status()
            .StartAsync("Initializing Database Nodes...", async ctx =>
            {
                foreach (var nodeId in Nodes)
                {
                    ctx.Status($"Creating [bold]{nodeId}[/]...");

                    await using var context = factory.CreateDbContext(nodeId);

                    // Drop and recreate to ensure clean state
                    await context.Database.EnsureDeletedAsync();
                    await context.Database.EnsureCreatedAsync();

                    if (!context.DatabaseNodes.Any())
                    {
                        var isPrimary = nodeId == "node1";
                        var databasePath = Path.Combine(AppContext.BaseDirectory, "databases");
                        var connectionString = $"Data Source={Path.Combine(databasePath, $"{nodeId}.db")}";

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
}
