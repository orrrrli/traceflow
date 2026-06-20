using Microsoft.EntityFrameworkCore;
using TraceFlow.Domain.Entities;
using TraceFlow.Domain.Enums;
using TraceEvent = TraceFlow.Domain.Events.TraceEvent;

namespace TraceFlow.Infrastructure.Persistence;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();
    public DbSet<TraceEvent> TraceEvents => Set<TraceEvent>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Order>(entity =>
        {
            entity.HasKey(o => o.Id);
            entity.Property(o => o.CustomerName).IsRequired().HasMaxLength(200);
            entity.Property(o => o.Description).HasMaxLength(1000);
            entity.Property(o => o.Status)
                .HasConversion<string>()
                .HasMaxLength(50)
                .IsRequired();
            entity.Property(o => o.CreatedAt).IsRequired();
            entity.Property(o => o.UpdatedAt).IsRequired();
        });

        modelBuilder.Entity<OutboxMessage>(entity =>
        {
            entity.HasKey(o => o.Id);
            entity.Property(o => o.Type).IsRequired().HasMaxLength(200);
            entity.Property(o => o.Payload).IsRequired().HasColumnType("jsonb");
            entity.Property(o => o.CorrelationId).IsRequired();
            entity.Property(o => o.CreatedAt).IsRequired();
            entity.HasIndex(o => o.ProcessedAt);
        });

        modelBuilder.Entity<TraceEvent>(entity =>
        {
            entity.HasKey(t => t.Id);
            entity.Property(t => t.Step).IsRequired().HasMaxLength(100);
            entity.Property(t => t.Service).IsRequired().HasMaxLength(100);
            entity.Property(t => t.Status).HasMaxLength(50);
            entity.Property(t => t.Timestamp).IsRequired();
            entity.HasIndex(t => t.OrderId);
            entity.HasIndex(t => t.CorrelationId);
        });
    }
}
