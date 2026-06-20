namespace TraceFlow.Infrastructure.Persistence;

public sealed class OutboxMessage
{
    public Guid Id { get; private set; }
    public string Type { get; private set; } = string.Empty;
    public string Payload { get; private set; } = string.Empty;
    public Guid CorrelationId { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? ProcessedAt { get; set; }
    public string? Error { get; set; }

    private OutboxMessage() { }

    public static OutboxMessage Create(string type, string payload, Guid correlationId)
    {
        return new OutboxMessage
        {
            Id = Guid.NewGuid(),
            Type = type,
            Payload = payload,
            CorrelationId = correlationId,
            CreatedAt = DateTime.UtcNow,
        };
    }
}
