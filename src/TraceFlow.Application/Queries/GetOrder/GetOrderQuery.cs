using ErrorOr;
using MediatR;

namespace TraceFlow.Application.Queries.GetOrder;

public sealed record GetOrderQuery(Guid Id) : IRequest<ErrorOr<GetOrderResult>>;