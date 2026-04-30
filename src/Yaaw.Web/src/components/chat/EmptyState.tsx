import { Plus, Sparkles } from "lucide-react";

interface EmptyStateProps {
  onNewConversation: () => void;
}

export function EmptyState({ onNewConversation }: EmptyStateProps) {
  return (
    <div className="flex flex-1 items-center justify-center p-8">
      <div className="animate-fade-in text-center">
        <div className="mx-auto mb-6 flex h-20 w-20 items-center justify-center rounded-2xl bg-accent/10">
          <Sparkles className="h-10 w-10 text-accent" />
        </div>
        <h2 className="mb-2 text-2xl font-semibold text-surface-100">
          Yet Another AI Wrapper
        </h2>
        <p className="mb-8 text-surface-400">
          Start a new conversation to begin chatting with AI
        </p>
        <button
          onClick={onNewConversation}
          className="inline-flex items-center gap-2 rounded-xl bg-accent/15 px-6 py-3 text-sm font-medium text-accent transition-colors hover:bg-accent/25"
        >
          <Plus className="h-4 w-4" />
          New conversation
        </button>
      </div>
    </div>
  );
}
