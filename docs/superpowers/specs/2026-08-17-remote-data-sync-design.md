# Remote Data Sync — Design

## Goal

Let a user log in on the desktop app (from [`2026-08-16-self-hosted-offline-installer-design.md`](2026-08-16-self-hosted-offline-installer-design.md)) with an account that already exists on their remote OrganizaTodo deployment, pull that account's data down for offline use, and manually sync changes back and forth so the same account can be used across multiple devices. This reverses that spec's non-goal "no Desktop↔server data sync" — the user has explicitly decided they want it.

## Non-goals (explicitly out of scope)

- Making the remote server reachable from outside the user's LAN (VPN, tunnel, port-forward, reverse proxy, TLS certificate) — the user's own infrastructure decision. Sync works immediately for devices on the same LAN as the remote server; reaching it from elsewhere is a separate step the user handles themselves.
- Automatic/background sync — manual "Sync now" trigger only.
- Manual conflict-review UI — last-write-wins by `UpdatedAt` only, resolved entirely server-side.
- Creating a brand-new remote account from the desktop app — only connecting to an existing remote account is supported; new-account signup remains a web-app-only flow.
- A change history/audit log of what synced.
- Syncing the shopping catalog (`carrefour_products`/`coto_products`) — unrelated reference data, server-populated by an external scraper, already excluded from the desktop build entirely.

## Account modes

Two coexist in the desktop app:

- **Local-only** (already built, per the offline-installer design) — sign up locally, never touches a server, fully offline forever.
- **Remote-linked** (this design) — connect to an existing account on a remote OrganizaTodo deployment; pulls that account's data down and enables ongoing sync. A user chooses one or the other; this design only adds the second as an option.

## Synced data

Every user-owned table, keyed by `UserId` — 16 tables: `HousingServices`, `CreditCardPurchases`, `FixedLiabilities`, `Income`, `OtherExpenses`, `Savings`, `ShoppingLists`, `ShoppingListItems`, `AssetPurchases`, `Vehicles`, `VehicleMonthlyPayments`, `VehicleMaintenanceLogs`, `RecurringIncomes`, `LedgerTags`, `LedgerTransactions`, `LedgerTransactionTags`.

`Users` itself does not sync row-by-row. The account is always created and owned on the remote server; the desktop app caches just enough of it locally (see "Connecting a device" below) to allow offline login after the first successful remote connection.

## Schema changes

Applied to **both** `init.sql` (fresh desktop installs) and a new migration run against the live production database, on each of the 16 synced tables:

- `SyncId UUID NOT NULL DEFAULT gen_random_uuid() UNIQUE` — stable cross-device record identity, generated once at creation, never changes. Local integer `Id` stays as the primary key for local FK/UI use; `SyncId` is what sync matches records by, so two devices creating records independently while offline never collide.
- `UpdatedAt TIMESTAMPTZ NOT NULL DEFAULT NOW()`, maintained by a `BEFORE UPDATE` trigger that sets it to `NOW()` on every row update — reliable, not dependent on application code remembering to set it.
- `IsDeleted BOOLEAN NOT NULL DEFAULT FALSE` — deletes become soft-deletes (`UPDATE ... SET "IsDeleted" = TRUE`) so they propagate through sync instead of silently vanishing on one side. Every existing "get my records" query function (local and remote) needs an `AND "IsDeleted" = FALSE` filter added — a real, necessary change across the existing repository functions, not just additive schema.

A `SyncTokens` table, remote-side: `(Id, UserId, TokenHash, CreatedAt, LastUsedAt)` — one row per desktop device connected to that account, token stored hashed like a password. Locally, only the one active token for that install is stored, in the same protected-file pattern `DbCredentialStore` already uses for the local Postgres superuser credentials.

## Connecting a device to remote

1. In the desktop app, a "Connect to remote server" action (separate from local signup) collects the remote server's URL plus the user's existing email/password.
2. Desktop calls `POST /api/sync/connect` on the remote server over HTTPS with `{ email, password }` — reuses the existing `AuthService` password-verification logic.
3. Remote validates the password, issues a new opaque `SyncToken` (stored hashed, tied to the caller's `UserId`), returns `{ token, userId }`.
4. Desktop persists the token locally, creates a local `Users` row reusing the **exact remote `UserId`** (an explicit-PK insert, followed by bumping the local sequence past it) rather than a fresh local auto-increment value — so every synced child table's `UserId` foreign key matches on both sides without remapping.
5. Desktop performs an initial full pull (`since = epoch`) across all 16 tables to populate the local database.
6. From then on: day-to-day desktop login authenticates against the cached local `Users` row and works fully offline, independent of sync. "Sync now" uses the stored `SyncToken` as a bearer credential on `/api/sync` calls.

## The `/api/sync` endpoint

New authenticated endpoint on `OrganizaTodo.Web`, authorized via the stored `SyncToken` (bearer).

**Request:**
```json
{
  "since": "2026-08-01T00:00:00Z",
  "changes": {
    "housingServices": [ { "syncId": "...", "updatedAt": "...", "isDeleted": false, "...fields": "..." } ],
    "creditCardPurchases": [ "..." ]
  }
}
```
Only tables/rows with local changes since the desktop's last successful sync are included — desktop determines this the same way the server does, by querying `WHERE "UpdatedAt" > @lastSyncedAt` per table (`LastSyncedAt` is a local-only watermark value, not itself synced).

**Server processing**, in one transaction:
- Every write is scoped to the authenticated caller's `UserId` — payload `UserId` fields are never trusted; any row implying a different user is rejected. This is the critical security boundary: a compromised or buggy client can never write another user's data.
- Per incoming row (matched by `SyncId`): if it doesn't exist remotely yet, insert it. If it exists and the incoming `UpdatedAt` is newer than the stored one, update. Otherwise, discard the incoming write — the existing remote row wins.
- A single failure rolls back the whole request — never a half-applied sync.

**Response:**
```json
{
  "syncedAt": "2026-08-17T12:00:00Z",
  "changes": {
    "housingServices": [ "...every row for this user with UpdatedAt > since, after applying incoming writes..." ]
  }
}
```
This naturally includes other devices' changes since the last sync, and self-corrects the calling device: if a row it just pushed lost a conflict, the response contains the winning (remote) version so the caller overwrites its own stale copy.

## Desktop-side application

For each table, for each row in the response, upsert locally by `SyncId` (insert if unseen, update if the `SyncId` already exists). A row with `IsDeleted = true` is soft-deleted locally too, mirroring the remote tombstone rather than hard-deleting (avoids resurrecting it on a future pull). `LastSyncedAt` only advances after the full round trip succeeds.

## Error handling

If the sync HTTP call fails for any reason — network unreachable, server down, revoked/invalid token — the watermark does not advance, nothing is lost, and the user keeps working offline; the UI surfaces a clear "sync failed: `<reason>`" message and the user can retry later.

## Testing

Integration tests spin up two real (temp) Postgres instances to stand in for "local" and "remote," exercising the actual push/pull/conflict round trip end-to-end — this is the part with real correctness risk (conflict resolution, soft-delete propagation, `UserId` isolation) and gets real test coverage, not just mocks. Conflict-comparison logic (given two `UpdatedAt` values, which wins) is additionally unit-tested in isolation.

## Development directive

Implementation should follow the project's superpowers workflow — `test-driven-development` for new code, `systematic-debugging` for any unexpected failures, and `writing-plans` to turn this spec into an implementation plan before writing code.
