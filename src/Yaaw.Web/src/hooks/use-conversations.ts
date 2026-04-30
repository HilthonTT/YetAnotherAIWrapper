import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import * as chatApi from "@/api/chat";
import type { ConversationDto } from "@/types";

export const conversationKeys = {
  all: ["conversations"] as const,
  list: (search?: string, sort?: string) =>
    ["conversations", "list", { search, sort }] as const,
  detail: (id: string) => ["conversations", "detail", id] as const,
};

export function useConversations(search?: string, sort?: string) {
  return useQuery({
    queryKey: conversationKeys.list(search, sort),
    queryFn: () => chatApi.getConversations(search, sort),
  });
}

export function useConversation(id: string | null) {
  return useQuery({
    queryKey: conversationKeys.detail(id!),
    queryFn: () => chatApi.getConversation(id!),
    enabled: !!id,
  });
}

export function useCreateConversation() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (name: string) => chatApi.createConversation(name),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: conversationKeys.all });
    },
  });
}

export function useRenameConversation() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: async ({ id, name }: { id: string; name: string }) => {
      const conversation = await chatApi.renameConversation(id, name);

      return conversation;
    },

    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: conversationKeys.all });
    },
    // Fired before the request — update the cache immediately
    onMutate: async ({ id, name }) => {
      // Cancel any in-flight refetches so they don't overwrite our optimistic update
      await queryClient.cancelQueries({ queryKey: conversationKeys.all });

      // Snapshot every active list query so we can roll back on error
      const previousLists = queryClient.getQueriesData<ConversationDto[]>({
        queryKey: conversationKeys.all,
      });

      // Apply the new name to all cached conversation lists
      queryClient.setQueriesData<ConversationDto[]>(
        { queryKey: conversationKeys.all },
        (old) =>
          old?.map((conv) => (conv.id === id ? { ...conv, name } : conv)),
      );

      // Return snapshot for rollback
      return { previousLists };
    },

    // On failure, restore the snapshot
    onError: (_err, _vars, context) => {
      if (context?.previousLists) {
        for (const [queryKey, data] of context.previousLists) {
          queryClient.setQueryData(queryKey, data);
        }
      }
    },

    // Always re-sync with the server after success or failure
    onSettled: () => {
      queryClient.invalidateQueries({ queryKey: conversationKeys.all });
      console.log("Invalidated");
    },
  });
}

export function useDeleteConversation() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (id: string) => chatApi.deleteConversation(id),

    onMutate: async (id) => {
      await queryClient.cancelQueries({ queryKey: conversationKeys.all });

      const previousLists = queryClient.getQueriesData<ConversationDto[]>({
        queryKey: conversationKeys.all,
      });

      queryClient.setQueriesData<ConversationDto[]>(
        { queryKey: conversationKeys.all },
        (old) => old?.filter((conv) => conv.id !== id),
      );

      return { previousLists };
    },

    onError: (_err, _vars, context) => {
      if (context?.previousLists) {
        for (const [queryKey, data] of context.previousLists) {
          queryClient.setQueryData(queryKey, data);
        }
      }
    },

    onSettled: () => {
      queryClient.invalidateQueries({ queryKey: conversationKeys.all });
    },
  });
}
