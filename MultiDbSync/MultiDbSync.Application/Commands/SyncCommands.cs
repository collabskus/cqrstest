using MultiDbSync.Application.CQRS;
using MultiDbSync.Domain.Entities;
using MultiDbSync.Domain.Interfaces;

namespace MultiDbSync.Application.Commands;

public sealed record AddDatabaseNodeCommand(
    string NodeId,
    string ConnectionString,
    int Priority = 0,
    bool IsPrimary = false) : Command;

public sealed record RemoveDatabaseNodeCommand(string NodeId) : Command;

public sealed record PromoteNodeCommand(string NodeId) : Command;

public sealed record DemoteNodeCommand(string NodeId) : Command;

public sealed record TriggerSyncCommand(string? NodeId = null) : Command;

public sealed record TriggerFailoverCommand(string? FailedNodeId = null) : Command;

public sealed class AddDatabaseNodeCommandHandler(
    IDatabaseNodeRepository nodeRepository,
    IHealthCheckService? healthCheckService = null)
    : ICommandHandler<AddDatabaseNodeCommand, DatabaseNode>
{
    public async Task<CommandResult<DatabaseNode>> HandleAsync(
        AddDatabaseNodeCommand command,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var node = new DatabaseNode(
                command.NodeId,
                command.ConnectionString,
                command.Priority,
                command.IsPrimary);

            if (healthCheckService is not null)
            {
                var health = await healthCheckService.CheckNodeHealthAsync(
                    command.NodeId,
                    cancellationToken);

                if (health.IsHealthy)
                {
                    node.MarkHealthy();
                }
            }

            await nodeRepository.AddAsync(node, cancellationToken);

            return CommandResult<DatabaseNode>.Success(node);
        }
        catch (Exception ex)
        {
            return CommandResult<DatabaseNode>.Failure($"Failed to add database node: {ex.Message}");
        }
    }
}

public sealed class RemoveDatabaseNodeCommandHandler(
    IDatabaseNodeRepository nodeRepository)
    : ICommandHandler<RemoveDatabaseNodeCommand, bool>
{
    public async Task<CommandResult<bool>> HandleAsync(
        RemoveDatabaseNodeCommand command,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var node = await nodeRepository.GetByIdAsync(command.NodeId, cancellationToken);
            if (node is null)
                return CommandResult<bool>.Failure("Node not found");

            await nodeRepository.DeleteAsync(command.NodeId, cancellationToken);

            return CommandResult<bool>.Success(true);
        }
        catch (Exception ex)
        {
            return CommandResult<bool>.Failure($"Failed to remove database node: {ex.Message}");
        }
    }
}

public sealed class PromoteNodeCommandHandler(
    IDatabaseNodeRepository nodeRepository,
    IQuorumService quorumService)
    : ICommandHandler<PromoteNodeCommand, DatabaseNode>
{
    public async Task<CommandResult<DatabaseNode>> HandleAsync(
        PromoteNodeCommand command,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var node = await nodeRepository.GetByIdAsync(command.NodeId, cancellationToken);
            if (node is null)
                return CommandResult<DatabaseNode>.Failure("Node not found");

            var hasConsensus = await quorumService.HasConsensusAsync(
                Guid.NewGuid(),
                cancellationToken);

            if (!hasConsensus)
                return CommandResult<DatabaseNode>.Failure("No quorum consensus for promotion");

            var allNodes = await nodeRepository.GetAllAsync(cancellationToken);
            foreach (var n in allNodes.Where(n => n.IsPrimary && n.NodeId != command.NodeId))
            {
                n.DemoteFromPrimary();
                await nodeRepository.UpdateAsync(n, cancellationToken);
            }

            node.PromoteToPrimary();
            await nodeRepository.UpdateAsync(node, cancellationToken);

            return CommandResult<DatabaseNode>.Success(node);
        }
        catch (Exception ex)
        {
            return CommandResult<DatabaseNode>.Failure($"Failed to promote node: {ex.Message}");
        }
    }
}

public sealed class TriggerSyncCommandHandler(
    ISynchronizationService syncService)
    : ICommandHandler<TriggerSyncCommand, SyncResult>
{
    public async Task<CommandResult<SyncResult>> HandleAsync(
        TriggerSyncCommand command,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var success = await syncService.ForceSyncAsync(
                command.NodeId ?? string.Empty,
                cancellationToken);

            var result = await syncService.GetSyncStatusAsync(cancellationToken);

            return CommandResult<SyncResult>.Success(result);
        }
        catch (Exception ex)
        {
            return CommandResult<SyncResult>.Failure($"Failed to trigger sync: {ex.Message}");
        }
    }
}

public sealed class TriggerFailoverCommandHandler(
    IFailoverService failoverService)
    : ICommandHandler<TriggerFailoverCommand, string>
{
    public async Task<CommandResult<string>> HandleAsync(
        TriggerFailoverCommand command,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var success = await failoverService.TriggerFailoverAsync(
                command.FailedNodeId ?? string.Empty,
                cancellationToken);

            if (!success)
                return CommandResult<string>.Failure("Failover failed");

            var newPrimary = await failoverService.GetOptimalNodeAsync(cancellationToken);
            return CommandResult<string>.Success(newPrimary ?? "Unknown");
        }
        catch (Exception ex)
        {
            return CommandResult<string>.Failure($"Failed to trigger failover: {ex.Message}");
        }
    }
}
