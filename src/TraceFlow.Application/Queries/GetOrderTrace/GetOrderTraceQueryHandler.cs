using ErrorOr;
using MediatR;
using TraceFlow.Application.Interfaces;

namespace TraceFlow.Application.Queries.GetOrderTrace;

public sealed record GetOrderTraceResult(
    Guid OrderId,
    IReadOnlyList<TraceEventSummary> Events);

public sealed record TraceEventSummary(
    Guid Id,
    Guid CorrelationId,
    string Step,
    string Service,
    string? Status,
    DateTime Timestamp);

public sealed class GetOrderTraceQueryHandler(
    IOrderRepository orderRepository,
    ITraceEventRepository traceEventRepository) : IRequestHandler<GetOrderTraceQuery, ErrorOr<GetOrderTraceResult>>
{
    public async Task<ErrorOr<GetOrderTraceResult>> Handle(
        GetOrderTraceQuery query,
        CancellationToken cancellationToken)
    {
        var orderExists = await orderRepository.GetByIdAsync(query.OrderId, cancellationToken);

        if (orderExists is null)
        {
            return Error.NotFound("Order.NotFound", "Order was not found.");
        }

        var traceEvents = await traceEventRepository.GetByOrderIdAsync(query.OrderId, cancellationToken);

        var summaries = traceEvents.Select(e => new TraceEventSummary(
            e.Id,
            e.CorrelationId,
            e.Step,
            e.Service,
            e.Status,
            e.Timestamp)).ToList();

        return new GetOrderTraceResult(query.OrderId, summaries);
    }
}