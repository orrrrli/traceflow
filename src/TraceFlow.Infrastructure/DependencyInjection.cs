using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TraceFlow.Application.Commands.CreateOrder;
using TraceFlow.Application.Interfaces;
using TraceFlow.Infrastructure.Persistence;
using TraceFlow.Infrastructure.Repositories;
using TraceFlow.Infrastructure.Services;

namespace TraceFlow.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' is not configured.");

        services.AddDbContext<AppDbContext>(options =>
            options.UseNpgsql(connectionString));

        services.AddScoped<IOrderRepository, OrderRepository>();
        services.AddScoped<IOutboxMessageRepository, OutboxMessageRepository>();
        services.AddScoped<ITraceEventRepository, TraceEventRepository>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<ICorrelationIdProvider, CorrelationIdProvider>();

        services.AddMediatR(cfg =>
            cfg.RegisterServicesFromAssembly(typeof(CreateOrderCommand).Assembly));

        return services;
    }
}
