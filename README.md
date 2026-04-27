# Yaaw — Yet Another AI Wrapper

A real-time AI chat application built with .NET Aspire, Blazor Server, and Ollama. Messages stream token-by-token via SignalR backed by Redis pub/sub, giving a responsive experience similar to ChatGPT or Claude.

## Architecture

```
┌──────────────┐      SignalR       ┌──────────────┐      Redis pub/sub      ┌─────────┐
│  Blazor Web  │◄──── WebSocket ───►│   .NET API   │◄─────────────────────►  │  Redis  │
│  (Frontend)  │      + REST        │  (Backend)   │                         └─────────┘
└──────────────┘                    └──────┬───────┘
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

**Yaaw.AppHost** — .NET Aspire orchestrator that wires all resources together: Ollama (with GPU support), PostgreSQL + pgAdmin, Redis + RedisInsight, the API project, and the web frontend.

**Yaaw.API** — ASP.NET Core Minimal API backend. Manages conversations and messages in PostgreSQL via EF Core. Streams LLM responses through a `ChatStreamingCoordinator` that publishes fragments to Redis, which are picked up by a SignalR hub for delivery to connected clients. Supports cancellation mid-generation via `RedisCancellationManager`.

**Yaaw.Web** — Blazor Server frontend with a dark-themed UI. Connects to the API over REST for CRUD operations and SignalR for real-time streaming. Features include stop generation, auto-rename conversations, copy-to-clipboard on code blocks, and markdown rendering.

## Features

- **Real-time streaming** — LLM tokens arrive one-by-one via Redis pub/sub → SignalR, with a blinking cursor during generation
- **Stop generation** — Cancel an in-progress response at any time; the server aborts the LLM call and persists whatever was generated
- **Auto-rename** — Conversations are automatically titled based on the first message
- **Copy code blocks** — Hover any code block to reveal a clipboard button
- **Markdown rendering** — Assistant responses render with full markdown support (headings, lists, tables, code blocks, links)
- **Conversation management** — Create, select, rename, and delete conversations
- **Cancellation propagation** — Cancel commands propagate through Redis so any API instance can handle them (horizontally scalable)
- **Persistent model** — Ollama container runs with `ContainerLifetime.Persistent` so the model stays loaded between restarts
- **GPU acceleration** — Ollama is configured with GPU support for faster inference

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- [Docker Desktop](https://www.docker.com/products/docker-desktop/) (for Aspire-managed containers)
- A GPU with CUDA support (optional, for faster inference — falls back to CPU)

## Getting Started

1. **Clone the repository**

   ```bash
   git clone https://github.com/hilthontt/YetAnotherAIWrapper.git
   cd yaaw
   ```

2. **Run via Aspire**

   ```bash
   dotnet run --project Yaaw.AppHost
   ```

   This starts all services: PostgreSQL, Redis, Ollama (pulls `phi4` on first run — ~9GB), the API, and the web frontend. The Aspire dashboard opens automatically at `https://localhost:17135`.

3. **Open the app**

   Navigate to the Yaaw.Web endpoint shown in the Aspire dashboard (typically `https://localhost:7200`).

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
Yaaw/
├── Yaaw.AppHost/          # Aspire orchestrator
├── Yaaw.ServiceDefaults/  # Shared service configuration
├── Yaaw.API/              # Backend API
│   ├── Database/          # EF Core DbContext and migrations
│   ├── DTOs/              # Request/response models
│   ├── Endpoints/         # Minimal API route handlers
│   ├── Entities/          # Domain entities
│   ├── Hubs/              # SignalR hub for streaming
│   ├── Services/          # Core services
│   │   ├── ChatStreamingCoordinator.cs  # LLM streaming + persistence
│   │   ├── RedisConversationState.cs    # Pub/sub + backlog management
│   │   └── RedisCancellationManager.cs  # Distributed cancellation
│   └── Program.cs
├── Yaaw.Web/              # Blazor Server frontend
│   ├── Components/
│   │   ├── Chat/          # MessageBubble, EmptyState
│   │   ├── Layout/        # MainLayout, ReconnectModal
│   │   └── Pages/         # Chat page
│   ├── Models/            # Client-side DTOs
│   ├── Services/          # API client, SignalR stream, markdown
│   └── Program.cs
```

## API Endpoints

| Method | Path | Description |
|--------|------|-------------|
| GET | `/api/chat` | List all conversations |
| GET | `/api/chat/{id}` | Get conversation with messages |
| POST | `/api/chat` | Create a new conversation |
| POST | `/api/chat/{id}` | Send a prompt to the LLM |
| PATCH | `/api/chat/{id}` | Rename a conversation |
| PUT | `/api/chat/{id}/cancel` | Cancel an in-progress generation |
| DELETE | `/api/chat/{id}` | Delete a conversation |
| SignalR | `/api/chat/stream` | Real-time message fragment streaming |

## Tech Stack

- **Backend:** ASP.NET Core 10 Minimal APIs, EF Core, SignalR, FluentValidation
- **Frontend:** Blazor Server, Tailwind CSS (CDN), Markdig
- **Infrastructure:** .NET Aspire, PostgreSQL, Redis, Ollama, Docker
- **LLM:** Microsoft Phi-4 (14B, Q4_K_M) via Ollama, with OpenAI fallback

## License

MIT
