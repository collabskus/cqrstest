using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MultiDbSync.Domain.Interfaces;

public sealed class SynchronizationService(
    INodeDiscoveryService nodeDiscovery,
    IHealthCheckService healthCheck,
    IChangeLogRepository changeLogRepository,
    IOptions<SyncConfiguration> config,
    ILogger<SynchronizationService> logger,
    TimeProvider timeProvider) : BackgroundService
{
    private readonly SyncConfiguration _config = config.Value;
    private readonly TimeSpan _initialStartupDelay = TimeSpan.FromSeconds(15);
    private readonly TimeSpan _noNodesRetryDelay = TimeSpan.FromSeconds(30);
    private int _consecutiveNoNodeWarnings = 0;
    private const int MaxConsecutiveWarnings = 3;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation(
            "Synchronization service starting. Waiting {DelaySeconds}s for initial node discovery and health checks",
            _initialStartupDelay.TotalSeconds);

        // Critical: Wait for initial node discovery and health stabilization
        try
        {
            await Task.Delay(_initialStartupDelay, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            logger.LogInformation("Synchronization service startup cancelled");
            return;
        }

        logger.LogInformation("Synchronization service starting sync cycles");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await PerformSyncCycleAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                logger.LogInformation("Synchronization service stopping");
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Unexpected error during synchronization cycle");
            }

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(_config.SyncIntervalSeconds), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        logger.LogInformation("Synchronization service stopped");
    }

    private async Task PerformSyncCycleAsync(CancellationToken cancellationToken)
    {
        var healthyNodes = await GetHealthyNodesAsync(cancellationToken);

        if (healthyNodes.Count == 0)
        {
            _consecutiveNoNodeWarnings++;

            // Intelligent logging: Only warn periodically to prevent log spam
            if (_consecutiveNoNodeWarnings == 1 || _consecutiveNoNodeWarnings % MaxConsecutiveWarnings == 0)
            {
                logger.LogWarning(
                    "No healthy nodes available for synchronization (occurrence #{Count}). " +
                    "Waiting {DelaySeconds}s before retry. " +
                    "Verify that nodes are registered in Consul and health checks are passing",
                    _consecutiveNoNodeWarnings,
                    _noNodesRetryDelay.TotalSeconds);
            }
            else
            {
                // Use Debug level for repeated warnings
                logger.LogDebug(
                    "No healthy nodes available (occurrence #{Count})",
                    _consecutiveNoNodeWarnings);
            }

            await Task.Delay(_noNodesRetryDelay, cancellationToken);
            return;
        }

        // Success message when recovering from no-node state
        if (_consecutiveNoNodeWarnings > 0)
        {
            logger.LogInformation(
                "Healthy nodes now available. Resuming synchronization after {Count} failed attempts",
                _consecutiveNoNodeWarnings);
            _consecutiveNoNodeWarnings = 0;
        }

        logger.LogInformation(
            "Starting synchronization cycle with {NodeCount} healthy nodes",
            healthyNodes.Count);

        foreach (var node in healthyNodes)
        {
            if (cancellationToken.IsCancellationRequested)
                break;

            try
            {
                await SynchronizeWithNodeAsync(node, cancellationToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex,
                    "Failed to synchronize with node {NodeId} ({Host}:{Port})",
                    node.Id, node.Host, node.Port);
            }
        }

        logger.LogInformation("Synchronization cycle completed");
    }

    private async Task<List<NodeInfo>> GetHealthyNodesAsync(CancellationToken cancellationToken)
    {
        try
        {
            var allNodes = await nodeDiscovery.DiscoverNodesAsync(cancellationToken);

            if (allNodes.Count == 0)
            {
                logger.LogDebug("Node discovery returned no nodes");
                return [];
            }

            logger.LogDebug("Discovered {NodeCount} nodes, checking health", allNodes.Count);

            var healthyNodes = new List<NodeInfo>();

            foreach (var node in allNodes)
            {
                try
                {
                    var isHealthy = await healthCheck.CheckNodeHealthAsync(node, cancellationToken);

                    if (isHealthy)
                    {
                        healthyNodes.Add(node);
                        logger.LogDebug("Node {NodeId} ({Host}:{Port}) is healthy",
                            node.Id, node.Host, node.Port);
                    }
                    else
                    {
                        logger.LogDebug("Node {NodeId} ({Host}:{Port}) failed health check",
                            node.Id, node.Host, node.Port);
                    }
                }
                catch (Exception ex)
                {
                    logger.LogDebug(ex, "Health check error for node {NodeId} ({Host}:{Port})",
                        node.Id, node.Host, node.Port);
                }
            }

            return healthyNodes;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error discovering or health checking nodes");
            return [];
        }
    }

    private async Task SynchronizeWithNodeAsync(NodeInfo node, CancellationToken cancellationToken)
    {
        logger.LogDebug("Synchronizing with node {NodeId} ({Host}:{Port})",
            node.Id, node.Host, node.Port);

        var lastSync = node.LastSyncTimestamp ?? DateTimeOffset.MinValue;
        var changes = await changeLogRepository.GetChangesSinceAsync(
            lastSync,
            cancellationToken);

        if (changes.Count == 0)
        {
            logger.LogDebug("No changes to sync with node {NodeId}", node.Id);
            return;
        }

        logger.LogInformation(
            "Syncing {ChangeCount} changes to node {NodeId} ({Host}:{Port})",
            changes.Count, node.Id, node.Host, node.Port);

        // Your existing sync logic here
    }
}
