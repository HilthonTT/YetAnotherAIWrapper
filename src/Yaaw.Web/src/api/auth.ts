import { apiFetch } from "./client";
import type { AuthResponse, UserProfile } from "@/types";

export async function login(
  email: string,
  password: string,
): Promise<AuthResponse> {
  const res = await apiFetch("/api/auth/login", {
    method: "POST",
    body: JSON.stringify({ email, password }),
  });
  if (!res.ok) throw res;
  return res.json();
}

export async function register(
  email: string,
  name: string,
  password: string,
): Promise<AuthResponse> {
  const res = await apiFetch("/api/auth/register", {
    method: "POST",
    body: JSON.stringify({ email, name, password }),
  });
  if (!res.ok) throw res;
  return res.json();
}

export async function getProfile(): Promise<UserProfile> {
  const res = await apiFetch("/api/auth/profile");
  if (!res.ok) throw res;
  return res.json();
}

export async function updateProfile(name: string): Promise<UserProfile> {
  const res = await apiFetch("/api/auth/profile", {
    method: "PUT",
    body: JSON.stringify({ name }),
  });
  if (!res.ok) throw res;
  return res.json();
}
