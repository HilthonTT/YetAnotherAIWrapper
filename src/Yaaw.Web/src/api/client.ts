import { useAuthStore } from "@/stores/auth-store";

export async function apiFetch(
  path: string,
  options: RequestInit = {},
): Promise<Response> {
  const token = useAuthStore.getState().token;
  const headers = new Headers(options.headers);

  if (token) {
    headers.set("Authorization", `Bearer ${token}`);
  }
  if (options.body && !headers.has("Content-Type")) {
    headers.set("Content-Type", "application/json");
  }

  const response = await fetch(path, { ...options, headers });

  if (response.status === 401) {
    useAuthStore.getState().logout();
    window.location.href = "/login";
  }

  return response;
}
