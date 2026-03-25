# URGENT ACTION PLAN — SQLite WAL Mode / Read Visibility Bug

| | |
|---|---|
| **Priority** | URGENT — blocks all write visibility |
| **Scope** | `Core/DatabaseService.cs` |
| **Risk** | Low — additive pragma, no schema change |
| **Affects** | Campaign creation, import, NPC creation, all post-write UI refreshes |

---

## Problem

SQLite is running in its default journal mode (`DELETE`). In this mode, when the app writes data and then immediately re-queries the same open connection, reads return stale cached pages rather than the freshly written data. The data is written correctly to disk — but the in-memory read cache is not invalidated between the write and the subsequent read.

This causes every refresh that fires immediately after a write to appear as though the write never happened. Data only becomes visible after a full app restart, because a fresh connection reads cleanly from disk.

### Confirmed affected flows

- **Campaign creation** — new campaign appears only after restarting the app
- **Import** — pressing Import writes all data silently, but the sidebar reload sees nothing
- **NPC creation** — newly added records appear only after restart (likely all entity creation)

### Root cause trace

1. `NavBar.ShowImportCampaignModal` calls `ApplyPackage(...)` → writes all imported entities to the DB
2. `CampaignDataImported` signal fires immediately after
3. `App.cs` calls `ReloadSidebar()` → `LoadAll()`
4. `LoadAll()` re-queries the same open connection and hits the stale read cache — sees nothing
5. Same pattern applies to `AddCampaignModal` and all other post-write UI refreshes

---

## Fix

Enable WAL (Write-Ahead Log) mode on the SQLite connection immediately after opening it. WAL separates readers from writers using a separate log file, meaning any read issued after a committed write on the same connection will always see the latest data.

This is a one-time PRAGMA. Once set, it persists in the database file itself — existing databases pick it up automatically on the next reconnect.

### File to change

`Core/DatabaseService.cs` → `private void InitConnection()`

### What to add

Immediately after `_conn.Open()`, before any repository is instantiated or any migration runs, execute two PRAGMAs in a single command:

- `PRAGMA journal_mode=WAL;` — switches to Write-Ahead Log mode
- `PRAGMA synchronous=NORMAL;` — standard companion setting; safe and significantly faster than the default `FULL` without any corruption risk on crash

### Why nothing else needs to change

- All repositories share the same `SqliteConnection` instance — the PRAGMA applies globally
- Purely additive; no schema changes, no migration version bump, no data at risk
- `Reconnect()` already calls `InitConnection()`, so the PRAGMA will also fire correctly after a backup restore
- The `-wal` and `-shm` sidecar files SQLite creates are already handled — `NavBar.cs` already zips them in the backup flow

---

## Verification Steps

1. Build and run the app
2. Create a new campaign — it should appear in the list immediately without restarting
3. Open a campaign, export data to a `.dndx` file, then import it back — the sidebar should update immediately after clicking Import
4. Add a new NPC — it should appear in the sidebar immediately
5. Confirm a `campaign.db-wal` file appears next to `campaign.db` (expected — this is WAL's sidecar file)
6. Run a backup — confirm the `.zip` contains `campaign.db-wal` and `campaign.db-shm` alongside `campaign.db`

---

## Notes

- The existing `backup_debug.log` written by `NavBar.cs` already captures `DbPath` at backup time — no changes needed there
- The existing `Disconnect()` call already invokes `SqliteConnection.ClearAllPools()`, which flushes the WAL on close — correct behaviour, no change needed
- Do **not** enable WAL by appending it to the connection string. Use the PRAGMA approach so it fires every time `InitConnection()` runs, including after `Reconnect()`
