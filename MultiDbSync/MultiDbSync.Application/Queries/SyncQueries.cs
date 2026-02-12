using MultiDbSync.Application.CQRS;
using MultiDbSync.Domain.Entities;
using MultiDbSync.Domain.Interfaces;

namespace MultiDbSync.Application.Queries;

public sealed record GetAllNodesQuery : Query;

public sealed record GetHealthyNodesQuery : Query;

public sealed record GetPrimaryNodesQuery : Query;

public sealed record GetNodeHealthQuery(string NodeId) : Query;

public sealed record GetSyncStatusQuery : Query;

public sealed record GetQuorumStatusQuery(Guid OperationId) : Query;

public sealed class GetAllNodesQueryHandler(
    IDatabaseNodeRepository nodeRepository)
    : IQueryHandler<GetAllNodesQuery, IReadOnlyList<DatabaseNode>>
{
    public async Task<QueryResult<IReadOnlyList<DatabaseNode>>> HandleAsync(
        GetAllNodesQuery query,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var nodes = await nodeRepository.GetAllAsync(cancellationToken);
            return QueryResult<IReadOnlyList<DatabaseNode>>.Success(nodes);
        }
        catch (Exception ex)
        {
            return QueryResult<IReadOnlyList<DatabaseNode>>.Failure($"Failed to get nodes: {ex.Message}");
        }
    }
}

public sealed class GetHealthyNodesQueryHandler(
    IDatabaseNodeRepository nodeRepository)
    : IQueryHandler<GetHealthyNodesQuery, IReadOnlyList<DatabaseNode>>
{
    public async Task<QueryResult<IReadOnlyList<DatabaseNode>>> HandleAsync(
        GetHealthyNodesQuery query,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var nodes = await nodeRepository.GetHealthyNodesAsync(cancellationToken);
            return QueryResult<IReadOnlyList<DatabaseNode>>.Success(nodes);
        }
        catch (Exception ex)
        {
            return QueryResult<IReadOnlyList<DatabaseNode>>.Failure($"Failed to get healthy nodes: {ex.Message}");
        }
    }
}

public sealed class GetPrimaryNodesQueryHandler(
    IDatabaseNodeRepository nodeRepository)
    : IQueryHandler<GetPrimaryNodesQuery, IReadOnlyList<DatabaseNode>>
{
    public async Task<QueryResult<IReadOnlyList<DatabaseNode>>> HandleAsync(
        GetPrimaryNodesQuery query,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var nodes = await nodeRepository.GetPrimaryNodesAsync(cancellationToken);
            return QueryResult<IReadOnlyList<DatabaseNode>>.Success(nodes);
        }
        catch (Exception ex)
        {
            return QueryResult<IReadOnlyList<DatabaseNode>>.Failure($"Failed to get primary nodes: {ex.Message}");
        }
    }
}

public sealed class GetNodeHealthQueryHandler(
    IHealthCheckService healthCheckService)
    : IQueryHandler<GetNodeHealthQuery, HealthStatus>
{
    public async Task<QueryResult<HealthStatus>> HandleAsync(
        GetNodeHealthQuery query,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var health = await healthCheckService.CheckNodeHealthAsync(
                query.NodeId,
                cancellationToken);

            return QueryResult<HealthStatus>.Success(health);
        }
        catch (Exception ex)
        {
            return QueryResult<HealthStatus>.Failure($"Failed to check node health: {ex.Message}");
        }
    }
}

public sealed class GetSyncStatusQueryHandler(
    ISynchronizationService syncService)
    : IQueryHandler<GetSyncStatusQuery, SyncResult>
{
    public async Task<QueryResult<SyncResult>> HandleAsync(
        GetSyncStatusQuery query,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var status = await syncService.GetSyncStatusAsync(cancellationToken);
            return QueryResult<SyncResult>.Success(status);
        }
        catch (Exception ex)
        {
            return QueryResult<SyncResult>.Failure($"Failed to get sync status: {ex.Message}");
        }
    }
}

public sealed class GetQuorumStatusQueryHandler(
    IQuorumService quorumService)
    : IQueryHandler<GetQuorumStatusQuery, QuorumResult>
{
    public async Task<QueryResult<QuorumResult>> HandleAsync(
        GetQuorumStatusQuery query,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var status = await quorumService.GetQuorumResultAsync(
                query.OperationId,
                cancellationToken);

            return QueryResult<QuorumResult>.Success(status);
        }
        catch (Exception ex)
        {
            return QueryResult<QuorumResult>.Failure($"Failed to get quorum status: {ex.Message}");
        }
    }
}
