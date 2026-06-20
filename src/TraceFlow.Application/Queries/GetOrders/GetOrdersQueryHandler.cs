using ErrorOr;
using MediatR;
using TraceFlow.Application.Interfaces;

namespace TraceFlow.Application.Queries.GetOrders;

public sealed record GetOrdersResult(
    IReadOnlyList<GetOrderSummary> Items,
    Guid? NextCursor);

public sealed record GetOrderSummary(
    Guid Id,
    string CustomerName,
    string Status,
    DateTime CreatedAt);

public sealed class GetOrdersQueryHandler(
    IOrderRepository orderRepository) : IRequestHandler<GetOrdersQuery, ErrorOr<GetOrdersResult>>
{
    public async Task<ErrorOr<GetOrdersResult>> Handle(
        GetOrdersQuery query,
        CancellationToken cancellationToken)
    {
        var effectiveLimit = Math.Clamp(query.Limit, 1, 100);

        var orders = await orderRepository.GetAllAsync(effectiveLimit + 1, query.Cursor, cancellationToken);

        var hasNextPage = orders.Count > effectiveLimit;
        var items = hasNextPage ? orders.Take(effectiveLimit).ToList() : orders;

        Guid? nextCursor = hasNextPage && items.Count > 0
            ? items[^1].Id
            : null;

        var summaries = items.Select(o => new GetOrderSummary(
            o.Id,
            o.CustomerName,
            o.Status.ToString(),
            o.CreatedAt)).ToList();

        return new GetOrdersResult(summaries, nextCursor);
    }
}