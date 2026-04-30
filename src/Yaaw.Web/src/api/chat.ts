import { apiFetch } from "./client";
import type { ConversationDto, CollectionResponse } from "@/types";

export async function getConversations(
  search?: string,
  sort?: string,
): Promise<ConversationDto[]> {
  const params = new URLSearchParams();
  if (search) {
    params.set("search", search);
  }
  if (sort) {
    params.set("sort", sort);
  }

  const query = params.toString();
  const url = `/api/chat${query ? `?${query}` : ""}`;

  const res = await apiFetch(url);
  if (!res.ok) {
    throw res;
  }

  const data: CollectionResponse<ConversationDto> = await res.json();
  return data.items;
}

export async function getConversation(id: string): Promise<ConversationDto> {
  const res = await apiFetch(`/api/chat/${id}`);
  if (!res.ok) {
    throw res;
  }
  return res.json();
}

export async function createConversation(
  name: string,
): Promise<ConversationDto> {
  const res = await apiFetch("/api/chat", {
    method: "POST",
    body: JSON.stringify({ name }),
  });
  if (!res.ok) {
    throw res;
  }
  return res.json();
}

export async function sendPrompt(
  conversationId: string,
  text: string,
): Promise<void> {
  const res = await apiFetch(`/api/chat/${conversationId}`, {
    method: "POST",
    body: JSON.stringify({ text }),
  });
  if (!res.ok) {
    throw res;
  }
}

export async function renameConversation(
  id: string,
  name: string,
): Promise<ConversationDto> {
  const res = await apiFetch(`/api/chat/${id}`, {
    method: "PATCH",
    body: JSON.stringify({ name }),
  });
  if (!res.ok) {
    throw res;
  }
  return res.json();
}

export async function deleteConversation(id: string): Promise<void> {
  const res = await apiFetch(`/api/chat/${id}`, {
    method: "DELETE",
  });
  if (!res.ok) {
    throw res;
  }
}

export async function cancelGeneration(id: string): Promise<void> {
  const res = await apiFetch(`/api/chat/${id}/cancel`, {
    method: "PUT",
  });
  if (!res.ok) {
    throw res;
  }
}
