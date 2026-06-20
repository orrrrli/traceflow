namespace TraceFlow.Domain.Events;

public sealed record OrderCreated(
    Guid OrderId,
    string CustomerName,
    string? Description,
    string Status,
    DateTime CreatedAt);
