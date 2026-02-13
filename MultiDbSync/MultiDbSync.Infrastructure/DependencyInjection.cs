using Microsoft.Extensions.DependencyInjection;
using MultiDbSync.Domain.Interfaces;
using MultiDbSync.Infrastructure.Data;
using MultiDbSync.Infrastructure.Repositories;
using MultiDbSync.Infrastructure.Services;

namespace MultiDbSync.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructureServices(
        this IServiceCollection services,
        string databasePath)
    {
        // Register the factory as singleton
        services.AddSingleton(_ => new MultiDbContextFactory(databasePath));

        // Register primary context for default operations
        services.AddSingleton<MultiDbContext>(sp =>
        {
            var factory = sp.GetRequiredService<MultiDbContextFactory>();
            return factory.CreateDbContext("node1");
        });

        // Register repositories as scoped
        services.AddScoped<IProductRepository, ProductRepository>();
        services.AddScoped<IOrderRepository, OrderRepository>();
        services.AddScoped<ICustomerRepository, CustomerRepository>();
        services.AddScoped<IDatabaseNodeRepository, DatabaseNodeRepository>();
        services.AddScoped<ISyncOperationRepository, SyncOperationRepository>();

        // Register services as singletons
        services.AddSingleton<IHealthCheckService, HealthCheckService>();
        services.AddSingleton<IQuorumService, QuorumService>();
        services.AddSingleton<ISynchronizationService, SynchronizationService>();
        services.AddSingleton<IFailoverService, FailoverService>();

        return services;
    }
}
