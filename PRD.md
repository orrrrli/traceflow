# TraceFlow — Product Requirements Document (PRD)

## Problem Statement

I want a portfolio-quality backend project that demonstrates real distributed systems concepts (outbox pattern, async messaging, distributed tracing, caching, real-time updates) in a way that is visually impressive and interview-ready. The project needs to be production-deployable on my own VPS (getsynka.com) and serve as both a learning exercise and a talking point for recruiters.

The core problem: when I describe these backend concepts in interviews, they're abstract. I need a concrete, live system where recruiters can literally see a Correlation ID propagating across independent Docker containers, watch real-time status updates without polling, and understand why the outbox pattern matters — all through a clean, modern UI.

## Solution

**TraceFlow** is an event-driven, real-time order tracking system. A user creates an order through a React frontend. The order is saved atomically with an outbox event in PostgreSQL. A background dispatcher publishes that event to RabbitMQ. An independent Worker (separate container) consumes the event, simulates the order's status progression with realistic delays, and publishes status updates back. The API listens for those updates and pushes them to the connected client via SignalR, updating the UI live. Throughout the entire flow, a Correlation ID is propagated across HTTP, AMQP, and structured logging — visualized in a dedicated trace panel.

The system runs entirely from a single `docker-compose.yml` and deploys to `app.getsynka.com` (frontend) and `api.getsynka.com` (backend).

## User Stories

1. As a recruiter visiting my portfolio, I want to create an order and see its status update in real-time without refreshing the page, so that I understand the system uses server-push technology (SignalR).

2. As a recruiter, I want to see a visual trace of an order's journey across multiple services (API → Dispatcher → RabbitMQ → Worker → API → Frontend), so that I understand distributed tracing and the Correlation ID concept.

3. As a recruiter, I want to see the same Correlation ID appear in every step of the trace, so that I understand how traceability works across asynchronous boundaries.

4. As a developer learning backend patterns, I want to read about why the outbox pattern was chosen over direct publishing, so that I understand transactional consistency between databases and message brokers.

5. As a developer, I want to see explicit cache invalidation in action (Redis), so that I understand why passive TTL-only caching is insufficient for real-time data.

6. As a demo user, I want to trigger a duplicate message and see the system gracefully ignore it, so that I understand idempotency and why it matters in distributed messaging.

7. As a developer, I want to explore the API via Swagger UI, so that I can understand the contract without reading source code.

8. As an interviewer, I want to see integration tests running against PostgreSQL (not SQLite), so that I understand the testing strategy is production-realistic.

9. As a portfolio visitor, I want the UI to support dark mode, so that the demo feels modern and polished.

10. As a deployer, I want the entire system to run from one `docker-compose up`, so that setup is trivial.

11. As an operator, I want a `/health` endpoint that checks PostgreSQL, Redis, and RabbitMQ, so that I know the system's dependencies are healthy.

12. As a frontend user, I want to share an order link (`?order={id}`) and have the exact same state load on refresh, so that the demo is shareable.

13. As a frontend user, I want SignalR to auto-reconnect if my connection drops, so that I don't miss status updates.

14. As a frontend user, I want SignalR to disconnect automatically when an order reaches `Delivered`, so that server resources aren't wasted.

15. As a developer, I want to see cursor-based pagination on the order list, so that I understand why offset pagination breaks under concurrent writes.

16. As a developer, I want API endpoints versioned via URL path (`/v1/`), so that I understand how to evolve APIs without breaking existing clients.

17. As an interviewer, I want to see raw RabbitMQ.Client usage (not MassTransit), so that I understand exchanges, queues, bindings, ack/nack, and dead-letter queues from first principles.

18. As a developer, I want to see FluentValidation and ErrorOr patterns, so that I understand input validation and error handling conventions in .NET.

19. As a developer, I want to see the Worker use manual ack/nack with a retry policy (3 retries → dead-letter queue), so that I understand failure handling in message consumers.

20. As a developer, I want the Worker to be a separate container/service, so that I understand process separation and decoupled architecture.

21. As a developer, I want to see Correlation ID generation in middleware (not manually passed through every function), so that I understand logging scopes and cross-cutting concerns.

22. As a developer, I want to see the outbox dispatcher as a hosted service with a polling interval, so that I understand how to bridge synchronous database writes and asynchronous messaging.

23. As a recruiter, I want the entire demo to complete in ~20 seconds, so that I don't lose attention waiting for status changes.

24. As a developer, I want database migrations to run automatically on API startup, so that deployment is zero-touch.

25. As a developer, I want the frontend to use Feature-Sliced Design (FSD) and Atomic Design, so that the codebase demonstrates modern React architecture.

## Implementation Decisions

### Modules to Build

1. **Domain layer** (`TraceFlow.Domain`)
   - `Order` entity with `OrderStatus` enum (`Pending`, `Paid`, `Preparing`, `Shipped`, `Delivered`)
   - Domain events: `OrderCreated`, `OrderStatusChanged`
   - No external dependencies — pure C#

