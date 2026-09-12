import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { environment } from '../../../environments/environment';

export interface ProviderInfo {
  provider: string;
  modelId: string;
  endpoint: string;
  apiKeyMasked: string;
  mode: 'offline' | 'online' | string;
}

@Injectable({ providedIn: 'root' })
export class ProviderApiService {
  private readonly http = inject(HttpClient);

  get() {
    return this.http.get<ProviderInfo>(`${environment.apiBaseUrl}/api/provider`);
  }
}
