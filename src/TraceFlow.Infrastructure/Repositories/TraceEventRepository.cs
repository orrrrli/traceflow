using Microsoft.EntityFrameworkCore;
using TraceFlow.Application.Interfaces;
using TraceFlow.Domain.Events;
using TraceFlow.Infrastructure.Persistence;
using TraceEvent = TraceFlow.Domain.Events.TraceEvent;

namespace TraceFlow.Infrastructure.Repositories;

public sealed class TraceEventRepository : ITraceEventRepository
{
    private readonly AppDbContext _dbContext;

    public TraceEventRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<TraceEvent>> GetByOrderIdAsync(
        Guid orderId,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.TraceEvents
            .AsNoTracking()
            .Where(t => t.OrderId == orderId)
            .OrderBy(t => t.Timestamp)
            .ToListAsync(cancellationToken);
    }

    public async Task AddAsync(TraceEvent traceEvent, CancellationToken cancellationToken = default)
    {
        await _dbContext.TraceEvents.AddAsync(traceEvent, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}