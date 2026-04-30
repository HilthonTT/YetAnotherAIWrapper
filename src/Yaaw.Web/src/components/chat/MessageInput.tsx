import { useState, type KeyboardEvent } from "react";
import { Send, Loader2 } from "lucide-react";

interface MessageInputProps {
  onSend: (text: string) => void;
  isSending: boolean;
}

export function MessageInput({ onSend, isSending }: MessageInputProps) {
  const [text, setText] = useState("");

  const canSend = text.trim().length > 0 && !isSending;

  function handleSubmit() {
    if (!canSend) return;
    onSend(text.trim());
    setText("");
  }

  function handleKeyDown(e: KeyboardEvent<HTMLTextAreaElement>) {
    if (e.key === "Enter" && !e.shiftKey) {
      e.preventDefault();
      handleSubmit();
    }
  }

  return (
    <div className="border-t border-surface-800/50 bg-surface-950 p-4">
      <div className="mx-auto max-w-3xl">
        <div className="flex items-end gap-3 rounded-2xl border border-surface-700/50 bg-surface-900 p-3 transition-colors focus-within:border-accent/30">
          <textarea
            value={text}
            onChange={(e) => setText(e.target.value)}
            onKeyDown={handleKeyDown}
            placeholder="Type your message..."
            disabled={isSending}
            rows={1}
            className="max-h-40 min-h-6 flex-1 resize-none bg-transparent text-sm text-surface-100 placeholder-surface-500 outline-none"
          />
          <button
            onClick={handleSubmit}
            disabled={!canSend}
            className="flex h-8 w-8 shrink-0 items-center justify-center rounded-lg bg-accent/20 text-accent transition-colors hover:bg-accent/30 disabled:opacity-30 disabled:hover:bg-accent/20"
          >
            {isSending ? (
              <Loader2 className="h-4 w-4 animate-spin" />
            ) : (
              <Send className="h-4 w-4" />
            )}
          </button>
        </div>
        <p className="mt-2 text-center text-xs text-surface-500">
          Yaaw can make mistakes. Verify important information.
        </p>
      </div>
    </div>
  );
}
