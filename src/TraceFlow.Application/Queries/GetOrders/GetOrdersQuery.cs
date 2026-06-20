using ErrorOr;
using MediatR;

namespace TraceFlow.Application.Queries.GetOrders;

public sealed record GetOrdersQuery(Guid? Cursor, int Limit = 20) : IRequest<ErrorOr<GetOrdersResult>>;