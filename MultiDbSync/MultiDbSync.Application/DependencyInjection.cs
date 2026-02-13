using Microsoft.Extensions.DependencyInjection;
using MultiDbSync.Application.Commands;
using MultiDbSync.Application.Queries;

namespace MultiDbSync.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        services.AddCQRSHandlers();
        return services;
    }

    private static IServiceCollection AddCQRSHandlers(this IServiceCollection services)
    {
        services.AddSingleton<CreateProductCommandHandler>();
        services.AddSingleton<UpdateProductPriceCommandHandler>();
        services.AddSingleton<UpdateProductStockCommandHandler>();
        services.AddSingleton<AdjustProductStockCommandHandler>();
        services.AddSingleton<DeleteProductCommandHandler>();

        services.AddSingleton<AddDatabaseNodeCommandHandler>();
        services.AddSingleton<RemoveDatabaseNodeCommandHandler>();
        services.AddSingleton<PromoteNodeCommandHandler>();
        services.AddSingleton<TriggerSyncCommandHandler>();
        services.AddSingleton<TriggerFailoverCommandHandler>();

        services.AddSingleton<GetProductByIdQueryHandler>();
        services.AddSingleton<GetAllProductsQueryHandler>();
        services.AddSingleton<GetProductsByCategoryQueryHandler>();
        services.AddSingleton<GetLowStockProductsQueryHandler>();
        services.AddSingleton<SearchProductsQueryHandler>();

        services.AddSingleton<GetAllNodesQueryHandler>();
        services.AddSingleton<GetHealthyNodesQueryHandler>();
        services.AddSingleton<GetPrimaryNodesQueryHandler>();
        services.AddSingleton<GetNodeHealthQueryHandler>();
        services.AddSingleton<GetSyncStatusQueryHandler>();
        services.AddSingleton<GetQuorumStatusQueryHandler>();

        return services;
    }
}
