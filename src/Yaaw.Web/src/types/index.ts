export interface ConversationDto {
  id: string;
  name: string;
  messages: ConversationMessageDto[];
}

export interface ConversationMessageDto {
  id: string;
  conversationId: string;
  role: string;
  text: string;
}

export interface ClientMessageFragmentDto {
  id: string;
  sender: string;
  text: string;
  fragmentId: string;
  isFinal: boolean;
}

export interface AuthResponse {
  token: string;
  userId: string;
  email: string;
  name: string;
}

export interface UserProfile {
  id: string;
  email: string;
  name: string;
  createdAtUtc: string;
}

export interface CollectionResponse<T> {
  items: T[];
  links?: LinkDto[];
}

export interface LinkDto {
  href: string;
  rel: string;
  method: string;
}

export interface ChatMessage {
  id: string;
  role: string;
  text: string;
  isStreaming: boolean;
  isPlaceholder: boolean;
}
