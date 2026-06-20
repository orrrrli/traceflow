using TraceFlow.Domain.Events;

namespace TraceFlow.Application.Interfaces;

public interface ITraceEventRepository
{
    Task<IReadOnlyList<TraceEvent>> GetByOrderIdAsync(Guid orderId, CancellationToken cancellationToken = default);
    Task AddAsync(TraceEvent traceEvent, CancellationToken cancellationToken = default);
}