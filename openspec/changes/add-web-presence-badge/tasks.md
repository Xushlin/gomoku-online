## 1. PresenceApiService extension

- [ ] 1.1 Add `UserPresenceWire` interface to `src/app/core/api/models/presence.model.ts`: `{ userId: string; isOnline: boolean }`.
- [ ] 1.2 Add `getUserOnline(userId: string): Observable<boolean>` to abstract `PresenceApiService` and `DefaultPresenceApiService`. Implementation: `http.get<UserPresenceWire>('/api/presence/users/' + encodeURIComponent(userId)).pipe(map(r => r.isOnline))`.
- [ ] 1.3 Spec at `presence-api.service.spec.ts`: GET path encoded; unwraps `isOnline` field.

## 2. ProfilePage presence dot

- [ ] 2.1 Update `src/app/pages/users/profile-page/profile-page.ts` — inject `PresenceApiService`. Add `presence = signal<boolean | null>(null)`.
- [ ] 2.2 In the existing `fetch(id)` method, in addition to the profile fetch, fire `presence.getUserOnline(id)` in parallel; on success set `presence.set(boolean)`; on error set `presence.set(null)` (silent).
- [ ] 2.3 Update `src/app/pages/users/profile-page/profile-page.html` — render a dot before the username `<h1>` only when `presence() !== null`. Use `<span [class.bg-success]="presence()" [class.bg-muted]="!presence()" class="inline-block h-2.5 w-2.5 rounded-full mr-2 align-middle" [attr.aria-label]="(presence() ? 'profile.online' : 'profile.offline') | transloco"></span>`.

## 3. i18n

- [ ] 3.1 Add `profile.online` / `profile.offline` to `public/i18n/en.json`.
- [ ] 3.2 Mirror in `zh-CN.json`.
- [ ] 3.3 Run parity flattener; confirm zero drift.

## 4. Tests

- [ ] 4.1 Update existing `profile-page.spec.ts` `mount()` to provide a stubbed `PresenceApiService`. Add tests:
  - online → dot has `bg-success` class.
  - offline → dot has `bg-muted` class.
  - failure → dot is not in the DOM.

## 5. Final

- [ ] 5.1 `npm run lint` passes.
- [ ] 5.2 `npm run test:ci` passes.
- [ ] 5.3 `npm run build` passes.
- [ ] 5.4 `openspec validate add-web-presence-badge` is clean.
