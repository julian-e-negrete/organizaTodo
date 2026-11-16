# Self-Hosted Offline Installer — Design

## Goal

Ship OrganizaTodo as a native, double-click installer for Windows and Linux
that a non-technical user can install and run with **no internet connection**
and **no separate PostgreSQL/.NET installation**. The existing Docker/server
deployment path (external Postgres, `.env`-driven config) stays unchanged and
fully supported — this is an additive distribution model, not a replacement.

## Non-goals (explicitly out of scope)

- Pinecone semantic search (`organizatodo-products` / `organizatodo-financial`,
  `scripts/sync_*_to_pinecone.py`) — inherently cloud-dependent, stays
  web/server-only, excluded entirely from the desktop build.
- Auto-update mechanism — v1 users reinstall via a new installer.
- Data sync between a desktop install and a separately hosted server instance.
- Any change to the existing Docker/server deployment path.

## Architecture overview

A new `OrganizaTodo.Desktop` project is the shipped artifact: a single native
executable per OS that owns the full runtime lifecycle.

```
OrganizaTodo.Desktop
        │
        ├── PostgreSQL (bundled, child process, private/loopback port)
        ├── Kestrel (OrganizaTodo.Web hosted in-process)
        └── Photino.NET window → http://127.0.0.1:<kestrel-port>
```

- **`OrganizaTodo.Web` stays application logic only.** It receives a normal
  connection string (`Host=127.0.0.1;Port=...;Database=...`) and has no
  awareness that Postgres is "embedded." This keeps it portable across
  Desktop → bundled Postgres, Docker → containerized Postgres, and Server →
  external Postgres.
- **`OrganizaTodo.Desktop` owns lifecycle**: locating bundled Postgres
  binaries, creating/initializing the data directory, generating Postgres
  config, port selection, starting Postgres, readiness checks, schema
  bootstrap, starting Kestrel in-process, opening the Photino window, and
  graceful shutdown in reverse order.
- **Kestrel runs in-process** inside `OrganizaTodo.Desktop` rather than as a
  separate child process — avoids a second process boundary and IPC/lifecycle
  management for the web app itself.
- **Photino.NET** (not Electron) provides the native window, using the OS's
  webview (WebView2 on Windows, WebKitGTK on Linux) — pure .NET toolchain, no
  Node/Electron build pipeline, small installer footprint. Tradeoff: depends
  on the OS webview being present/bundled (addressed in packaging below).
  Photino points only at `127.0.0.1`; Kestrel binds `127.0.0.1` only (not
  `0.0.0.0`); Postgres is never exposed outside loopback.
- **Directory separation**: read-only install directory (app binaries,
  Postgres binaries, static resources) is kept separate from writable user
  data (`pgdata/`, `logs/`, `config/`), so upgrades/uninstalls never touch
  user data.
  - Windows: `%LOCALAPPDATA%\OrganizaTodo\{pgdata,logs,config}`
  - Linux: `~/.local/share/organizatodo/{pgdata,logs,config}`

## `OrganizaTodo.Web` refactor

`Program.cs` currently uses top-level statements (implicit `Main`), which
can't be invoked in-process by another project. Extract:

```csharp
public static WebApplication BuildApp(string[] args, Action<WebApplicationBuilder>? configure = null)
```

`Main` becomes a thin wrapper (`BuildApp(args).Run()`) still used by
Docker/`dotnet run`. `OrganizaTodo.Desktop` calls `BuildApp` directly with a
`configure` delegate that:

- injects the generated connection strings (`ConnectionStrings:DefaultConnection`,
  `ConnectionStrings:ShoppingConnection`),
- binds Kestrel to `http://127.0.0.1:<port>` only,
- skips `UseHttpsRedirection()` / `UseHsts()` (irrelevant on loopback HTTP,
  no cert available) via a desktop-mode flag.

## Startup/shutdown lifecycle (strict ordering)

`Desktop → PostgreSQL → wait for readiness → Kestrel → Photino`, never
"start both and hope":

1. Resolve per-OS app-data dir; ensure `pgdata/`, `logs/`, `config/` exist.
2. Pick ports for Postgres and Kestrel: try fixed defaults first, fall back to
   probing a free loopback port if taken; remember the choice in `config/`
   for stable restarts.
