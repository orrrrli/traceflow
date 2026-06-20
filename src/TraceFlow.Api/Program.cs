using Serilog;
using TraceFlow.Api.Middleware;
using TraceFlow.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, configuration) =>
    configuration
        .ReadFrom.Configuration(context.Configuration)
        .Enrich.FromLogContext());

builder.Services.AddOpenApi();
builder.Services.AddInfrastructure(builder.Configuration);

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseMiddleware<CorrelationIdMiddleware>();
app.UseHttpsRedirection();

app.MapGet("/health", () => Results.Ok("OK"))
    .WithName("Health")
    .WithOpenApi();

app.Run();

public partial class Program { }