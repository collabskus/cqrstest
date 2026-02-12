using Microsoft.Extensions.Logging;
using MultiDbSync.Domain.Entities;
using MultiDbSync.Domain.Interfaces;

namespace MultiDbSync.Infrastructure.Services;

public sealed class FailoverService(
    IDatabaseNodeRepository nodeRepository,
    IHealthCheckService healthCheckService,
    IQuorumService quorumService,
    ILogger<FailoverService> logger)
    : IFailoverService
{
    private readonly IDatabaseNodeRepository _nodeRepository = nodeRepository;
    private readonly IHealthCheckService _healthCheckService = healthCheckService;
    private readonly IQuorumService _quorumService = quorumService;
    private readonly ILogger<FailoverService> _logger = logger;

    private readonly object _failoverLock = new();
    private CancellationTokenSource? _monitoringCts;

    public event EventHandler<FailoverEventArgs>? FailoverOccurred;

    public async Task<bool> TriggerFailoverAsync(
        string failedNodeId,
        CancellationToken cancellationToken = default)
    {
        lock (_failoverLock)
        {
            if (string.IsNullOrEmpty(failedNodeId))
            {
                _logger.LogWarning("No failed node ID provided");
                return false;
            }
        }

        try
        {
            _logger.LogInformation("Triggering failover for failed node {FailedNodeId}", failedNodeId);

            var failedNode = await _nodeRepository.GetByIdAsync(failedNodeId, cancellationToken);
            if (failedNode is null)
            {
                _logger.LogWarning("Failed node {FailedNodeId} not found", failedNodeId);
                return false;
            }

            var healthyNodes = await _nodeRepository.GetHealthyNodesAsync(cancellationToken);
            var nonPrimaryNodes = healthyNodes.Where(n => !n.IsPrimary).ToList();

            if (nonPrimaryNodes.Count == 0)
            {
                _logger.LogError("No healthy non-primary nodes available for failover");
                return false;
            }

            var newPrimary = nonPrimaryNodes
                .OrderByDescending(n => n.Priority)
                .ThenByDescending(n => n.HealthScore)
                .First();

            var hasConsensus = await _quorumService.HasConsensusAsync(
                Guid.NewGuid(),
                cancellationToken);

            if (!hasConsensus)
            {
                _logger.LogWarning("No quorum consensus for failover");
                return false;
            }

            var allNodes = await _nodeRepository.GetAllAsync(cancellationToken);
            foreach (var node in allNodes.Where(n => n.IsPrimary))
            {
                node.DemoteFromPrimary();
                await _nodeRepository.UpdateAsync(node, cancellationToken);
            }

            newPrimary.PromoteToPrimary();
            await _nodeRepository.UpdateAsync(newPrimary, cancellationToken);

            failedNode.MarkUnhealthy();
            await _nodeRepository.UpdateAsync(failedNode, cancellationToken);

            _logger.LogInformation(
                "Failover completed: {FailedNodeId} -> {NewPrimaryNodeId}",
                failedNodeId,
                newPrimary.NodeId);

            FailoverOccurred?.Invoke(
                this,
                new FailoverEventArgs(failedNodeId, newPrimary.NodeId));

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failover failed for node {FailedNodeId}", failedNodeId);
            return false;
        }
    }

    public async Task<string?> GetOptimalNodeAsync(
        CancellationToken cancellationToken = default)
    {
        var healthyNodes = await _nodeRepository.GetHealthyNodesAsync(cancellationToken);

        if (healthyNodes.Count == 0)
            return null;

        var optimalNode = healthyNodes
            .OrderByDescending(n => n.Priority)
            .ThenByDescending(n => n.HealthScore)
            .First();

        return optimalNode.NodeId;
    }

    public async Task<bool> IsFailoverNeededAsync(
        CancellationToken cancellationToken = default)
    {
        var primaryNodes = await _nodeRepository.GetPrimaryNodesAsync(cancellationToken);

        foreach (var primary in primaryNodes)
        {
            var health = await _healthCheckService.CheckNodeHealthAsync(
                primary.NodeId,
                cancellationToken);

            if (!health.IsHealthy)
                return true;
        }

        return false;
    }

    public async Task MonitorNodesAsync(CancellationToken cancellationToken = default)
    {
        _monitoringCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        _logger.LogInformation("Starting node monitoring");

        try
        {
            while (!_monitoringCts.Token.IsCancellationRequested)
            {
                try
                {
                    var allNodes = await _nodeRepository.GetAllAsync(_monitoringCts.Token);

                    foreach (var node in allNodes)
                    {
                        var health = await _healthCheckService.CheckNodeHealthAsync(
                            node.NodeId,
                            _monitoringCts.Token);

                        if (health.IsHealthy)
                        {
                            node.MarkHealthy();
                        }
                        else
                        {
                            node.MarkUnhealthy();

                            if (node.IsPrimary)
                            {
                                _logger.LogWarning(
                                    "Primary node {NodeId} is unhealthy, triggering failover",
                                    node.NodeId);

                                await TriggerFailoverAsync(node.NodeId, _monitoringCts.Token);
                            }
                        }

                        await _nodeRepository.UpdateAsync(node, _monitoringCts.Token);
                    }

                    await Task.Delay(TimeSpan.FromSeconds(5), _monitoringCts.Token);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error during node monitoring");
                    await Task.Delay(TimeSpan.FromSeconds(5), _monitoringCts.Token);
                }
            }
        }
        finally
        {
            _logger.LogInformation("Node monitoring stopped");
        }
    }

    public void StopMonitoring()
    {
        _monitoringCts?.Cancel();
    }
}
