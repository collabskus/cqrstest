using Microsoft.Extensions.DependencyInjection;
using Spectre.Console;

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
            var isAutomated = args.Any(a => a is "--demo" or "--automated" or "--ci");

            if (isAutomated)
            {
                var automatedDemo = new AutomatedDemo(serviceProvider, demoSettings);
                await automatedDemo.RunAsync();
            }
            else
            {
                var interactiveDemo = new InteractiveDemo(serviceProvider);
                await interactiveDemo.RunAsync();
            }

            return 0;
        }
        catch (Exception ex)
        {
            AnsiConsole.WriteException(ex);
            return 1;
        }
    }
}
