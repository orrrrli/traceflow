# TraceFlow — Technical Project Plan

## Summary

An event-driven, real-time order tracking system. Demonstrates decoupled asynchronous messaging (RabbitMQ), live updates (SignalR), the outbox pattern for transactional consistency, and end-to-end traceability via a Correlation ID propagated across independent processes.

Intended for the portfolio's Work section and as a foundation for technical writeups in the Build & Learn section.

---

## Project goal

A client creates an order through a REST API. The order is saved and, in the same transaction, a domain event is recorded in an outbox table. A dispatch process publishes that event to RabbitMQ. An independent worker (separate process) consumes the event, simulates the order's status progression with delays, and publishes a second event with the new status. The API listens for that second event and pushes it to the connected client via SignalR, updating the UI without a page reload.

Throughout the entire flow — including the hops between asynchronous processes — an `X-Correlation-Id` is propagated, making it possible to trace the full journey of a specific order across the logs of all three components (API, outbox dispatcher, worker).

---

## Technical concepts to demonstrate

- **Outbox pattern**: transactional consistency between writing the order and publishing the event.
- **Decoupled asynchronous messaging**: RabbitMQ as the broker, with exchanges, queues, and bindings configured explicitly (no abstraction library like MassTransit, so the low-level mechanism can be explained in interviews).
- **Real-time updates**: SignalR for server push to the client without polling.
- **Distributed traceability**: Correlation ID propagated manually across HTTP, AMQP messages, and structured logging.
- **Process separation**: API and Worker run as independent containers/services, communicating only through the queue.
- **Idempotency and failure handling** (incremental improvement): manual ack/nack, dead-letter queue, retries.
- **Distributed caching**: Redis for caching order lookups, with explicit cache invalidation driven by domain events.
- **Testing strategy**: integration tests for the full write path, unit tests for handlers and domain logic.
- **API versioning**: URL-path versioning (e.g., `/v1/orders`) to demonstrate backward-compatible API evolution.

---

## Layered architecture

| Layer | Responsibility | Technology |
|---|---|---|
| Domain | `Order` entity, `OrderStatus`, domain events (`OrderCreated`, `OrderStatusChanged`) | Plain C#, no external dependencies |
| Application | Commands/Queries, handler that creates the `OutboxMessage` in the same transaction as the order | CQRS, MediatR |
| Infrastructure | Persistence (`Orders` + `OutboxMessages` tables), RabbitMQ publisher, outbox dispatcher (hosted service), Redis cache | EF Core, PostgreSQL, RabbitMQ.Client, StackExchangeRedis |
| API | Correlation ID middleware, REST endpoints, SignalR Hub, listener for `order-status-updates`, Swagger UI | ASP.NET Core, SignalR, Swashbuckle |
| Worker | Independent project that consumes `order-events`, simulates status progression, publishes `order-status-updates` | .NET Worker Service, RabbitMQ.Client |
| Frontend | Order creation form, SignalR connection grouped by order ID, reactive status timeline | React / Next.js, @microsoft/signalr |

---

## API Contract

### `POST /v1/orders`
Creates a new order.

**Request body:**
```json
{
  "customerName": "string (required, max 100)",
  "description": "string? (optional)"
}
```

**Success — `201 Created`:**
```json
{
  "id": "550e8400-e29b-41d4-a716-446655440000",
  "customerName": "Orlando",
  "description": "Large pepperoni pizza",
  "status": "Pending",
  "createdAt": "2026-06-20T10:00:00Z",
  "updatedAt": "2026-06-20T10:00:00Z"
}
```

**Validation failure — `400 Bad Request` (FluentValidation):**
```json
{
  "errors": {
    "customerName": ["CustomerName is required"]
  }
}
```

### `GET /v1/orders/{id}`
Retrieves a single order. Cached in Redis with 30s TTL.

**Success — `200 OK`:**
Same shape as `POST` response.

**Not found — `404 Not Found` (ErrorOr):**
```json
{
  "error": "Order.NotFound",
  "message": "Order was not found."
}
```

**Invalid GUID — `400 Bad Request`:**
```json
{
  "errors": {
    "id": ["Invalid GUID format"]
  }
}
```

### `GET /v1/orders`
Lists all orders, sorted by `CreatedAt` desc. Uses **cursor pagination** (not offset) to avoid duplicate/missed items if orders are created mid-pagination.

