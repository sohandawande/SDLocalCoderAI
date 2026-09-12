# SD LocalCoder AI

Offline-first / online-ready **code generator** assistant.

- **API**: .NET 10 (`SD.LocalCoder.AI`)
- **Client**: Angular 22 (`SD.LocalCoder.AI.Client`)
- **Local models**: Ollama via OpenAI-compatible endpoint (default `qwen2.5-coder:14b`)

Generate code from prompts, bind a git/local repo for style context, multi-turn sessions, and apply generated files back into the repo.

## Quick start

### 1. Ollama
```bash
ollama pull qwen2.5-coder:14b
ollama serve
```

### 2. API
```bash
cd SD.LocalCoder.AI/SD.LocalCoder.AI.Api
dotnet run
```

Default CORS allows `http://localhost:4200`. Adjust `Program.cs` model/endpoint if needed.

### 3. Angular client
```bash
cd SD.LocalCoder.AI.Client
npm install
npm start
```

Set API URL in `src/environments/environment.development.ts` if your API port differs.

## API surface

| Area | Endpoints |
|------|-----------|
| Git | `GET /api/git`, `POST /api/git/clone`, `POST /api/git/local`, `GET /api/git/{repoId}/files`, `GET/PUT /api/git/{repoId}/file` |
| One-shot chat | `POST /api/chat` (`prompt`, optional `repoId`, `paths`) |
| Sessions | `POST/GET /api/sessions`, `GET/DELETE /api/sessions/{id}`, `POST /api/sessions/{id}/messages` |

## Product direction

Use this app to scaffold and evolve **any** application codebase (any language), offline on your machine, with optional cloud providers later.
