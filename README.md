# FeedbackHub

A hub that collects bug reports and feature requests from your products and tracks each one until it is closed. Instead of a chat message with no screenshot and no idea which device it came from, every report arrives with what IT needs to reproduce it.

- **Embeddable web widget** — a loader script plus an iframe served by the Hub. It captures an annotatable screenshot, device, browser and screen details, the URL, console errors, failed requests and data the host product passes in, without leaking the host page's tokens or cookies. Colleagues and testers are recognised from their Hub session; customers can report as guests (optionally anonymous) and follow their report through a private link.
- **Hub inbox** — each product has one or more owners who are notified (in-app, email, web push), triage, assign, comment (internal or to the reporter) and move feedback through its lifecycle: *Mới → Đã tiếp nhận → Đang xử lý → In review → Đã xử lý → Đóng*, plus *Cần thêm thông tin*, *Từ chối* and *Trùng lặp* (working names from [the core design](docs/superpowers/specs/2026-10-21-feedback-hub-core-design.md); PRD-00 will fix them).
- **Multi-tenant** — several organizations share one Hub with separate data; an account can belong to several organizations with a different role in each.
- **Satisfaction surveys** — periodic survey rounds with optionally anonymous answers and results ([surveys design](docs/superpowers/specs/2026-10-21-feedback-hub-surveys-design.md)).
- **Later:** a Flutter SDK, connectors (Plane, GitHub, GitLab) and AI triage.

**Status:** requirements stage. Each module gets a PRD (`docs/prd/`), a UI design and a technical design (`docs/tech/`) before it is built — see [the lifecycle](docs/process/lifecycle.md) and [per-module progress](docs/process/progress.md). No FeedbackHub-specific feature is implemented yet; the code today is the platform foundation below.

## Platform Foundation (already built)

- **Auth**: email + password register/login (JWT access + refresh token, refresh token stored as a SHA-256 hash), mandatory email verification via SMTP, Google + Microsoft login (credential flow / MSAL popup), session management (list/revoke by device).
- **Profile**: self-service profile (get/update) and change password for the current account. There is no account CRUD/admin surface — accounts are created only via registration, and removed only by leaving/being removed from every organization.
- **Organizations & RBAC**: multi-tenant — an account can belong to multiple organizations and switches its active one, which is encoded as the JWT `org_id` claim. Access within an organization is governed by custom roles built from a fixed permission catalog; `Owner`/`Admin`/`Member` are seeded as system roles on creation (Owner gets every permission), but roles beyond that are fully organization-defined.
- **ApiKey**, **AuditLog**, **Files** (local disk, provider pluggable via `IStorageProvider`), **SystemSettings**: all four require an active organization *and* the specific permission for it — there is no personal/org-less access and no global admin bypass.
- **Notifications**: in-app (REST + real-time via SignalR), email, and optional Web Push (Firebase Cloud Messaging) — channels fan out through a Hangfire background job.

No global admin role exists yet — outside of an organization ("personal" context) an account only has auth/profile/organization endpoints. FeedbackHub's own roles (*Quản trị*, *Phụ trách*, *Viewer*, *Người báo*, *Khách*) and the system-level admin that surveys need will be defined in PRD-00 and PRD-07; until then the working draft is the role table in [the core design](docs/superpowers/specs/2026-10-21-feedback-hub-core-design.md).

## Architecture

```text
backend/    .NET 10 API — Domain → Application → Infrastructure → API (src/, tests/, FeedbackHub.sln)
frontend/   Vue 3 + Vite dashboard (src/api, src/stores, src/views, ...)
shared/     openapi/ (shared contract — frontend generates its client/types from it)
docs/       process/ (lifecycle, progress), prd/ (requirements), tech/ (technical designs), superpowers/ (specs/ early designs, plans/ implementation plans), feature-map.md
plans/      history of plan files from the starter-kit era (reference for patterns, not product documentation; new plans go in docs/superpowers/plans/)
```

For layer/rule details for each part, see `AGENTS.md` and `.claude/rules/`. Work is done by Claude Code agents per stage (`.claude/agents/`); `AGENTS.md` § Software Lifecycle says who does what.

## Tech Stack

