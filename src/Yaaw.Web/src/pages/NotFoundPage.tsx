import { Link } from "react-router-dom";

export function NotFoundPage() {
  return (
    <div className="flex min-h-full items-center justify-center p-4">
      <div className="animate-slide-up text-center">
        <h1 className="mb-2 text-6xl font-bold text-surface-600">404</h1>
        <p className="mb-6 text-surface-400">
          The page you're looking for doesn't exist.
        </p>
        <Link
          to="/"
          className="inline-flex items-center gap-2 rounded-xl bg-accent/15 px-6 py-3 text-sm font-medium text-accent transition-colors hover:bg-accent/25"
        >
          Back to Yaaw
        </Link>
      </div>
    </div>
  );
}
