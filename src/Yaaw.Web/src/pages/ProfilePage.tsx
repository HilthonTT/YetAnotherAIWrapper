import { useNavigate } from "react-router-dom";
import { ArrowLeft, LogOut, User } from "lucide-react";
import { useAuthStore } from "@/stores/auth-store";

export function ProfilePage() {
  const navigate = useNavigate();
  const { name, email, userId, logout } = useAuthStore();

  function handleSignOut() {
    logout();
    navigate("/login");
  }

  return (
    <div className="flex min-h-full items-center justify-center p-4">
      <div className="animate-slide-up w-full max-w-sm">
        <div className="mb-8 text-center">
          <div className="mx-auto mb-4 flex h-20 w-20 items-center justify-center rounded-full bg-accent/10">
            <User className="h-10 w-10 text-accent" />
          </div>
          <h1 className="text-2xl font-semibold text-surface-100">Profile</h1>
        </div>

        <div className="space-y-3">
          <div className="rounded-xl bg-surface-900 px-4 py-3">
            <p className="text-xs text-surface-500">Name</p>
            <p className="text-sm text-surface-100">{name ?? "-"}</p>
          </div>
          <div className="rounded-xl bg-surface-900 px-4 py-3">
            <p className="text-xs text-surface-500">Email</p>
            <p className="text-sm text-surface-100">{email ?? "-"}</p>
          </div>
          <div className="rounded-xl bg-surface-900 px-4 py-3">
            <p className="text-xs text-surface-500">User ID</p>
            <p className="font-mono text-sm text-surface-400">
              {userId ?? "-"}
            </p>
          </div>
        </div>

        <div className="mt-6 flex gap-3">
          <button
            onClick={() => navigate("/")}
            className="flex flex-1 items-center justify-center gap-2 rounded-xl border border-surface-700/50 py-2.5 text-sm text-surface-300 transition-colors hover:bg-surface-800"
          >
            <ArrowLeft className="h-4 w-4" />
            Back to Chat
          </button>
          <button
            onClick={handleSignOut}
            className="flex flex-1 items-center justify-center gap-2 rounded-xl bg-red-900/20 py-2.5 text-sm text-red-400 transition-colors hover:bg-red-900/30"
          >
            <LogOut className="h-4 w-4" />
            Sign out
          </button>
        </div>
      </div>
    </div>
  );
}