| Component       | Technology                                                                |
| --------------- | -------------------------------------------------------------------------- |
| Backend         | .NET 10, ASP.NET Core, Clean Architecture, EF Core                         |
| Database        | PostgreSQL                                                                  |
| Frontend        | Vue 3 + Vite, Pinia, naive-ui, vue-i18n, Tailwind                          |
| Storage         | Local disk in dev; MinIO in production (`IStorageProvider`)                |
| Email           | SMTP (MailKit) — required, even in dev                                     |
| Background jobs | Hangfire (PostgreSQL storage, dashboard at `/hangfire`)                    |
| Realtime        | SignalR (`/hubs/notifications`; in-memory in dev, Redis backplane in production) |
| Push            | Firebase Cloud Messaging (`FirebaseAdmin` SDK) — optional, no-op if unconfigured |
| Logging         | Serilog (structured, console + rolling file) + correlation ID middleware   |
| Monitoring      | Prometheus metrics (`/metrics`) + Grafana, see `monitoring/`               |

## Getting Started

### 1. Local Infrastructure (Postgres + Mailpit + Prometheus + Grafana)

```bash
docker compose up -d
```

Account verification email is required even in the dev environment — there is no seeder/bypass. Use Mailpit (`http://localhost:8025`) to view the verification email sent out when testing local registration, instead of needing a real SMTP server.

**Monitoring:** the API exposes Prometheus metrics at `/metrics` (`prometheus-net.AspNetCore`). Grafana (`http://localhost:3000`, `admin`/`admin`) comes pre-provisioned with a Prometheus datasource and a "FeedbackHub API Overview" dashboard (request rate/errors/latency, process CPU/memory, GC) — see `monitoring/`. Since the API isn't containerized, Prometheus reaches it via `host.docker.internal`, which requires running it bound to all interfaces instead of the default loopback-only bind:

```bash
dotnet run --project backend/src/FeedbackHub.API --urls http://0.0.0.0:7100
```

Without this, the Prometheus target reads as `down` and the Grafana dashboard stays empty. Binding to `0.0.0.0` also exposes the API to your LAN while it's running — fine for local monitoring, just don't leave it running unattended.

### 2. Backend (`backend/`)

```bash
# Copy the sample config
cp backend/src/FeedbackHub.API/appsettings.Example.json backend/src/FeedbackHub.API/appsettings.json

# Build — serialized, parallel build is broken in this .NET 10 environment
dotnet build backend/FeedbackHub.sln --no-restore -m:1

# Apply migrations
dotnet ef database update --project backend/src/FeedbackHub.Infrastructure --startup-project backend/src/FeedbackHub.API

# Run API
dotnet run --project backend/src/FeedbackHub.API

# Test
backend/scripts/test.sh
```

Production: migrations never run on API startup. The one-shot `migrate` service (`dotnet FeedbackHub.API.dll migrate`) applies them before any `api` replica starts, and `DatabaseSettings:MigrateOnStartup` must be `false` — the API refuses to start in Production otherwise. Outside Development and Production (e.g. Staging), the API still migrates on startup unless that key is `false`.

### 3. Frontend (`frontend/`)

Package manager: `bun`.

```bash
bun install --cwd frontend            # also generates src/api/generated/** via postinstall
bun run --cwd frontend dev            # dev server
bun run --cwd frontend build          # type-check + production build
bun run --cwd frontend test:run       # unit tests (vitest)
bun run --cwd frontend test:e2e:install && bun run --cwd frontend test:e2e   # e2e (playwright)
bun run --cwd frontend format         # prettier
bun run --cwd frontend lint:boundaries   # ESLint, import-boundary rules only
```

### 4. Try the Registration Flow

`POST /api/auth/register` → open Mailpit (`localhost:8025`) → click the verification link → log in.

