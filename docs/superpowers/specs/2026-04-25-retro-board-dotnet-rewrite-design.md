# RetroBoard — .NET + Postgres Rewrite

**Date:** 2026-04-25
**Status:** Design approved, pending implementation plan

## 1. Goal & Scope

Rewrite the RetroBoard backend from Firebase Realtime Database to an ASP.NET Core API backed by Postgres, deployed to the user's home k8s cluster. The currently-deployed Firebase Hosting site stays live, untouched, until the new stack is shipped — but the Firebase code is removed from the repo (we are not maintaining it any further).

In scope:

- ASP.NET Core 9 web API with SignalR realtime hub.
- Postgres schema + EF Core migrations.
- CQRS via MediatR (commands, queries, notifications), FluentValidation pipeline.
- Angular client rewritten against the new API (single build target).
- Local dev via `docker-compose` (Postgres only) + `dotnet run`.
- Integration tests using `WebApplicationFactory` + Testcontainers Postgres.
- Containerization and k8s manifests for deployment.

Out of scope:

- Auth / user accounts (open access preserved).
- SignalR scale-out backplane (single replica for v1).
- Migrating data from the existing Firebase RTDB.
- CI pipeline configuration.

## 2. Existing Functionality (Reference)

The current Firebase-backed Angular app supports:

- Create board (slug derived from name, must be unique). Default columns: "What went well", "What didn't go well", "Shoutouts", "Action items".
- Import board with custom columns and prefilled cards (xlsx-based).
- List boards by recency.
- View a single board live.
- Add card (text, author, columnIndex).
- Delete card.
- Vote on a card; one vote per `sessionId` per card.
- Presence: join/leave with a display name; multiple connections per participant tracked under a session id.
- Realtime: SSE streams against Firebase RTDB REST.
- No auth: DB rules are read/write open. Identity = display name + client-generated `sessionId` (persisted in `localStorage`).

## 3. Decisions

| Topic | Decision |
|---|---|
| Realtime | SignalR (WebSocket hub) |
| Auth | Open access — display name + client `sessionId`, no accounts |
| Hosting | Home k8s, single API replica for v1 |
| Schema | Fully normalized Postgres |
| Presence | SignalR lifecycle as primary + periodic background sweeper |
| Client | Same repo, Firebase code deleted, single API build target |
| Write model | HTTP for writes, SignalR for server→client push |
| App architecture | CQRS via MediatR, FluentValidation behavior |
| Existing Firebase deploy | Left running until manually retired; no further deploys to it |

## 4. Architecture

```
┌──── Firebase Hosting (existing artifact, frozen) ────┐
│   Old Angular bundle still served from last deploy   │
│        └─► Firebase RTDB (untouched)                 │
└──────────────────────────────────────────────────────┘
            (no longer connected to this repo)

┌──── Home k8s (new) ──────────────────────────────────┐
│   Angular (api build) served by in-cluster nginx     │
│        │                                             │
│        ├─► HTTP  ──► ASP.NET Core API (1 replica)    │
│        └─► WS    ──► SignalR Hub (same app)          │
│                              │                       │
│                              └─► Postgres            │
└──────────────────────────────────────────────────────┘
```

### 4.1 Solution layout

- **`RetroBoard.Domain`** — entities, value objects, pure rules (slugify, participant key, default columns, validation invariants). No external dependencies.
- **`RetroBoard.Application`** — MediatR `IRequest` commands/queries + handlers, FluentValidation validators, MediatR pipeline behaviors (validation, logging), DTOs, `INotification` domain events. Depends on Domain.
- **`RetroBoard.Infrastructure`** — EF Core `DbContext`, EF migrations, Postgres-specific config, repository implementations. Depends on Domain + Application abstractions.
- **`RetroBoard.Api`** — ASP.NET Core host. Thin controllers (delegate to MediatR), `BoardHub` (SignalR), `BoardEventNotificationHandler` that converts `INotification` events into hub broadcasts, `PresenceSweeperService : BackgroundService`. Depends on Application + Infrastructure.
- **`RetroBoard.Domain.Tests`** — xUnit, no I/O.
- **`RetroBoard.Application.Tests`** — xUnit, in-memory fakes / EF in-memory.
- **`RetroBoard.Api.Tests`** — xUnit + `WebApplicationFactory` + Testcontainers Postgres.
- **Angular client** — same repo, single build target, depends only on the new API.

