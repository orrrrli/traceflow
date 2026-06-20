namespace TraceFlow.Domain.Events;

public sealed class TraceEvent
{
    public Guid Id { get; private set; }
    public Guid OrderId { get; private set; }
    public Guid CorrelationId { get; private set; }
    public string Step { get; private set; } = string.Empty;
    public string Service { get; private set; } = string.Empty;
    public string? Status { get; private set; }
    public DateTime Timestamp { get; private set; }

    private TraceEvent() { }

    public static TraceEvent Create(
        Guid orderId,
        Guid correlationId,
        string step,
        string service,
        string? status = null)
    {
        return new TraceEvent
        {
            Id = Guid.NewGuid(),
            OrderId = orderId,
            CorrelationId = correlationId,
            Step = step,
            Service = service,
            Status = status,
            Timestamp = DateTime.UtcNow,
        };
    }
}