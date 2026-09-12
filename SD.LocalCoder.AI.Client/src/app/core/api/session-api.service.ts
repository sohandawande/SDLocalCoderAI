import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { environment } from '../../../environments/environment';
import { ChatSession } from './api.types';

export type StreamHandlers = {
  onToken?: (text: string) => void;
  onContext?: (paths: string[]) => void;
  onDone?: (session: ChatSession) => void;
  onError?: (message: string) => void;
};

@Injectable({ providedIn: 'root' })
export class SessionApiService {
  private readonly http = inject(HttpClient);
  private readonly base = `${environment.apiBaseUrl}/api/sessions`;

  create(body: { repoId?: string | null; title?: string; paths?: string[] }) {
    return this.http.post<ChatSession>(this.base, body);
  }

  list() {
    return this.http.get<ChatSession[]>(this.base);
  }

  get(sessionId: string) {
    return this.http.get<ChatSession>(`${this.base}/${encodeURIComponent(sessionId)}`);
  }

  send(sessionId: string, prompt: string, paths?: string[]) {
    return this.http.post<{ session: ChatSession; model: string }>(
      `${this.base}/${encodeURIComponent(sessionId)}/messages`,
      { prompt, paths },
    );
  }

  /**
   * SSE stream: events token | context | done | error
   */
  async sendStream(
    sessionId: string,
    prompt: string,
    paths: string[] | undefined,
    handlers: StreamHandlers,
    signal?: AbortSignal,
  ): Promise<void> {
    const res = await fetch(`${this.base}/${encodeURIComponent(sessionId)}/messages/stream`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json', Accept: 'text/event-stream' },
      body: JSON.stringify({ prompt, paths }),
      signal,
    });

    if (!res.ok || !res.body) {
      let msg = `HTTP ${res.status}`;
      try {
        const j = await res.json();
        msg = j.error ?? msg;
      } catch {
        /* ignore */
      }
      handlers.onError?.(msg);
      return;
    }

    const reader = res.body.getReader();
    const decoder = new TextDecoder();
    let buffer = '';
    let eventName = 'message';

    while (true) {
      const { done, value } = await reader.read();
      if (done) break;

      buffer += decoder.decode(value, { stream: true });
      const parts = buffer.split('\n');
      buffer = parts.pop() ?? '';

      for (const line of parts) {
        if (line.startsWith('event:')) {
          eventName = line.slice(6).trim();
          continue;
        }
        if (line.startsWith('data:')) {
          const data = line.slice(5).trimStart();
          // continuation lines are merged by our server with "data: " prefix already split per line
          this.dispatch(eventName, data, handlers);
          continue;
        }
        if (line === '') {
          eventName = 'message';
        }
      }
    }
  }

  private dispatch(eventName: string, data: string, handlers: StreamHandlers) {
    if (eventName === 'token') {
      handlers.onToken?.(data);
      return;
    }
    if (eventName === 'context') {
      try {
        handlers.onContext?.(JSON.parse(data) as string[]);
      } catch {
        /* ignore */
      }
      return;
    }
    if (eventName === 'done') {
      try {
        handlers.onDone?.(JSON.parse(data) as ChatSession);
      } catch (e) {
        handlers.onError?.('Failed to parse session');
      }
      return;
    }
    if (eventName === 'error') {
      handlers.onError?.(data);
    }
  }

  delete(sessionId: string) {
    return this.http.delete(`${this.base}/${encodeURIComponent(sessionId)}`);
  }
}