**Query params:**
- `cursor` (string?, base64-encoded — opaque to the client)
- `limit` (int, default 20, max 100)

**Success — `200 OK`:**
```json
{
  "data": [ /* array of order objects */ ],
  "nextCursor": "eyJjcmVhdGVkQXQiOiIyMDI2LTA2LTIwVDEwOjAwOjAwWiIsImlkIjoiNTUwZTg0MDAtZTI5Yi00MWQ0LWE3MTYtNDQ2NjU1NDQwMDAwIn0=",
  "hasMore": true
}
```

**Cursor format (internal, base64-encoded JSON):**
```json
{ "createdAt": "2026-06-20T10:00:00Z", "id": "550e8400-e29b-41d4-a716-446655440000" }
```

**First page:** omit `cursor`.  
**Subsequent pages:** pass `nextCursor` from previous response.  
**End of list:** `hasMore: false`, `nextCursor: null`.

### SignalR Hub — `/hubs/orders`

**Client method:** `JoinOrderGroup(orderId: string)`

**Server push event:** `OrderStatusUpdated`
```json
{
  "orderId": "550e8400-e29b-41d4-a716-446655440000",
  "status": "Preparing",
  "updatedAt": "2026-06-20T10:00:03Z"
}
```

### Frontend behavior — page refresh, reconnection & disconnection

1. **Order ID persistence:** Stored in URL query param (`?order=550e8400-...`) so refreshing or sharing the link preserves context.
2. **Page load flow:**
   - Call `GET /v1/orders/{id}` to get current status and history
   - Connect to SignalR and call `JoinOrderGroup(orderId)`
   - Render timeline with existing data + listen for live updates
3. **If order is already `Delivered`:** Timeline shows complete history; SignalR disconnects automatically.
4. **SignalR reconnection:** Use built-in auto-reconnect with exponential backoff. If reconnection fails after 30 seconds, display "Connection lost — reconnecting..." to the user.
5. **Disconnection rules:**
   - Auto-disconnect when order reaches `Delivered`
   - Disconnect old group when user creates/navigates to a new order
   - On tab close: browser handles connection cleanup

---

## Outbox dispatcher (hosted service)

- **Polling interval:** 2 seconds
- **Batch size:** Fetch up to 10 unprocessed `OutboxMessage` rows per poll, ordered by `CreatedAt` asc
- **Publishing:** Each message is published individually to the correct RabbitMQ exchange (`traceflow.order-events`)
- **Failure handling:** If a single message fails to publish, log the error, update its `Error` column, and **continue with the remaining messages in the batch**. One bad message never blocks the queue.
- **Marking as processed:** After each successful publish, update `ProcessedAt = NOW()` in a **separate small transaction** (the original order creation transaction is long gone)

### `GET /health`
Health check endpoint that verifies connectivity to all infrastructure dependencies.

**Success — `200 OK`:**
```json
{
  "status": "Healthy",
  "checks": {
    "postgresql": "Healthy",
    "redis": "Healthy",
    "rabbitmq": "Healthy"
  }
}
```

**Failure — `503 Service Unavailable`:**
```json
{
  "status": "Unhealthy",
  "checks": {
    "postgresql": "Healthy",
    "redis": "Unhealthy",
    "rabbitmq": "Healthy"
  }
}
```

---

### `GET /v1/orders/{id}/trace`
Retrieves the full trace timeline for an order (used by the frontend trace visualizer).

**Success — `200 OK`:**
```json
{
  "correlationId": "abc-123-def",
  "events": [
    {
      "step": "OrderCreated",
      "service": "API",
      "status": null,
      "timestamp": "2026-06-20T10:00:00Z"
    },
    {
      "step": "DispatchedToQueue",
      "service": "Dispatcher",
      "status": null,
      "timestamp": "2026-06-20T10:00:01Z"
    },
    {
      "step": "WorkerReceived",
      "service": "Worker",
      "status": null,
      "timestamp": "2026-06-20T10:00:02Z"
    },
    {
      "step": "StatusChanged",
      "service": "Worker",
      "status": "Paid",
      "timestamp": "2026-06-20T10:00:05Z"
    }
  ]
}
```

---

## Database schema

### `Orders` table
| Column | Type |
|---|---|
| `Id` | UUID (PK) |
| `CustomerName` | varchar(100) |
| `Description` | text (nullable) |
| `Status` | varchar(50) |
| `CreatedAt` | timestamp |
| `UpdatedAt` | timestamp |

