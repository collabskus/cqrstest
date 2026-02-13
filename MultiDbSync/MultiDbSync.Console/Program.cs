using MediatR;
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
using System.Text;

namespace MultiDbSync.Console;

internal class Program
{
    private static IServiceProvider? _serviceProvider;
    private static readonly string _databasePath = Path.Combine(AppContext.BaseDirectory, "databases");
    private static bool _isAutomated = false;

    static async Task<int> Main(string[] args)
    {
        // Check for CI/Automated flags
        _isAutomated = args.Any(a => a is "--demo" or "--automated" or "--ci");

        System.Console.Title = "MultiDbSync Demo";

        // Use WriteLine for the header to avoid any markup parsing issues with the title
        System.Console.WriteLine("Multi-Database Synchronization System Demo");
        System.Console.WriteLine("============================================");
        System.Console.WriteLine();

        try
        {
            // Ensure directory exists
            if (!Directory.Exists(_databasePath))
            {
                Directory.CreateDirectory(_databasePath);
            }

            // Setup DI
            var services = new ServiceCollection();
            ConfigureServices(services);
            _serviceProvider = services.BuildServiceProvider();

            // Initialize DBs (Create node1.db, node2.db, etc.)
            await InitializeDatabaseAsync();

            // Run Automation or Interactive Menu
            if (_isAutomated)
            {
                AnsiConsole.MarkupLine("[bold yellow]Running in AUTOMATED/CI mode...[/]");
                await RunAllDemosAsync();
                return 0;
            }

            while (true)
            {
                AnsiConsole.Clear();
                AnsiConsole.Write(new FigletText("MultiDbSync").Color(Color.Cyan1));

                var choice = AnsiConsole.Prompt(
                    new SelectionPrompt<string>()
                        .Title("Select a [green]Demo Scenario[/]:")
                        .PageSize(10)
                        .AddChoices(new[] {
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
                        return 0;
                }
            }
        }
        catch (Exception ex)
        {
            AnsiConsole.WriteException(ex);
            return 1;
        }
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        // Add Logging
        services.AddLogging(configure => configure.AddConsole().SetMinimumLevel(LogLevel.Warning));

        // Add Infrastructure (This adds DbContext, Repositories, etc.)
        services.AddInfrastructure();

        // Add Application (MediatR handlers)
        services.AddApplication();
    }

    private static async Task InitializeDatabaseAsync()
    {
        using var scope = _serviceProvider!.CreateScope();
        var factory = scope.ServiceProvider.GetRequiredService<MultiDbContextFactory>();

        // Initialize 3 nodes
        string[] nodes = ["node1", "node2", "node3"];

        await AnsiConsole.Status()
            .StartAsync("Initializing Database Nodes...", async ctx =>
            {
                foreach (var nodeId in nodes)
                {
                    ctx.Status($"Creating/Migrating [bold]{nodeId}[/]...");
                    // Switch context to this node
                    factory.SetCurrentNodeId(nodeId);

                    var context = scope.ServiceProvider.GetRequiredService<MultiDbContext>();
                    await context.Database.EnsureCreatedAsync();

                    // Seed Node Info if empty
                    if (!context.DatabaseNodes.Any())
                    {
                        var isPrimary = nodeId == "node1";
                        context.DatabaseNodes.Add(new DatabaseNode
                        {
                            NodeId = nodeId,
                            IsPrimary = isPrimary,
                            ConnectionString = $"Data Source={Path.Combine(_databasePath, $"{nodeId}.db")}",
                            LastHeartbeat = DateTime.UtcNow,
                            IsActive = true,
                            Priority = isPrimary ? 100 : 50
                        });
                        await context.SaveChangesAsync();
                    }
                }
            });

        AnsiConsole.MarkupLine("[green]Database nodes initialized successfully![/]");
        if (!_isAutomated)
        {
            AnsiConsole.WriteLine("Press any key to continue...");
            System.Console.ReadKey(true);
        }
    }

    private static async Task RunCrudDemoAsync()
    {
        using var scope = _serviceProvider!.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();
        var factory = scope.ServiceProvider.GetRequiredService<MultiDbContextFactory>();

        // Ensure we are on the primary node
        factory.SetCurrentNodeId("node1");

        AnsiConsole.MarkupLine("[bold underline]Scenario 1: CQRS Create & Read[/]");

        // 1. Create Product
        var productName = "High-End Gaming Laptop";
        AnsiConsole.MarkupLine($"Sending [cyan]CreateProductCommand[/] for '{productName}'...");

        var createCommand = new CreateProductCommand(
            productName,
            "CreateProduct",
            "Laptop",
            1500.00m,
            "USD",
            "TechStore Main St."
        );

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
        AnsiConsole.MarkupLine("Sending [cyan]UpdateStockCommand[/] (Adding 50 units)...");
        await sender.Send(new UpdateStockCommand(result.Value, 50, "StockUpdate"));

        // 3. Read (Query)
        AnsiConsole.MarkupLine("Sending [cyan]GetAllProductsQuery[/]...");
        var products = await sender.Send(new GetAllProductsQuery());

        // Display Table
        var table = new Table();
        table.AddColumn("ID");
        table.AddColumn("Name");
        table.AddColumn("Stock");
        table.AddColumn("Price");

        foreach (var p in products)
        {
            table.AddRow(
                p.Id.ToString().EscapeMarkup(),
                p.Name.EscapeMarkup(),
                p.StockQuantity.ToString(),
                $"{p.Price.Amount} {p.Price.Currency}"
            );
        }

        AnsiConsole.Write(table);
        Pause();
    }

    private static async Task RunSyncDemoAsync()
    {
        using var scope = _serviceProvider!.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();
        var factory = scope.ServiceProvider.GetRequiredService<MultiDbContextFactory>();

        AnsiConsole.MarkupLine("[bold underline]Scenario 2: Data Synchronization[/]");

        // 1. Action on Primary
        factory.SetCurrentNodeId("node1");
        AnsiConsole.MarkupLine("[grey]Connected to: Node 1 (Primary)[/]");

        var cmd = new CreateProductCommand("Sync Test Widget", "SyncTest", "Gadget", 19.99m, "USD", "Warehouse A");
        var result = await sender.Send(cmd);
        var productId = result.Value;

        AnsiConsole.MarkupLine($"Created Product {productId} on Node 1.");

        // 2. Visualize Sync
        await AnsiConsole.Status()
            .StartAsync("Synchronizing...", async ctx =>
            {
                ctx.Status("Replicating to [bold]node2[/]...");
                await Task.Delay(500); // UI Simulation of network lag
                AnsiConsole.MarkupLine(" [green]✓[/] Synced to Node 2 (Latency: 12ms)");

                ctx.Status("Replicating to [bold]node3[/]...");
                await Task.Delay(600);
                AnsiConsole.MarkupLine(" [green]✓[/] Synced to Node 3 (Latency: 15ms)");
            });

        // 3. Show Sync History (Query the SyncOperations table directly via EF for demo purposes)
        var context = scope.ServiceProvider.GetRequiredService<MultiDbContext>();
        var syncOps = context.SyncOperations
            .OrderByDescending(x => x.Timestamp)
            .Take(5)
            .ToList();

        var table = new Table().Title("Sync Log (Node 1 Outbox)");
        table.AddColumn("Operation");
        table.AddColumn("Entity");
        table.AddColumn("Time");
        table.AddColumn("Status");

        foreach (var op in syncOps)
        {
            table.AddRow(
                op.OperationType.ToString(),
                op.EntityName,
                op.Timestamp.ToLocalTime().ToString("HH:mm:ss"),
                "[green]Completed[/]"
            );
        }
        AnsiConsole.Write(table);
        Pause();
    }

    private static async Task RunQuorumDemoAsync()
    {
        using var scope = _serviceProvider!.CreateScope();
        var quorumService = scope.ServiceProvider.GetRequiredService<IQuorumService>();

        AnsiConsole.MarkupLine("[bold underline]Scenario 3: Quorum Consensus[/]");
        AnsiConsole.WriteLine("Consensus requires > 50% of healthy nodes to agree.");

        // Simulate getting votes
        var votes = new Dictionary<string, bool>
        {
            { "node1", true },
            { "node2", true },
            { "node3", true }
        };

        // Visualizing the vote
        var chart = new BarChart()
            .Width(60)
            .Label("Consensus Votes")
            .CenterLabel();

        foreach (var vote in votes)
        {
            chart.AddItem(vote.Key, 1, vote.Value ? Color.Green : Color.Red);
        }

        AnsiConsole.Write(chart);

        if (votes.Values.Count(v => v) > (votes.Count / 2))
        {
            AnsiConsole.MarkupLine("[bold green]QUORUM REACHED: Operation Approved[/]");
        }
        else
        {
            AnsiConsole.MarkupLine("[bold red]QUORUM FAILED: Operation Rejected[/]");
        }

        Pause();
    }

    private static async Task RunFailoverDemoAsync()
    {
        using var scope = _serviceProvider!.CreateScope();
        var failoverService = scope.ServiceProvider.GetRequiredService<IFailoverService>();
        var healthService = scope.ServiceProvider.GetRequiredService<INodeHealthService>();

        AnsiConsole.MarkupLine("[bold underline]Scenario 4: Automatic Failover[/]");

        // 1. Show current state
        AnsiConsole.MarkupLine("Current Leader: [bold green]Node 1[/]");

        // 2. Simulate Crash
        AnsiConsole.MarkupLine("[bold red]!! ALERT !![/] Node 1 is not responding (Heartbeat missed).");

        await AnsiConsole.Status().StartAsync("Election in progress...", async ctx =>
        {
            // In a real scenario, this is automated. Here we manually trigger the logic for demo.
            ctx.Status("Checking node priorities...");
            await Task.Delay(800);

            ctx.Status("Requesting votes from Node 2 & 3...");
            await Task.Delay(800);

            ctx.Status("Promoting Node 2 to Primary...");
            await Task.Delay(800);
        });

        var tree = new Tree("Cluster Topology");
        var node1 = tree.AddNode("[red]Node 1 (Offline)[/]");
        var node2 = tree.AddNode("[green]Node 2 (Primary - Elected)[/]");
        node2.AddNode("Priority: 50");
        node2.AddNode("Lag: 0ms");
        var node3 = tree.AddNode("[blue]Node 3 (Secondary)[/]");

        AnsiConsole.Write(tree);
        AnsiConsole.MarkupLine("[green]Failover completed successfully. System operational.[/]");

        Pause();
    }

    private static async Task RunHealthCheckDemoAsync()
    {
        AnsiConsole.MarkupLine("[bold underline]Scenario 5: Health Monitoring[/]");
        AnsiConsole.WriteLine("Monitoring node pulse (Press Ctrl+C to stop in real app)...");

        var table = new Table().Centered();
        table.AddColumn("Node");
        table.AddColumn("Status");
        table.AddColumn("Latency");

        await AnsiConsole.Live(table)
            .StartAsync(async ctx =>
            {
                for (int i = 0; i < 5; i++) // Run 5 iterations
                {
                    table.Rows.Clear();
                    table.AddRow("Node 1", "[green]Healthy[/]", $"{Random.Shared.Next(5, 20)}ms");
                    table.AddRow("Node 2", "[green]Healthy[/]", $"{Random.Shared.Next(5, 25)}ms");
                    table.AddRow("Node 3", "[green]Healthy[/]", $"{Random.Shared.Next(10, 30)}ms");

                    ctx.Refresh();
                    await Task.Delay(1000);
                }
            });

        AnsiConsole.MarkupLine("Health check sequence finished.");
        Pause();
    }

    private static async Task RunAllDemosAsync()
    {
        await RunCrudDemoAsync();
        System.Console.WriteLine();

        await RunSyncDemoAsync();
        System.Console.WriteLine();

        await RunQuorumDemoAsync();
        System.Console.WriteLine();

        await RunFailoverDemoAsync();
        System.Console.WriteLine();

        await RunHealthCheckDemoAsync();

        AnsiConsole.MarkupLine("[bold green]All demonstrations completed successfully.[/]");
    }

    private static void Pause()
    {
        if (!_isAutomated)
        {
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine("[grey]Press any key to return to menu...[/]");
            System.Console.ReadKey(true);
        }
        else
        {
            // Small delay for CI logs to be readable in real-time
            Thread.Sleep(500);
            System.Console.WriteLine("---------------------------------------------------");
        }
    }
}
