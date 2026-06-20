using System.Threading;
using TraceFlow.Application.Interfaces;

namespace TraceFlow.Infrastructure.Services;

public sealed class CorrelationIdProvider : ICorrelationIdProvider
{
    private readonly AsyncLocal<Guid> _correlationId = new();

    public Guid Current
    {
        get
        {
            if (_correlationId.Value == Guid.Empty)
            {
                _correlationId.Value = Guid.NewGuid();
            }

            return _correlationId.Value;
        }
    }

    public void Set(Guid correlationId)
    {
        _correlationId.Value = correlationId;
    }
}
