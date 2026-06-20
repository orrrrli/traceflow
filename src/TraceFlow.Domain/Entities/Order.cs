using TraceFlow.Domain.Enums;

namespace TraceFlow.Domain.Entities;

public sealed class Order
{
    public Guid Id { get; private set; }
    public string CustomerName { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public OrderStatus Status { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    private Order() { }

    public static Order Create(string customerName, string? description = null)
    {
        return new Order
        {
            Id = Guid.NewGuid(),
            CustomerName = customerName,
            Description = description,
            Status = OrderStatus.Pending,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
    }

    public void AdvanceStatus(OrderStatus nextStatus)
    {
        Status = nextStatus;
        UpdatedAt = DateTime.UtcNow;
    }
}
