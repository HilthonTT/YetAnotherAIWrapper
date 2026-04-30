import { useState, useEffect, useRef, useCallback } from "react";
import { Menu } from "lucide-react";
import { ChatSidebar } from "@/components/chat/ChatSidebar";
import { MessageBubble } from "@/components/chat/MessageBubble";
import { MessageInput } from "@/components/chat/MessageInput";
import { EmptyState } from "@/components/chat/EmptyState";
import {
  useConversations,
  useConversation,
  useCreateConversation,
  useRenameConversation,
  useDeleteConversation,
} from "@/hooks/use-conversations";
import { useChatStream } from "@/hooks/use-chat-stream";
import * as chatApi from "@/api/chat";
import type { ChatMessage, ClientMessageFragmentDto } from "@/types";

export function ChatPage() {
  const [activeId, setActiveId] = useState<string | null>(null);
  const [messages, setMessages] = useState<ChatMessage[]>([]);
  const [isSending, setIsSending] = useState(false);
  const [sidebarOpen, setSidebarOpen] = useState(false);
  const [search, setSearch] = useState("");
  const [debouncedSearch, setDebouncedSearch] = useState("");
  const [sort, setSort] = useState("id desc");

  const messagesEndRef = useRef<HTMLDivElement>(null);
  const isStreamingRef = useRef(false);

  // Debounce search
  useEffect(() => {
    const timer = setTimeout(() => setDebouncedSearch(search), 300);
    return () => clearTimeout(timer);
  }, [search]);

  // Queries
  const { data: conversations = [] } = useConversations(
    debouncedSearch || undefined,
    sort,
  );
  const { data: activeConversation } = useConversation(activeId);

  // Mutations
  const createMutation = useCreateConversation();
  const renameMutation = useRenameConversation();
  const deleteMutation = useDeleteConversation();

  // Load conversation messages when selected
  useEffect(() => {
    if (activeConversation && !isStreamingRef.current) {
      setMessages(
        activeConversation.messages.map((m) => ({
          id: m.id,
          role: m.role,
          text: m.text,
          isStreaming: false,
          isPlaceholder: false,
        })),
      );
    }
  }, [activeConversation]);

  // Auto-scroll
  useEffect(() => {
    messagesEndRef.current?.scrollIntoView({ behavior: "smooth" });
  }, [messages]);

  // Stream handlers
  const onFragment = useCallback((fragment: ClientMessageFragmentDto) => {
    setMessages((prev) => {
      const updated = [...prev];

      if (fragment.sender === "user") {
        // Update the user message ID from the server
        const userMsg = updated.findLast((m) => m.role === "user");
        if (userMsg) {
          userMsg.id = fragment.id;
        }
        return updated;
      }

      // Assistant fragment
      const assistantIdx = updated.findLastIndex(
        (m) => m.role === "assistant" && (m.isStreaming || m.isPlaceholder),
      );

      if (assistantIdx === -1) {
        return updated;
      }

      const msg = { ...updated[assistantIdx] };

      if (fragment.isFinal) {
        msg.isStreaming = false;
        msg.isPlaceholder = false;
        updated[assistantIdx] = msg;
        return updated;
      }

      // Skip the "Generating reply..." placeholder text
      if (msg.isPlaceholder && fragment.text === "Generating reply...") {
        return updated;
      }

      if (msg.isPlaceholder) {
        msg.isPlaceholder = false;
        msg.text = fragment.text;
      } else {
        msg.text += fragment.text;
      }

      msg.id = fragment.id;
      msg.isStreaming = true;
      updated[assistantIdx] = msg;
      return updated;
    });
  }, []);

  const onStreamError = useCallback((error: Error) => {
    console.error("Stream error:", error);
    setMessages((prev) => {
      const updated = [...prev];
      const assistantIdx = updated.findLastIndex(
        (m) => m.role === "assistant" && (m.isStreaming || m.isPlaceholder),
      );
      if (assistantIdx !== -1) {
        updated[assistantIdx] = {
          ...updated[assistantIdx],
          text: "An error occurred while generating the response.",
          isStreaming: false,
          isPlaceholder: false,
        };
      }
      return updated;
    });
    setIsSending(false);
    isStreamingRef.current = false;
  }, []);

  const onStreamComplete = useCallback(() => {
    setIsSending(false);
    isStreamingRef.current = false;
  }, []);

  const { startStream, cancelStream } = useChatStream({
    onFragment,
    onError: onStreamError,
    onComplete: onStreamComplete,
  });

  // Actions
  async function handleNewConversation() {
    const now = new Date();
    const name = `Chat ${now.toLocaleDateString()} ${now.toLocaleTimeString([], { hour: "2-digit", minute: "2-digit" })}`;
    const conv = await createMutation.mutateAsync(name);
    setActiveId(conv.id);
    setSidebarOpen(false);
  }

  async function handleSend(text: string) {
    if (!activeId || isSending) {
      return;
    }

    setIsSending(true);
    isStreamingRef.current = true;

    const userMessage: ChatMessage = {
      id: crypto.randomUUID(),
      role: "user",
      text,
      isStreaming: false,
      isPlaceholder: false,
    };

    const assistantMessage: ChatMessage = {
      id: crypto.randomUUID(),
      role: "assistant",
      text: "",
      isStreaming: true,
      isPlaceholder: true,
    };

    setMessages((prev) => [...prev, userMessage, assistantMessage]);

    // Get the last real message ID for reconnect context
    const lastMessageId =
      messages.length > 0 ? messages[messages.length - 1].id : null;

    // Start stream first, then send prompt
    startStream(activeId, lastMessageId);

    try {
      await chatApi.sendPrompt(activeId, text);
    } catch (err) {
      console.error("Failed to send prompt:", err);
      onStreamError(err as Error);
    }
  }

  function handleCancelStream() {
    if (activeId) {
      chatApi.cancelGeneration(activeId);
      cancelStream();
      setIsSending(false);
      isStreamingRef.current = false;
    }
  }

  function handleSelect(id: string) {
    if (isSending) {
      handleCancelStream();
    }
    setActiveId(id);
    setSidebarOpen(false);
  }

  function handleDelete(id: string) {
    deleteMutation.mutate(id);
    if (activeId === id) {
      setActiveId(null);
      setMessages([]);
    }
  }

  function handleRename(id: string, newName: string) {
    renameMutation.mutate({ id, name: newName });
  }

  return (
    <div className="flex h-full">
      <ChatSidebar
        conversations={conversations}
        activeId={activeId}
        isOpen={sidebarOpen}
        search={search}
        sort={sort}
        onSearchChange={setSearch}
        onSortChange={setSort}
        onSelect={handleSelect}
        onNewConversation={handleNewConversation}
        onDelete={handleDelete}
        onRename={handleRename}
        onToggle={() => setSidebarOpen(!sidebarOpen)}
      />

      {/* Main area */}
      <div className="flex flex-1 flex-col">
        {activeId ? (
          <>
            {/* Top bar */}
            <div className="flex items-center gap-3 border-b border-surface-800/50 px-4 py-3">
              <button
                onClick={() => setSidebarOpen(true)}
                className="rounded-lg p-1.5 text-surface-400 hover:bg-surface-800 hover:text-surface-200 lg:hidden"
              >
                <Menu className="h-5 w-5" />
              </button>
              <h2 className="truncate text-sm font-medium text-surface-200">
                {conversations.find((c) => c.id === activeId)?.name ??
                  "Conversation"}
              </h2>
              {isSending && (
                <button
                  onClick={handleCancelStream}
                  className="ml-auto rounded-lg px-3 py-1 text-xs text-red-400 transition-colors hover:bg-red-900/20"
                >
                  Stop generating
                </button>
              )}
            </div>

            {/* Messages */}
            <div className="flex-1 overflow-y-auto p-4">
              <div className="mx-auto max-w-3xl space-y-4">
                {messages.length === 0 ? (
                  <div className="flex h-full items-center justify-center py-20">
                    <p className="text-sm text-surface-500">
                      Send a message to start the conversation.
                    </p>
                  </div>
                ) : (
                  messages.map((msg) => (
                    <MessageBubble key={msg.id} message={msg} />
                  ))
                )}
                <div ref={messagesEndRef} />
              </div>
            </div>

            {/* Input */}
            <MessageInput onSend={handleSend} isSending={isSending} />
          </>
        ) : (
          <>
            {/* Mobile hamburger when no conversation selected */}
            <div className="flex items-center border-b border-surface-800/50 px-4 py-3 lg:hidden">
              <button
                onClick={() => setSidebarOpen(true)}
                className="rounded-lg p-1.5 text-surface-400 hover:bg-surface-800 hover:text-surface-200"
              >
                <Menu className="h-5 w-5" />
              </button>
            </div>
            <EmptyState onNewConversation={handleNewConversation} />
          </>
        )}
      </div>
    </div>
  );
}