### `OutboxMessages` table
| Column | Type |
|---|---|
| `Id` | UUID (PK) |
| `Type` | varchar(100) |
| `Payload` | jsonb |
| `CorrelationId` | varchar(100) |
| `CreatedAt` | timestamp |
| `ProcessedAt` | timestamp (nullable) |
| `Error` | text (nullable) |

### `TraceEvents` table
| Column | Type |
|---|---|
| `Id` | UUID (PK) |
| `CorrelationId` | varchar(100) |
| `OrderId` | UUID (FK → Orders) |
| `Step` | varchar(100) |
| `Service` | varchar(50) |
| `Status` | varchar(50) (nullable) |
| `Timestamp` | timestamp |

### TraceEvents write responsibilities

| Step | Service | When |
|---|---|---|
| `OrderCreated` | API | In same transaction as order creation |
| `DispatchedToQueue` | Outbox Dispatcher | After successful publish to RabbitMQ |
| `WorkerReceived` | Worker | Immediately after consuming the message |
| `StatusChanged` | API | When background consumer receives status update event |

---

## Write path

1. The React client creates an order and connects to the SignalR hub (joining a group identified by the order ID).
2. The ASP.NET Core API receives the request. The Correlation ID middleware generates (or reads, if already present in the header) the `X-Correlation-Id`.
3. The API saves the order and an `OutboxMessage` with the `OrderCreated` event in the same database transaction. The Correlation ID is stored as part of the message payload/metadata.
4. The outbox dispatcher (hosted service) polls every 2 seconds, reads up to 10 pending messages, and publishes them to the `traceflow.order-events` exchange in RabbitMQ, including the Correlation ID as an AMQP header.
5. The Worker consumes the message, extracts the Correlation ID, and adds it to its own logging scope before processing.

## RabbitMQ topology

| Exchange | Type | Bound To Queue(s) | Purpose |
|---|---|---|---|
| `traceflow.order-events` | `direct` | `traceflow.worker.order-events` | Outbox dispatcher publishes `OrderCreated` events here; Worker consumes |
| `traceflow.order-status-updates` | `direct` | `traceflow.api.order-status-updates` | Worker publishes status changes here; API consumes |
| `traceflow.dlx` (dead-letter) | `direct` | `traceflow.dead-letter` | Failed messages land here after retries are exhausted |

**Queue properties:**
- `durable: true` (survives broker restart)
- Dead-letter exchange configured: `traceflow.dlx`
- Manual ack/nack — consumer must explicitly `basicAck` or `basicNack`
- **Retry policy**: 3 immediate retries on failure (RabbitMQ redelivers), then message is routed to `traceflow.dead-letter`

## Real-time update path

1. The **Worker** consumes the `OrderCreated` event, extracts the Correlation ID, writes a `TraceEvents` row (`WorkerReceived`), and holds the order in memory.
2. **Idempotency check:** Before processing, the Worker checks an in-memory `HashSet<OrderId>`. If already present, it logs a warning, `basicAck`s the message, and skips processing. This prevents duplicate status progressions if RabbitMQ redelivers.
3. It simulates the order's status progression (`Pending` → `Paid` → `Preparing` → `Shipped` → `Delivered`) with **variable delays** for realism:
   - `Pending` → `Paid`: **3 seconds** (instant payment)
   - `Paid` → `Preparing`: **7 seconds** (kitchen prep takes longer)
   - `Preparing` → `Shipped`: **4 seconds** (packaging)
   - `Shipped` → `Delivered`: **6 seconds** (delivery)
3. On each status change, the Worker publishes an `OrderStatusChanged` event to the `traceflow.order-status-updates` exchange, propagating the same Correlation ID it received.
4. The API hosts a **background consumer** (`IHostedService`) that maintains a persistent connection to the `traceflow.api.order-status-updates` queue with `basicQos(prefetchCount: 1)`. On receiving the event, it logs it (with the Correlation ID), **updates the `Order` row in PostgreSQL**, invalidates the cached order entry in Redis, writes a `TraceEvents` row, and pushes it through `IHubContext` to the corresponding SignalR group.
5. The React client receives the live update and reflects the new status in the timeline without reloading.

**Note:** The Worker is **stateless in business logic** — it does not own or persist order state. The ASP.NET Core API remains the single source of truth for all order data. However, the Worker **does connect to PostgreSQL** to write `TraceEvents` rows for observability (trace visualizer), since it has the most accurate timestamp for when it receives and processes messages.

