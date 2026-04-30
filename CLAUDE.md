# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Build & Run

All source is under `src/`. The solution uses .NET 10 with Aspire 13.2.4.

```bash
# Build the full solution (from repo root)
dotnet build

# Run everything via Aspire (PostgreSQL, Redis, Ollama, API, Web)
dotnet run --project src/Yaaw.AppHost

# Build a single project
dotnet build src/Yaaw.API
dotnet build src/Yaaw.Web
```

There are no test projects. No CI/CD pipelines.

## Architecture

YAAW is a .NET Aspire-orchestrated real-time AI chat app with 4 projects:

- **Yaaw.AppHost** — Aspire orchestrator. Wires PostgreSQL, Redis, Ollama (or OpenAI on macOS), API, and Web. Custom `AIModel` resource abstraction in `Extensions/` supports switching between Ollama and OpenAI providers.
- **Yaaw.API** — ASP.NET Core Minimal API backend. JWT auth with ASP.NET Core Identity. EF Core with PostgreSQL (`IdentityDbContext<IdentityUser>`). Redis for caching, rate limiting, streaming state, and cancellation.
- **Yaaw.Web** — Blazor Server frontend (Interactive Server mode). Communicates with API via typed `HttpClient` (REST) and SignalR (streaming). JWT stored in `ProtectedSessionStorage`.
- **Yaaw.ServiceDefaults** — Shared Aspire defaults (OpenTelemetry, health checks, HTTP resilience, service discovery).

### Key Data Flow: AI Streaming

1. Client POSTs prompt to `/api/chat/{id}` (returns 202 immediately)
2. `ChatStreamingCoordinator` runs LLM call on background `Task.Run`, publishes fragments through `MessageBuffer` (Nagle-style batching) to Redis
3. `RedisConversationState` fans out fragments via Redis pub/sub pattern channel
4. SignalR `ChatHub` at `/api/chat/stream` yields fragments to connected Blazor client
5. Cancellation propagates via `RedisCancellationManager` through Redis pub/sub

### Authentication Flow

- API: JWT Bearer tokens with ASP.NET Core Identity. `TokenService` generates JWTs with `sub=User.Id` (custom `u_` prefixed ID), email, name, roles. SignalR receives JWT via `access_token` query parameter.
- Web: `JwtAuthenticationStateProvider` parses JWT claims (base64 decode, no external JWT library). `JwtDelegatingHandler` attaches Bearer token to `ChatApiService` HttpClient. `TokenStorageService` wraps `ProtectedSessionStorage` (handles `JSDisconnectedException` during prerender).
- Custom `User` entity is separate from `IdentityUser` — linked via `IdentityId` field.

### Caching & ETags

- `ETagCachingFilter` (IEndpointFilter) on GET endpoints: user-scoped Redis cache keys (`cache:{userId}:{path}{query}`), SHA256-based ETags, If-None-Match/304 support.
- `CacheInvalidationFilter` on mutation endpoints: clears user's cache prefix after success.
- `RedisCacheService` stores JSON data + ETag in Redis HASHes.

## API Structure

Endpoints are organized as static extension methods in `Endpoints/`:
- `AuthEndpoints.cs` — `/api/auth` (register, login, profile)
- `ChatEndpoints.cs` — `/api/chat` (CRUD conversations, send prompts, cancel, SignalR hub)

All chat endpoints require authorization and scope queries by `CurrentUserService.GetUserId()`.

## Key Conventions

- **Minimal APIs** with endpoint groups, not controllers (despite `AddControllers()` in DI)
- **FluentValidation** with assembly scanning — validators auto-discovered
- **HATEOAS links** via `LinkService` on all conversation responses
- **Data shaping** via `DataShapingService` (field selection with `?fields=` query param)
- **Dynamic sorting** via `SortMappingProvider` + `System.Linq.Dynamic.Core`
- **Custom Redis rate limiting** — `SlidingWindowRateLimiter` as middleware (not ASP.NET Core's built-in)
- **No EF migrations** — uses `EnsureCreatedAsync()` at startup. Database must be dropped/recreated on schema changes.
- **Role seeding** — "Admin" and "User" roles created in `EnsureDatabaseCreatedHostedService`
- DTOs are duplicated between API (`DTOs/`) and Web (`Models/`) — no shared contracts project

## Database

PostgreSQL with schema `"application"`. Entities: `User`, `Conversation` (with `UserId` FK), `ConversationMessage`. Plus ASP.NET Core Identity tables (`AspNetUsers`, `AspNetRoles`, etc.).

## Configuration

JWT settings, CORS origins, and rate limiter config are in `appsettings.json` / `appsettings.Development.json`. JWT secret is a dev placeholder — must be changed for production via user secrets or environment variables.
