## Context

The backend is a 4-project Clean Architecture solution where all 187 source files and all 68 test files sit under `Gomoku.*` namespaces. The web app is an Angular project named `gomoku-web`. Both are about to host six more games that have nothing to do with gomoku, so the platform is being renamed to 格物 / `Gewu` before any platform work starts.

Constraints that shape the design:

- **No deployed instance.** No deploy workflow, no Dockerfile, no `appsettings.Production.json`; the only database is the gitignored local `backend/src/Gomoku.Api/gomoku.db`. Nothing external is pinned to any identifier, which is why a wholesale rename is even on the table.
- **OpenSpec is mandatory** and the live spec tree is the source of truth. Twelve spec files name `Gomoku.*` types and paths in their prose, which forces a decision about how spec text gets updated (see Decisions).
- **PRs target ≤400 lines of net change.** This change cannot honour that — the sweep touches 255 files. The mitigation is that the diff is mechanical and reviewable by pattern rather than by line (see Decisions → commit split).
- **CI is the proof.** `dotnet build` + `dotnet test` + `npm run lint` + `ng test` + `ng build` must be green, and because this change alters no behaviour, green CI is a near-complete correctness argument. A missed namespace is a compile error, not a latent runtime bug.

## Goals / Non-Goals

**Goals:**

- Every assembly, namespace, project, and build identifier reads `Gewu` instead of `Gomoku`.
- Zero observable behaviour change: identical REST routes, hub URL and method names, JSON shapes, localStorage keys, JWT claims, migration IDs, and DB schema.
- No new EF migration, and `__EFMigrationsHistory` remains valid against the existing schema.
- The `DbContext` gets a name that never needs renaming again.
- Later phases author all new code under the final names.

**Non-Goals:**

- Renaming the hub (`GomokuHub`, `/hubs/gomoku`) — deferred to `generalize-match-contract`.
- Renaming `gomoku:*` localStorage keys — deferred to a change that carries a migration shim.
- Squashing migrations — deferred to `squash-migration-baseline`.
- Restructuring `Domain/` into `Domain/Games/<key>/` — deferred to `generalize-match-domain`.
- Renaming the repo directory on disk, or the GitHub repo itself.
- Any behaviour, feature, dependency, or performance change whatsoever.

## Decisions

### D1: `AppDbContext`, not `GewuDbContext`

The context class name is baked into all 6 migrations, their 6 `.Designer.cs` files, and the model snapshot. Naming it after the platform means a future rename drags migration history along a second time. A platform-neutral `AppDbContext` decouples the two forever, and the assembly namespace (`Gewu.Infrastructure.Persistence`) already supplies the branding.

*Alternative considered:* `GewuDbContext`, for symmetry with the other renames. Rejected — symmetry is worth less than never touching migration designers again.

### D2: Rename the namespace root only; keep the internal folder structure

`Gomoku.Domain.Rooms` becomes `Gewu.Domain.Rooms`, not `Gewu.Domain.Games.Gomoku.Rooms`. The restructuring into per-game folders is real work that depends on the `IGameRules` abstraction, and doing it here would mean the same files get new namespaces twice, in two changes, for two different reasons.

*Alternative considered:* rename and restructure in one pass, to touch each file once. Rejected — it would make a mechanical change into a judgement-heavy one and destroy the "green CI is the proof" property.

### D3: Migrations are renamed at the namespace level only

Migration class names, migration IDs, and every `Up`/`Down` body stay byte-identical apart from `namespace` / `using` lines and the `GomokuDbContext` → `AppDbContext` type reference in the `[DbContext(typeof(...))]` attribute and designer partials. No new migration is generated and `dotnet ef migrations add` is never run.

This is what keeps the existing local database usable and — more importantly — keeps three normative requirements intact: `ai-opponent` requires an `AddBotSupport` migration seeding the Easy/Medium bot accounts and an `AddHardBotAccount` migration seeding the Hard one, and `room-and-gameplay` requires the `AddGameEndReason` backfill. Squashing would rewrite all three and would need to prove the three bot GUIDs survive — a different proof obligation, hence a separate change.

### D4: Spec-tree identifier sweep is applied directly, not through delta files

Twelve live spec files reference `Gomoku.*` namespaces, project paths, or `GomokuDbContext` inside requirement prose. Producing twelve `MODIFIED Requirements` delta files — each of which must reproduce the entire requirement block verbatim per the OpenSpec workflow — would generate hundreds of lines of near-identical text and bury the actual change. So the rename is applied straight to `openspec/specs/**/spec.md`.

This is a deliberate, bounded deviation from the delta flow, and it is only defensible because of a property that can be checked mechanically rather than argued: **the sweep is a pure identifier substitution.** The guard rails are

1. the only substitutions permitted in `openspec/specs/` are `Gomoku.` → `Gewu.`, `GomokuDbContext` → `AppDbContext`, `Gomoku.slnx` → `Gewu.slnx`, and `gomoku-web` → `gewu-web`;
2. a reviewer can verify this by confirming that reversing those four substitutions reproduces the pre-change files exactly;
3. `/hubs/gomoku`, `gomoku:*` storage keys, and all migration names MUST still appear unchanged in the spec tree afterwards — their survival is the evidence that no contract was quietly moved. A task asserts this with a grep.

