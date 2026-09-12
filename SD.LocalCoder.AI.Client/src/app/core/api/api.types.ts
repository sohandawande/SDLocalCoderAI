export interface ChatSession {
  sessionId: string;
  repoId?: string | null;
  title: string;
  createdAtUtc: string;
  updatedAtUtc: string;
  defaultPaths: string[];
  messages: ChatMessage[];
}

export interface ChatMessage {
  role: 'user' | 'assistant' | 'system' | string;
  content: string;
  atUtc: string;
  includedPaths?: string[] | null;
}

export interface RepoListResponse {
  total: number;
  repos: string[];
}

export interface FileListResponse {
  repoId: string;
  totalFiles: number;
  files: string[];
}

export interface FileContentResponse {
  repoId: string;
  path: string;
  content: string;
}
