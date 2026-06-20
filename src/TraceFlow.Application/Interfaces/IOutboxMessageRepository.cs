namespace TraceFlow.Application.Interfaces;

public interface IOutboxMessageRepository
{
    Task AddAsync(
        string type,
        string payload,
        Guid correlationId,
        CancellationToken cancellationToken = default);
}
