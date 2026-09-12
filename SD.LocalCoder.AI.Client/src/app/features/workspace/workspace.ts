import { Component, computed, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { GitApiService } from '../../core/api/git-api.service';
import { SessionApiService } from '../../core/api/session-api.service';
import { ProviderApiService, ProviderInfo } from '../../core/api/provider-api.service';
import { ChatMessage, ChatSession } from '../../core/api/api.types';

@Component({
  selector: 'app-workspace',
  imports: [CommonModule, FormsModule],
  templateUrl: './workspace.html',
  styleUrl: './workspace.css',
})
export class Workspace {
  private readonly gitApi = inject(GitApiService);
  private readonly sessionApi = inject(SessionApiService);
  private readonly providerApi = inject(ProviderApiService);

  readonly repos = signal<string[]>([]);
  readonly selectedRepoId = signal<string | null>(null);
  readonly files = signal<string[]>([]);
  readonly selectedPaths = signal<string[]>([]);
  readonly filePreview = signal<{ path: string; content: string } | null>(null);

  readonly sessions = signal<ChatSession[]>([]);
  readonly activeSession = signal<ChatSession | null>(null);
  readonly prompt = signal('');
  readonly busy = signal(false);
  readonly error = signal<string | null>(null);
  readonly streamingText = signal('');
  readonly provider = signal<ProviderInfo | null>(null);

  readonly cloneUrl = signal('');
  readonly localPath = signal('');
  readonly applyPath = signal('');
  readonly applyContent = signal('');
  readonly suggestedFiles = signal<{ path: string; content: string }[]>([]);

  readonly messages = computed(() => this.activeSession()?.messages ?? []);

  constructor() {
    this.refreshRepos();
    this.refreshSessions();
    this.providerApi.get().subscribe({
      next: (p) => this.provider.set(p),
      error: () => this.provider.set(null),
    });
  }

  refreshRepos() {
    this.gitApi.listRepos().subscribe({
      next: (r) => this.repos.set(r.repos ?? []),
      error: (e) => this.error.set(e?.error?.error ?? e.message ?? 'Failed to list repos'),
    });
  }

  refreshSessions() {
    this.sessionApi.list().subscribe({
      next: (s) => this.sessions.set(s ?? []),
      error: () => {
        /* ignore */
      },
    });
  }

  selectRepo(repoId: string) {
    this.selectedRepoId.set(repoId);
    this.selectedPaths.set([]);
    this.filePreview.set(null);
    this.gitApi.listFiles(repoId).subscribe({
      next: (r) => this.files.set(r.files ?? []),
      error: (e) => this.error.set(e?.error?.error ?? 'Failed to list files'),
    });
  }

  togglePath(path: string) {
    const cur = this.selectedPaths();
    this.selectedPaths.set(cur.includes(path) ? cur.filter((p) => p !== path) : [...cur, path]);
  }

  openFile(path: string) {
    const repoId = this.selectedRepoId();
    if (!repoId) return;
    this.gitApi.readFile(repoId, path).subscribe({
      next: (r) => {
        this.filePreview.set({ path: r.path, content: r.content });
        this.applyPath.set(r.path);
        this.applyContent.set(r.content);
      },
      error: (e) => this.error.set(e?.error?.error ?? 'Failed to read file'),
    });
  }

  cloneRepo() {
    const url = this.cloneUrl().trim();
    if (!url) return;
    this.busy.set(true);
    this.gitApi.clone(url).subscribe({
      next: (r) => {
        this.busy.set(false);
        this.cloneUrl.set('');
        this.refreshRepos();
        if (r.repoId) this.selectRepo(r.repoId);
      },
      error: (e) => {
        this.busy.set(false);
        this.error.set(e?.error?.error ?? 'Clone failed');
      },
    });
  }

  addLocalRepo() {
    const path = this.localPath().trim();
    if (!path) return;
    this.busy.set(true);
    this.gitApi.addLocal(path).subscribe({
      next: (r) => {
        this.busy.set(false);
        this.localPath.set('');
        this.refreshRepos();
        if (r.repoId) this.selectRepo(r.repoId);
      },
      error: (e) => {
        this.busy.set(false);
        this.error.set(e?.error?.error ?? 'Add local failed');
      },
    });
  }

  newSession() {
    this.busy.set(true);
    this.sessionApi
      .create({
        repoId: this.selectedRepoId(),
        title: this.selectedRepoId() ? `Code · ${this.selectedRepoId()}` : 'New session',
        paths: this.selectedPaths(),
      })
      .subscribe({
        next: (s) => {
          this.busy.set(false);
          this.activeSession.set(s);
          this.streamingText.set('');
          this.suggestedFiles.set([]);
          this.refreshSessions();
        },
        error: (e) => {
          this.busy.set(false);
          this.error.set(e?.error?.error ?? 'Failed to create session');
        },
      });
  }

  openSession(sessionId: string) {
    this.sessionApi.get(sessionId).subscribe({
      next: (s) => {
        this.activeSession.set(s);
        this.streamingText.set('');
        if (s.repoId) this.selectRepo(s.repoId);
      },
      error: (e) => this.error.set(e?.error?.error ?? 'Session not found'),
    });
  }

  async send() {
    const session = this.activeSession();
    const text = this.prompt().trim();
    if (!session || !text || this.busy()) return;

    this.busy.set(true);
    this.error.set(null);
    this.streamingText.set('');
    this.suggestedFiles.set([]);

    // Optimistic user message
    const optimistic: ChatSession = {
      ...session,
      messages: [
        ...session.messages,
        { role: 'user', content: text, atUtc: new Date().toISOString() },
      ],
    };
    this.activeSession.set(optimistic);
    this.prompt.set('');

    await this.sessionApi.sendStream(session.sessionId, text, this.selectedPaths(), {
      onToken: (t) => this.streamingText.update((s) => s + t),
      onDone: (s) => {
        this.activeSession.set(s);
        this.streamingText.set('');
        this.busy.set(false);
        this.refreshSessions();
        const last = [...s.messages].reverse().find((m) => m.role === 'assistant');
        if (last) this.extractApplyBlocks(last.content);
      },
      onError: (msg) => {
        this.busy.set(false);
        this.error.set(msg);
        this.streamingText.set('');
      },
    });
  }

  /** Parse ### FILE: path + fenced code from assistant output */
  extractApplyBlocks(content: string) {
    const blocks: { path: string; content: string }[] = [];
    const re =
      /###\s*FILE:\s*([^\n]+)\s*```[^\n]*\n([\s\S]*?)```/gi;
    let m: RegExpExecArray | null;
    while ((m = re.exec(content)) !== null) {
      blocks.push({ path: m[1].trim(), content: m[2].replace(/\s+$/, '') });
    }
    this.suggestedFiles.set(blocks);
    if (blocks.length === 1) {
      this.applyPath.set(blocks[0].path);
      this.applyContent.set(blocks[0].content);
    }
  }

  loadSuggestion(s: { path: string; content: string }) {
    this.applyPath.set(s.path);
    this.applyContent.set(s.content);
  }

  applyFile() {
    const repoId = this.selectedRepoId();
    const path = this.applyPath().trim();
    if (!repoId || !path) return;

    this.busy.set(true);
    this.gitApi.writeFile(repoId, path, this.applyContent()).subscribe({
      next: () => {
        this.busy.set(false);
        this.gitApi.listFiles(repoId).subscribe((r) => this.files.set(r.files ?? []));
        this.openFile(path);
      },
      error: (e) => {
        this.busy.set(false);
        this.error.set(e?.error?.error ?? 'Write failed');
      },
    });
  }

  trackMsg(_: number, m: ChatMessage) {
    return m.atUtc + m.role + m.content.slice(0, 20);
  }
}