### 4.2 Write flow (CQRS)

1. Client `POST /api/boards/{slug}/cards` with body.
2. Controller calls `await _mediator.Send(new AddCardCommand(...))`.
3. Validation pipeline behavior runs the FluentValidation validator.
4. Logging behavior emits structured logs.
5. Handler persists via repository (`SaveChangesAsync` inside a transaction where needed) and returns `CardDto`.
6. Handler publishes `CardAddedNotification` via `IMediator.Publish` (after commit).
7. `CardAddedNotificationHandler` invokes `IHubContext<BoardHub>.Clients.Group("board:{slug}").SendAsync("CardAdded", dto)`.
8. Controller returns `201 Created` with the DTO.

### 4.3 Read flow

- **Initial load:** client `GET /api/boards/{slug}` returns full snapshot (`BoardDto`).
- **Streaming:** client opens a SignalR connection to `/hubs/board`, invokes `JoinBoard(slug, sessionId, displayName)`, then receives `CardAdded` / `CardDeleted` / `VoteCast` / `PresenceChanged` events on the group.
- **Reconnect:** SignalR auto-reconnects; client re-invokes `JoinBoard` and refetches the snapshot to replace any partial state.

## 5. Data Model

All timestamps `timestamptz`. snake_case names.

### `boards`

| column | type | notes |
|---|---|---|
| `id` | `bigint generated always as identity` PK | |
| `slug` | `text` UNIQUE NOT NULL | from `slugify(name)`, used in URLs |
| `name` | `text` NOT NULL | |
| `created_at` | `timestamptz` NOT NULL DEFAULT `now()` | |

### `board_columns`

| column | type | notes |
|---|---|---|
| `id` | `bigint generated always as identity` PK | |
| `board_id` | `bigint` NOT NULL REFERENCES `boards(id)` ON DELETE CASCADE | |
| `position` | `int` NOT NULL | 0-based |
| `title` | `text` NOT NULL | |
| UNIQUE `(board_id, position)` | | |

### `cards`

| column | type | notes |
|---|---|---|
| `id` | `uuid` PK DEFAULT `gen_random_uuid()` | client uses as stable key |
| `board_id` | `bigint` NOT NULL REFERENCES `boards(id)` ON DELETE CASCADE | |
| `column_id` | `bigint` NOT NULL REFERENCES `board_columns(id)` ON DELETE CASCADE | |
| `text` | `text` NOT NULL | |
| `author` | `text` NOT NULL | display name at creation time |
| `created_at` | `timestamptz` NOT NULL DEFAULT `now()` | |
| INDEX `(board_id, created_at)` | | |

### `card_votes`

| column | type | notes |
|---|---|---|
| `card_id` | `uuid` NOT NULL REFERENCES `cards(id)` ON DELETE CASCADE | |
| `session_id` | `text` NOT NULL | |
| `created_at` | `timestamptz` NOT NULL DEFAULT `now()` | |
| PRIMARY KEY `(card_id, session_id)` | | enforces vote-once-per-session at DB level |

Vote count is an aggregate: `SELECT count(*) FROM card_votes WHERE card_id = ?`. No denormalized counter.

### `participants`

