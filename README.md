# TraceFlow

An event-driven order tracking system built to demonstrate distributed-systems concepts: outbox pattern, RabbitMQ messaging, correlation IDs, Redis caching, and SignalR real-time updates.

## Stack

- **Backend:** ASP.NET Core 9, Clean Architecture, CQRS with MediatR
- **Database:** PostgreSQL with EF Core migrations
- **Messaging:** RabbitMQ via raw `RabbitMQ.Client`
- **Caching:** Redis with explicit invalidation
- **Real-time:** SignalR
- **Tests:** xUnit, FluentAssertions, Testcontainers, WebApplicationFactory

## Running tests

Integration tests spin up a real PostgreSQL container via Testcontainers.

### macOS with Colima

The easiest way is the provided wrapper script, which starts Colima if needed and exports the required Docker environment variables:

```bash
./scripts/test.sh
```

You can also pass any `dotnet test` arguments:

```bash
./scripts/test.sh --filter FullyQualifiedName~CreateOrder
./scripts/test.sh --no-build
```

### Manual

If you already have a Docker-compatible runtime running:

```bash
dotnet test TraceFlow.sln
```

### Prerequisites

- [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)
- A container runtime such as [Colima](https://github.com/abiosoft/colima) + Docker CLI, Docker Desktop, or Rancher Desktop

## Running the API

```bash
dotnet run --project src/TraceFlow.Api/TraceFlow.Api.csproj
```

The API requires PostgreSQL, RabbitMQ, and Redis. Use the included `docker-compose.yml` to start all dependencies:

```bash
docker-compose up -d
```
