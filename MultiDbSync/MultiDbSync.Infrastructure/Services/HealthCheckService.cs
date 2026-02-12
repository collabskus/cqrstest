using System.Diagnostics;
using Microsoft.Extensions.Logging;
using MultiDbSync.Domain.Entities;
using MultiDbSync.Domain.Interfaces;

namespace MultiDbSync.Infrastructure.Services;

public sealed class HealthCheckService(
    IDatabaseNodeRepository nodeRepository,
    ILogger<HealthCheckService> logger)
    : IHealthCheckService
{
    private readonly IDatabaseNodeRepository _nodeRepository = nodeRepository;
    private readonly ILogger<HealthCheckService> _logger = logger;

    public async Task<HealthStatus> CheckNodeHealthAsync(
        string nodeId,
        CancellationToken cancellationToken = default)
    {
        var node = await _nodeRepository.GetByIdAsync(nodeId, cancellationToken);

        if (node is null)
        {
            return new HealthStatus(nodeId, false, 0, "Node not found");
        }

        var stopwatch = Stopwatch.StartNew();

        try
        {
            await Task.Delay(50, cancellationToken);

            stopwatch.Stop();

            var isHealthy = node.IsAlive && node.Status != NodeStatus.Offline;

            _logger.LogDebug("Health check for node {NodeId}: {IsHealthy} ({ResponseTime}ms)",
                nodeId, isHealthy, stopwatch.ElapsedMilliseconds);

            return new HealthStatus(
                nodeId,
                isHealthy,
                stopwatch.ElapsedMilliseconds,
                isHealthy ? null : "Node is not responding");
        }
        catch (Exception ex)
        {
            stopwatch.Stop();

            _logger.LogError(ex, "Health check failed for node {NodeId}", nodeId);

            return new HealthStatus(
                nodeId,
                false,
                stopwatch.ElapsedMilliseconds,
                ex.Message);
        }
    }

    public async Task<IReadOnlyDictionary<string, HealthStatus>> CheckAllNodesAsync(
        CancellationToken cancellationToken = default)
    {
        var nodes = await _nodeRepository.GetAllAsync(cancellationToken);
        var results = new Dictionary<string, HealthStatus>();

        foreach (var node in nodes)
        {
            var health = await CheckNodeHealthAsync(node.NodeId, cancellationToken);
            results[node.NodeId] = health;
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
