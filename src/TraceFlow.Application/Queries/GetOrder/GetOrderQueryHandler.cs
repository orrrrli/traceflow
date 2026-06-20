using ErrorOr;
using MediatR;
using TraceFlow.Application.Interfaces;

namespace TraceFlow.Application.Queries.GetOrder;

public sealed record GetOrderResult(
    Guid Id,
    string CustomerName,
    string? Description,
    string Status,
    DateTime CreatedAt,
    DateTime UpdatedAt);

public sealed class GetOrderQueryHandler(
    IOrderRepository orderRepository) : IRequestHandler<GetOrderQuery, ErrorOr<GetOrderResult>>
{
    public async Task<ErrorOr<GetOrderResult>> Handle(
        GetOrderQuery query,
        CancellationToken cancellationToken)
    {
        var order = await orderRepository.GetByIdAsync(query.Id, cancellationToken);

        if (order is null)
        {
            return Error.NotFound("Order.NotFound", "Order was not found.");
        }

        return new GetOrderResult(
            order.Id,
            order.CustomerName,
            order.Description,
            order.Status.ToString(),
            order.CreatedAt,
            order.UpdatedAt);
    }
}