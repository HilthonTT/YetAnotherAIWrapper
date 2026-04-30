import * as signalR from "@microsoft/signalr";
import { useAuthStore } from "@/stores/auth-store";
import type { ClientMessageFragmentDto } from "@/types";

let connection: signalR.HubConnection | null = null;

async function ensureConnected(): Promise<signalR.HubConnection> {
  if (connection?.state === signalR.HubConnectionState.Connected) {
    return connection;
  }

  if (connection) {
    try {
      await connection.stop();
    } catch {
      // ignore
    }
  }

  connection = new signalR.HubConnectionBuilder()
    .withUrl("/api/chat/stream", {
      accessTokenFactory: () => useAuthStore.getState().token ?? "",
    })
    .withAutomaticReconnect()
    .build();

  await connection.start();
  return connection;
}

export async function streamConversation(
  conversationId: string,
  lastMessageId: string | null,
  onFragment: (fragment: ClientMessageFragmentDto) => void,
  signal: AbortSignal,
): Promise<void> {
  const conn = await ensureConnected();

  return new Promise<void>((resolve, reject) => {
    const stream = conn.stream<ClientMessageFragmentDto>("Stream", conversationId, {
      lastMessageId,
    });

    const subscription = stream.subscribe({
      next: (fragment) => {
        if (!signal.aborted) onFragment(fragment);
      },
      error: (err) => {
        if (signal.aborted) {
          resolve();
        } else {
          reject(err);
        }
      },
      complete: () => resolve(),
    });

    signal.addEventListener("abort", () => {
      subscription.dispose();
      resolve();
    });
  });
}

export function disconnect(): void {
  connection?.stop();
  connection = null;
}
