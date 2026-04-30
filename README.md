# Yaaw — Yet Another AI Wrapper

A real-time AI chat application built with .NET Aspire, React, and Ollama. Messages stream token-by-token via SignalR backed by Redis pub/sub, giving a responsive experience similar to ChatGPT or Claude.

## Architecture

```
┌──────────────┐      SignalR       ┌──────────────┐      Redis pub/sub      ┌─────────┐
│  React SPA   │◄──── WebSocket ───►│   .NET API   │◄─────────────────────►  │  Redis  │
│  (Frontend)  │      + REST        │  (Backend)   │                         └─────────┘
└──────────────┘                    └─────┬────────┘
                                          │
                                   ┌──────┴───────┐
                                   │   Ollama     │
                                   │  (phi4 LLM)  │
                                   └──────┬───────┘
                                          │
                                   ┌──────┴───────┐
                                   │  PostgreSQL  │
                                   └──────────────┘
```

**Yaaw.AppHost** — .NET Aspire orchestrator that wires all resources together: Ollama (with GPU support), PostgreSQL + pgAdmin, Redis + RedisInsight, the API project, and the Vite-based web frontend.

**Yaaw.API** — ASP.NET Core Minimal API backend. Manages conversations and messages in PostgreSQL via EF Core. Streams LLM responses through a `ChatStreamingCoordinator` that publishes fragments to Redis, which are picked up by a SignalR hub for delivery to connected clients. Supports cancellation mid-generation via `RedisCancellationManager`. Includes HATEOAS links, dynamic sorting, data shaping, ETag caching, and custom rate limiting.

**Yaaw.Web** — React 19 SPA built with TypeScript, Vite, and Tailwind CSS v4. Connects to the API over REST (via TanStack Query) and SignalR for real-time streaming. State management with Zustand. Features a dark-themed UI with custom design tokens, responsive layout, and markdown rendering.

**Yaaw.ServiceDefaults** — Shared Aspire defaults (OpenTelemetry, health checks, HTTP resilience, service discovery).

## Features

