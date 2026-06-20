namespace TraceFlow.Application.Interfaces;

public interface ICorrelationIdProvider
{
    Guid Current { get; }

    void Set(Guid correlationId);
}
