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

YAAW follows **Clean Architecture** with **DDD** and **CQRS** (MediatR). The backend is split into 4 layered projects:

### Project Structure

```
src/
├── Yaaw.Domain/          — Entities, repository interfaces, domain constants
├── Yaaw.Application/     — CQRS commands/queries (MediatR), DTOs, validators, application interfaces
├── Yaaw.Infrastructure/  — EF Core DbContexts, Redis services, Identity, JWT, AI streaming
├── Yaaw.API/             — Endpoints, SignalR hub, middleware, DI composition root
├── Yaaw.AppHost/         — Aspire orchestrator (PostgreSQL, Redis, Ollama/OpenAI, API, Web)
├── Yaaw.ServiceDefaults/ — Shared Aspire defaults (OpenTelemetry, health checks, service discovery)
└── Yaaw.Web/             — React/Vite frontend
```

### Layer Dependencies

- **Yaaw.Domain** — No dependencies. Contains `User`, `Conversation`, `ConversationMessage` entities, `IConversationRepository`, `IUserRepository`, and `Schemas` constants.
- **Yaaw.Application** — References Domain only. Contains MediatR commands/queries/handlers, DTOs, FluentValidation validators, `ValidationBehavior`, application interfaces (`ICurrentUserService`, `ITokenService`, `IChatStreamingCoordinator`, `ICancellationManager`, `IIdentityService`, `IRedisCacheService`, `ICacheKeyManager`), sorting utilities, `DataShapingService`.
- **Yaaw.Infrastructure** — References Domain + Application. Implements all interfaces. Contains two DbContexts, repositories, Redis services, Identity/JWT services, AI streaming coordinator.
- **Yaaw.API** — References Application + Infrastructure + ServiceDefaults. Thin endpoints dispatching to MediatR. Contains `LinkService`, `ChatHub`, middleware, rate limiting.

### Two DbContexts (Separate Schemas)

Both point to the same PostgreSQL database (`"yaaw"`) but use different schemas:

- **`ApplicationDbContext`** (schema: `"application"`) — `User`, `Conversation`, `ConversationMessage` entities
- **`IdentityAppDbContext`** (schema: `"identity"`) — ASP.NET Core Identity tables (`AspNetUsers`, `AspNetRoles`, etc.)

`EnsureDatabaseCreatedHostedService` calls `EnsureCreatedAsync()` on both contexts at startup.

### CQRS Pattern

All business logic flows through MediatR:
- **Commands**: `RegisterCommand`, `LoginCommand`, `CreateConversationCommand`, `RenameConversationCommand`, `DeleteConversationCommand`, `SendPromptCommand`, `CancelStreamCommand`, `UpdateProfileCommand`
- **Queries**: `GetProfileQuery`, `GetConversationsQuery`, `GetConversationQuery`
- **Pipeline**: `ValidationBehavior<TRequest, TResponse>` runs FluentValidation before handlers

Endpoints are thin — parse request → create Command/Query → `mediator.Send()` → return result.

### Key Data Flow: AI Streaming

1. Client POSTs prompt to `/api/chat/{id}` (returns 202 immediately)
2. `SendPromptHandler` → `IChatStreamingCoordinator.AddStreamingMessage()`
3. `ChatStreamingCoordinator` runs LLM call on background `Task.Run`, publishes fragments through `MessageBuffer` (Nagle-style batching) to Redis
4. `RedisConversationState` fans out fragments via Redis pub/sub pattern channel
5. SignalR `ChatHub` at `/api/chat/stream` yields fragments via `IChatStreamingCoordinator.StreamFragments()`
6. Cancellation propagates via `RedisCancellationManager` through Redis pub/sub

### Authentication Flow

- API: JWT Bearer tokens with ASP.NET Core Identity. `TokenService` (Infrastructure) generates JWTs. `IdentityService` wraps `UserManager<IdentityUser>`. SignalR receives JWT via `access_token` query parameter.
- Web: `JwtAuthenticationStateProvider` parses JWT claims. `JwtDelegatingHandler` attaches Bearer token. `TokenStorageService` wraps `ProtectedSessionStorage`.
- Custom `User` entity (Domain) is separate from `IdentityUser` — linked via `IdentityId` field.

### Caching & ETags

- `ETagCachingFilter` (IEndpointFilter) on GET endpoints: user-scoped Redis cache keys, SHA256-based ETags, If-None-Match/304 support.
- `CacheInvalidationFilter` on mutation endpoints: clears user's cache prefix after success.
- `RedisCacheService` (Infrastructure) implements `IRedisCacheService` (Application).

## API Structure

Endpoints are organized as static extension methods in `Yaaw.API/Endpoints/`:
- `AuthEndpoints.cs` — `/api/auth` (register, login, profile)
- `ChatEndpoints.cs` — `/api/chat` (CRUD conversations, send prompts, cancel, SignalR hub)

All chat endpoints require authorization and scope queries by `ICurrentUserService.GetUserId()`.

## Key Conventions

- **Clean Architecture** with Domain → Application → Infrastructure → API layering
- **CQRS** via MediatR with `ValidationBehavior` pipeline
- **Minimal APIs** with endpoint groups, not controllers (despite `AddControllers()` in DI)
- **FluentValidation** with assembly scanning — validators auto-discovered in Application layer
- **Repository pattern** — `IConversationRepository`, `IUserRepository` in Domain, implemented in Infrastructure
- **HATEOAS links** via `LinkService` (API layer) on all conversation responses
- **Data shaping** via `DataShapingService` (Application layer, field selection with `?fields=` query param)
- **Dynamic sorting** via `SortMappingProvider` + `System.Linq.Dynamic.Core` (Application layer)
- **Custom Redis rate limiting** — `SlidingWindowRateLimiter` as middleware (not ASP.NET Core's built-in)
- **No EF migrations** — uses `EnsureCreatedAsync()` at startup on both DbContexts. Database must be dropped/recreated on schema changes.
- **Role seeding** — "Admin" and "User" roles created in `EnsureDatabaseCreatedHostedService`
- DTOs live in Application layer. Web has its own `Models/` — no shared contracts project.

## Database

PostgreSQL with two schemas:
- `"application"` — `User`, `Conversation` (with `UserId` FK), `ConversationMessage`
- `"identity"` — ASP.NET Core Identity tables

## Configuration

JWT settings, CORS origins, and rate limiter config are in `appsettings.json` / `appsettings.Development.json`. JWT secret is a dev placeholder — must be changed for production via user secrets or environment variables.

## DI Registration

- `builder.AddInfrastructure()` — registers both DbContexts, Redis, Identity, JWT, repositories, AI services
- `services.AddApplication()` — registers MediatR, validators, sorting, data shaping
- `services.AddApiServices()` — registers SignalR, OpenApi, LinkService
- `builder.AddChatClient("llm")` — registers AI chat client (Ollama or OpenAI)
