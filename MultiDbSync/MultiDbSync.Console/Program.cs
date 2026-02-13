using Microsoft.Extensions.DependencyInjection;
using MultiDbSync.Application.Commands;
using MultiDbSync.Application.Queries;
using MultiDbSync.Domain.Entities;
using MultiDbSync.Infrastructure.Data;
using Spectre.Console;
using System.Collections.Concurrent;
using System.Diagnostics;

namespace MultiDbSync.Console;

internal sealed class Program(string[] args)
{
    private static readonly string DatabasePath = Path.Combine(AppContext.BaseDirectory, "databases");

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
            // Ensure database directory exists
            if (!Directory.Exists(DatabasePath))
            {
                Directory.CreateDirectory(DatabasePath);
            }

            // Load configuration and build service provider
            var configurationManager = new ConfigurationManager(DatabasePath);
            var serviceProvider = configurationManager.BuildServiceProvider();

            // Initialize databases
            var databaseInitializer = new DatabaseInitializer();
            await databaseInitializer.InitializeAsync(serviceProvider);

            // Run appropriate demo
            var demoSettings = serviceProvider.GetRequiredService<DemoSettings>();
            var isCiMode = args.Any(a => a is "--demo" or "--automated" or "--ci");

            if (isCiMode)
            {
                return await RunCiModeAsync(serviceProvider, demoSettings);
            }
            else
            {
                var interactiveDemo = new InteractiveDemo(serviceProvider);
                await interactiveDemo.RunAsync();
                return 0;
            }
        }
        catch (Exception ex)
        {
            AnsiConsole.WriteException(ex);
            return 1;
        }
    }

    private static async Task<int> RunCiModeAsync(IServiceProvider serviceProvider, DemoSettings demoSettings)
    {
        AnsiConsole.MarkupLine("[bold underline cyan1]CI Mode: CQRS Load Simulation with Concurrent Readers[/]\n");

        const int numberOfProducts = 100;
        const int writeThreads = 3;
        const int readThreads = 5;
        const int readsPerWrite = 10;
        const int totalExpectedReads = numberOfProducts * readsPerWrite;

        // Configuration display
        var configTable = new Table()
            .Border(TableBorder.Rounded)
            .BorderColor(Color.Yellow);

        configTable.AddColumn("Setting");
        configTable.AddColumn("Value");

        configTable.AddRow("Products to create", $"[cyan]{numberOfProducts:N0}[/]");
        configTable.AddRow("Write threads", $"[green]{writeThreads}[/]");
        configTable.AddRow("Read threads", $"[blue]{readThreads}[/]");
        configTable.AddRow("Reads per write", $"[yellow]{readsPerWrite}[/]");
        configTable.AddRow("Expected total reads", $"[magenta]{totalExpectedReads:N0}[/]");
        configTable.AddRow("Read replicas", "[cyan]node1, node2, node3[/]");

        AnsiConsole.Write(new Panel(configTable)
            .Header("[bold yellow]Configuration[/]")
            .BorderColor(Color.Yellow));

        AnsiConsole.WriteLine();

        var stopwatch = Stopwatch.StartNew();

        // Shared state
        var writtenProductIds = new ConcurrentQueue<Guid>();
        var allWritesComplete = new TaskCompletionSource<bool>();
        var writeLatencies = new ConcurrentBag<TimeSpan>();
        var readLatencies = new ConcurrentBag<TimeSpan>();
        var totalReads = 0;
        var totalWrites = 0;

        // Start write tasks (writes go to primary node1)
        var writeTasks = new List<Task<int>>();
        for (int i = 0; i < writeThreads; i++)
        {
            int threadId = i;
            int productsPerThread = numberOfProducts / writeThreads;
            if (i < numberOfProducts % writeThreads)
                productsPerThread++;

            var task = Task.Run(async () =>
            {
                return await ExecuteWriterThreadAsync(
                    serviceProvider,
                    threadId,
                    productsPerThread,
                    writtenProductIds,
                    writeLatencies);
            });
            writeTasks.Add(task);
        }

        // Start read tasks (reads distributed across replicas)
        var readTasks = new List<Task<int>>();
        var replicaNodes = new[] { "node1", "node2", "node3" };

        for (int i = 0; i < readThreads; i++)
        {
            int threadId = i;
            string nodeId = replicaNodes[i % replicaNodes.Length];
            int readsPerThread = totalExpectedReads / readThreads;
            if (i < totalExpectedReads % readThreads)
                readsPerThread++;

            var task = Task.Run(async () =>
            {
                return await ExecuteReaderThreadAsync(
                    serviceProvider,
                    threadId,
                    nodeId,
                    readsPerThread,
                    writtenProductIds,
                    allWritesComplete.Task,
                    readLatencies);
            });
            readTasks.Add(task);
        }

        // Wait for all writes to complete
        var writeResults = await Task.WhenAll(writeTasks);
        totalWrites = writeResults.Sum();
        allWritesComplete.SetResult(true);
        AnsiConsole.MarkupLine("\n[green]✓ All write operations completed[/]");

        // Wait for all reads to complete
        var readResults = await Task.WhenAll(readTasks);
        totalReads = readResults.Sum();
        AnsiConsole.MarkupLine("[cyan]✓ All read operations completed[/]");

        stopwatch.Stop();

        // Print statistics and return exit code
        return PrintStatistics(
            stopwatch.Elapsed,
            numberOfProducts,
            readsPerWrite,
            totalWrites,
            totalReads,
            writeLatencies,
            readLatencies);
    }

    private static async Task<int> ExecuteWriterThreadAsync(
        IServiceProvider serviceProvider,
        int threadId,
        int productsToCreate,
        ConcurrentQueue<Guid> writtenProductIds,
        ConcurrentBag<TimeSpan> writeLatencies)
    {
        int localWrites = 0;

        for (int i = 0; i < productsToCreate; i++)
        {
            // Create a new scope for each product to avoid DbContext threading issues
            using var scope = serviceProvider.CreateScope();
            var factory = scope.ServiceProvider.GetRequiredService<MultiDbContextFactory>();
            await using var context = factory.CreateDbContext("node1"); // Write to primary

            var repository = new Infrastructure.Repositories.ProductRepository(context);

            var adjective = ProductDataGenerator.AdjectivesArray[Random.Shared.Next(ProductDataGenerator.AdjectivesArray.Length)];
            var product = ProductDataGenerator.ProductsArray[Random.Shared.Next(ProductDataGenerator.ProductsArray.Length)];
            var category = ProductDataGenerator.CategoriesArray[Random.Shared.Next(ProductDataGenerator.CategoriesArray.Length)];
            var name = $"{adjective} {product} W{threadId}-{i:D4}";
            var price = Math.Round((decimal)(Random.Shared.NextDouble() * 1900 + 100), 2);
            var stock = Random.Shared.Next(0, 500);

            var productEntity = new Product(
                name,
                $"High-quality {product.ToLower()} with advanced features",
                new Domain.ValueObjects.Money(price, "USD"),
                stock,
                category
            );

            var sw = Stopwatch.StartNew();
            try
            {
                await repository.AddAsync(productEntity);
                sw.Stop();

                writeLatencies.Add(sw.Elapsed);
                localWrites++;

                writtenProductIds.Enqueue(productEntity.Id);

                if (localWrites % 25 == 0)
                {
                    AnsiConsole.MarkupLine($"[dim]Writer {threadId}: Progress {localWrites} products created[/]");
                }
            }
            catch (Exception ex)
            {
                AnsiConsole.MarkupLine($"[red]Writer {threadId} Error: {ex.Message}[/]");
            }

            // Small delay to simulate realistic write patterns
            await Task.Delay(Random.Shared.Next(10, 30));
        }

        AnsiConsole.MarkupLine($"[green]Writer {threadId}: Completed {productsToCreate} products[/]");
        return localWrites;
    }

    private static async Task<int> ExecuteReaderThreadAsync(
        IServiceProvider serviceProvider,
        int threadId,
        string nodeId,
        int targetReads,
        ConcurrentQueue<Guid> writtenProductIds,
        Task allWritesComplete,
        ConcurrentBag<TimeSpan> readLatencies)
    {
        int localReads = 0;
        int consecutiveEmptyReads = 0;
        const int maxConsecutiveEmptyReads = 50;

        while (localReads < targetReads)
        {
            // If we haven't seen any products and writes aren't complete, wait
            if (writtenProductIds.IsEmpty && !allWritesComplete.IsCompleted)
            {
                consecutiveEmptyReads++;
                if (consecutiveEmptyReads >= maxConsecutiveEmptyReads)
                {
                    await Task.Delay(100);
                    consecutiveEmptyReads = 0;
                }
                continue;
            }

            consecutiveEmptyReads = 0;

            // Create a new scope for each read to avoid DbContext threading issues
            using var scope = serviceProvider.CreateScope();
            var factory = scope.ServiceProvider.GetRequiredService<MultiDbContextFactory>();
            await using var context = factory.CreateDbContext(nodeId);

            var repository = new Infrastructure.Repositories.ProductRepository(context);

            try
            {
                // Distribute read patterns: 50% by ID, 30% list all, 20% search by category
                var readType = Random.Shared.Next(100);

                if (readType < 50 && writtenProductIds.TryPeek(out var productId))
                {
                    // Read by ID from replica
                    var sw = Stopwatch.StartNew();
                    var product = await repository.GetByIdAsync(productId);
                    sw.Stop();

                    readLatencies.Add(sw.Elapsed);
                    localReads++;
                }
                else if (readType < 80)
                {
                    // List all products from replica
                    var sw = Stopwatch.StartNew();
                    var products = await repository.GetAllAsync();
                    sw.Stop();

                    readLatencies.Add(sw.Elapsed);
                    localReads++;
                }
                else
                {
                    // Search by category from replica
                    var categories = ProductDataGenerator.CategoriesArray;
                    var category = categories[Random.Shared.Next(categories.Length)];

                    var sw = Stopwatch.StartNew();
                    var products = await repository.GetByCategoryAsync(category);
                    sw.Stop();

                    readLatencies.Add(sw.Elapsed);
                    localReads++;
                }

                if (localReads % 200 == 0)
                {
                    AnsiConsole.MarkupLine($"[dim]Reader {threadId}@{nodeId}: Progress {localReads} reads[/]");
                }
            }
            catch (Exception ex)
            {
                AnsiConsole.MarkupLine($"[red]Reader {threadId}@{nodeId} Error: {ex.Message}[/]");
            }

            // Small delay between reads
            await Task.Delay(Random.Shared.Next(3, 10));
        }

        AnsiConsole.MarkupLine($"[cyan]Reader {threadId}@{nodeId}: Completed {localReads} reads[/]");
        return localReads;
    }

    private static int PrintStatistics(
        TimeSpan totalTime,
        int expectedWrites,
        int readsPerWrite,
        int totalWrites,
        int totalReads,
        ConcurrentBag<TimeSpan> writeLatencies,
        ConcurrentBag<TimeSpan> readLatencies)
    {
        AnsiConsole.WriteLine();
        AnsiConsole.Write(new Rule("[bold cyan]Performance Statistics[/]").RuleStyle("cyan"));
        AnsiConsole.WriteLine();

        AnsiConsole.MarkupLine($"[bold]Total Execution Time:[/] [yellow]{totalTime.TotalSeconds:F2}[/] seconds");
        AnsiConsole.WriteLine();

        // Write Statistics
        var writeStatsTable = new Table()
            .Border(TableBorder.Rounded)
            .BorderColor(Color.Green);

        writeStatsTable.AddColumn("Metric");
        writeStatsTable.AddColumn("Value");

        writeStatsTable.AddRow("Total Writes", $"[cyan]{totalWrites:N0}[/]");
        writeStatsTable.AddRow("Expected Writes", $"{expectedWrites:N0}");

        if (writeLatencies.Any())
        {
            var avgWrite = TimeSpan.FromTicks((long)writeLatencies.Average(t => t.Ticks));
            var minWrite = writeLatencies.Min();
            var maxWrite = writeLatencies.Max();
            var p95Write = GetPercentile(writeLatencies, 0.95);

            writeStatsTable.AddRow("Average Latency", $"{avgWrite.TotalMilliseconds:F2} ms");
            writeStatsTable.AddRow("Min Latency", $"{minWrite.TotalMilliseconds:F2} ms");
            writeStatsTable.AddRow("Max Latency", $"{maxWrite.TotalMilliseconds:F2} ms");
            writeStatsTable.AddRow("P95 Latency", $"{p95Write.TotalMilliseconds:F2} ms");
            writeStatsTable.AddRow("Throughput", $"[green]{totalWrites / totalTime.TotalSeconds:F2}[/] writes/sec");
        }

        AnsiConsole.Write(new Panel(writeStatsTable)
            .Header("[bold green]Write Operations (Primary)[/]")
            .BorderColor(Color.Green));

        AnsiConsole.WriteLine();

        // Read Statistics
        var readStatsTable = new Table()
            .Border(TableBorder.Rounded)
            .BorderColor(Color.Cyan);

        readStatsTable.AddColumn("Metric");
        readStatsTable.AddColumn("Value");

        readStatsTable.AddRow("Total Reads", $"[cyan]{totalReads:N0}[/]");
        readStatsTable.AddRow("Expected Reads", $"{expectedWrites * readsPerWrite:N0}");
        readStatsTable.AddRow("Read/Write Ratio", $"[yellow]{(double)totalReads / totalWrites:F2}:1[/]");

        if (readLatencies.Any())
        {
            var avgRead = TimeSpan.FromTicks((long)readLatencies.Average(t => t.Ticks));
            var minRead = readLatencies.Min();
            var maxRead = readLatencies.Max();
            var p95Read = GetPercentile(readLatencies, 0.95);

            readStatsTable.AddRow("Average Latency", $"{avgRead.TotalMilliseconds:F2} ms");
            readStatsTable.AddRow("Min Latency", $"{minRead.TotalMilliseconds:F2} ms");
            readStatsTable.AddRow("Max Latency", $"{maxRead.TotalMilliseconds:F2} ms");
            readStatsTable.AddRow("P95 Latency", $"{p95Read.TotalMilliseconds:F2} ms");
            readStatsTable.AddRow("Throughput", $"[cyan]{totalReads / totalTime.TotalSeconds:F2}[/] reads/sec");
        }

        AnsiConsole.Write(new Panel(readStatsTable)
            .Header("[bold cyan]Read Operations (Replicas)[/]")
            .BorderColor(Color.Cyan));

        AnsiConsole.WriteLine();

        // CQRS Benefits
        var benefitsTable = new Table()
            .Border(TableBorder.Rounded)
            .BorderColor(Color.Yellow)
            .HideHeaders();

        benefitsTable.AddColumn("Benefit");

        benefitsTable.AddRow("[green]✓[/] Concurrent reads and writes executed simultaneously");
        benefitsTable.AddRow($"[green]✓[/] Read operations achieved [yellow]{(double)totalReads / totalWrites:F1}x[/] volume compared to writes");
        benefitsTable.AddRow("[green]✓[/] Reads distributed across [cyan]3 replica nodes[/] (node1, node2, node3)");
        benefitsTable.AddRow("[green]✓[/] Separate read/write databases enable independent scaling");
        benefitsTable.AddRow($"[green]✓[/] Read throughput: [cyan]{totalReads / totalTime.TotalSeconds:F2}[/] ops/sec");
        benefitsTable.AddRow($"[green]✓[/] Write throughput: [green]{totalWrites / totalTime.TotalSeconds:F2}[/] ops/sec");

        if (readLatencies.Any() && writeLatencies.Any())
        {
            var avgRead = TimeSpan.FromTicks((long)readLatencies.Average(t => t.Ticks));
            var avgWrite = TimeSpan.FromTicks((long)writeLatencies.Average(t => t.Ticks));

            if (avgRead < avgWrite)
            {
                benefitsTable.AddRow($"[green]✓[/] Read operations were [yellow]{avgWrite.TotalMilliseconds / avgRead.TotalMilliseconds:F1}x faster[/] than writes on average");
            }
        }

        AnsiConsole.Write(new Panel(benefitsTable)
            .Header("[bold yellow]CQRS Benefits Demonstrated[/]")
            .BorderColor(Color.Yellow));

        AnsiConsole.WriteLine();

        // Success Criteria
        double writeSuccessRate = (double)totalWrites / expectedWrites;
        double readSuccessRate = (double)totalReads / (expectedWrites * readsPerWrite);
        bool writeSuccess = totalWrites >= expectedWrites * 0.95;
        bool readSuccess = totalReads >= (expectedWrites * readsPerWrite) * 0.95;

        var criteriaTable = new Table()
            .Border(TableBorder.Rounded)
            .BorderColor(writeSuccess && readSuccess ? Color.Green : Color.Red);

        criteriaTable.AddColumn("Criterion");
        criteriaTable.AddColumn("Result");
        criteriaTable.AddColumn("Threshold");

        criteriaTable.AddRow(
            "Write Success Rate",
            writeSuccess ? $"[green]{writeSuccessRate:P2}[/]" : $"[red]{writeSuccessRate:P2}[/]",
            "95%"
        );
        criteriaTable.AddRow(
            "Read Success Rate",
            readSuccess ? $"[green]{readSuccessRate:P2}[/]" : $"[red]{readSuccessRate:P2}[/]",
            "95%"
        );

        AnsiConsole.Write(criteriaTable);
        AnsiConsole.WriteLine();

        if (writeSuccess && readSuccess)
        {
            AnsiConsole.Write(
                new Panel("[bold green]✓ CI Mode: SUCCESS[/]\n[dim]All performance thresholds met[/]")
                .BorderColor(Color.Green)
                .Padding(1, 0));
            return 0;
        }
        else
        {
            var errors = new List<string>();
            if (!writeSuccess)
                errors.Add($"Write operations below threshold: {totalWrites}/{expectedWrites}");
            if (!readSuccess)
                errors.Add($"Read operations below threshold: {totalReads}/{expectedWrites * readsPerWrite}");

            AnsiConsole.Write(
                new Panel($"[bold red]✗ CI Mode: FAILED[/]\n{string.Join("\n", errors.Select(e => $"[red]- {e}[/]"))}")
                .BorderColor(Color.Red)
                .Padding(1, 0));
            return 1;
        }
    }

    private static TimeSpan GetPercentile(ConcurrentBag<TimeSpan> values, double percentile)
    {
        var sorted = values.OrderBy(t => t.Ticks).ToArray();
        int index = (int)Math.Ceiling(sorted.Length * percentile) - 1;
        return sorted[Math.Max(0, Math.Min(index, sorted.Length - 1))];
    }
}