| column | type | notes |
|---|---|---|
| `id` | `bigint generated always as identity` PK | |
| `board_id` | `bigint` NOT NULL REFERENCES `boards(id)` ON DELETE CASCADE | |
| `participant_key` | `text` NOT NULL | slugified display name |
| `display_name` | `text` NOT NULL | |
| `joined_at` | `timestamptz` NOT NULL DEFAULT `now()` | |
| `last_seen_at` | `timestamptz` NOT NULL DEFAULT `now()` | |
| UNIQUE `(board_id, participant_key)` | | |

### `participant_connections`

| column | type | notes |
|---|---|---|
| `participant_id` | `bigint` NOT NULL REFERENCES `participants(id)` ON DELETE CASCADE | |
| `connection_id` | `text` NOT NULL | SignalR `Context.ConnectionId` |
| `session_id` | `text` NOT NULL | client-generated, stable across reconnects |
| `connected_at` | `timestamptz` NOT NULL DEFAULT `now()` | refreshed by heartbeat |
| PRIMARY KEY `(participant_id, connection_id)` | | |
| INDEX `(connection_id)` | | for disconnect lookup |

A participant is "online" iff at least one row exists in `participant_connections`.

### Migrations

EF Core migrations live in `RetroBoard.Infrastructure/Migrations`. Applied at startup in Development; applied via a one-shot k8s `Job` (or startup-gated env var) in production — final choice made during implementation.

## 6. API Surface

All under `/api`, JSON in/out (camelCase), errors as `application/problem+json`. Swagger UI at `/swagger` in Development.

| Method | Path | Body | Returns | Notes |
|---|---|---|---|---|
| `POST` | `/api/boards` | `{ name, columns?: string[] }` | `201` + `BoardDto` | `409` if slug exists |
| `POST` | `/api/boards/import` | `{ name, columns: string[], cards: [{text, author, columnIndex, votes}] }` | `201` + `BoardDto` | xlsx import path |
| `GET`  | `/api/boards` | — | `BoardSummaryDto[]` ordered by `createdAt desc` | |
| `GET`  | `/api/boards/{slug}` | — | `BoardDto` | `404` if missing |
| `HEAD` | `/api/boards/{slug}` | — | `200` / `404` | replaces `boardExists` |
| `POST` | `/api/boards/{slug}/cards` | `{ text, author, columnIndex }` | `201` + `CardDto` | |
| `DELETE` | `/api/boards/{slug}/cards/{cardId}` | — | `204` | |
| `POST` | `/api/boards/{slug}/cards/{cardId}/votes` | `{ sessionId }` | `200` + `{ voted: bool, votes: int }` | idempotent |

### DTO shapes

```ts
BoardSummaryDto    { id: string, slug: string, name: string, createdAt: string }
BoardDto           { id, slug, name, createdAt, columns: ColumnDto[], cards: CardDto[] }
ColumnDto          { id, position, title }
CardDto            { id, columnId, columnIndex, text, author, createdAt, votes }
ParticipantDto     { participantKey, displayName, joinedAt, lastSeenAt, connectionCount }
VoteResultDto      { voted: boolean, votes: number }
```

`columnIndex` on `CardDto` is derived from the column's `position` for client convenience.

## 7. SignalR Contract

Hub path: `/hubs/board`. Single hub class `BoardHub`.

### Client → Server (hub invocations)

| Method | Args | Effect |
|---|---|---|
| `JoinBoard` | `slug, sessionId, displayName` | Adds connection to group `board:{slug}`; upserts `participants` row + `participant_connections` row; broadcasts `PresenceChanged`; returns current `ParticipantDto[]` to caller |
| `LeaveBoard` | `slug` | Removes from group; deletes connection row; broadcasts `PresenceChanged` |
| `Heartbeat` | — | Updates `last_seen_at` on the participant and `connected_at` on the connection. Client invokes every 60 seconds |

`OnDisconnectedAsync` performs the same cleanup as `LeaveBoard` for every group the connection joined (tracked in `Context.Items`).

### Server → Client (group broadcasts on `board:{slug}`)

