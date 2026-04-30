## 1. Spec text amendment only

- [x] 1.1 Confirm the implementation already matches the new spec text — `RoomPage.handleLeave()` branches on `state.status === 'Waiting' && host.id === auth.user()?.id` → `rooms.dissolve(id)`; otherwise → `rooms.leave(id)`. Both navigate `/home` on success. (Already in `main` from commit 0b27f6f.)
- [x] 1.2 Confirm `RoomsApiService.dissolve(roomId): Observable<void>` exists (DELETE `/api/rooms/{id}`) on both abstract and default classes. (Already in `main`.)
- [x] 1.3 Confirm regression tests cover dissolve — `rooms-api.service.spec.ts` has the DELETE endpoint test; `RoomPage` spec's `StubRoomsApi` has a `dissolve` mock. (Already in `main`.)

## 2. Validation

- [x] 2.1 `openspec validate fix-spec-host-dissolve-drift` is clean.
- [x] 2.2 Archive: the MODIFIED requirement headers match the existing live spec headers exactly (whitespace-insensitive) so the delta applies cleanly.
