using ErrorOr;
using MediatR;

namespace TraceFlow.Application.Commands.CreateOrder;

public sealed record CreateOrderCommand(
    string CustomerName,
    string? Description = null) : IRequest<ErrorOr<CreateOrderResult>>;

public sealed record CreateOrderResult(Guid Id, string CustomerName, string Status, DateTime CreatedAt);