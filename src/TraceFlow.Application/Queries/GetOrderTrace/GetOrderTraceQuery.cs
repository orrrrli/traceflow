using ErrorOr;
using MediatR;

namespace TraceFlow.Application.Queries.GetOrderTrace;

public sealed record GetOrderTraceQuery(Guid OrderId) : IRequest<ErrorOr<GetOrderTraceResult>>;