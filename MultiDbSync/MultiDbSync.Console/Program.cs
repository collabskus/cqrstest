using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MultiDbSync.Application;
using MultiDbSync.Application.Commands;
using MultiDbSync.Application.CQRS;
using MultiDbSync.Application.Queries;
using MultiDbSync.Domain.Interfaces;
using MultiDbSync.Infrastructure;
using MultiDbSync.Infrastructure.Data;
using MultiDbSync.Infrastructure.Repositories;
using Spectre.Console;

namespace MultiDbSync.Console;

class Program
{
    private static IServiceProvider? _serviceProvider;
    private static string _databasePath = Path.Combine(AppContext.BaseDirectory, "databases");

    static async Task<int> Main(string[] args)
    {
        System.Console.WriteLine("Multi-Database Synchronization System Demo");
        System.Console.WriteLine("============================================\n");

        try
        {
            Directory.CreateDirectory(_databasePath);

            var services = new ServiceCollection();
            ConfigureServices(services);
            _serviceProvider = services.BuildServiceProvider();

            await InitializeDatabaseAsync();

            await RunDemoAsync();

            return 0;
        }
        catch (Exception ex)
        {
            AnsiConsole.WriteException(ex);
            return 1;
        }
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        services.AddLogging(builder =>
        {
            builder.AddConsole();
            builder.SetMinimumLevel(LogLevel.Information);
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
        AnsiConsole.MarkupLine("[cyan]Initializing database...[/cyan]");

        using var scope = _serviceProvider!.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<MultiDbContext>();
        await context.Database.EnsureCreatedAsync();

        var nodeRepo = scope.ServiceProvider.GetRequiredService<IDatabaseNodeRepository>();

        var nodes = await nodeRepo.GetAllAsync();
        if (nodes.Count == 0)
        {
            var node1 = new Domain.Entities.DatabaseNode("node1", "Data Source=node1.db", 1, true);
            var node2 = new Domain.Entities.DatabaseNode("node2", "Data Source=node2.db", 2, false);
            var node3 = new Domain.Entities.DatabaseNode("node3", "Data Source=node3.db", 3, false);

            node1.MarkHealthy();
            node2.MarkHealthy();
            node3.MarkHealthy();

            await nodeRepo.AddAsync(node1);
            await nodeRepo.AddAsync(node2);
            await nodeRepo.AddAsync(node3);
        }

        AnsiConsole.MarkupLine("[green]Database initialized successfully![/green]\n");
    }

    private static async Task RunDemoAsync()
    {
        var exit = false;

        while (!exit)
        {
            var choice = AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                    .Title("Select an operation:")
                    .AddChoices([
                        "1. CRUD Operations (CQRS)",
                        "2. Database Synchronization",
                        "3. Quorum Consensus",
                        "4. Automatic Failover",
                        "5. Node Health Monitoring",
                        "6. Run All Demos",
                        "0. Exit"
                    ]));

            switch (choice)
            {
                case "1. CRUD Operations (CQRS)":
                    await RunCrudDemoAsync();
                    break;
                case "2. Database Synchronization":
                    await RunSyncDemoAsync();
                    break;
                case "3. Quorum Consensus":
                    await RunQuorumDemoAsync();
                    break;
                case "4. Automatic Failover":
                    await RunFailoverDemoAsync();
                    break;
                case "5. Node Health Monitoring":
                    await RunHealthCheckDemoAsync();
                    break;
                case "6. Run All Demos":
                    await RunAllDemosAsync();
                    break;
                case "0. Exit":
                    exit = true;
                    break;
            }
        }
    }

    private static async Task RunCrudDemoAsync()
    {
        AnsiConsole.MarkupLine("\n[bold cyan]=== CQRS CRUD Operations Demo ===[/bold cyan]\n");

        using var scope = _serviceProvider!.CreateScope();
        var createHandler = scope.ServiceProvider.GetRequiredService<CreateProductCommandHandler>();
        var getAllHandler = scope.ServiceProvider.GetRequiredService<GetAllProductsQueryHandler>();
        var updateHandler = scope.ServiceProvider.GetRequiredService<UpdateProductPriceCommandHandler>();
        var deleteHandler = scope.ServiceProvider.GetRequiredService<DeleteProductCommandHandler>();

        AnsiConsole.MarkupLine("[yellow]Creating products...[/yellow]");
        var createResult1 = await createHandler.HandleAsync(
            new CreateProductCommand("Laptop", "High-performance laptop", 1299.99m, "USD", 50, "Electronics"));
        var createResult2 = await createHandler.HandleAsync(
            new CreateProductCommand("Mouse", "Wireless mouse", 29.99m, "USD", 200, "Electronics"));
        var createResult3 = await createHandler.HandleAsync(
            new CreateProductCommand("Keyboard", "Mechanical keyboard", 89.99m, "USD", 150, "Electronics"));

        if (createResult1.IsSuccess)
            AnsiConsole.MarkupLine($"[green]Created: {createResult1.Data?.Name} - {createResult1.Data?.Price}[/green]");
        if (createResult2.IsSuccess)
            AnsiConsole.MarkupLine($"[green]Created: {createResult2.Data?.Name} - {createResult2.Data?.Price}[/green]");
        if (createResult3.IsSuccess)
            AnsiConsole.MarkupLine($"[green]Created: {createResult3.Data?.Name} - {createResult3.Data?.Price}[/green]");

        AnsiConsole.MarkupLine("\n[yellow]Querying all products...[/yellow]");
        var getAllResult = await getAllHandler.HandleAsync(new GetAllProductsQuery());

        if (getAllResult.IsSuccess && getAllResult.Data is { })
        {
            var table = new Table();
            table.AddColumn("Name");
            table.AddColumn("Price");
            table.AddColumn("Stock");
            table.AddColumn("Category");

            foreach (var product in getAllResult.Data)
            {
                table.AddRow(
                    product.Name,
                    product.Price.ToString(),
                    product.StockQuantity.ToString(),
                    product.Category);
            }

            AnsiConsole.Write(table);
        }

        if (createResult1.IsSuccess && createResult1.Data is { })
        {
            var productId = createResult1.Data.Id;
            AnsiConsole.MarkupLine($"\n[yellow]Updating price for '{createResult1.Data.Name}'...[/yellow]");

            var updateResult = await updateHandler.HandleAsync(
                new UpdateProductPriceCommand(productId, 1199.99m, "USD"));

            if (updateResult.IsSuccess)
                AnsiConsole.MarkupLine($"[green]Updated price to: {updateResult.Data?.Price}[/green]");

            AnsiConsole.MarkupLine($"\n[yellow]Deleting product...[/yellow]");
            var deleteResult = await deleteHandler.HandleAsync(new DeleteProductCommand(productId));

            if (deleteResult.IsSuccess)
                AnsiConsole.MarkupLine("[green]Product deleted successfully![/green]");
        }

        AnsiConsole.MarkupLine("\n[bold green]CQRS Demo completed![/bold green]\n");
    }

    private static async Task RunSyncDemoAsync()
    {
        AnsiConsole.MarkupLine("\n[bold cyan]=== Database Synchronization Demo ===[/bold cyan]\n");

        using var scope = _serviceProvider!.CreateScope();
        var syncService = scope.ServiceProvider.GetRequiredService<ISynchronizationService>();
        var nodeRepo = scope.ServiceProvider.GetRequiredService<IDatabaseNodeRepository>();

        var nodes = await nodeRepo.GetAllAsync();

        var table = new Table();
        table.AddColumn("Node ID");
        table.AddColumn("Status");
        table.AddColumn("Is Primary");
        table.AddColumn("Health Score");

        foreach (var node in nodes)
        {
            table.AddRow(
                node.NodeId,
                node.Status.ToString(),
                node.IsPrimary ? "Yes" : "No",
                $"{node.HealthScore:F1}%");
        }

        AnsiConsole.Write(table);

        AnsiConsole.MarkupLine("\n[yellow]Forcing synchronization across all nodes...[/yellow]");
        var syncResult = await syncService.GetSyncStatusAsync();

        AnsiConsole.MarkupLine($"[green]Sync Status:[/green]");
        AnsiConsole.MarkupLine($"  Total Nodes: {syncResult.TotalNodes}");
        AnsiConsole.MarkupLine($"  Successful: {syncResult.SuccessfulNodes}");
        AnsiConsole.MarkupLine($"  Failed: {syncResult.FailedNodes}");

        AnsiConsole.MarkupLine("\n[bold green]Synchronization Demo completed![/bold green]\n");
    }

    private static async Task RunQuorumDemoAsync()
    {
        AnsiConsole.MarkupLine("\n[bold cyan]=== Quorum Consensus Demo ===[/bold cyan]\n");

        using var scope = _serviceProvider!.CreateScope();
        var quorumService = scope.ServiceProvider.GetRequiredService<IQuorumService>();
        var nodeRepo = scope.ServiceProvider.GetRequiredService<IDatabaseNodeRepository>();

        var operationId = Guid.NewGuid();
        var operationDescription = "Promote node to primary";

        AnsiConsole.MarkupLine($"[yellow]Requesting vote for operation: {operationDescription}[/yellow]");

        var voteRequested = await quorumService.RequestVoteAsync(operationId, operationDescription);

        AnsiConsole.MarkupLine($"Vote requested: [{(voteRequested ? "green" : "red")}]{voteRequested}[/]");

        var result = await quorumService.GetQuorumResultAsync(operationId);

        AnsiConsole.MarkupLine($"\n[green]Quorum Result:[/green]");
        AnsiConsole.MarkupLine($"  Total Votes: {result.TotalVotes}");
        AnsiConsole.MarkupLine($"  Yes Votes: {result.YesVotes}");
        AnsiConsole.MarkupLine($"  No Votes: {result.NoVotes}");
        AnsiConsole.MarkupLine($"  Has Consensus: [{(result.HasConsensus ? "green" : "red")}]{result.HasConsensus}[/]");
        AnsiConsole.MarkupLine($"  Decision: {result.Decision}");

        AnsiConsole.MarkupLine("\n[bold green]Quorum Demo completed![/bold green]\n");
    }

    private static async Task RunFailoverDemoAsync()
    {
        AnsiConsole.MarkupLine("\n[bold cyan]=== Automatic Failover Demo ===[/bold cyan]\n");

        using var scope = _serviceProvider!.CreateScope();
        var failoverService = scope.ServiceProvider.GetRequiredService<IFailoverService>();
        var nodeRepo = scope.ServiceProvider.GetRequiredService<IDatabaseNodeRepository>();

        failoverService.FailoverOccurred += (sender, args) =>
        {
            AnsiConsole.MarkupLine($"[yellow]Failover Event: {args.FailedNodeId} -> {args.NewPrimaryNodeId}[/yellow]");
        };

        var nodes = await nodeRepo.GetAllAsync();
        var primaryNode = nodes.FirstOrDefault(n => n.IsPrimary);

        if (primaryNode is { })
        {
            AnsiConsole.MarkupLine($"[yellow]Current primary node: {primaryNode.NodeId}[/yellow]");

            AnsiConsole.MarkupLine("[yellow]Checking if failover is needed...[/yellow]");
            var isNeeded = await failoverService.IsFailoverNeededAsync();

            AnsiConsole.MarkupLine($"Failover needed: [{(isNeeded ? "green" : "yellow")}]{isNeeded}[/]");

            var optimalNode = await failoverService.GetOptimalNodeAsync();
            AnsiConsole.MarkupLine($"Optimal node for failover: [green]{optimalNode ?? "None"}[/]");
        }

        AnsiConsole.MarkupLine("\n[bold green]Failover Demo completed![/bold green]\n");
    }

    private static async Task RunHealthCheckDemoAsync()
    {
        AnsiConsole.MarkupLine("\n[bold cyan]=== Node Health Monitoring Demo ===[/bold cyan]\n");

        using var scope = _serviceProvider!.CreateScope();
        var healthCheckService = scope.ServiceProvider.GetRequiredService<IHealthCheckService>();
        var nodeRepo = scope.ServiceProvider.GetRequiredService<IDatabaseNodeRepository>();

        var nodes = await nodeRepo.GetAllAsync();

        foreach (var node in nodes)
        {
            var health = await healthCheckService.CheckNodeHealthAsync(node.NodeId);

            AnsiConsole.MarkupLine($"[yellow]Node: {health.NodeId}[/yellow]");
            AnsiConsole.MarkupLine($"  Is Healthy: [{(health.IsHealthy ? "green" : "red")}]{health.IsHealthy}[/]");
            AnsiConsole.MarkupLine($"  Response Time: {health.ResponseTimeMs:F2}ms");
            if (health.ErrorMessage is { })
                AnsiConsole.MarkupLine($"  Error: [red]{health.ErrorMessage}[/]");
            AnsiConsole.WriteLine();
        }

        AnsiConsole.MarkupLine("[bold green]Health Check Demo completed![/bold green]\n");
    }

    private static async Task RunAllDemosAsync()
    {
        AnsiConsole.MarkupLine("[bold magenta]Running all demos...[/bold magenta]\n");

        await RunCrudDemoAsync();
        await RunSyncDemoAsync();
        await RunQuorumDemoAsync();
        await RunFailoverDemoAsync();
        await RunHealthCheckDemoAsync();

        AnsiConsole.MarkupLine("[bold magenta]=== All Demos Completed! ===[/bold magenta]\n");
    }
}