2. **Application layer** (`TraceFlow.Application`)
   - CQRS with MediatR: `CreateOrderCommand`, `GetOrderQuery`, `GetOrdersQuery`, `GetOrderTraceQuery`
   - Command handler creates `OutboxMessage` in the **same database transaction** as the `Order`
   - Query handlers for cached and uncached reads

3. **Infrastructure layer** (`TraceFlow.Infrastructure`)
   - EF Core `DbContext` with `Orders`, `OutboxMessages`, and `TraceEvents` tables
   - RabbitMQ publisher using raw `RabbitMQ.Client`
   - Outbox dispatcher: `IHostedService` polling every 2 seconds, batch size 10
   - Redis cache service with explicit invalidation
   - `ITraceEventRepository` for writing trace events from API and Worker

4. **API layer** (`TraceFlow.Api`)
   - Correlation ID middleware: reads `X-Correlation-Id` header or generates GUID, pushes to `LogContext`
   - REST endpoints: `POST /v1/orders`, `GET /v1/orders/{id}`, `GET /v1/orders`, `GET /v1/orders/{id}/trace`, `GET /health`
   - SignalR Hub at `/hubs/orders` with `JoinOrderGroup` method and `OrderStatusUpdated` event
   - Background consumer (`IHostedService`) for `traceflow.api.order-status-updates` queue with `prefetchCount=1`
   - Swagger UI available in Production at `/swagger`
   - FluentValidation for input validation
   - ErrorOr for domain errors
   - Serilog for structured logging with Correlation ID in scope

5. **Worker** (`TraceFlow.Worker`)
   - .NET Worker Service
   - Consumes from `traceflow.order-events` queue with manual ack/nack
   - Idempotency: in-memory `HashSet<OrderId>` to skip duplicates
   - Simulates status progression with delays: `Pending`(3s) → `Paid`(7s) → `Preparing`(4s) → `Shipped`(6s) → `Delivered`
   - Publishes `OrderStatusChanged` events to `traceflow.order-status-updates`
   - Writes `TraceEvents` to PostgreSQL
   - **Known limitation:** If Worker restarts mid-processing, stuck orders remain. Documented as a demo trade-off.

6. **Frontend** (`traceflow-web`)
   - Next.js with App Router
   - Feature-Sliced Design (FSD) folder structure
   - Atomic Design component hierarchy (`atoms` / `molecules` / `organisms` / `templates` / `pages`)
   - **shadcn/ui** components as the base UI kit (installed in `shared/ui/atoms/` and composed into molecules/organisms)
   - State: React `useState` + `useEffect` only (no global state library)
   - Tailwind CSS with dark mode support
   - SignalR connection with auto-reconnect and manual disconnect on `Delivered`
   - URL query param persistence (`?order={id}`)
   - Components: `OrderForm`, `StatusTimeline`, `TraceVisualizer`, `SignalRStatus`
   - "Trigger Duplicate" button (dev/demo mode) to demonstrate idempotency visually

### RabbitMQ Topology

| Exchange | Type | Bound To | Purpose |
|---|---|---|---|
| `traceflow.order-events` | `direct` | `traceflow.worker.order-events` | Outbox → Worker |
| `traceflow.order-status-updates` | `direct` | `traceflow.api.order-status-updates` | Worker → API |
| `traceflow.dlx` | `direct` | `traceflow.dead-letter` | Failed messages after 3 retries |

### Database Schema

- **`Orders`**: `Id` (UUID PK), `CustomerName` (varchar 100), `Description` (text, nullable), `Status` (varchar 50), `CreatedAt`, `UpdatedAt`
- **`OutboxMessages`**: `Id` (UUID PK), `Type`, `Payload` (jsonb), `CorrelationId`, `CreatedAt`, `ProcessedAt` (nullable), `Error` (nullable)
- **`TraceEvents`**: `Id` (UUID PK), `CorrelationId`, `OrderId` (FK), `Step`, `Service`, `Status` (nullable), `Timestamp`

### API Contract Summary

- `POST /v1/orders` → `201 Created` (creates order + outbox message atomically)
- `GET /v1/orders/{id}` → `200 OK` (Redis-cached, 30s TTL)
- `GET /v1/orders` → `200 OK` (cursor pagination with `?cursor=&limit=`)
- `GET /v1/orders/{id}/trace` → `200 OK` (trace visualizer data)
- `GET /health` → `200 OK` / `503` (PostgreSQL + Redis + RabbitMQ checks)
- SignalR Hub: `/hubs/orders`, method `JoinOrderGroup(orderId)`, event `OrderStatusUpdated`

### Deployment Architecture

