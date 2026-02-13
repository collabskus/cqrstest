using Microsoft.Extensions.Logging;
using MultiDbSync.Domain.Interfaces;

namespace MultiDbSync.Infrastructure.Services;

public sealed class QuorumService(
    IDatabaseNodeRepository nodeRepository,
    ILogger<QuorumService> logger) : IQuorumService
{
    private readonly Dictionary<Guid, QuorumVoting> _activeVotes = new();

    public async Task<bool> RequestVoteAsync(
        Guid operationId,
        string operationDescription,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var healthyNodes = await nodeRepository.GetHealthyNodesAsync(cancellationToken);

            if (healthyNodes.Count == 0)
            {
                logger.LogWarning("No healthy nodes available for quorum vote");
                return false;
            }

            var voting = new QuorumVoting
            {
                OperationId = operationId,
                Description = operationDescription,
                TotalNodes = healthyNodes.Count,
                StartedAt = DateTime.UtcNow
            };

            _activeVotes[operationId] = voting;

            logger.LogInformation(
                "Quorum vote requested for operation {OperationId}: {Description}",
                operationId,
                operationDescription);

            // In a real implementation, this would contact each node for a vote
            // For demo purposes, we'll simulate automatic approval
            voting.YesVotes = healthyNodes.Count;
            voting.NoVotes = 0;

            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to request quorum vote for operation {OperationId}", operationId);
            return false;
        }
    }

    public Task<QuorumResult> GetQuorumResultAsync(
        Guid operationId,
        CancellationToken cancellationToken = default)
    {
        if (!_activeVotes.TryGetValue(operationId, out var voting))
        {
            logger.LogWarning("No quorum vote found for operation {OperationId}", operationId);
            return Task.FromResult(new QuorumResult(
                operationId,
                0,
                0,
                0,
                false,
                QuorumDecision.Rejected));
        }

        var requiredVotes = GetRequiredVotes(voting.TotalNodes);
        var hasConsensus = voting.YesVotes >= requiredVotes;
        var decision = hasConsensus ? QuorumDecision.Approved :
                      voting.YesVotes + voting.NoVotes < voting.TotalNodes ? QuorumDecision.Pending :
                      QuorumDecision.Rejected;

        return Task.FromResult(new QuorumResult(
            operationId,
            voting.TotalNodes,
            voting.YesVotes,
            voting.NoVotes,
            hasConsensus,
            decision));
    }

    public async Task<bool> HasConsensusAsync(
        Guid operationId,
        CancellationToken cancellationToken = default)
    {
        var result = await GetQuorumResultAsync(operationId, cancellationToken);
        return result.HasConsensus;
    }

    public int GetRequiredVotes()
    {
        // This would typically get the total number of nodes first
        // For now, return a default
        return GetRequiredVotes(3);
    }

    private static int GetRequiredVotes(int totalNodes)
    {
        // Require majority (more than half)
        return (totalNodes / 2) + 1;
    }

    private sealed class QuorumVoting
    {
        public required Guid OperationId { get; init; }
        public required string Description { get; init; }
        public required int TotalNodes { get; init; }
        public int YesVotes { get; set; }
        public int NoVotes { get; set; }
        public required DateTime StartedAt { get; init; }
    }
}