- **Real-time streaming** — LLM tokens arrive one-by-one via Redis pub/sub and SignalR, with a typing indicator during generation
- **Stop generation** — Cancel an in-progress response at any time; the server aborts the LLM call and persists whatever was generated
- **Auto-rename** — Conversations are automatically titled based on the first message
- **Markdown rendering** — Assistant responses render with full markdown support via react-markdown (headings, lists, tables, code blocks with syntax highlighting, links)
- **Conversation management** — Create, select, rename, search, sort, and delete conversations with optimistic UI updates
- **Authentication** — JWT-based auth with registration and login, stored in localStorage with automatic expiry handling
- **ETag caching** — Server-side Redis caching with SHA256-based ETags and automatic cache invalidation on mutations
- **HATEOAS** — Hypermedia links on all conversation responses for API discoverability
- **Data shaping** — Field selection via `?fields=` query parameter
- **Dynamic sorting** — Configurable sort mappings with `?sort=` query parameter
- **Rate limiting** — Custom sliding window rate limiter backed by Redis
- **Cancellation propagation** — Cancel commands propagate through Redis so any API instance can handle them (horizontally scalable)
- **Persistent model** — Ollama container runs with `ContainerLifetime.Persistent` so the model stays loaded between restarts
- **GPU acceleration** — Ollama is configured with GPU support for faster inference
- **Responsive design** — Mobile-friendly layout with collapsible sidebar

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- [Node.js](https://nodejs.org/) (v18+) with [pnpm](https://pnpm.io/)
- [Docker Desktop](https://www.docker.com/products/docker-desktop/) (for Aspire-managed containers)
- A GPU with CUDA support (optional, for faster inference — falls back to CPU)

## Getting Started

1. **Clone the repository**

   ```bash
   git clone https://github.com/HilthonTT/YetAnotherAIWrapper.git
   cd YetAnotherAIWrapper
   ```

2. **Install frontend dependencies**

   ```bash
   cd src/Yaaw.Web
   pnpm install
   cd ../..
   ```

3. **Run via Aspire**

   ```bash
   dotnet run --project src/Yaaw.AppHost
   ```

   This starts all services: PostgreSQL, Redis, Ollama (pulls `phi4` on first run), the API, and the Vite dev server. The Aspire dashboard opens automatically.

4. **Open the app**

   Navigate to the Yaaw.Web endpoint shown in the Aspire dashboard. Register an account and start chatting.

> **First launch note:** The phi4 model download and initial load takes a few minutes. Subsequent starts are fast since the Ollama container is persistent and the model is cached.

## Configuration

### Using OpenAI instead of Ollama

On macOS (or if you prefer a cloud model), the AppHost automatically switches to OpenAI:

```csharp
if (OperatingSystem.IsMacOS())
{
    model.AsOpenAI("gpt-4.1");
}
```

Set your API key via user secrets or environment variables as required by the Aspire OpenAI integration.

### Resilience timeouts

The API's service defaults configure HTTP client timeouts for the LLM. If you're running a large model on CPU and hitting timeouts, increase these in your service defaults:

```csharp
options.AttemptTimeout.Timeout = TimeSpan.FromMinutes(2);
options.TotalRequestTimeout.Timeout = TimeSpan.FromMinutes(5);
```

## Project Structure

```
src/
├── Yaaw.AppHost/             # Aspire orchestrator
│   └── Extensions/           # AIModel resource abstraction, Swagger/Scalar/ReDoc commands
├── Yaaw.ServiceDefaults/     # Shared service configuration (OpenTelemetry, health checks)
├── Yaaw.API/                 # Backend API
│   ├── Database/             # EF Core DbContext
│   ├── DTOs/                 # Request/response models & query parameters
│   ├── Endpoints/            # Minimal API route handlers (Auth, Chat)
│   ├── Entities/             # Domain entities (User, Conversation, ConversationMessage)
│   ├── Extensions/           # Chat client provider abstraction
│   ├── Hubs/                 # SignalR ChatHub for streaming
│   ├── Middleware/           # ETag caching & cache invalidation filters, rate limiting
│   ├── Services/
│   │   ├── Auth/             # Token service, current user service
│   │   ├── Caching/          # Redis cache service, cache key manager
│   │   ├── Chat/             # Streaming coordinator, message buffer
│   │   ├── Links/            # HATEOAS link service
│   │   ├── Redis/            # Pub/sub state, cancellation manager
│   │   ├── Shaping/          # Data shaping service
│   │   └── Sorting/          # Sort mapping provider
│   └── Validators/           # FluentValidation validators (auto-discovered)
└── Yaaw.Web/                 # React SPA frontend
    ├── src/
    │   ├── api/              # REST client (apiFetch) & SignalR connection
    │   ├── components/       # UI components (chat, layout, auth)
    │   ├── hooks/            # React Query hooks (conversations, chat stream)
    │   ├── pages/            # Route pages (Chat, Login, Register, Profile)
    │   └── stores/           # Zustand auth store
    ├── package.json          # pnpm dependencies
    └── vite.config.ts        # Vite config with API proxy
```

## API Endpoints

| Method  | Path                    | Description                                                    |
| ------- | ----------------------- | -------------------------------------------------------------- |
| POST    | `/api/auth/register`    | Register a new account                                         |
| POST    | `/api/auth/login`       | Login and receive JWT                                          |
| GET     | `/api/auth/profile`     | Get current user profile                                       |
| GET     | `/api/chat`             | List conversations (supports `?search=`, `?sort=`, `?fields=`) |
| GET     | `/api/chat/{id}`        | Get conversation with messages                                 |
| POST    | `/api/chat`             | Create a new conversation                                      |
| POST    | `/api/chat/{id}`        | Send a prompt to the LLM                                       |
| PATCH   | `/api/chat/{id}`        | Rename a conversation                                          |
| PUT     | `/api/chat/{id}/cancel` | Cancel an in-progress generation                               |
| DELETE  | `/api/chat/{id}`        | Delete a conversation                                          |
| SignalR | `/api/chat/stream`      | Real-time message fragment streaming                           |

## Tech Stack

- **Backend:** ASP.NET Core 10, Minimal APIs, EF Core, SignalR, FluentValidation
- **Frontend:** React 19, TypeScript, Vite 8, Tailwind CSS v4, Zustand, TanStack Query
- **Infrastructure:** .NET Aspire 13.2, PostgreSQL, Redis, Ollama, Docker
- **LLM:** Microsoft Phi-4 via Ollama, with OpenAI fallback on macOS

## License

MIT
