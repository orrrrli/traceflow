using ErrorOr;
using MediatR;
using TraceFlow.Application.Interfaces;
using TraceFlow.Domain.Entities;

namespace TraceFlow.Application.Commands.CreateOrder;

public sealed class CreateOrderCommandHandler(
    IOrderRepository orderRepository) : IRequestHandler<CreateOrderCommand, ErrorOr<CreateOrderResult>>
{
    public async Task<ErrorOr<CreateOrderResult>> Handle(
        CreateOrderCommand command,
        CancellationToken cancellationToken)
    {
        var order = Order.Create(command.CustomerName, command.Description);

        await orderRepository.AddAsync(order, cancellationToken);

        return new CreateOrderResult(
            order.Id,
            order.CustomerName,
            order.Status.ToString(),
            order.CreatedAt);
    }
}