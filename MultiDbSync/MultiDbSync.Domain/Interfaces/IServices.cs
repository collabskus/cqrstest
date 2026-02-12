using MultiDbSync.Domain.Entities;

namespace MultiDbSync.Domain.Interfaces;

public interface ISynchronizationService
{
    Task<bool> SyncEntityAsync<T>(
        T entity,
        OperationType operationType,
        CancellationToken cancellationToken = default) where T : class;

    Task<bool> SyncBatchAsync<T>(
        IEnumerable<T> entities,
        OperationType operationType,
        CancellationToken cancellationToken = default) where T : class;

    Task<bool> ForceSyncAsync(
        string nodeId,
        CancellationToken cancellationToken = default);

    Task<SyncResult> GetSyncStatusAsync(CancellationToken cancellationToken = default);
}

public interface IQuorumService
{
    Task<bool> RequestVoteAsync(
        Guid operationId,
        string operationDescription,
        CancellationToken cancellationToken = default);

    Task<QuorumResult> GetQuorumResultAsync(
        Guid operationId,
        CancellationToken cancellationToken = default);

    Task<bool> HasConsensusAsync(
        Guid operationId,
        CancellationToken cancellationToken = default);

    int GetRequiredVotes();
}

public interface IFailoverService
{
    Task<bool> TriggerFailoverAsync(
        string failedNodeId,
        CancellationToken cancellationToken = default);

    Task<string?> GetOptimalNodeAsync(
        CancellationToken cancellationToken = default);

    Task<bool> IsFailoverNeededAsync(
        CancellationToken cancellationToken = default);

    Task MonitorNodesAsync(CancellationToken cancellationToken = default);

    event EventHandler<FailoverEventArgs>? FailoverOccurred;
}

public interface IHealthCheckService
{
    Task<HealthStatus> CheckNodeHealthAsync(
        string nodeId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyDictionary<string, HealthStatus>> CheckAllNodesAsync(
        CancellationToken cancellationToken = default);

    Task<bool> IsNodeHealthyAsync(
        string nodeId,
        CancellationToken cancellationToken = default);
}

public record SyncResult(
    int TotalNodes,
    int SuccessfulNodes,
    int FailedNodes,
    IReadOnlyList<string> Errors);

public record QuorumResult(
    Guid OperationId,
    int TotalVotes,
    int YesVotes,
    int NoVotes,
    bool HasConsensus,
    QuorumDecision Decision);

public record HealthStatus(
    string NodeId,
    bool IsHealthy,
    double ResponseTimeMs,
    string? ErrorMessage);

public class FailoverEventArgs : EventArgs
{
    public string FailedNodeId { get; }
    public string NewPrimaryNodeId { get; }
    public DateTime OccurredAt { get; }

    public FailoverEventArgs(string failedNodeId, string newPrimaryNodeId)
    {
        FailedNodeId = failedNodeId;
        NewPrimaryNodeId = newPrimaryNodeId;
        OccurredAt = DateTime.UtcNow;
    }
}

public enum QuorumDecision
{
    Approved,
    Rejected,
    Pending
}
