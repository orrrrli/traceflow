using Microsoft.EntityFrameworkCore;
using TraceFlow.Application.Interfaces;
using TraceFlow.Domain.Entities;
using TraceFlow.Infrastructure.Persistence;

namespace TraceFlow.Infrastructure.Repositories;

public sealed class OrderRepository : IOrderRepository
{
    private readonly AppDbContext _dbContext;

    public OrderRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Order?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _dbContext.Orders
            .AsNoTracking()
            .FirstOrDefaultAsync(o => o.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<Order>> GetAllAsync(
        int limit,
        Guid? cursor,
        CancellationToken cancellationToken = default)
    {
        var query = _dbContext.Orders.AsNoTracking();

        if (cursor.HasValue)
        {
            query = query.Where(o => o.CreatedAt > (
                _dbContext.Orders
                    .Where(x => x.Id == cursor.Value)
                    .Select(x => x.CreatedAt)
                    .FirstOrDefault()
            ));
        }

        return await query
            .OrderBy(o => o.CreatedAt)
            .Take(limit)
            .ToListAsync(cancellationToken);
    }

    public async Task AddAsync(Order order, CancellationToken cancellationToken = default)
    {
        await _dbContext.Orders.AddAsync(order, cancellationToken);
    }
}