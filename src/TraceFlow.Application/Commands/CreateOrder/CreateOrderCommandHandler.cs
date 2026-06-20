using System.Text.Json;
using ErrorOr;
using MediatR;
using TraceFlow.Application.Interfaces;
using TraceFlow.Domain.Entities;
using TraceFlow.Domain.Events;

namespace TraceFlow.Application.Commands.CreateOrder;

public sealed class CreateOrderCommandHandler(
    IOrderRepository orderRepository,
    IOutboxMessageRepository outboxMessageRepository,
    IUnitOfWork unitOfWork,
    ICorrelationIdProvider correlationIdProvider)
    : IRequestHandler<CreateOrderCommand, ErrorOr<CreateOrderResult>>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<ErrorOr<CreateOrderResult>> Handle(
        CreateOrderCommand command,
        CancellationToken cancellationToken)
    {
        var order = Order.Create(command.CustomerName, command.Description);

        var domainEvent = new OrderCreated(
            order.Id,
            order.CustomerName,
            order.Description,
            order.Status.ToString(),
            order.CreatedAt);

        var payload = JsonSerializer.Serialize(domainEvent, JsonOptions);

        await orderRepository.AddAsync(order, cancellationToken);
        await outboxMessageRepository.AddAsync(
            nameof(OrderCreated),
            payload,
            correlationIdProvider.Current,
            cancellationToken);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new CreateOrderResult(
            order.Id,
            order.CustomerName,
            order.Status.ToString(),
            order.CreatedAt);
    }
}