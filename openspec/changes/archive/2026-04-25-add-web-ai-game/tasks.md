## 1. API service + DTOs

- [x] 1.1 Add `BotDifficulty` string-literal union to `src/app/core/api/models/room.model.ts`: `export type BotDifficulty = 'Easy' | 'Medium' | 'Hard';`
- [x] 1.2 Add `createAiRoom(name: string, difficulty: BotDifficulty): Observable<RoomState>` to abstract `RoomsApiService` and `DefaultRoomsApiService` in `src/app/core/api/rooms-api.service.ts`. POST to `/api/rooms/ai` with body `{ name, difficulty }`.
- [x] 1.3 Add a test for `createAiRoom()` in `src/app/core/api/rooms-api.service.spec.ts`: verifies POST path and body shape.

## 2. CreateAiRoomDialog

- [x] 2.1 Create `src/app/pages/lobby/dialogs/create-ai-room-dialog/create-ai-room-dialog.{ts,html}` — standalone, OnPush, mirrors `CreateRoomDialog` patterns. Use ReactiveFormsModule, CDK DialogRef.
- [x] 2.2 Form: `name` control with the same validators as `CreateRoomDialog` (required, minLength(3), maxLength(50), pattern `\S`). Plus `difficulty` control with `Validators.required`, default `'Medium'`.
- [x] 2.3 Template: title, name input + per-error messages (matching `CreateRoomDialog` HTML structure), difficulty button group with `role="radiogroup"` + three `role="radio"` buttons (Easy/Medium/Hard) using `aria-checked`, submit + cancel buttons, error banner row.
- [x] 2.4 Submit logic: validate → set submitting signal → call `rooms.createAiRoom(name.trim(), difficulty)` → on success `dialogRef.close(roomState)` → on `HttpErrorResponse` 400 ProblemDetails map to form via existing `mapProblemDetailsToForm` helper; status 0 → `errors.network` banner; else → `errors.generic` banner.
- [x] 2.5 Cancel: `dialogRef.close(undefined)`.
- [x] 2.6 Spec at `create-ai-room-dialog.spec.ts`: default difficulty Medium; difficulty switch updates outgoing arg; bad name blocks submit; success closes with the RoomState; 400 ProblemDetails maps to field error.

## 3. AiGameCard

- [x] 3.1 Create `src/app/pages/lobby/cards/ai-game/ai-game.{ts,html}` — standalone, OnPush, `:host { display: block }`. Inject `Dialog`, `Router`.
- [x] 3.2 Template: card layout (matches sibling cards visually) with title, short description, primary button "New AI game".
- [x] 3.3 Click handler: `dialog.open<RoomState | undefined>(CreateAiRoomDialog, { ariaLabel: 'New AI game' })`; subscribe to `closed`; on truthy result navigate `router.navigateByUrl('/rooms/' + result.id)`; otherwise no-op.
- [x] 3.4 Spec at `ai-game.spec.ts`: clicking the button calls `dialog.open` with the dialog component; closing with a RoomState navigates; closing with undefined does NOT navigate.

## 4. Lobby integration

- [x] 4.1 Update `src/app/pages/lobby/lobby.ts` imports to include `AiGameCard`.
- [x] 4.2 Update `src/app/pages/lobby/lobby.html` — slot `<app-ai-game-card />` between `MyActiveRoomsCard` and `FindPlayerCard` in the right column.

## 5. i18n

- [x] 5.1 Add `lobby.ai-game.*` subtree to `public/i18n/en.json`: `title`, `description`, `button`, `dialog-title`, `name-label`, `name-placeholder`, `difficulty-label`, `difficulty-easy`, `difficulty-medium`, `difficulty-hard`, `submit`, `submit-loading`, `cancel`, `errors.generic`, `errors.network`.
- [x] 5.2 Mirror in `zh-CN.json` with simplified Chinese values.
- [x] 5.3 Run parity flattener; confirm 0 drift.

## 6. Cross-cutting + final

- [x] 6.1 Grep sweep: no hex/rgb/hsl in new files.
- [x] 6.2 Grep sweep: no CJK in new templates.
- [x] 6.3 No `inject(HttpClient)` outside `core/api/`.
- [x] 6.4 `npm run lint` passes.
- [x] 6.5 `npm run test:ci` passes (new tests included).
- [x] 6.6 `npm run build` passes; lobby chunk grows ≤ 5 KB gzip.
- [x] 6.7 `openspec validate add-web-ai-game` is clean.
