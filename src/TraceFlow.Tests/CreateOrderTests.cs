using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TraceFlow.Application.Commands.CreateOrder;
using TraceFlow.Domain.Events;
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

    [Fact]
    public async Task CreateOrderCommandHandler_SavesOrderAndOutboxMessage()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var command = new CreateOrderCommand("Charlie", "Handler test order");
        var result = await mediator.Send(command);

        result.IsError.Should().BeFalse();

        var order = await dbContext.Orders.FirstOrDefaultAsync(o => o.Id == result.Value.Id);
        order.Should().NotBeNull();
        order!.CustomerName.Should().Be("Charlie");
        order.Status.Should().Be(TraceFlow.Domain.Enums.OrderStatus.Pending);

        var outboxMessage = (await dbContext.OutboxMessages.ToListAsync())
            .FirstOrDefault(o => o.Payload.Contains(result.Value.Id.ToString()));

        outboxMessage.Should().NotBeNull();
        outboxMessage!.Type.Should().Be(nameof(OrderCreated));
        outboxMessage.Payload.Should().Contain(result.Value.Id.ToString());
        outboxMessage.ProcessedAt.Should().BeNull();
    }

    [Fact]
    public async Task CreateOrderCommandHandler_RollsBackBoth_WhenSaveFails()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var initialOrderCount = await dbContext.Orders.CountAsync();
        var initialOutboxCount = await dbContext.OutboxMessages.CountAsync();

        var command = new CreateOrderCommand(new string('X', 201), "This should fail");

        Func<Task> act = async () => await mediator.Send(command);
        await act.Should().ThrowAsync<Exception>();

        var orderCount = await dbContext.Orders.CountAsync();
        var outboxCount = await dbContext.OutboxMessages.CountAsync();

        orderCount.Should().Be(initialOrderCount);
        outboxCount.Should().Be(initialOutboxCount);
    }
}
