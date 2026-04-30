import { useCallback, useRef } from "react";
import { streamConversation } from "@/api/signalr";
import type { ClientMessageFragmentDto } from "@/types";

interface UseChatStreamOptions {
  onFragment: (fragment: ClientMessageFragmentDto) => void;
  onError: (error: Error) => void;
  onComplete: () => void;
}

export function useChatStream({
  onFragment,
  onError,
  onComplete,
}: UseChatStreamOptions) {
  const abortRef = useRef<AbortController | null>(null);

  const startStream = useCallback(
    async (conversationId: string, lastMessageId: string | null) => {
      abortRef.current?.abort();
      abortRef.current = new AbortController();

      try {
        await streamConversation(
          conversationId,
          lastMessageId,
          onFragment,
          abortRef.current.signal,
        );
        onComplete();
      } catch (err) {
        if (!(err instanceof DOMException && err.name === "AbortError")) {
          onError(err as Error);
        }
      }
    },
    [onFragment, onError, onComplete],
  );

  const cancelStream = useCallback(() => {
    abortRef.current?.abort();
    abortRef.current = null;
  }, []);

  return { startStream, cancelStream };
}
