import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { environment } from '../../../environments/environment';
import { ChatSession } from './api.types';

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

  delete(sessionId: string) {
    return this.http.delete(`${this.base}/${encodeURIComponent(sessionId)}`);
  }
}