**Known limitation — Worker restart:** If the Worker container restarts while processing an order (e.g., mid-transition from `Paid` to `Preparing`), that order will remain stuck at its current status forever. For a portfolio/demo project, this is acceptable. In production, a self-healing Worker would query the database on startup to resume stuck orders, or a persistent scheduler (Hangfire/Quartz) would manage the status transitions.

---

## Correlation ID propagation — technical detail

- **Generation**: API middleware reads the incoming `X-Correlation-Id` header; if absent, generates a new one (GUID).
- **Logging**: added to the `ILogger` scope (or `LogContext` if using Serilog) so it automatically appears in every log line for the request, without passing it manually through every function.
- **Propagation to RabbitMQ**: included as a custom header in the AMQP message's `BasicProperties` when publishing.
- **Reception in the Worker**: extracted from the consumed message's headers and re-injected into the Worker's logging scope before processing.
- **Propagation back**: the Worker includes the same Correlation ID when publishing the status update event, preserving full traceability across the whole cycle.

---

## Infrastructure

The entire environment runs from a single `docker-compose.yml`:

- PostgreSQL (order and outbox persistence)
- RabbitMQ (with the management plugin enabled for visual queue inspection — useful for demos)
- Redis (distributed cache for order lookups)
- API container (port 5000)
- Worker container (separate service)
- Web container (Next.js frontend, port 3000)

### Database migrations

EF Core migrations run automatically on **API startup** via `db.Database.Migrate()`. The API waits for PostgreSQL to be healthy before starting (Docker Compose `depends_on` with `healthcheck`). The Worker does not run migrations — it connects to the already-migrated database.

## Frontend architecture

The frontend follows **Feature-Sliced Design (FSD)** for project structure and **Atomic Design** for component hierarchy.

### Folder structure (FSD layers)

```
src/
├── app/                    # Next.js App Router — routing, root layout, providers
│   ├── page.tsx            # Imports from pages/home-page/
│   ├── layout.tsx
│   └── globals.css
├── pages/                  # FSD Pages — compositions of widgets
│   └── home-page/
│       └── ui/
│           └── home-page.tsx
├── widgets/                # Complex independent page blocks
│   ├── order-dashboard/
│   └── trace-panel/
├── features/               # User scenarios and business logic
│   ├── create-order/
│   ├── track-order/
│   └── view-trace/
├── entities/               # Business entities
│   └── order/
│       ├── model/          # Types, status enum, constants
│       └── ui/             # OrderCard, OrderStatusBadge
└── shared/                 # Reusable infrastructure
    ├── ui/                 # Atomic Design components
    ├── api/                # Base fetch client, SignalR connection
    ├── lib/                # Utilities, helpers
    └── config/             # Environment config
```

### Component hierarchy (Atomic Design within `shared/ui/`)

| Level | Examples | FSD Location |
|---|---|---|
| **Atoms** | `Button`, `Input`, `Label`, `StatusDot`, `Card` | `shared/ui/atoms/` |
| **Molecules** | `FormField` (Label + Input + error text), `StatusBadge` (StatusDot + text) | `shared/ui/molecules/` |
| **Organisms** | `OrderForm`, `StatusTimeline`, `TraceVisualizer`, `SignalRStatus` | `features/*/ui/` or `widgets/*/ui/` |
| **Templates** | `PageLayout` (header + main + footer) | `shared/ui/templates/` |
| **Pages** | `HomePage` (PageLayout + OrderForm + conditional OrderTracker) | `pages/home-page/ui/` |

### State management

- **React `useState` + `useEffect`** only. No Zustand, no Redux, no Context.
- State is local to features:
  - `create-order` feature manages form state
  - `track-order` feature manages order data + SignalR connection
  - `view-trace` feature manages trace events

### Styling

- **Tailwind CSS** exclusively. No inline styles, no CSS-in-JS.
- **Dark mode:** Full support via `dark:` Tailwind modifiers and Next.js `next-themes` or Tailwind `darkMode: 'class'` strategy. The UI defaults to system preference with a manual toggle.

---

## Deployment & CORS

**Domain:** `getsynka.com`

| Service | URL | Notes |
|---|---|---|
| Frontend (Next.js) | `https://app.getsynka.com` | Separate container, port 3000 |
| API (ASP.NET Core) | `https://api.getsynka.com` | Separate container, port 5000 |
| API Swagger UI | `https://api.getsynka.com/swagger` | Interactive API docs, available in Production |
| RabbitMQ Management | `https://rabbitmq.getsynka.com` | Optional, for demos |

