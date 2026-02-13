using Microsoft.Extensions.Logging;
using MultiDbSync.Domain.Interfaces;
using MultiDbSync.Infrastructure.Data;

namespace MultiDbSync.Infrastructure.Services;

public sealed class HealthCheckService(
    MultiDbContextFactory dbFactory,
    ILogger<HealthCheckService> logger) : IHealthCheckService
{
    public async Task<HealthStatus> CheckNodeHealthAsync(
        string nodeId,
        CancellationToken cancellationToken = default)
    {
        var startTime = DateTime.UtcNow;

        try
        {
            await using var context = dbFactory.CreateDbContext(nodeId);

            // Test database connectivity
            await context.Database.CanConnectAsync(cancellationToken);

            var responseTime = (DateTime.UtcNow - startTime).TotalMilliseconds;

            logger.LogDebug(
                "Node {NodeId} health check passed in {ResponseTime}ms",
                nodeId,
                responseTime);

            return new HealthStatus(nodeId, true, responseTime, null);
        }
        catch (Exception ex)
        {
            var responseTime = (DateTime.UtcNow - startTime).TotalMilliseconds;

            logger.LogWarning(
                ex,
                "Node {NodeId} health check failed after {ResponseTime}ms",
                nodeId,
                responseTime);

            return new HealthStatus(nodeId, false, responseTime, ex.Message);
        }
    }

    public async Task<IReadOnlyDictionary<string, HealthStatus>> CheckAllNodesAsync(
        CancellationToken cancellationToken = default)
    {
        var results = new Dictionary<string, HealthStatus>();

        try
        {
            // Get list of known nodes from the primary database
            await using var primaryContext = dbFactory.CreateDbContext("node1");
            var nodes = await primaryContext.DatabaseNodes.AsAsyncEnumerable()
                .ToListAsync(cancellationToken);

            foreach (var node in nodes)
            {
                var health = await CheckNodeHealthAsync(node.NodeId, cancellationToken);
                results[node.NodeId] = health;
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to check all nodes");
        }

        return results;
    }

    public async Task<bool> IsNodeHealthyAsync(
        string nodeId,
        CancellationToken cancellationToken = default)
    {
        var health = await CheckNodeHealthAsync(nodeId, cancellationToken);
        return health.IsHealthy;
    }
}