- **Frontend:** `https://app.getsynka.com` (Cloudflare → Nginx → Next.js container on port 3000)
- **API:** `https://api.getsynka.com` (Cloudflare → Nginx → ASP.NET Core container on port 5000)
- **Swagger:** `https://api.getsynka.com/swagger`
- **RabbitMQ Management:** `https://rabbitmq.getsynka.com` (optional)
- **SSL:** Terminated by Cloudflare; Nginx receives HTTP from Cloudflare
- **Config:** All via `.env` file (never committed), Docker Compose reads automatically

### Environment Configuration

All services configured via environment variables:
- `ConnectionStrings__Default` (PostgreSQL)
- `ConnectionStrings__Redis`
- `RabbitMQ__Host`, `RabbitMQ__Username`, `RabbitMQ__Password`
- `AllowedOrigins__0` (CORS: `https://app.getsynka.com`)
- `NEXT_PUBLIC_API_BASE_URL`

### TraceEvents Write Responsibilities

| Step | Service | When |
|---|---|---|
| `OrderCreated` | API | Same transaction as order creation |
| `DispatchedToQueue` | Outbox Dispatcher | After successful RabbitMQ publish |
| `WorkerReceived` | Worker | Immediately after consuming message |
| `StatusChanged` | API | When background consumer receives status update |

## Testing Decisions

### What Makes a Good Test
- Test **external behavior**, not implementation details
- Integration tests prove the outbox atomicity guarantee (Order + OutboxMessage saved together)
- Unit tests cover pure handlers and domain logic
- No testing of SignalR UI interactions (too brittle for a demo)

### Modules to Test

1. **Integration tests** (Testcontainers with PostgreSQL):
   - `CreateOrderCommandHandler` — proves order and outbox message are saved atomically
   - `GetOrderQuery` — proves Redis cache hit/miss behavior (if feasible with Testcontainers)
   - `GetOrdersQuery` — proves cursor pagination works correctly

2. **Unit tests** (xUnit, in-memory):
   - Domain logic (if any complex status rules emerge)
   - FluentValidation rules (input validation)

### No Tests For
- RabbitMQ publishing/consuming (tested manually via the running system)
- SignalR hub (integration with frontend is manual/demo)
- Worker status progression (deterministic delays, tested via running the system)
- Redis cache invalidation (tested via integration with the full Docker stack)

## Out of Scope

- **Authentication/Authorization** — No auth. The API is fully open for demo purposes.
- **Order cancellation or backwards status movement** — Status lifecycle is strictly linear.
- **Real payment processing** — "Paid" is simulated.
- **Items, pricing, inventory** — Order is minimal (name + description + status).
- **Multiple Workers / horizontal scaling** — Single Worker instance. Competing consumer pattern is a future evolution.
- **Worker restart recovery** — If Worker restarts mid-processing, orders may stall. Self-healing is a future improvement.
- **OpenTelemetry / W3C Trace Context** — Manual Correlation ID is intentional to demonstrate first principles.
- **CI/CD pipeline** — Manual deployment via `scp` and `docker-compose`. GitHub Actions is a future improvement.
- **Rate limiting** — Not needed for a portfolio demo.
- **Background job scheduler (Hangfire/Quartz)** — Worker uses simple `Task.Delay`. Persistent scheduling is a future improvement.
- **Order editing or deletion** — Orders are create-and-track only.

## Further Notes

### Portfolio Talking Points

1. **Outbox pattern:** "I didn't publish directly to RabbitMQ because if the process crashes after saving the order but before publishing, the database and queue are inconsistent. The outbox pattern guarantees atomicity."

2. **Raw RabbitMQ.Client:** "I used raw RabbitMQ.Client instead of MassTransit so I can explain exchanges, queues, bindings, ack/nack, and dead-letter queues in an interview. MassTransit is a great production choice, but I wanted to understand the fundamentals first."

3. **Correlation ID:** "I implemented manual Correlation ID propagation across HTTP headers, AMQP headers, and structured logging scopes before adopting OpenTelemetry. This helped me understand traceability from first principles."

4. **Redis invalidation:** "I used explicit cache invalidation driven by domain events instead of passive TTL. If I only relied on TTL, a client hitting the API between a status update and TTL expiry would see stale data."

5. **Trace visualizer:** "The trace panel visualizes the Correlation ID propagating across three independent Docker containers. Recruiters can literally see the distributed system working."

### Build Sequence

1. Domain + Application with outbox pattern, tested against PostgreSQL (Testcontainers)
2. Correlation ID middleware + structured logging (Serilog)
3. API versioning + Swagger UI
4. RabbitMQ integration (outbox dispatcher publishing)
5. Real Worker (status progression + publishing)
6. Redis caching layer + explicit invalidation
7. API background consumer + SignalR Hub
8. React frontend (SignalR + status timeline + trace visualizer)
9. Dockerize everything
10. Create formal PRD (this document)

### Known Limitations (Documented for Interviews)

- Worker restart does not resume stuck orders (self-healing is a future improvement)
- Idempotency is in-memory only (Worker restart loses the deduplication set)
- No authentication (demo-only API)
- Single Worker instance (no competing consumers)
