## Why

A bug fix in `feat(web): host of a Waiting room dissolves instead of leaves` (commit 0b27f6f) added the missing routing for the host-of-Waiting case — `RoomPage.handleLeave()` now branches to `rooms.dissolve(id)` (DELETE) instead of `rooms.leave(id)` (POST) when the user is host and the room status is Waiting. The implementation is in `main` and tested. **The `web-game-board` spec text didn't move with the code**, so:

- The "RoomSidebar — Leave 按钮" requirement still says `rooms.leave(id)` unconditionally — incorrect for one of the two real branches.
- The "`RoomsApiService` 增加 `resign(roomId)` 方法" requirement enumerates `resign` and `getReplay` but doesn't mention `dissolve`, even though it's a peer service method now in production.

This change is a **spec-only correction** — it brings two requirements in line with the shipped behaviour. No code, no tests, no i18n.

## What Changes

- **MODIFIED** `web-game-board` "RoomSidebar — Leave 按钮" requirement: rewrite the Leave-button bullet so it explicitly branches between dissolve (host + Waiting) and leave (everything else); add a scenario for the dissolve path.
- **MODIFIED** `web-game-board` "`RoomsApiService` 增加 `resign(roomId)` 方法" requirement: extend the abstract-class snippet and the prose to also enumerate `dissolve(roomId): Observable<void>`; add a scenario for the dissolve path.

Out of scope:
- Any code change. The implementation is already correct and committed.
- Renaming the "RoomsApiService 增加 `resign(roomId)` 方法" requirement title — the title now lists three methods (resign, getReplay, dissolve) but renaming a requirement requires a separate RENAMED delta which is overhead for a doc fix. The title imprecision is acceptable; the body lists all three.
- Touching the `web-replay`, `web-user-profile`, or `web-lobby` capabilities — they don't reference dissolve.

## Capabilities

### New Capabilities
(none)

### Modified Capabilities

- `web-game-board`: two existing requirements re-stated to match production behaviour.

## Impact

- **Modified files**: `openspec/specs/web-game-board/spec.md` (after archive applies the delta).
- **Code impact**: none.
- **Test impact**: none.
- **Backend impact**: none.
