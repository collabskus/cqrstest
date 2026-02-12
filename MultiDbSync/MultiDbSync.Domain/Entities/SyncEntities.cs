namespace MultiDbSync.Domain.Entities;

public sealed class DatabaseNode
{
    public string NodeId { get; private set; } = string.Empty;
    public string ConnectionString { get; private set; } = string.Empty;
    public NodeStatus Status { get; private set; }
    public bool IsPrimary { get; private set; }
    public int Priority { get; private set; }
    public DateTime LastHeartbeat { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public int SuccessCount { get; private set; }
    public int FailureCount { get; private set; }
    public double HealthScore => SuccessCount + FailureCount > 0
        ? (double)SuccessCount / (SuccessCount + FailureCount) * 100
        : 100;

    private DatabaseNode() { }

    public DatabaseNode(
        string nodeId,
        string connectionString,
        int priority = 0,
        bool isPrimary = false)
    {
        NodeId = nodeId ?? throw new ArgumentNullException(nameof(nodeId));
        ConnectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
        Status = NodeStatus.Unknown;
        IsPrimary = isPrimary;
        Priority = priority;
        LastHeartbeat = DateTime.UtcNow;
        CreatedAt = DateTime.UtcNow;
    }

    public void MarkHealthy()
    {
        Status = NodeStatus.Healthy;
        LastHeartbeat = DateTime.UtcNow;
        SuccessCount++;
    }

    public void MarkUnhealthy()
    {
        Status = NodeStatus.Unhealthy;
        LastHeartbeat = DateTime.UtcNow;
        FailureCount++;
    }

    public void MarkUnknown()
    {
        Status = NodeStatus.Unknown;
    }

    public void PromoteToPrimary()
    {
        IsPrimary = true;
    }

    public void DemoteFromPrimary()
    {
        IsPrimary = false;
    }

    public bool IsAlive => (DateTime.UtcNow - LastHeartbeat).TotalSeconds < 30;
}

public enum NodeStatus
{
    Unknown,
    Healthy,
    Unhealthy,
    Offline
}

public sealed class SyncOperation
{
    public Guid Id { get; private set; }
    public string NodeId { get; private set; } = string.Empty;
    public OperationType Type { get; private set; }
    public string EntityType { get; private set; } = string.Empty;
    public string EntityId { get; private set; } = string.Empty;
    public string Payload { get; private set; } = string.Empty;
    public SyncStatus Status { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? CompletedAt { get; private set; }
    public string? ErrorMessage { get; private set; }
    public int RetryCount { get; private set; }

    private SyncOperation() { }

    public SyncOperation(
        string nodeId,
        OperationType type,
        string entityType,
        string entityId,
        string payload)
    {
        Id = Guid.NewGuid();
        NodeId = nodeId ?? throw new ArgumentNullException(nameof(nodeId));
        Type = type;
        EntityType = entityType ?? throw new ArgumentNullException(nameof(entityType));
        EntityId = entityId ?? throw new ArgumentNullException(nameof(entityId));
        Payload = payload ?? throw new ArgumentNullException(nameof(payload));
        Status = SyncStatus.Pending;
        CreatedAt = DateTime.UtcNow;
    }

    public void MarkInProgress()
    {
        Status = SyncStatus.InProgress;
    }

    public void MarkCompleted()
    {
        Status = SyncStatus.Completed;
        CompletedAt = DateTime.UtcNow;
    }

    public void MarkFailed(string errorMessage)
    {
        Status = SyncStatus.Failed;
        ErrorMessage = errorMessage;
        CompletedAt = DateTime.UtcNow;
    }

    public void MarkPending()
    {
        Status = SyncStatus.Pending;
        RetryCount++;
    }

    public bool CanRetry => RetryCount < 3 && Status == SyncStatus.Failed;
}

public enum OperationType
{
    Create,
    Update,
    Delete
}

public enum SyncStatus
{
    Pending,
    InProgress,
    Completed,
    Failed
}

public sealed class QuorumVote
{
    public Guid Id { get; private set; }
    public Guid OperationId { get; private set; }
    public string NodeId { get; private set; } = string.Empty;
    public bool Vote { get; private set; }
    public string? Reason { get; private set; }
    public DateTime VotedAt { get; private set; }

    private QuorumVote() { }

    public QuorumVote(
        Guid operationId,
        string nodeId,
        bool vote,
        string? reason = null)
    {
        Id = Guid.NewGuid();
        OperationId = operationId;
        NodeId = nodeId ?? throw new ArgumentNullException(nameof(nodeId));
        Vote = vote;
        Reason = reason;
        VotedAt = DateTime.UtcNow;
    }
}
