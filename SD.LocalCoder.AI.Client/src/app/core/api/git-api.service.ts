import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { environment } from '../../../environments/environment';
import { FileContentResponse, FileListResponse, RepoListResponse } from './api.types';

@Injectable({ providedIn: 'root' })
export class GitApiService {
  private readonly http = inject(HttpClient);
  private readonly base = `${environment.apiBaseUrl}/api/git`;

  listRepos() {
    return this.http.get<RepoListResponse>(this.base);
  }

  clone(gitUrl: string) {
    return this.http.post<{ message: string; repoId: string }>(`${this.base}/clone`, { gitUrl });
  }

  addLocal(localPath: string) {
    return this.http.post<{ message: string; repoId: string }>(`${this.base}/local`, {
      localPath,
    });
  }

  listFiles(repoId: string) {
    return this.http.get<FileListResponse>(`${this.base}/${encodeURIComponent(repoId)}/files`);
  }

  readFile(repoId: string, path: string) {
    return this.http.get<FileContentResponse>(`${this.base}/${encodeURIComponent(repoId)}/file`, {
      params: { path },
    });
  }

  writeFile(repoId: string, path: string, content: string) {
    return this.http.put<{ message: string; repoId: string; path: string }>(
      `${this.base}/${encodeURIComponent(repoId)}/file`,
      { path, content },
    );
  }
}
