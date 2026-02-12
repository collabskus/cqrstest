namespace MultiDbSync.Application.CQRS;

public sealed record CommandResult<T>(
    bool IsSuccess,
    T? Data,
    string? ErrorMessage)
{
    public static CommandResult<T> Success(T data) => new(true, data, null);
    public static CommandResult<T> Failure(string error) => new(false, default, error);
}

public sealed record QueryResult<T>(
    bool IsSuccess,
    T? Data,
    string? ErrorMessage)
{
    public static QueryResult<T> Success(T data) => new(true, data, null);
    public static QueryResult<T> Failure(string error) => new(false, default, error);
}

public interface ICommandHandler<TCommand, TResult> where TCommand : ICommand
{
    Task<CommandResult<TResult>> HandleAsync(TCommand command, CancellationToken cancellationToken = default);
}

public interface IQueryHandler<TQuery, TResult> where TQuery : IQuery
{
    Task<QueryResult<TResult>> HandleAsync(TQuery query, CancellationToken cancellationToken = default);
}

public interface ICommand
{
    Guid Id { get; }
    DateTime CreatedAt { get; }
}

public interface IQuery
{
}

public abstract record Command : ICommand
{
    public Guid Id { get; } = Guid.NewGuid();
    public DateTime CreatedAt { get; } = DateTime.UtcNow;
}

public abstract record Query : IQuery
{
}
