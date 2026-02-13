using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MultiDbSync.Application;
using MultiDbSync.Infrastructure;

namespace MultiDbSync.Console;

internal sealed class ConfigurationManager(string databasePath)
{
    private readonly string _databasePath = databasePath;

    public IServiceProvider BuildServiceProvider()
    {
        // Load configuration
        var configuration = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .Build();

        var demoSettings = configuration.GetSection("Demo").Get<DemoSettings>()
            ?? throw new InvalidOperationException("Demo settings are not configured in appsettings.json");

        // Setup Dependency Injection
        var services = new ServiceCollection();

        // Add configuration
        services.AddSingleton(demoSettings);
        services.AddSingleton(configuration);

        // Add Logging
        services.AddLogging(configure =>
        {
            configure.AddConsole();
            configure.AddConfiguration(configuration.GetSection("Logging"));
        });

        // Add application layers
        services.AddInfrastructureServices(_databasePath);
        services.AddApplicationServices();

        return services.BuildServiceProvider();
    }
}
