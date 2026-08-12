## 1. Baseline

- [ ] 1.1 Confirm the working tree is clean apart from this change directory, and record the baseline: `dotnet build backend/Gomoku.slnx` and `dotnet test backend/Gomoku.slnx` both green, plus the test count. The same count MUST hold after the sweep — a dropped test project is the one failure mode a green build would hide.
- [ ] 1.2 Clean stale build output so post-rename failures are never caused by caches keyed on the old assembly names: remove `backend/**/bin`, `backend/**/obj`, `backend/.vs/`, `frontend-web/.angular/cache/`.

## 2. Commit 1 — moves only, no content changes

- [ ] 2.1 `git mv` the four source project directories: `backend/src/Gomoku.{Domain,Application,Infrastructure,Api}` → `backend/src/Gewu.{Domain,Application,Infrastructure,Api}`.
- [ ] 2.2 `git mv` the `.csproj` files inside them to match: `Gewu.Domain.csproj`, `Gewu.Application.csproj`, `Gewu.Infrastructure.csproj`, `Gewu.Api.csproj`.
- [ ] 2.3 `git mv` the two test project directories and their `.csproj` files: `backend/tests/Gomoku.{Domain,Application}.Tests` → `backend/tests/Gewu.{Domain,Application}.Tests`.
- [ ] 2.4 `git mv backend/Gomoku.slnx backend/Gewu.slnx`.
- [ ] 2.5 Commit as `chore(build): move Gomoku.* projects to Gewu.* paths`. Verify with `git show --stat --find-renames` that every entry is detected as a rename (100% similarity) and that no file content changed. The solution does **not** build at this commit — that is expected and is the reason for the split.

## 3. Backend content sweep

- [ ] 3.1 Rewrite `backend/Gewu.slnx`: every `<Project Path="...">` entry points at the new directory and `.csproj` filename.
- [ ] 3.2 Rewrite the six `.csproj` files: `RootNamespace` / `AssemblyName` if declared explicitly, and every `<ProjectReference Include="...">` path.
- [ ] 3.3 Sweep `Gomoku.` → `Gewu.` across all tracked `.cs` files under `backend/src` and `backend/tests` (187 + 68 files). Covers `namespace` declarations, `using` directives, the `using DomainMove = ...` / `using SubMove = ...` aliases in `Room.cs`, `global using` declarations in the test csprojs, and fully-qualified type references in XML doc comments (`<see cref="Gomoku...."/>`).
- [ ] 3.4 Update `backend/smoke/AiSmoke/AiSmoke` project references and any `Gomoku.*` using directives.
- [ ] 3.5 Build: `dotnet build backend/Gewu.slnx`. Fix every compile error before moving on — a stale namespace cannot survive this step.

## 4. DbContext rename

- [ ] 4.1 Rename the type `GomokuDbContext` → `AppDbContext` and `git mv` `Persistence/GomokuDbContext.cs` → `Persistence/AppDbContext.cs`. Update the constructor, the `DbContextOptions<>` generic argument, and the `typeof(...)` in `ApplyConfigurationsFromAssembly`.
- [ ] 4.2 Update every consumer: `Program.cs` DI registration (`AddDbContext<>`), the repositories in `Persistence/Repositories/`, `UnitOfWork.cs`, the 7 `IEntityTypeConfiguration<>` classes if they reference it, and any test fixture that builds an in-memory / SQLite context.
- [ ] 4.3 Rename `Migrations/GomokuDbContextModelSnapshot.cs` → `AppDbContextModelSnapshot.cs`, including the class name and its `[DbContext(typeof(AppDbContext))]` attribute.
- [ ] 4.4 Update the `[DbContext(typeof(...))]` attribute and the `partial class` bodies in all 6 `*.Designer.cs` files. Migration **class names, migration IDs, and `Up`/`Down` bodies MUST remain byte-identical** — verify with `git diff` that the only changed lines in `Migrations/*.cs` are namespace, using, and the context type name.
- [ ] 4.5 Assert no new migration was generated: `Migrations/` still contains exactly the 6 original migrations plus the snapshot, and their filenames/timestamps are unchanged.
- [ ] 4.6 Change the connection string in `appsettings.json` from `Data Source=gomoku.db` to `Data Source=gewu.db` (and `appsettings.Development.json` if it overrides it).
- [ ] 4.7 Build + test: `dotnet build backend/Gewu.slnx` then `dotnet test backend/Gewu.slnx`. Test count MUST equal the 1.1 baseline.

## 5. Web

