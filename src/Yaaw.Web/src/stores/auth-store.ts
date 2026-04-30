import { create } from "zustand";
import type { AuthResponse } from "@/types";

interface AuthState {
  token: string | null;
  userId: string | null;
  email: string | null;
  name: string | null;
  isAuthenticated: boolean;
  login: (response: AuthResponse) => void;
  logout: () => void;
  hydrate: () => void;
}

function isTokenExpired(token: string): boolean {
  try {
    const payload = token.split(".")[1];
    const decoded = JSON.parse(atob(payload));
    const exp = decoded.exp as number;
    return Date.now() >= exp * 1000;
  } catch {
    return true;
  }
}

export const useAuthStore = create<AuthState>((set) => ({
  token: null,
  userId: null,
  email: null,
  name: null,
  isAuthenticated: false,

  login: (response: AuthResponse) => {
    localStorage.setItem("auth_token", response.token);
    localStorage.setItem("auth_user_id", response.userId);
    localStorage.setItem("auth_user_email", response.email);
    localStorage.setItem("auth_user_name", response.name);
    set({
      token: response.token,
      userId: response.userId,
      email: response.email,
      name: response.name,
      isAuthenticated: true,
    });
  },

  logout: () => {
    localStorage.removeItem("auth_token");
    localStorage.removeItem("auth_user_id");
    localStorage.removeItem("auth_user_email");
    localStorage.removeItem("auth_user_name");
    set({
      token: null,
      userId: null,
      email: null,
      name: null,
      isAuthenticated: false,
    });
  },

  hydrate: () => {
    const token = localStorage.getItem("auth_token");
    if (!token || isTokenExpired(token)) {
      localStorage.removeItem("auth_token");
      localStorage.removeItem("auth_user_id");
      localStorage.removeItem("auth_user_email");
      localStorage.removeItem("auth_user_name");
      return;
    }
    set({
      token,
      userId: localStorage.getItem("auth_user_id"),
      email: localStorage.getItem("auth_user_email"),
      name: localStorage.getItem("auth_user_name"),
      isAuthenticated: true,
    });
  },
}));
