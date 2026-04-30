import { useState, useEffect, useRef } from "react";
import { useNavigate } from "react-router-dom";
import {
  Plus,
  Search,
  MessageCircle,
  Pencil,
  Trash2,
  X,
  User,
  LogOut,
  Check,
  ChevronDown,
  Clock,
  Clock3,
  ArrowDownAZ,
  ArrowUpAZ,
} from "lucide-react";
import { useAuthStore } from "@/stores/auth-store";
import type { ConversationDto } from "@/types";

interface ChatSidebarProps {
  conversations: ConversationDto[];
  activeId: string | null;
  isOpen: boolean;
  search: string;
  sort: string;
  onSearchChange: (value: string) => void;
  onSortChange: (value: string) => void;
  onSelect: (id: string) => void;
  onNewConversation: () => void;
  onDelete: (id: string) => void;
  onRename: (id: string, newName: string) => void;
  onToggle: () => void;
}

const SORT_OPTIONS = [
  { value: "id desc", label: "Newest first", Icon: Clock },
  { value: "id asc", label: "Oldest first", Icon: Clock3 },
  { value: "name asc", label: "Name A → Z", Icon: ArrowDownAZ },
  { value: "name desc", label: "Name Z → A", Icon: ArrowUpAZ },
] as const;

export function ChatSidebar({
  conversations,
  activeId,
  isOpen,
  search,
  sort,
  onSearchChange,
  onSortChange,
  onSelect,
  onNewConversation,
  onDelete,
  onRename,
  onToggle,
}: ChatSidebarProps) {
  const navigate = useNavigate();
  const logout = useAuthStore((s) => s.logout);
  const userName = useAuthStore((s) => s.name);

  const [renamingId, setRenamingId] = useState<string | null>(null);
  const [renameText, setRenameText] = useState("");
  const renameInputRef = useRef<HTMLInputElement>(null);

  const [sortOpen, setSortOpen] = useState(false);
  const sortRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    function handleOutside(e: MouseEvent) {
      if (sortRef.current && !sortRef.current.contains(e.target as Node)) {
        setSortOpen(false);
      }
    }
    document.addEventListener("mousedown", handleOutside);
    return () => document.removeEventListener("mousedown", handleOutside);
  }, []);

  useEffect(() => {
    if (renamingId && renameInputRef.current) {
      renameInputRef.current.focus();
      renameInputRef.current.select();
    }
  }, [renamingId]);

  function startRename(conv: ConversationDto) {
    setRenamingId(conv.id);
    setRenameText(conv.name);
  }

  function confirmRename() {
    if (renamingId && renameText.trim()) {
      onRename(renamingId, renameText.trim());
    }
    setRenamingId(null);
  }

  function cancelRename() {
    setRenamingId(null);
  }

  function handleSignOut() {
    logout();
    navigate("/login");
  }

  const activeOption =
    SORT_OPTIONS.find((o) => o.value === sort) ?? SORT_OPTIONS[0];

  const sidebarContent = (
    <div className="flex h-full flex-col bg-surface-900 border-r border-surface-800/50">
      {/* Header */}
      <div className="flex items-center justify-between p-4">
        <h1 className="text-lg font-semibold text-surface-100">Yaaw</h1>
        <button
          onClick={onToggle}
          className="rounded-lg p-1.5 text-surface-400 hover:bg-surface-800 hover:text-surface-200 lg:hidden"
        >
          <X className="h-5 w-5" />
        </button>
      </div>

      {/* New conversation button */}
      <div className="px-3 pb-3">
        <button
          onClick={onNewConversation}
          className="flex w-full items-center gap-2 rounded-xl border border-surface-700/50 px-4 py-2.5 text-sm text-surface-300 transition-colors hover:bg-surface-800 hover:text-surface-100"
        >
          <Plus className="h-4 w-4" />
          New conversation
        </button>
      </div>

      {/* Search */}
      <div className="px-3 pb-2">
        <div className="flex items-center gap-2 rounded-lg bg-surface-800/50 px-3 py-2">
          <Search className="h-4 w-4 text-surface-500" />
          <input
            type="text"
            value={search}
            onChange={(e) => onSearchChange(e.target.value)}
            placeholder="Search conversations..."
            className="w-full bg-transparent text-sm text-surface-200 placeholder-surface-500 outline-none"
          />
        </div>
      </div>

      {/* Sort — custom dropdown */}
      <div className="px-3 pb-3" ref={sortRef}>
        <div className="relative">
          <button
            onClick={() => setSortOpen((v) => !v)}
            className={`flex w-full items-center gap-2 rounded-lg px-3 py-2 text-sm transition-colors ${
              sortOpen
                ? "bg-surface-700 text-surface-100"
                : "bg-surface-800/50 text-surface-300 hover:bg-surface-800 hover:text-surface-200"
            }`}
          >
            <activeOption.Icon className="h-3.5 w-3.5 shrink-0 text-surface-500" />
            <span className="flex-1 text-left">{activeOption.label}</span>
            <ChevronDown
              className={`h-3.5 w-3.5 shrink-0 text-surface-500 transition-transform duration-150 ${
                sortOpen ? "rotate-180" : ""
              }`}
            />
          </button>

          {sortOpen && (
            <div className="absolute left-0 right-0 top-full z-50 mt-1 overflow-hidden rounded-lg border border-surface-700/60 bg-surface-800 shadow-xl">
              {SORT_OPTIONS.map(({ value, label, Icon }) => {
                const isActive = value === sort;
                return (
                  <button
                    key={value}
                    onClick={() => {
                      onSortChange(value);
                      setSortOpen(false);
                    }}
                    className={`flex w-full items-center gap-2.5 px-3 py-2 text-sm transition-colors ${
                      isActive
                        ? "bg-accent/10 text-accent"
                        : "text-surface-300 hover:bg-surface-700 hover:text-surface-100"
                    }`}
                  >
                    <Icon className="h-3.5 w-3.5 shrink-0" />
                    <span className="flex-1 text-left">{label}</span>
                    {isActive && <Check className="h-3.5 w-3.5 shrink-0" />}
                  </button>
                );
              })}
            </div>
          )}
        </div>
      </div>

      {/* Conversation list */}
      <div className="scrollbar-thin flex-1 overflow-y-auto px-2">
        {conversations.length === 0 ? (
          <p className="px-3 py-4 text-center text-sm text-surface-500">
            No conversations yet
          </p>
        ) : (
          <div className="space-y-0.5">
            {conversations.map((conv) => (
              <div
                key={conv.id}
                className={`group flex items-center gap-2 rounded-lg px-3 py-2.5 transition-colors cursor-pointer ${
                  conv.id === activeId
                    ? "bg-accent/10 text-accent"
                    : "text-surface-300 hover:bg-surface-800 hover:text-surface-100"
                }`}
                onClick={() => onSelect(conv.id)}
                onDoubleClick={() => startRename(conv)}
              >
                <MessageCircle className="h-4 w-4 shrink-0" />
                {renamingId === conv.id ? (
                  <div className="flex flex-1 items-center gap-1">
                    <input
                      ref={renameInputRef}
                      value={renameText}
                      onChange={(e) => setRenameText(e.target.value)}
                      onKeyDown={(e) => {
                        if (e.key === "Enter") confirmRename();
                        if (e.key === "Escape") cancelRename();
                      }}
                      onClick={(e) => e.stopPropagation()}
                      className="min-w-0 flex-1 rounded bg-surface-800 px-2 py-0.5 text-sm text-surface-100 outline-none"
                    />
                    <button
                      onClick={(e) => {
                        e.stopPropagation();
                        confirmRename();
                      }}
                      className="rounded p-0.5 text-accent hover:bg-surface-700"
                    >
                      <Check className="h-3.5 w-3.5" />
                    </button>
                    <button
                      onClick={(e) => {
                        e.stopPropagation();
                        cancelRename();
                      }}
                      className="rounded p-0.5 text-surface-400 hover:bg-surface-700"
                    >
                      <X className="h-3.5 w-3.5" />
                    </button>
                  </div>
                ) : (
                  <>
                    <span className="flex-1 truncate text-sm">{conv.name}</span>
                    <div className="flex shrink-0 gap-0.5 opacity-0 group-hover:opacity-100">
                      <button
                        onClick={(e) => {
                          e.stopPropagation();
                          startRename(conv);
                        }}
                        className="rounded p-1 text-surface-400 hover:bg-surface-700 hover:text-surface-200"
                      >
                        <Pencil className="h-3.5 w-3.5" />
                      </button>
                      <button
                        onClick={(e) => {
                          e.stopPropagation();
                          onDelete(conv.id);
                        }}
                        className="rounded p-1 text-surface-400 hover:bg-red-900/30 hover:text-red-400"
                      >
                        <Trash2 className="h-3.5 w-3.5" />
                      </button>
                    </div>
                  </>
                )}
              </div>
            ))}
          </div>
        )}
      </div>

      {/* Footer */}
      <div className="border-t border-surface-800/50 p-3">
        <div className="flex items-center gap-2">
          <button
            onClick={() => navigate("/profile")}
            className="flex flex-1 items-center gap-2 rounded-lg px-3 py-2 text-sm text-surface-400 transition-colors hover:bg-surface-800 hover:text-surface-200"
          >
            <User className="h-4 w-4" />
            <span className="truncate">{userName ?? "Profile"}</span>
          </button>
          <button
            onClick={handleSignOut}
            className="rounded-lg p-2 text-surface-400 transition-colors hover:bg-surface-800 hover:text-red-400"
            title="Sign out"
          >
            <LogOut className="h-4 w-4" />
          </button>
        </div>
      </div>
    </div>
  );

  return (
    <>
      {isOpen && (
        <div
          className="fixed inset-0 z-30 bg-black/50 lg:hidden"
          onClick={onToggle}
        />
      )}
      <div className="hidden w-72 shrink-0 lg:block">{sidebarContent}</div>
      <div
        className={`fixed inset-y-0 left-0 z-40 w-72 transform transition-transform lg:hidden ${
          isOpen ? "translate-x-0" : "-translate-x-full"
        }`}
      >
        {sidebarContent}
      </div>
    </>
  );
}
