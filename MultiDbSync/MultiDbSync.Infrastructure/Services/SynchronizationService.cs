using Microsoft.Extensions.Logging;
using MultiDbSync.Domain.Entities;
using MultiDbSync.Domain.Interfaces;

namespace MultiDbSync.Infrastructure.Services;

public sealed class SynchronizationService(
    IDatabaseNodeRepository nodeRepository,
    IHealthCheckService healthCheck,
    ILogger<SynchronizationService> logger) : ISynchronizationService
{
    public async Task<bool> SyncEntityAsync<T>(
        T entity,
        OperationType operationType,
        CancellationToken cancellationToken = default) where T : class
    {
        try
        {
            var healthyNodes = await GetHealthyNodesAsync(cancellationToken);

            if (healthyNodes.Count == 0)
            {
                logger.LogWarning("No healthy nodes available for synchronization");
                return false;
            }

            logger.LogInformation(
                "Synchronizing {EntityType} across {NodeCount} nodes",
                typeof(T).Name,
                healthyNodes.Count);

            // In a real implementation, this would sync the entity to all healthy nodes
            // For this demo, we'll consider it successful if we have healthy nodes
            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to synchronize {EntityType}", typeof(T).Name);
            return false;
        }
    }

    public async Task<bool> SyncBatchAsync<T>(
        IEnumerable<T> entities,
        OperationType operationType,
        CancellationToken cancellationToken = default) where T : class
    {
        try
        {
            var healthyNodes = await GetHealthyNodesAsync(cancellationToken);

            if (healthyNodes.Count == 0)
            {
                logger.LogWarning("No healthy nodes available for batch synchronization");
                return false;
            }

            var entitiesList = entities.ToList();

            logger.LogInformation(
                "Synchronizing {Count} {EntityType} entities across {NodeCount} nodes",
                entitiesList.Count,
                typeof(T).Name,
                healthyNodes.Count);

            // In a real implementation, this would sync the batch to all healthy nodes
            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to synchronize batch of {EntityType}", typeof(T).Name);
            return false;
        }
    }

    public async Task<bool> ForceSyncAsync(
        string nodeId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var node = await nodeRepository.GetByIdAsync(nodeId, cancellationToken);

            if (node == null)
            {
                logger.LogWarning("Node {NodeId} not found", nodeId);
                return false;
            }

            var health = await healthCheck.CheckNodeHealthAsync(nodeId, cancellationToken);

            if (!health.IsHealthy)
            {
                logger.LogWarning(
                    "Cannot force sync to unhealthy node {NodeId}: {Message}",
                    nodeId,
                    health.ErrorMessage);
                return false;
            }

            logger.LogInformation("Forcing synchronization with node {NodeId}", nodeId);

            // In a real implementation, this would force a full sync with the specified node
            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to force sync with node {NodeId}", nodeId);
            return false;
        }
    }

    public async Task<SyncResult> GetSyncStatusAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var allNodes = await nodeRepository.GetAllAsync(cancellationToken);
            var healthStatuses = await healthCheck.CheckAllNodesAsync(cancellationToken);

            var totalNodes = allNodes.Count;
            var successfulNodes = healthStatuses.Count(h => h.Value.IsHealthy);
            var failedNodes = totalNodes - successfulNodes;

            var errors = healthStatuses
                .Where(h => !h.Value.IsHealthy)
                .Select(h => h.Value.ErrorMessage ?? "Unknown error")
                .ToList();

            return new SyncResult(
                totalNodes,
                successfulNodes,
                failedNodes,
                errors);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to get sync status");
            return new SyncResult(0, 0, 0, new List<string> { ex.Message });
        }
    }

    private async Task<List<DatabaseNode>> GetHealthyNodesAsync(CancellationToken cancellationToken)
    {
        try
        {
            var allNodes = await nodeRepository.GetAllAsync(cancellationToken);
            var healthyNodes = new List<DatabaseNode>();

            foreach (var node in allNodes)
            {
                var health = await healthCheck.CheckNodeHealthAsync(node.NodeId, cancellationToken);

                if (health.IsHealthy)
                {
                    healthyNodes.Add(node);
                }
            }

            return healthyNodes;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to get healthy nodes");
            return new List<DatabaseNode>();
        }
    }
}