- [ ] 5.1 `package.json` + `package-lock.json`: `"name": "gomoku-web"` → `"gewu-web"` (the lock file has it in two places — root `name` and `packages[""].name`).
- [ ] 5.2 `angular.json`: project key `gomoku-web` → `gewu-web`, plus the three `buildTarget` references (`gewu-web:build:production`, `gewu-web:build:development` ×2).
- [ ] 5.3 `src/index.html`: `<title>GomokuWeb</title>` → `<title>Gewu</title>`.
- [ ] 5.4 Add `header.brand` to `public/i18n/en.json` (`"Gewu"`) and `public/i18n/zh-CN.json` (`"格物"`), inside the existing `header` object. Keep the two files' key sets identical.
- [ ] 5.5 `src/app/shell/header/header.html`: replace the hardcoded `Gomoku` brand text with `{{ 'header.brand' | transloco }}`, keeping `routerLink="/home"` and the existing classes. Confirm `TranslocoPipe` is already imported by `header.ts`; add it if not.
- [ ] 5.6 Update the `Gomoku.Application.*` references in the doc comments of `core/api/models/{leaderboard,room,user-profile}.model.ts` to `Gewu.Application.*`.
- [ ] 5.7 Update the `Gomoku board` / `gomoku boards` comments in `src/styles/global.css` — these describe the 15×15 gomoku board and remain accurate; reword only if they claim to be about the app rather than the game.
- [ ] 5.8 Add a `header.spec.ts` covering the two brand scenarios: with the active language stubbed to `zh-CN` the brand renders 「格物」, with `en` it renders "Gewu". Follow the existing spec style in `frontend-web/src/app` (TestBed + the project's transloco test setup).
- [ ] 5.9 Verify: `npm run lint`, `npx ng test --watch=false`, `npx ng build` all green.

## 6. Docs, tooling, CI

- [ ] 6.1 `.github/workflows/ci.yml`: the three `dotnet` steps referencing `Gomoku.slnx` → `Gewu.slnx`.
- [ ] 6.2 `start-dev.cmd`: 5 occurrences (project paths, solution name).
- [ ] 6.3 `CLAUDE.md`: 20 occurrences — project names, the solution filename, the `dotnet ef` command block's `--project` / `--startup-project` paths, and the "What this project is" section, which MUST now describe 格物 as a multi-game platform whose first game is gomoku rather than a gomoku-only project.
- [ ] 6.4 `README.md` (16) and `README.zh-CN.md` (15): same treatment, keeping the two in sync.
- [ ] 6.5 `.github/` PR + issue templates: any `Gomoku.*` path references.
- [ ] 6.6 Confirm `.gitignore` still covers the renamed DB (`*.db` is a glob, so `gewu.db` is already ignored — verify, do not assume).

## 7. Spec tree identifier sweep

- [ ] 7.1 Apply the four permitted substitutions across `openspec/specs/**/spec.md` only: `Gomoku.` → `Gewu.`, `GomokuDbContext` → `AppDbContext`, `Gomoku.slnx` → `Gewu.slnx`, `gomoku-web` → `gewu-web`. Nothing else in the spec tree may change.
- [ ] 7.2 Assert the deliberately-untouched contracts still read the old names, proving no contract moved: `grep -rn "hubs/gomoku" openspec/specs/` still returns hits in `api-ops`, `observability`, `room-and-gameplay`, `web-game-board`; `grep -rn "gomoku:" openspec/specs/` still returns hits in the five web specs; `AddBotSupport` / `AddHardBotAccount` / `AddGameEndReason` still appear in `ai-opponent` and `room-and-gameplay`; the `openspec/specs/gomoku-domain/` folder name is unchanged.
- [ ] 7.3 Spot-check the reversal property from design D4 on two files: reversing the four substitutions reproduces the pre-change content exactly.

## 8. Final verification

- [ ] 8.1 Full-tree audit: `grep -rni "gomoku" .` excluding `node_modules`, `bin`, `obj`, `.vs`, `.angular`, `package-lock.json`, and `openspec/changes/archive/`. Every surviving hit MUST be on the allow-list — `/hubs/gomoku` and `GomokuHub`, the `gomoku:*` localStorage keys, the three migration names, `openspec/specs/gomoku-domain/`, `frontend-web/src/app/games`-bound gomoku game code and its `Gomoku*` type names, prose that legitimately discusses the game 五子棋, and this change directory. Anything else is a miss.
- [ ] 8.2 Clean-tree CI parity locally: `dotnet build backend/Gewu.slnx --configuration Release` → `dotnet test backend/Gewu.slnx --configuration Release` → `npm ci` → `npm run lint` → `npx ng test --watch=false` → `npx ng build`.
- [ ] 8.3 Delete the local `backend/src/Gewu.Api/gomoku.db*` (leaving the original untouched elsewhere is unnecessary — it is throwaway test data), start the API once, and confirm: migrations apply from empty, and the `Users` table contains the three bot accounts with the GUIDs ending `ea51`, `bed10`, `00ad`.
- [ ] 8.4 Manual contract smoke, proving zero behaviour change: register/login, create a room, play one move over the hub at the **unchanged** `/hubs/gomoku`, send a chat message. Confirm the browser session survives a reload (localStorage keys deliberately unchanged).
- [ ] 8.5 Commit 2 as `refactor(build)!: rename Gomoku.* to Gewu.*, GomokuDbContext to AppDbContext` with a `BREAKING CHANGE:` footer noting the solution filename and DB filename changes for local developers.
- [ ] 8.6 Record the two deferred follow-ups so they are not lost: `squash-migration-baseline` and the `gomoku:*` → `gewu:*` localStorage rename with its read-old/write-new shim.