A freshly registered account has no organization yet, so Files/API keys/audit logs/system settings all return 403 until you create one: `POST /api/organizations` (or use the frontend's Organizations page), then `POST /api/auth/switch-organization` to make it the session's active organization — the creator is seeded as its Owner with every permission.

### Git Hooks (once, at the repo root)

```bash
bun install   # run at the repo root (not --cwd frontend) — wires up git hooks via Lefthook
```

Hooks installed:

- **pre-commit**: blocks direct commits to `main` (release only happens via `gh pr merge` — see the `git-release` skill; bypass with `git commit --no-verify` for tooling that legitimately needs it); blocks staged changes containing unresolved merge-conflict markers (`<<<<<<<`/`=======`/`>>>>>>>`); runs `secretlint` on all staged files (catches known secret *formats* — private keys, GitHub/Slack/npm tokens; it does **not** catch arbitrary custom secrets like a hardcoded `JwtSettings:SecretKey` or `DB_PASSWORD=...` — keep those in the gitignored `appsettings.json`, not in source); runs `prettier --write` on staged `frontend/src/**`, `frontend/e2e/**` and `frontend/user-guide/tools/**` files, and on the user guide's own files with its pinned prettier; checks comment length (`scripts/check-comments.ts`). Backend files aren't formatted by a hook; C# style rules in the root `.editorconfig` that are set to `error` fail `dotnet build` instead.
- **commit-msg**: enforces Conventional Commits (`feat:`, `fix:`, `docs:`, ... — see CONTRIBUTING.md) via commitlint.

### Updating the API Contract

Whenever you add or change a backend controller route, request/response DTO, or status code, regenerate the OpenAPI spec and the frontend client:

```bash
# 1. Backend: re-export the spec to shared/openapi/openapi.json (no running server/DB required)
dotnet build backend/src/FeedbackHub.API/FeedbackHub.API.csproj --no-restore -m:1 -p:OpenApiGenerateDocumentsOnBuild=true

# 2. Frontend: regenerate src/api/generated/** from the updated spec
bun run --cwd frontend codegen
```

- Step 1 writes `shared/openapi/openapi.json` — this file is committed (it's the single source of truth for the contract), so commit the diff along with your backend change.
- `OpenApiGenerateDocumentsOnBuild` defaults to `false`: generating the doc costs ~5-9s via a design-time host, so it's opt-in rather than slowing every plain `dotnet build`.
- Step 2 writes `frontend/src/api/generated/**` — gitignored, never hand-edited, and already regenerated automatically on every `bun install` via `postinstall`. Only needed here to pick up the change without reinstalling.
- There is no hand-written API wrapper layer on the frontend — views/stores call the generated client directly (side effects like updating auth state live in `frontend/src/stores/auth.ts`). See `.claude/rules/api-contract.md` for the full contract-sync workflow and how the generated client is wired in.

### CI

Every PR into `dev`/`main`/`release/**` and every push to `dev` runs the test gate (`.github/workflows/ci.yml` → `test.yml`) in four jobs: `comments` (script tests and the comment-length check, always runs), `backend` (tests plus an OpenAPI-export drift check), `frontend` (formatting, type-check, import-boundary lint and unit tests) and `guide` (the user guide's format check, tests and build). CI runs only the jobs whose parts a change touches and counts a skipped job as passing. The release pipeline runs the same gate: a `v*` tag runs the tests, builds and pushes the production images, then creates the release (`.github/workflows/release.yml`; `.gitlab-ci.yml` is the same pipeline for a fork on GitLab). Protect `v*` tags — whoever can push one publishes images.

For full details, see `.claude/reference/ci-and-git-hooks.md`.

## Production (Docker)

`docker-compose.prod.yml` builds and runs the API and frontend as containers, with Redis, MinIO and a
one-shot `migrate` service. Production is multi-instance ready: Redis and shared file storage are required, and
`appsettings.json` must set `EmailSettings:SecureSocket`. Scale with
`up -d --scale api=N`. Restart `web` after scaling or redeploying `api` or `minio`: nginx resolves their names once at start
and keeps those addresses. `minio` must be running when `web` restarts, or nginx cannot resolve the name and does not start. Compose environment values override `appsettings.json`, so change the Redis or storage
endpoints in `docker-compose.prod.yml` or `.env.prod`.
File storage is MinIO AIStor Free, a single node. The operator supplies its licence file (`MINIO_LICENSE_FILE` in
`.env.prod`, default `./minio.license`, never committed) and its root user and password (`MINIO_ROOT_USER` /
`MINIO_ROOT_PASSWORD`), which the API also uses as its MinIO keys. The free tier has no at-rest encryption; use host
disk encryption if you need it. MinIO publishes no port: the API reaches it on the compose network.
An nginx container serves the built frontend and reverse-proxies `/api/` and `/hubs/` to the API, and
`/storage/public/` (read-only) to the MinIO bucket's `public/` prefix;
`/hangfire` and `/metrics` are deliberately not exposed through it, same "network-layer only"
restriction the API already applies (see `Program.cs`).

```bash
# 1. App secrets — same convention as local dev, just bind-mounted into the container instead of
#    baked into the image. Fill in real ConnectionStrings/JwtSettings/CorsSettings/EmailSettings/...
cp backend/src/FeedbackHub.API/appsettings.Example.json backend/src/FeedbackHub.API/appsettings.json

# 2. Compose-level vars (Postgres and MinIO credentials, MinIO licence path, frontend build arg)
cp .env.prod.example .env.prod

# 3. Build and start
docker compose -f docker-compose.prod.yml --env-file .env.prod up -d --build
```

Each release tag also publishes both images, versioned from the tag: `ghcr.io/<owner>/<repo>/{backend,frontend}:X.Y.Z` (`:latest` follows the newest stable tag; on GitLab, `$CI_REGISTRY_IMAGE/...`). Build them yourself as above, or point the `api`/`web` services at those images to deploy without building on the server.

Migrations run once in the `migrate` service before `api` starts (`DatabaseSettings:MigrateOnStartup=false`). Uploaded files persist in `feedbackhub-minio-data`, API logs in `feedbackhub-api-logs`, Redis in `feedbackhub-redis-data`; Postgres data in `feedbackhub-postgres-data`. No backup mechanism ships: include `feedbackhub-postgres-data` and `feedbackhub-minio-data` in your daily backup.

Deploy notes:

- **Keep `VITE_API_BASE_URL` empty** for this setup — nginx serves the SPA and the API from one
  origin, so the frontend calls relative `/api/...` paths.
- **Frontend config is runtime, not build-time.** The Google/Microsoft client ids and Firebase
  values in `.env.prod` are passed to the `web` container, which writes them into `/config.json` at
  startup (`frontend/docker/40-write-runtime-config.sh`). One image serves every environment;
  changing a value needs `docker compose ... up -d`, not `--build`. Any key left empty falls back to
  the value baked in at build time (none, by default).
- **Serving over plain http (no TLS in front of nginx)?** The auth cookies default to
  `Secure` + `SameSite=None`, which a browser silently drops on a non-`localhost` http origin —
  login returns 200 and every later request is 401. Set this in the bind-mounted `appsettings.json`:

  ```json
  "AuthCookieSettings": { "Secure": false, "SameSite": "Lax" }
  ```

  Tokens then travel in cleartext — acceptable for an internal demo, not for real data. The API
  refuses to start with `Secure: false` + `SameSite: None` (browsers reject that pair). Put TLS in
  front and restore the defaults before real use.
- **Using Redis for the cache?** Set its `maxmemory-policy` to `noeviction` — no `volatile-*` or
  `allkeys-*` policy. Every key the cache writes has a TTL, the invalidation counters included
  (`CacheSettings:MaxExpirationMinutes`, default 1440, plus a minute; longer entry TTLs are clamped
  to it); evicting a counter before that would resurrect stale entries (e.g. revoked permissions).
  Size Redis so it doesn't fill up: at `maxmemory` it rejects writes, so invalidations fail (logged)
  and stale entries live until their TTL. A Redis outage only slows requests down — the cache falls
  back to the database and logs it.
- **User guide at `/guide/`** is private by default: nginx asks the API whether the reader is signed
  in, which needs the API on the same origin (`VITE_API_BASE_URL` empty). Set `GUIDE_PUBLIC=true` in
  `.env.prod` to open it to everyone — see [frontend/user-guide/README.md](frontend/user-guide/README.md).
- **Secrets stay out of images.** `appsettings*.json` and `.env*` are excluded from the Docker build
  context (`.dockerignore`); only the bind-mounted copy is read at runtime.

Not included yet (fine for a first deploy, revisit before scaling): TLS termination at nginx. `api` and
`web` have healthchecks (`/api/health` and the SPA root); `web` starts once `api` is healthy.

## Documentation

- Contributing / git workflow: [CONTRIBUTING.md](CONTRIBUTING.md)
- Code of Conduct: [CODE_OF_CONDUCT.md](CODE_OF_CONDUCT.md)
- Security policy / reporting a vulnerability: [SECURITY.md](SECURITY.md)
- Changelog: [CHANGELOG.md](CHANGELOG.md)
- How work flows from idea to release: [docs/process/lifecycle.md](docs/process/lifecycle.md)
- Requirements (PRDs): [docs/prd/README.md](docs/prd/README.md) · Technical designs: [docs/tech/README.md](docs/tech/README.md)
- Platform-wide architecture decisions: [.claude/decisions.md](.claude/decisions.md) (module decisions live in each technical design)
- End-user guide (Hướng dẫn sử dụng, VitePress, served at `/guide/`): [frontend/user-guide/README.md](frontend/user-guide/README.md)

## License

[MIT](LICENSE)