*Alternative considered:* leave the spec tree stale and clean it up later in a `fix-spec-gewu-drift` change. Rejected — it would leave the source of truth describing files that no longer exist for the entire duration of phase 1, which is exactly the drift the workflow exists to prevent.

### D5: Two commits — moves first, then content

Git detects renames by content similarity rather than recording them. Renaming a directory *and* rewriting lines inside its files in a single commit defeats that detection for smaller files, producing a diff that reads as "255 files deleted, 255 files added". So:

1. **Commit 1** — `git mv` only: project directories, `.csproj` filenames, `.slnx`. No file contents change, so rename detection is perfect and the diff is a clean list of moves.
2. **Commit 2** — content sweep: namespaces, `AppDbContext`, project references, build identifiers, docs, spec text.

The branch is squash-merged per project convention, so this costs nothing at merge time but makes the PR reviewable — the reviewer reads commit 1 as a file listing and commit 2 as a substitution pattern.

*Alternative considered:* one commit. Rejected on reviewability alone.

### D6: Scripted sweep, not IDE refactor

The namespace rewrite is done with an explicit, re-runnable text substitution over tracked files (`git ls-files` piped through the substitution), not with a Visual Studio / Rider "rename namespace" refactor. Reasons: the IDE will not touch `.md`, `.json`, `.cmd`, `.yml`, or the spec tree; IDE refactors silently reformat unrelated code; and a scripted sweep is auditable and repeatable if the branch needs rebasing. Build and test output remain the correctness gate either way.

`bin/`, `obj/`, `backend/.vs/`, and `frontend-web/.angular/cache/` MUST be excluded (they are gitignored, but stale caches keyed on the old assembly names cause confusing local failures — the tasks include a clean step).

### D7: The local dev DB is recreated, not migrated

The connection string filename changes, so the app looks for `gewu.db` and finds nothing on first run. Options were: rename the file on disk, or let migrations recreate it. Either is fine and neither is a code concern — the local data is throwaway test rooms. The tasks note both, defaulting to "delete and let it recreate", which also proves the migration chain still runs end to end from empty. The old `gomoku.db` is left on disk untouched, which doubles as the rollback path.

## Risks / Trade-offs

- **[A missed identifier ships silently]** → Cannot happen in C#: a stale `Gomoku.*` namespace or `using` fails compilation. The real exposure is in *non-compiled* text — docs, i18n JSON, spec prose, `.yml`, `.cmd`. Mitigation: a final task greps the whole tree for `Gomoku`/`gomoku` case-insensitively and requires every surviving hit to be on the explicit allow-list (the hub URL, the `gomoku:*` storage keys, migration names, the `gomoku-domain` spec folder name, and prose that legitimately discusses the game 五子棋).
- **[The `gomoku-domain` spec name looks inconsistent afterwards]** → Accepted, and correct: gomoku is still a game in the platform, and its domain spec is legitimately named after it. Renaming it would imply the spec covers the whole platform, which it does not.
- **[`GomokuHub` sitting inside `Gewu.Api.Hubs` reads oddly]** → Accepted for one phase. It is honest — the hub genuinely only serves gomoku until `generalize-match-contract` lands.
- **[255-file diff exceeds the 400-line PR guideline]** → Unavoidable for an atomic rename; splitting by project would leave the solution non-compiling between PRs. Mitigation is D5's commit split plus the substitution-reversal check from D4.
- **[Stale build caches produce failures unrelated to the change]** → Clean `bin`/`obj`/`.vs`/`.angular/cache` before the verification build; CI is unaffected since it builds from a clean checkout.
- **[Rebase pain if another branch is opened mid-change]** → Land this change first and alone. It is the reason this is sequenced before every other platform change.

## Migration Plan

1. Land this change alone, on a clean tree, with no other branches open.
2. Verify locally: clean caches → `dotnet build Gewu.slnx` → `dotnet test Gewu.slnx` → `npm ci && npm run lint && npx ng test --watch=false && npx ng build`.
3. Delete the local `gomoku.db` (or rename to `gewu.db`) and start the API once to confirm migrations apply from empty and the three bot accounts are seeded.
4. Smoke the unchanged contracts by hand: log in, create a room, play a move over the hub, confirm the hub still answers at `/hubs/gomoku` and the browser session survives (localStorage keys deliberately unchanged).
5. Merge. **Then**, outside git: rename the working directory and the GitHub repo, and re-create worktrees.

**Rollback:** revert the squash commit. Nothing external changed — no schema migration ran, no contract moved, and the original `gomoku.db` is still on disk, so a revert restores the previous state exactly.

## Open Questions

- **GitHub repo / domain availability.** `gewu` as a repo name and the `gewu.*` domains have not been checked, nor has trademark class 41. This does not block the code rename (nothing here depends on an external name being free) but should be resolved before the name goes public.
- **English display name.** `zh-CN` is 格物; `en` currently gets the pinyin `Gewu`. An English tagline or a different English brand can be swapped later — it is one i18n value, not a code concern.
