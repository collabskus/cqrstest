using System.Text.Json;
using Microsoft.Extensions.Logging;
using MultiDbSync.Domain.Entities;
using MultiDbSync.Domain.Interfaces;
using MultiDbSync.Infrastructure.Data;
using MultiDbSync.Infrastructure.Repositories;

namespace MultiDbSync.Infrastructure.Services;

public sealed class SynchronizationService(
    IEnumerable<MultiDbContext> dbContexts,
    IDatabaseNodeRepository nodeRepository,
    ISyncOperationRepository syncOperationRepository,
    IQuorumService quorumService,
    ILogger<SynchronizationService> logger)
    : ISynchronizationService
{
    private readonly IEnumerable<MultiDbContext> _dbContexts = dbContexts;
    private readonly IDatabaseNodeRepository _nodeRepository = nodeRepository;
    private readonly ISyncOperationRepository _syncOperationRepository = syncOperationRepository;
    private readonly IQuorumService _quorumService = quorumService;
    private readonly ILogger<SynchronizationService> _logger = logger;

    private readonly SemaphoreSlim _syncLock = new(1, 1);

    public async Task<bool> SyncEntityAsync<T>(
        T entity,
        OperationType operationType,
        CancellationToken cancellationToken = default) where T : class
    {
        await _syncLock.WaitAsync(cancellationToken);
        try
        {
            var healthyNodes = await _nodeRepository.GetHealthyNodesAsync(cancellationToken);

            if (healthyNodes.Count == 0)
            {
                _logger.LogWarning("No healthy nodes available for synchronization");
                return false;
            }

            var entityType = typeof(T).Name;
            var entityId = GetEntityId(entity);
            var payload = JsonSerializer.Serialize(entity);

            var successCount = 0;
            var errors = new List<string>();

            foreach (var node in healthyNodes)
            {
                try
                {
                    var syncOperation = new SyncOperation(
                        node.NodeId,
                        operationType,
                        entityType,
                        entityId,
                        payload);

                    await _syncOperationRepository.AddAsync(syncOperation, cancellationToken);

                    await SyncToNodeAsync(node, entity, operationType, cancellationToken);

                    syncOperation.MarkCompleted();
                    await _syncOperationRepository.UpdateAsync(syncOperation, cancellationToken);

                    node.MarkHealthy();
                    await _nodeRepository.UpdateAsync(node, cancellationToken);

                    successCount++;
                    _logger.LogInformation("Successfully synced {EntityType} to node {NodeId}", entityType, node.NodeId);
                }
                catch (Exception ex)
                {
                    node.MarkUnhealthy();
                    await _nodeRepository.UpdateAsync(node, cancellationToken);
                    errors.Add($"{node.NodeId}: {ex.Message}");
                    _logger.LogError(ex, "Failed to sync to node {NodeId}", node.NodeId);
                }
            }

            return successCount > 0;
        }
        finally
        {
            _syncLock.Release();
        }
    }

    public async Task<bool> SyncBatchAsync<T>(
        IEnumerable<T> entities,
        OperationType operationType,
        CancellationToken cancellationToken = default) where T : class
    {
        var entityList = entities.ToList();
        var allSuccess = true;

        foreach (var entity in entityList)
        {
            var success = await SyncEntityAsync(entity, operationType, cancellationToken);
            if (!success)
                allSuccess = false;
        }

        return allSuccess;
    }

    public async Task<bool> ForceSyncAsync(
        string nodeId,
        CancellationToken cancellationToken = default)
    {
        await _syncLock.WaitAsync(cancellationToken);
        try
        {
            var pendingOperations = await _syncOperationRepository.GetPendingOperationsAsync(cancellationToken);

            if (!string.IsNullOrEmpty(nodeId))
            {
                pendingOperations = pendingOperations.Where(o => o.NodeId == nodeId).ToList();
            }

            foreach (var operation in pendingOperations)
            {
                try
                {
                    operation.MarkInProgress();
                    await _syncOperationRepository.UpdateAsync(operation, cancellationToken);

                    var entity = JsonSerializer.Deserialize<object>(operation.Payload);

                    _logger.LogInformation("Retrying sync operation {OperationId} to node {NodeId}",
                        operation.Id, operation.NodeId);

                    operation.MarkCompleted();
                    await _syncOperationRepository.UpdateAsync(operation, cancellationToken);
                }
                catch (Exception ex)
                {
                    if (operation.CanRetry)
                    {
                        operation.MarkPending();
                    }
                    else
                    {
                        operation.MarkFailed(ex.Message);
                    }
                    await _syncOperationRepository.UpdateAsync(operation, cancellationToken);
                }
            }

            return true;
        }
        finally
        {
            _syncLock.Release();
        }
    }

    public async Task<SyncResult> GetSyncStatusAsync(CancellationToken cancellationToken = default)
    {
        var nodes = await _nodeRepository.GetAllAsync(cancellationToken);
        var pendingOps = await _syncOperationRepository.GetPendingOperationsAsync(cancellationToken);

        var successfulNodes = nodes.Count(n => n.Status == NodeStatus.Healthy);
        var failedNodes = nodes.Count - successfulNodes;

        return new SyncResult(
            nodes.Count,
            successfulNodes,
            failedNodes,
            pendingOps.Select(o => $"Operation {o.Id}: {o.ErrorMessage}").ToList());
    }

    private async Task SyncToNodeAsync<T>(
        DatabaseNode node,
        T entity,
        OperationType operationType,
        CancellationToken cancellationToken) where T : class
    {
        await Task.Delay(10, cancellationToken);
    }

    private static string GetEntityId(object entity)
    {
        var idProperty = entity.GetType().GetProperty("Id");
        if (idProperty?.GetValue(entity) is Guid guid)
            return guid.ToString();

        return Guid.NewGuid().ToString();
    }
}
