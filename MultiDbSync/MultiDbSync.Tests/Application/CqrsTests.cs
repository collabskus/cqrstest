using MultiDbSync.Application.CQRS;
using Xunit;

namespace MultiDbSync.Tests.Application;

public class CqrsBaseTests
{
    [Fact]
    public void CommandResult_Success_ShouldReturnSuccessResult()
    {
        var result = CommandResult<string>.Success("test data");

        Assert.True(result.IsSuccess);
        Assert.Equal("test data", result.Data);
        Assert.Null(result.ErrorMessage);
    }

    [Fact]
    public void CommandResult_Failure_ShouldReturnFailureResult()
    {
        var result = CommandResult<string>.Failure("error message");

        Assert.False(result.IsSuccess);
        Assert.Null(result.Data);
        Assert.Equal("error message", result.ErrorMessage);
    }

    [Fact]
    public void QueryResult_Success_ShouldReturnSuccessResult()
    {
        var result = QueryResult<string>.Success("test data");

        Assert.True(result.IsSuccess);
        Assert.Equal("test data", result.Data);
        Assert.Null(result.ErrorMessage);
    }

    [Fact]
    public void QueryResult_Failure_ShouldReturnFailureResult()
    {
        var result = QueryResult<string>.Failure("error message");

        Assert.False(result.IsSuccess);
        Assert.Null(result.Data);
        Assert.Equal("error message", result.ErrorMessage);
    }

    [Fact]
    public void Command_ShouldHaveUniqueId()
    {
        var command1 = new TestCommand();
        var command2 = new TestCommand();

        Assert.NotEqual(Guid.Empty, command1.Id);
        Assert.NotEqual(Guid.Empty, command2.Id);
        Assert.NotEqual(command1.Id, command2.Id);
    }

    [Fact]
    public void Command_ShouldHaveCreationTimestamp()
    {
        var before = DateTime.UtcNow;
        var command = new TestCommand();
        var after = DateTime.UtcNow;

        Assert.InRange(command.CreatedAt, before, after);
    }

    private sealed record TestCommand : Command;
}