| Event | Payload |
|---|---|
| `CardAdded` | `CardDto` |
| `CardDeleted` | `{ cardId }` |
| `VoteCast` | `{ cardId, votes, sessionId }` |
| `PresenceChanged` | `ParticipantDto[]` (full list — small enough; simpler than diffs) |

## 8. CQRS Components

**Commands:** `CreateBoardCommand`, `ImportBoardCommand`, `AddCardCommand`, `DeleteCardCommand`, `CastVoteCommand`, `JoinBoardCommand`, `LeaveBoardCommand`, `RefreshPresenceCommand`, `SweepStalePresenceCommand`.

**Queries:** `GetBoardQuery`, `ListBoardsQuery`, `BoardExistsQuery`.

**Notifications (`INotification`):** `CardAddedNotification`, `CardDeletedNotification`, `VoteCastNotification`, `PresenceChangedNotification`. Each has a single notification handler that calls `IHubContext<BoardHub>.Clients.Group(...)`.

**Pipeline behaviors:** `ValidationBehavior` (FluentValidation), `LoggingBehavior` (structured request/response logging with timing).

Notifications are published from inside command handlers *after* `SaveChangesAsync` commits, so a rolled-back transaction never fans out events.

## 9. Realtime, Concurrency, Edge Cases

### 9.1 Vote concurrency

`card_votes (card_id, session_id)` composite PK enforces vote-once-per-session at the database. Handler issues:

```sql
INSERT INTO card_votes (card_id, session_id)
VALUES (@cardId, @sessionId)
ON CONFLICT DO NOTHING;
```

Then `SELECT count(*) FROM card_votes WHERE card_id = @cardId`. `voted = (rowsInserted == 1)`. `VoteCastNotification` is published only when `voted` is true. No race condition possible.

### 9.2 Presence — connect, disconnect, sweep

**`OnConnectedAsync`** — no DB work; client must call `JoinBoard` to identify itself.

**`JoinBoard(slug, sessionId, displayName)`** —
1. Upsert `participants` row for `(board_id, participant_key)`.
2. Insert `participant_connections (participant_id, connection_id, session_id)`.
3. `Groups.AddToGroupAsync(connectionId, "board:{slug}")`.
4. Stash `(slug, participantId)` in `Context.Items` for later cleanup.
5. Publish `PresenceChangedNotification(slug)`.

**`OnDisconnectedAsync`** —
1. For each `(slug, participantId)` in `Context.Items`:
   1. Delete row in `participant_connections` where `connection_id = Context.ConnectionId`.
   2. If the participant has zero remaining connections, delete the participant row.
   3. Publish `PresenceChangedNotification(slug)`.

**`PresenceSweeperService : BackgroundService`** — every 60s:
1. Delete `participant_connections` rows where `connected_at < now() - interval '5 minutes'`.
2. Delete `participants` rows that now have zero connections.
3. For each affected board, publish `PresenceChangedNotification`.

**Heartbeat** — client invokes `Heartbeat` every 60s; handler updates `participant_connections.connected_at` and `participants.last_seen_at` for the caller. Sweeper threshold (5 min) sits well above 2× heartbeat interval to tolerate transient delays.

### 9.3 Edge cases

- **Same display name, different sessions:** share the same `participants` row (same `participant_key`); each connection is tracked separately. Matches existing Firebase behavior.
- **Empty / invalid display name:** server falls back to `anon-{n}` (where `n` is the original length), matching the existing `participantKey` helper.
- **Slug collision on `CreateBoard`:** unique constraint on `boards.slug` → repository catches Postgres error 23505 → handler throws a domain exception → controller returns `409 Conflict`.
- **Voting / deleting on a missing card:** FK violation (23503) → controller returns `404 Not Found`.
- **Reconnect:** SignalR auto-reconnects; client re-invokes `JoinBoard` with the same `sessionId` (persisted in `localStorage`). New `connection_id`, same session. Old connection row is removed by `OnDisconnectedAsync` or the sweeper.
- **Out-of-order events:** SignalR is ordered per connection. After reconnect, the client refetches the board snapshot via HTTP and resumes streaming. No event sequencing required.
- **Missed events while disconnected:** explicit re-snapshot on reconnect replaces partial state.
- **Notification fails after commit:** logged, not retried. Worst case a client misses a delta and sees it on next refresh. Acceptable for this app.

