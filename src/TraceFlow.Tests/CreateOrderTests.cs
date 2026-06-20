using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TraceFlow.Infrastructure.Persistence;
using Xunit;

namespace TraceFlow.Tests;

public class CreateOrderTests : IClassFixture<TraceFlowApiFactory>
{
    private readonly TraceFlowApiFactory _factory;

    public CreateOrderTests(TraceFlowApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task CreateOrder_SavesOrderWithPendingStatus()
    {
        var order = TraceFlow.Domain.Entities.Order.Create("Alice", "Integration test order");

        await using var scope = _factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        await dbContext.Orders.AddAsync(order);
        await dbContext.SaveChangesAsync();

        var saved = await dbContext.Orders.FirstOrDefaultAsync(o => o.Id == order.Id);

        saved.Should().NotBeNull();
        saved!.CustomerName.Should().Be("Alice");
        saved.Status.Should().Be(TraceFlow.Domain.Enums.OrderStatus.Pending);
    }

    [Fact]
    public async Task CreateOrder_PersistsOrderToTestDatabase()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var order = TraceFlow.Domain.Entities.Order.Create("Bob", "Another test order");
        await dbContext.Orders.AddAsync(order);
        await dbContext.SaveChangesAsync();

        var count = await dbContext.Orders.CountAsync();
        count.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task Migrations_AreAppliedToTestContainerDatabase()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var pendingMigrations = await dbContext.Database.GetPendingMigrationsAsync();

        pendingMigrations.Should().BeEmpty();
    }
}