**CORS policy:**
- **Development:** Allow `http://localhost:3000` and `https://localhost:3000`
- **Production:** Allow `https://app.getsynka.com` only
- **Methods:** `GET`, `POST`, `OPTIONS`
- **Headers:** `Content-Type`, `X-Correlation-Id`
- **Never wildcard (`*`)** in production

**Frontend API base URL:** Configured via environment variable:
- Development: `http://localhost:5000`
- Production: `https://api.getsynka.com`

## Reverse proxy & SSL (Cloudflare + Nginx)

**SSL/TLS:** Handled by **Cloudflare** (proxy mode enabled for `*.getsynka.com`). Cloudflare terminates TLS and forwards HTTP traffic to the VPS.

**Reverse proxy on VPS:** **Nginx** listens on port 80 and routes to Docker containers by Host header:

```nginx
# /etc/nginx/sites-available/getsynka.com
server {
    listen 80;
    server_name api.getsynka.com;

    location / {
        proxy_pass http://localhost:5000;
        proxy_set_header Host $host;
        proxy_set_header X-Real-IP $remote_addr;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
    }
}

server {
    listen 80;
    server_name app.getsynka.com;

    location / {
        proxy_pass http://localhost:3000;
        proxy_set_header Host $host;
        proxy_set_header X-Real-IP $remote_addr;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
    }
}
```

**Why this setup:**
- **Cloudflare** provides free SSL, DDoS protection, and CDN caching for static assets
- **Nginx** is familiar, lightweight, and gives full control over routing
- No Let's Encrypt/Certbot needed on the VPS (Cloudflare handles certificates)
- Docker containers expose ports 5000 and 3000 only to localhost (via `ports: - "127.0.0.1:5000:5000"`)

**Security note:** Cloudflare → Nginx traffic is HTTP (not HTTPS) since it stays within the VPS. Only expose Nginx to the internet, not Docker ports directly.

---

## Environment configuration

All services read configuration from **environment variables** — no secrets in `appsettings.json` or committed files.

### `.env` file structure (VPS)

```bash
# Infrastructure
POSTGRES_USER=traceflow
POSTGRES_PASSWORD=<strong_password>
POSTGRES_DB=traceflow
RABBITMQ_DEFAULT_USER=traceflow
RABBITMQ_DEFAULT_PASS=<strong_password>
REDIS_PASSWORD=<strong_password>

# API
ConnectionStrings__Default=Host=postgres;Port=5432;Database=traceflow;Username=${POSTGRES_USER};Password=${POSTGRES_PASSWORD}
ConnectionStrings__Redis=redis:6379,password=${REDIS_PASSWORD}
RabbitMQ__Host=rabbitmq
RabbitMQ__Username=${RABBITMQ_DEFAULT_USER}
RabbitMQ__Password=${RABBITMQ_DEFAULT_PASS}
AllowedOrigins__0=https://app.getsynka.com

# Worker
Worker__ConnectionStrings__Default=${ConnectionStrings__Default}
Worker__RabbitMQ__Host=rabbitmq
Worker__RabbitMQ__Username=${RABBITMQ_DEFAULT_USER}
Worker__RabbitMQ__Password=${RABBITMQ_DEFAULT_PASS}

# Frontend
NEXT_PUBLIC_API_BASE_URL=https://api.getsynka.com
```

### Security
- `.env` is in `.gitignore` — never committed
- On VPS: `chmod 600 .env` so only the deploy user can read it
- Docker Compose reads `.env` automatically from the same directory as `docker-compose.yml`

### Getting `.env` onto the VPS
- **Initial setup:** `scp .env user@getsynka.com:/opt/traceflow/` or create manually via SSH
- **Future:** GitHub Actions pipeline with SSH secrets for automated deployment

---

## Suggested build sequence

