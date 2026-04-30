import { useState } from "react";
import { Link, useNavigate } from "react-router-dom";
import { Loader2, Sparkles } from "lucide-react";
import { useAuthStore } from "@/stores/auth-store";
import * as authApi from "@/api/auth";

export function LoginPage() {
  const navigate = useNavigate();
  const login = useAuthStore((s) => s.login);

  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [error, setError] = useState("");
  const [loading, setLoading] = useState(false);

  async function handleSubmit(e: React.SubmitEvent<HTMLFormElement>) {
    e.preventDefault();
    setError("");
    setLoading(true);

    try {
      const response = await authApi.login(email, password);
      login(response);
      navigate("/");
    } catch (err) {
      if (err instanceof Response && err.status === 401) {
        setError("Invalid email or password.");
      } else {
        setError("An error occurred. Please try again.");
      }
    } finally {
      setLoading(false);
    }
  }

  return (
    <div className="flex min-h-full items-center justify-center p-4">
      <div className="animate-slide-up w-full max-w-sm">
        <div className="mb-8 text-center">
          <div className="mx-auto mb-4 flex h-14 w-14 items-center justify-center rounded-2xl bg-accent/10">
            <Sparkles className="h-7 w-7 text-accent" />
          </div>
          <h1 className="text-2xl font-semibold text-surface-100">
            Welcome back
          </h1>
          <p className="mt-1 text-sm text-surface-400">
            Sign in to continue to Yaaw
          </p>
        </div>

        <form onSubmit={handleSubmit} className="space-y-4">
          {error && (
            <div className="rounded-lg bg-red-900/20 px-4 py-3 text-sm text-red-400">
              {error}
            </div>
          )}

          <div>
            <label className="mb-1.5 block text-sm text-surface-300">
              Email
            </label>
            <input
              type="email"
              value={email}
              onChange={(e) => setEmail(e.target.value)}
              required
              className="w-full rounded-xl border border-surface-700/50 bg-surface-900 px-4 py-2.5 text-sm text-surface-100 placeholder-surface-500 outline-none transition-colors focus:border-accent/30"
              placeholder="you@example.com"
            />
          </div>

          <div>
            <label className="mb-1.5 block text-sm text-surface-300">
              Password
            </label>
            <input
              type="password"
              value={password}
              onChange={(e) => setPassword(e.target.value)}
              required
              className="w-full rounded-xl border border-surface-700/50 bg-surface-900 px-4 py-2.5 text-sm text-surface-100 placeholder-surface-500 outline-none transition-colors focus:border-accent/30"
              placeholder="Enter your password"
            />
          </div>

          <button
            type="submit"
            disabled={loading}
            className="flex w-full items-center justify-center gap-2 rounded-xl bg-accent/15 py-2.5 text-sm font-medium text-accent transition-colors hover:bg-accent/25 disabled:opacity-50"
          >
            {loading && <Loader2 className="h-4 w-4 animate-spin" />}
            Sign in
          </button>
        </form>

        <p className="mt-6 text-center text-sm text-surface-400">
          Don&apos;t have an account?{" "}
          <Link
            to="/register"
            className="text-accent underline underline-offset-2 hover:text-accent-bright"
          >
            Sign up
          </Link>
        </p>
      </div>
    </div>
  );
}
