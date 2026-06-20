using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TraceFlow.Application.Commands.CreateOrder;
using TraceFlow.Domain.Events;
using TraceFlow.Infrastructure.Persistence;
using Xunit;

namespace TraceFlow.Tests;

public class OutboxAtomicityTests : IClassFixture<TraceFlowApiFactory>
{
    private readonly TraceFlowApiFactory _factory;

    public OutboxAtomicityTests(TraceFlowApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task SuccessfulCommit_PersistsOrderAndOutboxMessageTogether()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var command = new CreateOrderCommand("Atomic Customer", "Atomicity success path");
        var result = await mediator.Send(command);

        result.IsError.Should().BeFalse();

        var order = await dbContext.Orders.FirstOrDefaultAsync(o => o.Id == result.Value.Id);
        order.Should().NotBeNull();

        var outboxMessage = (await dbContext.OutboxMessages.ToListAsync())
            .FirstOrDefault(o => o.Payload.Contains(result.Value.Id.ToString()));

        outboxMessage.Should().NotBeNull();
        outboxMessage!.Type.Should().Be(nameof(OrderCreated));
        outboxMessage.Payload.Should().Contain(result.Value.Id.ToString());
    }

    [Fact]
    public async Task FailedCommit_RollsBackOrderAndOutboxMessage()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var initialOrderCount = await dbContext.Orders.CountAsync();
        var initialOutboxCount = await dbContext.OutboxMessages.CountAsync();

        var command = new CreateOrderCommand(new string('X', 201), "Forces DB constraint violation");

        Func<Task> act = async () => await mediator.Send(command);
        await act.Should().ThrowAsync<Exception>();

        var orderCount = await dbContext.Orders.CountAsync();
        var outboxCount = await dbContext.OutboxMessages.CountAsync();

        orderCount.Should().Be(initialOrderCount);
        outboxCount.Should().Be(initialOutboxCount);
    }
}
