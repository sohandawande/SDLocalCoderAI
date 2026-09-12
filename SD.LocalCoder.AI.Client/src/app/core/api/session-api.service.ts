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
    let dataLines: string[] = [];

    const flush = () => {
      if (dataLines.length === 0) {
        eventName = 'message';
        return;
      }
      const data = dataLines.join('\n');
      dataLines = [];
      this.dispatch(eventName, data, handlers);
      eventName = 'message';
    };

    while (true) {
      const { done, value } = await reader.read();
      if (done) {
        flush();
        break;
      }

      buffer += decoder.decode(value, { stream: true });
      const parts = buffer.split('\n');
      buffer = parts.pop() ?? '';

      for (const raw of parts) {
        const line = raw.endsWith('\r') ? raw.slice(0, -1) : raw;

        if (line.startsWith('event:')) {
          eventName = line.slice(6).trim();
          continue;
        }
        if (line.startsWith('data:')) {
          // SSE: optional single space after data:
          const payload = line.startsWith('data: ') ? line.slice(6) : line.slice(5);
          dataLines.push(payload);
          continue;
        }
        if (line === '') {
          flush();
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
      } catch {
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
