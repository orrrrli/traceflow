using TraceFlow.Application.Interfaces;
using TraceFlow.Infrastructure.Persistence;

namespace TraceFlow.Infrastructure.Repositories;

public sealed class OutboxMessageRepository(AppDbContext dbContext) : IOutboxMessageRepository
{
    public async Task AddAsync(
        string type,
        string payload,
        Guid correlationId,
        CancellationToken cancellationToken = default)
    {
        var message = OutboxMessage.Create(type, payload, correlationId);
        await dbContext.OutboxMessages.AddAsync(message, cancellationToken);
    }
}
