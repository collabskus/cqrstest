using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MultiDbSync.Application;
using MultiDbSync.Application.Commands;
using MultiDbSync.Application.Queries;
using MultiDbSync.Domain.Interfaces;
using MultiDbSync.Infrastructure;
using MultiDbSync.Infrastructure.Data;
using Spectre.Console;

namespace MultiDbSync.Console;

internal class Program
{
    private static IServiceProvider? _serviceProvider;
    private static readonly string _databasePath = Path.Combine(AppContext.BaseDirectory, "databases");

    static async Task<int> Main(string[] args)
    {
        System.Console.Title = "MultiDbSync Demo";

        System.Console.WriteLine("Multi-Database Synchronization System Demo");
        System.Console.WriteLine("============================================\n");

        try
        {
            // Force Spectre to use ANSI in VS debug console
            AnsiConsole.Console.Profile.Capabilities.Ansi = true;

            Directory.CreateDirectory(_databasePath);

            var services = new ServiceCollection();
            ConfigureServices(services);
            _serviceProvider = services.BuildServiceProvider();

            await InitializeDatabaseAsync();

            // Automated mode for CI
            if (args.Any(a => a is "--demo" or "--automated" or "--ci"))
            {
                System.Console.WriteLine("Running automated demo mode...");
                await RunAllDemosAsync();
                return 0;
            }

            await RunDemoAsync();
            return 0;
        }
        catch (Exception ex)
        {
            AnsiConsole.WriteException(ex);
            System.Console.ReadLine(); // prevent auto-close
            return 1;
        }
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        services.AddLogging(b =>
        {
            b.AddConsole();
            b.SetMinimumLevel(LogLevel.Information);
        });

        services.AddInfrastructureServices(_databasePath);
        services.AddApplicationServices();

        services.AddSingleton<CreateProductCommandHandler>();
        services.AddSingleton<UpdateProductPriceCommandHandler>();
        services.AddSingleton<DeleteProductCommandHandler>();
        services.AddSingleton<GetAllProductsQueryHandler>();
        services.AddSingleton<GetSyncStatusQueryHandler>();
        services.AddSingleton<AddDatabaseNodeCommandHandler>();
        services.AddSingleton<GetAllNodesQueryHandler>();
        services.AddSingleton<GetHealthyNodesQueryHandler>();
    }

    private static async Task InitializeDatabaseAsync()
    {
        AnsiConsole.MarkupLine("[cyan]Initializing database...[/]");

        using var scope = _serviceProvider!.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<MultiDbContext>();
        await context.Database.EnsureCreatedAsync();

        var repo = scope.ServiceProvider.GetRequiredService<IDatabaseNodeRepository>();
        var nodes = await repo.GetAllAsync();

        if (nodes.Count == 0)
        {
            await repo.AddAsync(new Domain.Entities.DatabaseNode("node1", "Data Source=node1.db", 1, true));
            await repo.AddAsync(new Domain.Entities.DatabaseNode("node2", "Data Source=node2.db", 2, false));
            await repo.AddAsync(new Domain.Entities.DatabaseNode("node3", "Data Source=node3.db", 3, false));
        }

        AnsiConsole.MarkupLine("[green]Database initialized.[/]\n");
    }

    private static async Task RunDemoAsync()
    {
        while (true)
        {
            var choice = AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                    .Title("Select operation")
                    .AddChoices(new[]
                    {
                        "CRUD Operations",
                        "Database Synchronization",
                        "Quorum Consensus",
                        "Automatic Failover",
                        "Node Health Monitoring",
                        "Run All Demos",
                        "Exit"
                    })
            );

            switch (choice)
            {
                case "CRUD Operations":
                    await RunCrudDemoAsync();
                    break;
                case "Database Synchronization":
                    await RunSyncDemoAsync();
                    break;
                case "Quorum Consensus":
                    await RunQuorumDemoAsync();
                    break;
                case "Automatic Failover":
                    await RunFailoverDemoAsync();
                    break;
                case "Node Health Monitoring":
                    await RunHealthCheckDemoAsync();
                    break;
                case "Run All Demos":
                    await RunAllDemosAsync();
                    break;
                case "Exit":
                    return;
            }
        }
    }

    // STUBS so it compiles if missing
    private static Task RunCrudDemoAsync() => Task.CompletedTask;
    private static Task RunSyncDemoAsync() => Task.CompletedTask;
    private static Task RunQuorumDemoAsync() => Task.CompletedTask;
    private static Task RunFailoverDemoAsync() => Task.CompletedTask;
    private static Task RunHealthCheckDemoAsync() => Task.CompletedTask;
    private static Task RunAllDemosAsync() => Task.CompletedTask;
}