## 10. Angular Client

Single repo, single build target, no Firebase code. Specifically:

- Delete `src/app/services/retro-board-firebase.service.ts`.
- Drop `firebase` and `@angular/fire` from `package.json`.
- Drop `firebase.json` and `database.rules.json`.
- Add a single `RetroBoardApiService` that uses `HttpClient` for writes and `@microsoft/signalr` `HubConnection` for the realtime read stream.
- `environment.ts` shape:
  ```ts
  export const environment = {
    production: boolean,
    apiBaseUrl: string,
    hubUrl: string,
  };
  ```
- The existing Firebase Hosting deployment continues to serve its previously-built artifact and continues to read/write Firebase RTDB. It is independent of source — no further work in this project touches it.

## 11. Testing

Three layers:

1. **Domain unit tests** — xUnit, no I/O. Slugify, participant key, default columns, validation invariants.
2. **Application handler tests** — xUnit. Each handler tested against in-memory fake repositories or EF Core `UseInMemoryDatabase`. Validators reject bad input. Notification publishing verified with a `Mock<IPublisher>`.
3. **Integration tests** — xUnit + `WebApplicationFactory` + Testcontainers Postgres (real Postgres in Docker, real EF migrations). Coverage:
   - Full HTTP request → DB → response for every endpoint.
   - Concurrent vote race: spawn N parallel requests, assert exactly one `voted=true` and final `votes==1`.
   - SignalR contract: two `HubConnection` test clients, one adds a card via HTTP, the other receives `CardAdded`. Same for `VoteCast`, `CardDeleted`, `PresenceChanged`.
   - Presence sweeper: insert stale rows, run one sweep tick, assert cleanup + notification.

## 12. Local Dev

`docker-compose.yml` at repo root with only `postgres:16`, named volume, exposed on `localhost:5432`. The API runs on the host via `dotnet run` for fast inner loop. Migrations run on startup in Development. Swagger UI at `/swagger`. CORS open to `http://localhost:4200`.

## 13. Deployment (Home k8s)

Following the pattern of existing `hs-*` services and `hs-deployment-manifests`:

- **Container** — multistage Dockerfile (sdk-build → aspnet-runtime), publishes `RetroBoard.Api`. Image pushed to the same registry used by other home services (final registry chosen during implementation).
- **Manifests** (location chosen during implementation — either added to `hs-deployment-manifests` or new `retro-board-deployment` repo, matching project convention):
  - `Deployment` for the API — **1 replica for v1**. SignalR scale-out across replicas requires a Redis backplane and is deferred.
  - `Service` (ClusterIP) on port 80.
  - `Ingress` with TLS (existing cert-manager), WebSocket upgrade headers enabled. Hostname e.g. `retro.<domain>`.
  - `Deployment` + `PVC` + `Service` for Postgres in the same namespace, OR connection details to a shared Postgres if one exists in cluster (verified during implementation).
  - `Secret` for the Postgres connection string, mounted as env var.
  - `Job` for `dotnet ef database update` per release, OR migrations on startup gated by an env var (chosen during implementation).
  - `ConfigMap` for non-secret config.
  - **Frontend** — separate `Deployment` running `nginx:alpine` serving the Angular `dist/` artifact. Behind the same ingress: `/` serves the SPA, `/api/*` and `/hubs/*` route to the API service.

### Rollout

The existing Firebase-hosted site stays live throughout. The new stack ships on a new hostname. After the new stack is verified in production, retiring the Firebase site is a separate, manual decision.