1. **Domain + Application with the outbox pattern**, tested against the database only (no RabbitMQ yet). Confirm the order and the event are saved atomically. Write integration tests for the command handler.
2. **Correlation ID middleware** in the API, with structured logging (Serilog) visible in the console.
3. **API versioning + Swagger**: introduce `/v1/` route prefix, version-aware routing, and Swagger UI (`/swagger`) available in both Development and Production so recruiters can explore the API interactively.
4. **RabbitMQ integration**: outbox dispatcher publishing, and a minimal consumer that only logs what it receives (no business logic yet).
5. **Real worker**: simulates status progression and publishes the second event.
6. **Redis + caching layer**: add `GET /v1/orders/{id}` with Redis caching and TTL; invalidate cache on status update.
7. **Second listener in the API** + connected SignalR Hub.
8. **React frontend** connected via SignalR, with a visual timeline.
9. **Dockerize everything** and document in Build & Learn the reasoning behind each decision (outbox, Correlation ID, consumer idempotency, cache invalidation).
10. **Create PRD**: synthesize the final architecture, domain model, API contract, and event schemas into a formal PRD for future reference and portfolio documentation.

---

## Technical decisions and rationale (for Build & Learn writeups)

- **Why the outbox pattern instead of publishing the event directly?** Prevents inconsistency between the database and the queue if the process fails right after saving but before publishing.
- **Why raw RabbitMQ.Client instead of MassTransit?** The value of this project lies in understanding and being able to explain in an interview what an exchange, queue, and binding are, and how ack/nack and dead-letter queues are handled manually. MassTransit can be mentioned as a future production improvement.
- **Why a manual Correlation ID instead of OpenTelemetry from the start?** Demonstrates the concept from first principles before adopting the standard (`traceparent` / W3C Trace Context) as a natural evolution of the project.
- **Why Redis with explicit invalidation instead of a passive TTL-only cache?** Passive TTL risks serving stale data to clients who hit the API between the status update and the TTL expiry. Explicit invalidation on event receipt guarantees consistency and is a pattern worth explaining in interviews.
- **Why URL-path API versioning instead of header-based?** Header versioning is cleaner for pure APIs, but URL-path versioning is more explicit and easier to demonstrate in a portfolio where recruiters may inspect endpoints directly in the browser or Swagger.
- **Why Testcontainers (PostgreSQL) instead of SQLite In-Memory?** SQLite is faster and supports transactions, but PostgreSQL is the real production database. Testing the outbox pattern against PostgreSQL proves that the transaction boundary (saving Order + OutboxMessage atomically) works exactly as it will in production, including UUID handling and any PG-specific behavior. The ~5-10s container startup is acceptable for a portfolio project and demonstrates production-like testing practices.

---

## Project name

**TraceFlow**

Alternative considered: OrderPulse (more domain-oriented; TraceFlow was chosen because it emphasizes the architecture and naturally opens the door to explaining the Correlation ID in interviews).

---

## Portfolio placement

- **Work**: project entry written using the four-question framework (problem → technical decision → who benefits → impact/complexity).
- **Build & Learn**: one or more writeups documenting specific decisions (outbox pattern, Correlation ID propagation, why raw RabbitMQ vs. an abstraction library).

## Trace visualizer (frontend feature)

A dedicated **"Trace"** panel or tab in the React frontend that visualizes the full journey of an order across the distributed system.

**How it works:**
- When an order is created, the frontend captures the Correlation ID from the API response headers.
- As live updates arrive via SignalR, the frontend builds a timeline of events:
  - `OrderCreated` (API)
  - `DispatchedToQueue` (API outbox dispatcher)
  - `WorkerReceived` (Worker)
  - `StatusChanged: Paid` (Worker → API)
  - `StatusChanged: Preparing` (Worker → API)
  - `StatusChanged: Shipped` (Worker → API)
  - `StatusChanged: Delivered` (Worker → API)
- Each event shows:
  - Timestamp (relative or absolute)
  - Service/component that emitted it (API, Worker, Dispatcher)
  - The Correlation ID (same across all events)
  - Status or action description
- Visual styling: connecting line between events, color-coded by component, animated dots for live updates.

**Idempotency demo instruction:**
A "Trigger Duplicate" button in the frontend (dev/demo mode only) manually publishes a duplicate `OrderCreated` event to RabbitMQ for the current order. The trace visualizer then shows:
- The duplicate `WorkerReceived` event with a **warning icon** and label: "Duplicate message detected — ignored by Worker"
- This visually demonstrates that the system handles idempotency correctly

**Why this matters for the portfolio:** Recruiters and interviewers can literally SEE the Correlation ID propagating across independent processes, AND they can see the system gracefully handling duplicate messages. It turns abstract backend concepts into concrete, visual demonstrations. This is the "flashy" part that makes the project memorable.
