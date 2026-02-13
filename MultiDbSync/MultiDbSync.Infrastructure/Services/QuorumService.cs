using Microsoft.Extensions.Logging;
using MultiDbSync.Domain.Entities;
using MultiDbSync.Domain.Interfaces;

namespace MultiDbSync.Infrastructure.Services;

public sealed class QuorumService(
    IDatabaseNodeRepository nodeRepository,
    ILogger<QuorumService> logger)
    : IQuorumService
{
    private readonly IDatabaseNodeRepository _nodeRepository = nodeRepository;
    private readonly ILogger<QuorumService> _logger = logger;

    private readonly Dictionary<Guid, List<QuorumVote>> _votes = [];
    private readonly Dictionary<Guid, DateTime> _voteTimers = [];
    private readonly TimeSpan _voteTimeout = TimeSpan.FromSeconds(10);

    public async Task<bool> RequestVoteAsync(
        Guid operationId,
        string operationDescription,
        CancellationToken cancellationToken = default)
    {
        var healthyNodes = await _nodeRepository.GetHealthyNodesAsync(cancellationToken);

        if (healthyNodes.Count == 0)
        {
            _logger.LogWarning("No healthy nodes available for quorum vote");
            return false;
        }

        var votes = new List<QuorumVote>();
        _votes[operationId] = votes;
        _voteTimers[operationId] = DateTime.UtcNow.Add(_voteTimeout);

        _logger.LogInformation("Requesting quorum vote for operation {OperationId}: {Description}",
            operationId, operationDescription);

        foreach (var node in healthyNodes)
        {
            try
            {
                var vote = await CastVoteAsync(operationId, node, cancellationToken);
                votes.Add(vote);

                _logger.LogDebug("Node {NodeId} voted {Vote} for operation {OperationId}",
                    node.NodeId, vote.Vote ? "YES" : "NO", operationId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get vote from node {NodeId}", node.NodeId);
            }
        }

        return await HasConsensusAsync(operationId, cancellationToken);
    }

    public async Task<QuorumResult> GetQuorumResultAsync(
        Guid operationId,
        CancellationToken cancellationToken = default)
    {
        if (!_votes.TryGetValue(operationId, out var votes))
        {
            var healthyNodes = await _nodeRepository.GetHealthyNodesAsync(cancellationToken);
            return new QuorumResult(
                operationId,
                healthyNodes.Count,
                0,
                0,
                false,
                QuorumDecision.Pending);
        }

        var yesVotes = votes.Count(v => v.Vote);
        var noVotes = votes.Count(v => !v.Vote);
        var totalVotes = votes.Count;

        var hasConsensus = totalVotes > 0 && yesVotes > totalVotes / 2;

        var decision = hasConsensus ? QuorumDecision.Approved :
                      noVotes > totalVotes / 2 ? QuorumDecision.Rejected :
                      QuorumDecision.Pending;

        return new QuorumResult(
            operationId,
            totalVotes,
            yesVotes,
            noVotes,
            hasConsensus,
            decision);
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
        return 2;
    }

    private async Task<QuorumVote> CastVoteAsync(
        Guid operationId,
        DatabaseNode node,
        CancellationToken cancellationToken)
    {
        await Task.Delay(50, cancellationToken);

        var vote = new QuorumVote(
            operationId,
            node.NodeId,
            true,
            "Node operational");

        return vote;
    }
}
