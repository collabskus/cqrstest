using Microsoft.Extensions.Logging;
using MultiDbSync.Domain.Entities;
using MultiDbSync.Domain.Interfaces;

namespace MultiDbSync.Infrastructure.Services;

public sealed class FailoverService(
    IDatabaseNodeRepository nodeRepository,
    IHealthCheckService healthCheckService,
    ILogger<FailoverService> logger) : IFailoverService
{
    public event EventHandler<FailoverEventArgs>? FailoverOccurred;

    public async Task<bool> TriggerFailoverAsync(
        string failedNodeId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            logger.LogWarning("Triggering failover for failed node {NodeId}", failedNodeId);

            var failedNode = await nodeRepository.GetByIdAsync(failedNodeId, cancellationToken);
            if (failedNode == null)
            {
                logger.LogError("Failed node {NodeId} not found in repository", failedNodeId);
                return false;
            }

            // Mark the failed node as unhealthy
            failedNode.MarkUnhealthy();
            await nodeRepository.UpdateAsync(failedNode, cancellationToken);

            // Find a new primary if the failed node was primary
            if (failedNode.IsPrimary)
            {
                var newPrimaryId = await GetOptimalNodeAsync(cancellationToken);

                if (newPrimaryId == null)
                {
                    logger.LogError("No suitable replacement node found for failed primary {NodeId}", failedNodeId);
                    return false;
                }

                var newPrimary = await nodeRepository.GetByIdAsync(newPrimaryId, cancellationToken);
                if (newPrimary != null)
                {
                    // Demote the failed node from primary
                    failedNode.DemoteFromPrimary();
                    await nodeRepository.UpdateAsync(failedNode, cancellationToken);

                    // Promote the new node to primary
                    newPrimary.PromoteToPrimary();
                    await nodeRepository.UpdateAsync(newPrimary, cancellationToken);

                    logger.LogInformation(
                        "Promoted node {NewNodeId} to primary after failure of {FailedNodeId}",
                        newPrimaryId,
                        failedNodeId);

                    FailoverOccurred?.Invoke(this, new FailoverEventArgs(failedNodeId, newPrimaryId));
                }
            }

            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to trigger failover for node {NodeId}", failedNodeId);
            return false;
        }
    }

    public async Task<string?> GetOptimalNodeAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var healthyNodes = await nodeRepository.GetHealthyNodesAsync(cancellationToken);

            if (healthyNodes.Count == 0)
            {
                logger.LogWarning("No healthy nodes available for selection");
                return null;
            }

            // Select node with highest priority
            var optimalNode = healthyNodes
                .OrderByDescending(n => n.Priority)
                .ThenBy(n => n.NodeId)
                .FirstOrDefault();

            return optimalNode?.NodeId;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to get optimal node");
            return null;
        }
    }

    public async Task<bool> IsFailoverNeededAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var primaryNodes = await nodeRepository.GetPrimaryNodesAsync(cancellationToken);

            // Check if primary nodes are healthy
            foreach (var primaryNode in primaryNodes)
            {
                var isHealthy = await healthCheckService.IsNodeHealthyAsync(
                    primaryNode.NodeId,
                    cancellationToken);

                if (!isHealthy)
                {
                    logger.LogWarning("Primary node {NodeId} is unhealthy - failover needed", primaryNode.NodeId);
                    return true;
                }
            }

            return false;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to check if failover is needed");
            return false;
        }
    }

    public async Task MonitorNodesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var isFailoverNeeded = await IsFailoverNeededAsync(cancellationToken);

                if (isFailoverNeeded)
                {
                    var primaryNodes = await nodeRepository.GetPrimaryNodesAsync(cancellationToken);

                    foreach (var primaryNode in primaryNodes)
                    {
                        var isHealthy = await healthCheckService.IsNodeHealthyAsync(
                            primaryNode.NodeId,
                            cancellationToken);

                        if (!isHealthy)
                        {
                            await TriggerFailoverAsync(primaryNode.NodeId, cancellationToken);
                        }
                    }
                }

                // Wait before next check
                await Task.Delay(TimeSpan.FromSeconds(30), cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
            logger.LogInformation("Node monitoring stopped");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error in node monitoring");
        }
    }
}