3. If `pgdata/PG_VERSION` is absent: run bundled `initdb` with a generated
   local superuser + password (written to `config/db-credentials.json`,
   file permissions restricted to the current user), configuring
   `listen_addresses='127.0.0.1'`.
4. Start bundled `postgres`/`pg_ctl` on the chosen port. Poll for readiness
   (bundled `pg_isready` or a retrying Npgsql connection) until ready or a
   timeout; on timeout, clean up anything started and surface an error —
   never leave an orphaned Postgres process.
5. On a fresh cluster: create both databases, apply `init.sql` to each, then
   apply `migrate_*.sql` / `migrate_shopping_*.sql` in numeric order, tracked
   in a `schema_migrations` marker table (needed for future app updates, not
   just first run — track which files have been applied so updates only run
   new ones).
6. Call `OrganizaTodo.Web.Program.BuildApp(...)` with the generated
   connection strings, run it on a background task, wait for the
   "started" signal, then open the Photino window at that URL.
7. On window close: `app.StopAsync()` → stop Postgres cleanly
   (`pg_ctl stop -m fast`, SIGKILL only as a last-resort fallback) → exit.
8. Single-instance guard (named mutex on Windows / pidfile+flock on Linux): a
   second launch focuses the existing window instead of starting a second
   Postgres against the same data directory.

## Error handling

Any failure in the ordered startup sequence (port exhaustion, `initdb`
failure, schema-apply failure, Kestrel bind failure) triggers full cleanup of
anything already started before showing a native error dialog with a path to
the log file, then exiting non-zero — no silent partial states. If the
Postgres child process dies unexpectedly *after* a successful startup (e.g.
disk full), Desktop detects the exit and surfaces an error rather than
leaving Kestrel serving against a dead DB. Logs under `logs/` are size-capped
to avoid unbounded growth on long-lived installs.

## Build & packaging

**Build pipeline** produces two separate, platform-specific artifacts — no
cross-bundling:

- `dotnet publish` of `OrganizaTodo.Desktop`, self-contained + single-file,
  per RID (`win-x64`, `linux-x64`) — bundles the .NET runtime, no SDK needed
  on the target machine.
- Vendored Postgres binaries for the matching RID (one pinned version,
  official win-x64/linux-x64 builds, vendored at build time — PostgreSQL
  License is permissive, redistribution is a build-pipeline/versioning
  concern, not a legal blocker).
- Windows only: bundle the offline "Fixed Version" WebView2 runtime
  redistributable so install works without internet and without WebView2
  preinstalled.

**Windows packaging** — Inno Setup wraps the self-contained publish +
Postgres binaries + WebView2 redistributable into a single wizard `.exe`:
installs to `Program Files`, creates a Start Menu/Desktop shortcut. No
service registration — nothing runs until the user launches the app.

**Linux packaging** — self-extracting shell installer (makeself-style):
extracts to `/opt/organizatodo` (or a per-user dir to avoid requiring root),
drops a `.desktop` launcher entry into the applications menu. No systemd
unit — the app is a desktop app launched interactively, not a background
service. WebKitGTK is documented as a system prerequisite (near-universal on
desktop distros already).

Both installers are single offline artifacts — no package-manager/registry
access needed at install time.

## Testing

Per the project's superpowers TDD workflow:

- Unit tests: port-selection/fallback logic, `schema_migrations` tracking,
  connection-string construction, the lifecycle state machine (process
  start/stop mocked).
- One integration-style test spins up the actual bundled Postgres binary in a
  temp dir to validate `initdb` → schema apply → connect end-to-end — this is
  the riskiest part of the design, so it gets a real test rather than a mock.
- Final manual verification: install the artifact on a clean VM (no internet,
  no .NET, no Postgres) for both OSes before calling the feature done.

## Development directive

Implementation should follow the superpowers workflow already in use on this
project — `test-driven-development` for new code, `systematic-debugging` for
any unexpected failures encountered, and `writing-plans` to turn this spec
into an implementation plan before writing code.
