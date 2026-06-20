using Serilog.Context;
using TraceFlow.Application.Interfaces;

namespace TraceFlow.Api.Middleware;

public sealed class CorrelationIdMiddleware
{
    private readonly RequestDelegate _next;

    public CorrelationIdMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, ICorrelationIdProvider correlationIdProvider)
    {
        var correlationId = ReadOrGenerateCorrelationId(context);

        correlationIdProvider.Set(correlationId);
        context.Response.Headers["X-Correlation-Id"] = correlationId.ToString();

        using (LogContext.PushProperty("CorrelationId", correlationId))
        {
            await _next(context);
        }
    }

    private static Guid ReadOrGenerateCorrelationId(HttpContext context)
    {
        if (context.Request.Headers.TryGetValue("X-Correlation-Id", out var headerValue)
            && Guid.TryParse(headerValue.ToString(), out var parsedCorrelationId))
        {
            return parsedCorrelationId;
        }

        return Guid.NewGuid();
    }
}
