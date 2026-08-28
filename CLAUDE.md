# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What this project is

**格物 / Gewu** — a multi-platform online game hall. Planned games: idiom games (成语纵横 / 成语接龙 / 猜成语), 五子棋, 一字棋, 中国象棋, 华容道, 俄罗斯方块. 「格」 means grid cell, which is what they all have in common.

**Nine games ship.** They fall into three categories that deliberately do **not** share one aggregate:

| Category | Games | Realtime | Core concepts |
| --- | --- | --- | --- |
| Turn-based adversarial | 五子棋, 一字棋, 中国象棋, 成语接龙, 斗地主, 挖坑 | SignalR | room, N seats, turn order, move sequence, ELO, spectators, replay |
| ↳ what the card games add | 斗地主, 挖坑 | — | **hidden per-seat state**, a **server-only setup** (the deal), **three** seats, a first mover decided by the deal, and a settlement in points rather than ELO |
| Single-player levels | 成语纵横, 华容道, 猜成语 (planned) | none (REST) | level catalogue, progress, stars, hints, time leaderboard |
| Single-player score-attack | 俄罗斯方块 | none (submit at end) | run record, score validation, periodic leaderboard |

**Three kernels, and each has three authority models to keep straight** — this is the one piece of history worth carrying eagerly, because getting it wrong designs the next game wrong:

- **match** (`Room` / `Game` / `IGameRules`) — the server owns legality. Proven general by 中国象棋 (`from → to`, 10×9) and 成语接龙 (no board at all), not by 一字棋, which is gomoku in miniature. **A game's opening setup has exactly two sources, and they are parallel, not optional:** `IDealtGameRules` (the rules *make* it from a seed — the card deals) and `IPositionalStartRules` (the caller *chooses* it and the rules *validate* it — 象棋残局). `Room` asks "is it one of these two", and **throws in both directions**; making setup merely optional would delete both checks at once. That is also why 残局 is its own game key rather than a flag on 象棋: a key that *sometimes* wants a setup breaks the invariant, and `IDealtGameRules` own doc calls a seed-ignoring `CreateSetup` "the kind of dishonest implementation the next person cannot delete".
- **puzzle** (`PuzzleLevel` / `IPuzzleRules`) — authoritative two different ways: 成语纵横 *withholds* the answer, 华容道 *replays* every claimed move. Same platform rule, opposite mechanism.
- **score-attack** (`ScoreRun`) — replays **placements, not keystrokes**. No registry behind it, because *a switch with one arm is a switch*.

Everything else about how each game landed — including which client judges its own rules and why the four answers differ — is in [`JOURNAL.md`](JOURNAL.md).

## Current phase

**Nine games ship** — 五子棋, 成语纵横, 一字棋, 中国象棋, 华容道, 成语接龙, 俄罗斯方块, 斗地主, 挖坑. The archived changes are the directories in `openspec/changes/archive/` — **count them there, do not trust a number written here**: the one that used to sit in this sentence was off by one on the day it was written and had drifted +2 ten commits later. The three kernels are built and each has been proven by a second game that was not a variant of the first.

### Where the history lives

Everything about *how it got here* is in **[`JOURNAL.md`](JOURNAL.md)** — one entry per change, in merge order, recording what it cost and what turned out to be false. Read it through **`.claude/skills/gewu-history/SKILL.md`**, which indexes it by game and by kernel seam and loads on demand.

It is not in this file because this file is loaded in full on every session whatever the task. The journal was 89% of it (~50k tokens), so a one-line i18n fix paid the same price as adding a tenth game — and the attention went the wrong way: every change appended a journal entry while the guidance below quietly went stale. **The part loaded unconditionally is the part that must not rot.**

The two lists below stayed here on purpose. Their value is that they arrive **before you ask**; a lazily-loaded trigger only fires if you already suspected it.

### Deferred, with triggers

Each of these was decided, written down, and left. **A deferral that names its own trigger is the good kind** — three of them have since fired on schedule.

| Deferred | Trigger |
| --- | --- |
| Renaming `SayWord` — it is in fact the generic text-payload path (the server only builds `MakeMoveCommand(Text:)` and never reads the game key), but the name was coined for 成语接龙 and now misleads in three places. | The day a **third** text-payload game lands. |
| `WakengScoring.Settle` / `DoudizhuScoring.Settle` have **no production caller**, and the same gap makes **认输 impossible in a three-seat game**: `Room.Resign` needs exactly two seats to name a winner, so the client hides the button and the API answers 409. **This trigger was written inside the `Room.Resign` requirement itself and nobody read it** — it said 「拆除条件：第一个 `SeatCount != 2` 的棋种落地」, 斗地主 and 挖坑 landed, and the cost was a **500** on a real click. | The platform needs a **points ladder** — then 认输 in a three-seat game means forfeiting on points. |
| The per-seat broadcast fan-out has no end-to-end test — projection is unit-tested and the group function is exhaustive by construction, but "three real SignalR connections each receive only their own" is unasserted, so a typo in `ViewGroupName` turns nothing red. | A `Gewu.Api.Tests` project exists. |
| `AddGameSetup`'s `Down` drops the column, and 斗地主 + 挖坑 now write it. A merged migration is not edited, so the bill is **one new guarded migration** — the cost is per *column*, not per game. `GameSetupMigrationTests` is the list of keys whose data it must carry back. | Anyone needs to roll back past it. |
| `squash-migration-baseline` — **measured and declined**, so do not re-litigate: the 14 migrations are 400 lines applying in 259 ms, and squashing deletes the 16 tests that stop at a *named intermediate* migration, which is the only point where "did the data move correctly" is observable. | A provider change, ~100 migrations, or an actual deployment. |
| `--radius-pill` was proposed and **not added** — the class-attribute walk found zero call sites, and a token with no call site is a dead entry every theme pays for. | The first control that genuinely needs a fully rounded shape. |
| The bundle budget is **480 kB** with **69.8 kB free** (initial total **410.19 kB**). Fired **six** times, never raised — once *lowered*. `lazy-header-menu` took the answer this table had been holding for the *seventh* firing — `@angular/cdk`, **75.21 kB** — early, because 2.2 kB of headroom is not headroom. The named next answer is **every game's board CSS is eager**: `board-skins.css` + `global.css` are two of the four entries in `angular.json`'s `styles`, and stubbing both drops the initial bundle to **390.17 kB**. So it is worth **at most 18.55 kB**, paid by everyone including people who never open a game. | When it fires: ask **what is eager that need not be**, and **measure by stubbing** — the answers so far were concrete things, not across-the-board shaving. **The stub number is the best case, not the expected one**: stubbing predicted 396.42 and the real split landed at 402.62, and a proposal that turned that figure into a target shipped a `MUST < 400 kB` **no test could ever fail on**. |
| **「古谱的走法合法性」欠账还了一半,而剩下那一半是数据,不是能力。** `play-from-position` 之后残局能从给定局面开局,于是**量过了全量**:1665 条走强路径,**1658 条每一半手都合法,7 条被拒**,七条理由完全相同 —— 那一手把自己的将送进被将的位置。看过其中一条(`077蛛网空悬` 第 3 手 帥 (9,4)→(9,3),黑卒就在 (8,3) 上),**是数据错,不是规则严**。播种器仍走两条路(188 强 / 1477 结构),因为直接换会在第一部谱上抛,而报出来的样子是「产物坏了」。`EndgameStrongPathTests` 钉着「恰好 7 条、且每条都是自杀」。 | 那 7 条被决定掉(修、丢、还是单独隔开)。届时把播种器换成单一强路径,并把 `xiangqi-manual` 那条要求一并收紧。 |
| **古谱**研习**页里没有任何「你解对了」,而残局房写明平台不判和棋。** 不是工期,而**这一行的理由已经被自己的一次落地推翻过一次,值得看清楚**:它原文写的是「领域里**没有重复局面 / 长将 / 长捉规则**」,而 `limit-repeated-checks` 之后领域里**有**重复局面计数、也有一条长将规则 —— 那句理由从此是错的,**结论却仍然对**。现在的理由:那条规则只用来**拒绝一步棋**,不用来**宣布**一个结果;判和还差两样,一是规则(长捉 / 长拦,以及「重复到第几次算**和**」——那是与「第几次不许再走」不同的判据),二是一个决定。而**和棋的定义就在那里**(六辑 1634 局里和棋 391 局)。「摆此局对弈」已经落地,所以那条源码规则**收窄了一半**:禁「解对了」还在(而且多了一条**正面**规则 —— 残局房必须写明平台不判和棋),禁「接着自己走」拆了 —— 那一半守的曾是「AI 从残局出发会按标准开局重建棋盘」,而人人对弈一步 AI 都不走。「和机器对弈」现在由 `IBoardGameAi.SelectMove` 的签名钉着(只收走子历史,所以残局那个键注册不出 AI,建 AI 房被 `MustHaveAnAi` 拒掉)—— **一条源码规则守不住的东西,让类型去守。** | **长捉 / 长拦规则 + 一个「重复到第几次算和」的判据落地** → 平台才能宣布和棋。(此前这里写的是「长将 / 重复局面规则落地」,而那**已经落地了**,和棋并没有因此变成可能 —— 一个写得太宽的触发条件会在自己被满足时**看起来**该开工。)AI 从给定局面走棋是另一笔账,而它已经不靠源码规则拦着了。 |

Open questions waiting on the user: 红桃四 and 三带 rules (links promised). Not yet done in a browser: **the lobby's seat row** — and that is now the only one. It is covered by a test using the **real** `DefaultGameCapabilitiesService` with only the HTTP boundary stubbed, which catches a missing `ensureLoaded()` that seven stub-based tests do not; **an approximation, not a substitute.** 斗地主's play-assist, the action bar (44 px measured, 认输 clicked) and the leave guard were all driven in a real game by `fix-three-seat-resign` — **and that trip found two defects**, which is what the row above was for. `lazy-header-menu` then found a third that way, and it was the **only** way it could have been found (see the timing trap below). The harness that made it cheap: put the refresh token in `localStorage['gewu:refresh']` to land authenticated, and raise `Game__TurnTimeoutSeconds` so the game does not end while you take a few round trips — **its ceiling is 3600**, and `Jwt__SigningKey` must be **base64** (both are validated at startup, so both cost a restart to discover). **A long move history is cheapest to build outside the browser:** moves only exist on the hub (there is no REST move endpoint), and a Node script requiring `@microsoft/signalr` with two tokens plays 22 plies in one call — then the browser only has to click the *last* one. Require the **CJS** build (`createRequire`); the ESM build's extensionless imports do not resolve in Node, and a `file:///D:/…` URL is needed because a leading-slash path resolves against the current drive. **Three things that cost a round trip each and will again:** refresh tokens **rotate**, so a token the app has already spent lands you on `/login` — mint a fresh one per user per switch; a CJK request body typed into this shell is **not valid UTF-8** (the console is GBK), so `curl` it from a file with `--data-binary @file`; and at 375 px the header collapses the username into a 「设置」 menu, so "who am I logged in as" is not in the page text — read the board's own state instead. **And the pane's viewport is not always dead:** `add-xiangqi-manual` measured `innerWidth: 0` and had to defer its 375 px reading, while `add-xiangqi-endgames` got 297 and then a real 375 after `resize_window`; `play-from-position` got 0 and then a real 375 the same way, and drove a whole two-player endgame there (both seats, an accepted move, a rejected move, resign) — so **read `innerWidth` before concluding you cannot measure**, and reload at the target width because `transform` does not follow a live resize. Screenshots still need a **displayed** pane: it was hidden throughout, so `screenshot` timed out while every layout read stayed valid.

### Traps this project keeps re-learning

The *generic* traps — a pipe eating the exit code, a mutation that fails to build, an assertion green on an empty collection — are the same in every repo and are not repeated here; keep them in your own global guidance. These are the ones specific to **this** codebase:

- **A hand-written list posing as a registry.** Fixed **six** times in shipped code — and it appeared a **seventh** time in `restyle-klotski-board`, in the *new check script written to enforce this very rule*, as `['boss','general','guard','soldier']`; it was caught before merge only because the rule was being re-read. It now derives from the `KlotskiRole` union. (`add-xiangqi`, `enforce-human-vs-human`, `enforce-ai-availability`, the sound-pack list twice, and an exported "emblem shape kinds" list whose comment claimed it was derived from the mapper it was typed beside) and it recurred *seven lines below* a fix. **And once the rule was quoted in a commit message while its sibling sat unfixed one directory over:** `drop-theme-token-mirrors` deleted the per-theme mirrors and did not grep for `core/theme/skins/`, which had the identical `register(name, tokens)` + `validate()` shape. A bundle attribution table found it, not the rule. **The eighth** was `hub-error.mapper.spec.ts`'s "every code has copy in both locales" walk: it iterated a *hand-typed* copy of the code list that was **missing all three 接龙 codes**, so `idiom-not-found` having copy had never once been checked — and the walk's output was identical either way. It now derives from `HUB_ERROR_CODES`; the positive control is deleting one locale's copy and watching it go red. Every "walk every game / pack / skin / code" test must derive its data from the production list — `BuiltInGameRules.All`, `BuiltInGameAis.All`, `PACK_LOADERS`, `SOUND_EVENTS`, `HUB_ERROR_CODES`. If you fix one, **grep for the siblings**; "I just fixed this class of problem" is a reason to look, not to relax. And note *where* the eighth was found: not by grepping for the class, but by **adding one entry and asking what covers it** — a new row is the cheapest probe there is for whether the guard beside it is real.
- **A one-sided walk asserts nothing.** A registry walk needs both outcomes present in the sample (一字棋 is the only unrated versus game, and it is what keeps several walks from degenerating into empty loops). Prefer "exactly one" over "at least one" — "exactly" goes red when the second case lands, which is when to ask whether the two needs are really the same thing.
- **`--no-build` measures whatever is on disk.** A *failed* build followed by a passing `--no-build` run looks identical to two successes; `shutil.copy2` preserving mtime makes MSBuild compile nothing and report 0 errors. Hit three times. When mutation-testing, force a rebuild and check the file count.
- **`npx tsc -p tsconfig.json --noEmit` type-checks *zero* app files here, and exits 0.** The root config is solution-style; the app's files come in through `tsconfig.app.json` / the Angular builder. `--listFiles | grep -c src/app` returned **0** — a positive control caught a "types are clean" claim that had measured nothing. The real type check is `ng build` / `ng test` / `ng lint`; do not substitute a bare `tsc`.
- **`GET /api/users/{id}` is not where per-game record lives.** It returns a single legacy aggregate that stayed at `rating=1200, games=0` even after a **rated** xiangqi game settled — so reading it to prove "this game did not enter the ladder" proves nothing. Per-`(user, gameKey)` numbers come out of `GET /api/leaderboard?gameKey=…` (authenticated), where the same rated game showed 1220 / 1180 and the unrated endgame showed **zero rows**. The positive control is the whole test here: **a negative assertion against an endpoint that never moves is green for the wrong reason.**
- **Clicking your own piece re-selects it; it never becomes a move.** So "move onto a square my own piece occupies" cannot be used to test a *server* rejection in the browser — nothing is sent (`xiangqi-board` line ~229). A browser test of an illegal move needs a destination that is **empty or enemy**, which is also the only way to know the client did not quietly block it: the board judges no legality itself (design D2).
- **A mutation that fails to build is not a mutation.** `@if (false)` in a template and an exception-throwing stub both produce "exit 1 with no test run", which reads as a kill. Hit **three** times — the third was `add-game-action-bar`, in a mutation list where the other five were valid, so "five red and one weird" is what it looks like from the outside.
- **An empty collection passes every layout assertion.** 375 px checks must run with the **longest real content** on screen — the dictionary's 15-character idiom, a 20-character username (that is the registration cap, so a longer invented string is not honest), 19 cards, 44 blocks. Three of the four overflow defects were invisible on empty data.
- **shrink-to-fit vs. the card table's fan formula.** `100%` inside `transform: translate()` resolves against the *element*, not the container, so the fan's step silently computes to 0 or goes negative. Hit **four** times. `frontend-web/scripts/check-styles.mjs` pins the invariants by filename — it runs under `npm run lint`, not vitest, and a file move will (correctly) break it.
- **For text, the box you can query is not the ink you can see.** `getBBox()` on an SVG `<text>` returns the **line box**: for CJK, width is exactly the font-size but height is about **1.45×** it, most of the extra being ascender space no glyph fills. Sizing a glyph by its width says "it fits" when it does not; judging it by `getBBox` produces a *false failure* on the top edge. The measurement that answers the question is drawing the SVG into a canvas and **sampling ink pixels**. (`dominant-baseline: central` does centre the ink to within half a unit — it is the box that is lopsided, so no `dy` correction is needed.)
- **A stroked container's usable interior is `radius − strokeWidth / 2`.** Got this wrong three times in a row on the same emblem: 象棋's inner ring is r=7 with a 1.6 stroke, so ink has **6.2** to live in, and 帥 at font-size 9.5 has a half-diagonal of 6.79 — sitting exactly on the ring, which is what the screenshot showed. **The first positive control used r=7 and therefore passed the broken version**; a check whose boundary is wrong quietly admits the thing it exists to reject, and its output is identical to a check that works.
- **Same-colour on same-colour is invisible and silent.** A `currentColor` glyph drawn over a `currentColor` filled shape throws nothing, fails nothing and renders nothing. 猜成语's `?` sat on a filled cell.
- **A colour is not a valid `background-image`, and the failure is silent.** 华容道's skin faces are gradients under `wood`/`midnight` but a `color-mix()` under the theme-following skin; assigning that to `background-image` computes to **`none`**, so the pieces had no fill at all under that one skin. CSS throws nothing, jsdom does not compute backgrounds, and the style checker read the *token's* value rather than whether the receiving property accepts it. **The `background` shorthand takes both.** Only a browser could see this — it joins the same-colour-on-same-colour family.
- **An element cannot use its own container query — units or `@container`.** Both resolve against the *nearest ancestor* container, so `@container (min-width: …)` written to widen the very element that declares `container-type` never matches and is dead code, and a `cqw` in that element's own padding is not what you think. That is why the klotski board and its coordinate system are two nested elements: the wrapper is load-bearing, not decoration.
- **In the Browser pane, layout updates but `transform` does not.** Finer than the composite trap above: after changing a container's width from JS, the pieces' **widths** followed the new cell size while their **positions stayed on the old one** — which reads exactly like a real horizontal overflow. Anything positioned by `transform` must be measured **after a fresh render** (reload at the target width), and `getComputedStyle(...).transform` is not trustworthy there at all — compare `getBoundingClientRect()` against the coordinates instead.
- **EF's generated migration was wrong in `Up`, and this is the fifth time its data movement has cost something.** `RenameColumn` carries the old *values* into the new column, so renaming a seat number (`0` = red) into a differently-numbered enum (`Unrecorded = 0`, `RedBetter = 1`) silently relabels every existing row — 15 "red wins" became "not recorded". It also filled a required fixed-length column with `""`. The previous four were all in `Down`; **`Up` is the one you discover in production**. Hand-write the `UPDATE`s, and mind their order: remapping `1 → 2` then `0 → 1` works, the other order moves the same rows twice.
- **A convention derived from a small sample, written as a MUST, rejects valid data — and it happened twice running.** `add-xiangqi-manual` wrote "the last half-move must end the game" from 31 records (20 of them do not); `add-xiangqi-endgames` then wrote "red moves first" from a 30-record sample, and the full 1634 have **7 that open with Black**. Both would have refused legitimate records, and **the refusal reads exactly like "the data is broken"**. Before a sample-derived rule becomes a MUST, run the whole set — and when the type itself cannot hold what the data says (a *seat number* cannot express a draw, and 391 records are draws), the fix is the type, not another state.
- **A duplicated label is invisible to a substring assertion.** The verdict row rendered "The manual judges: **Manual:** Red better" because the label and the value both carried the prefix. Every unit test was green: they assert `toContain('谱评:黑优')`, and that substring is present in the doubled string. Only reading the whole sentence out of the page showed it. **Copy that composes from two pieces needs one assertion on the composed line**, not on each piece.
- **A signal → input → child loop looks stuck after exactly one step when the pane is not compositing.** The shared move scrubber emits `seek(n+1)`; the page writes the ply signal; Angular must run change detection to push the new *input* back into the child before it can emit `n+2`. Zoneless CD does not run in a hidden pane, so autoplay advanced to `1 / 46` and stopped — **which reads exactly like "autoplay is broken"**. Forcing `window.ng.applyChanges(...)` on a timer during the wait showed 0→1→2→3→4→5. Any parent-owned-state / child-emits-intent loop needs a CD pass per step; measure it by driving CD, not by watching.
- **`expect(undefined).toContain(x)` does not fail, and it has now bitten twice in the same file.** `app.routes.spec.ts` documented it for `canDeactivate` and then had the identical hole one test above it: `expect(route.canMatch).toContain(authGuard)` was green for two routes that had no `canMatch` at all. Both are now "count the offenders, compare to `[]`", and the deliberate exemption is an explicit `data.publicContent` flag whose list is asserted with **exactly**, not `toContain`. **Fixing one instance of this shape is a reason to grep for its siblings in the same file.**
- **A test stub that leaves the page in its loading state renders nothing, which looks exactly like the feature being absent.** A new lobby assertion failed because `StubGameCapabilities.sized(...)` leaves `loaded()` false, so the whole card column was the skeleton — the AI card was missing too, which is what gave it away. `rated(...)` is the stub that gets past the gate. When a "the element is not there" failure surprises you, check whether *anything* is there.
- **jsdom has no event loop the way a browser has one, so interaction *timing* bugs are green there.** `lazy-header-menu` restored the pending menu by calling `CdkMenuTrigger.open()` synchronously once the deferred chunk arrived: unit tests green, and in a real browser the menu never appeared. The callback *did* run (a probe confirmed the index and the trigger count) — but **the click that started all this was still bubbling**, and the overlay CDK had just opened subscribed to document's close-on-outside-click, which caught the tail of that same event. One `setTimeout` fixed it. Anything whose correctness depends on *when* it runs relative to a real event's propagation is not covered by Vitest, whatever it says.
- **The Browser pane does not composite when it is not displayed.** `document.timeline.currentTime` freezes at 0, zoneless change detection and `effect()` never run, so every DOM read after an interaction is stale. Layout metrics (`scrollWidth`/`clientWidth`) *are* still valid. `window.ng.applyChanges(window.ng.getComponent(el))` forces a pass and makes effect-driven behaviour testable there.
- **A toast that dismisses itself is unreadable there, and it reads exactly like「没有 toast」.** `flash()` sets a signal and schedules `set(null)` 3 s later. The **timer still runs** in a hidden pane while CD does not — so by the time you force a pass the value is gone, and every DOM probe comes back empty. Read the **signal** (`ng.getComponent(page).errorToastKey()`), or pin it open by wrapping `page.schedule` — and note the key is **`flash-<value>`**, not a fixed `'error-toast'`; filtering the wrong name looks identical to not patching at all. Two more things this cost: `delete signal.set` **destroys** the signal (it is an own property with no prototype fallback, so `flashError` then throws and nothing appears at all — that one needs a reload), and a probe whose regex was `/check|将|repeat/` matched the **room name** `repeat-v1` and broke its own wait loop after 250 ms. **A probe can be wrong in the shape of an answer.**
- **A trigger written inside a spec requirement is a trigger nobody reads.** `Room.Resign` carried its own dismantling condition (「第一个 `SeatCount != 2` 的棋种落地」); two such games shipped and nothing happened, because the thing that actually gets read every session is the **deferral table above**. Cost: clicking 认输 in a real three-seat game returned **500**. When a requirement defers a decision, put the trigger in that table too — the spec is where the *reasoning* lives, not where the reminder fires.
- **一条要求可以把正确答案写在括号里,而它自己的 Scenario 与之相反。** `web-game-board` 的侧栏要求写着「渲染「黑方 / 白方」两个座位(**象棋读作红 / 黑**)」—— 括号里那句就是对的,而**它没有机制**:没有实现读它,没有测试守它,而同一条要求下面的 Scenario 明说「侧栏说『黑方 / 白方』」。三处代码照着 Scenario 写,于是象棋房把红方叫成黑方,活了很久。**一个写在括号里的例外和没写是一样的** —— 例外要么变成判据,要么删掉。顺带:同一次调查发现 `web-doudizhu` 有一条 live 要求(`MUST NOT 去问棋种注册表要 seatCount`)与已发布代码**正好相反**,`publish-seat-count` 早就推翻了它却没改它。这两件事 `validate --strict` 都看不见。
- **一个在稳态下正确的判据,可能是在用近似量回答问题。** 「座位数大于二 → 说座位号」的理由原文是「『白方走棋』在一个没有白方的棋种里是错的」—— 那句理由是对的,而它被写成了座位数。象棋和成语接龙都是「没有白方的棋种」,却恰好有两个座位,于是同一条理由该拦的东西从缝里过去了。**判据要贴着理由写,不要贴着当时手边那个数字写。**
- **Counting by grep ≠ counting by failing test ≠ counting by requirement.** All three have been wrong here, in that order.
- **`openspec validate --strict` validates spec *shape*, never spec *truth*.** It was 37/37, 38/38 and 41/41 green across every drift this repo has found, including a Scenario that had never been implemented (four occurrences) and a live spec contradicting the code for 36 commits. **The signal is a merged PR whose change directory is still in `openspec/changes/`** — check that list.
- **Archive in merge order, and that is necessary but not sufficient.** MODIFIED replaces a requirement wholesale, so when two unarchived changes touch one requirement, one must be hand-merged. Generate MODIFIED bodies by *extracting from the live spec and patching*, never by retyping — retyping is how an unrelated sentence gets silently reverted. Renaming a requirement needs a `RENAMED` block or archive aborts.
- **SignalR applies no C# optional-parameter defaults, in either direction.** A client sending fewer *or more* arguments than the method declares is rejected in the binding layer — before any filter, and below the configured log level, so it is invisible from both ends. Adding a parameter to a live hub method is a breaking change; add a method.

### Discipline

**Do not start a new game until the previous one is archived.** Nine games × (rules + AI + UI + i18n + tests) will otherwise all rot half-finished. The rule is narrower than the failure it must prevent: `enable-xiangqi-human-play` was not a game, so nothing stopped it sitting unarchived for 36 commits with the live spec contradicting the code.

## Workflow — OpenSpec is mandatory

**Never write implementation code without an approved OpenSpec proposal.** This is a hard rule, not a preference.

1. **Propose** — for each new feature, create a change directory at `openspec/changes/<change-name>/` containing `proposal.md`, `tasks.md`, and `specs/`. Use `/opsx:propose` or `/openspec-propose`.
2. **Review** — the user reads the proposal and requests edits. Wait for explicit approval before touching code.
3. **Implement** — once approved, work through `tasks.md` item by item, checking off as you go. Use `/opsx:apply`.
4. **Archive** — when done, `openspec archive <change-name>` moves spec deltas from `changes/` into the live `openspec/specs/` tree and renames the change directory under `archive/`.

Pure bug fixes that bring code into compliance with an existing spec don't need a new proposal — fix the code, commit. Spec-level corrections that document already-shipped behaviour can ship as a tiny `fix-spec-<name>-drift` change.

## Tech stack

### Backend (`backend/`) — .NET 10

- ASP.NET Core Web API, target `net10.0` on every project (nullable + implicit usings enabled)
- **MediatR** for CQRS — every write is a `Command`, every read is a `Query`, one handler per file
- **EF Core** — SQLite for local dev, SQL Server when scaling
- **SignalR** for real-time play and chat
- **FluentValidation** for input validation, **Serilog** for logging, **JWT** for auth
- Tests: **xUnit** + **FluentAssertions** + **Moq**

The solution file is `.slnx` (XML), not `.sln`. The `dotnet` CLI handles it transparently; older tooling may not.

### Web (`frontend-web/`) — phase 1

- **Angular 21** + TypeScript strict mode
- **Tailwind CSS v4** + **Angular Material** + **`@angular/cdk`** (overlays / dialogs / a11y)
- **Transloco** for runtime i18n (initial locales: `zh-CN`, `en`; adding a new locale = drop one JSON file + register one line)
- `@microsoft/signalr` client (lazy-imported on first hub call to keep it out of the main bundle)
- State: **Angular Signals** first; NgRx only for genuinely complex flows
- Tests: **Vitest** (not Karma/Jasmine)

### Desktop (`frontend-desktop/`) — phase 2

Electron wrapping the Angular app.

### Mobile (`frontend-mobile/`) — phase 3

Flutter + Material Design 3, `signalr_netcore` client.

## Backend architecture

### Layer dependency direction (strict)

```
Domain  ← Application  ← Infrastructure
                       ← Api
```

- **`Gewu.Domain`** — entities, value objects, domain events. Zero outward dependencies.
- **`Gewu.Application`** — use cases (MediatR handlers), DTOs, interfaces for infrastructure concerns. Depends on `Domain` only.
- **`Gewu.Infrastructure`** — EF Core, persistence, external adapters. Implements `Application` interfaces.
- **`Gewu.Api`** — ASP.NET host, HTTP endpoints, SignalR hubs, DI composition root.

Preserve the direction when adding project references. **Never** have `Api` reference `Domain` directly; **never** put DB access outside `Infrastructure`.

### DDD aggregates

- **Room aggregate** (root: `Room`) contains `Game` + `Chat`. All mutations to game state and chat go through `Room`.
- **User aggregate** (root: `User`) is independent.
- Value objects (no identity, immutable): `Move`, `BoardPosition`, `Score`.
- Domain events (e.g. `MoveMade`, `GameEnded`) published via MediatR.

### CQRS conventions

- Commands: `CreateRoomCommand`, `MakeMoveCommand`, ...
- Queries: `GetRoomListQuery`, `GetLeaderboardQuery`, ...
- One handler per file; the file name matches the command/query.
- **SignalR hubs route messages only** — they dispatch to MediatR and push results back. No business logic in hubs.

### Hard rules

- **Domain and Application must not use `async void`, `.Result`, or `.Wait()`.** Use `async Task` / `await` end-to-end.
- Database access is **only** allowed in `Infrastructure`.
- Public methods need at least an XML `<summary>` doc comment.
- Interfaces use the `I` prefix (`IRoomRepository`).
- C# files: PascalCase.

### Required tests

Don't ship without unit tests for:
- Domain core logic — **win detection** and **ELO calculation** in particular.
- Every Application handler.
- Frontend: components and services with real logic (pure display components can skip).

Test projects live under `backend/tests/` — read the directory rather than a list here. There is **no Api-level project**; if one is added, name it `Gewu.Api.Tests` and register it in `Gewu.slnx`. That absence is load-bearing: it is why the per-seat SignalR fan-out has no end-to-end test. The test csprojs declare `Xunit` as a global using — don't add `using Xunit;` in test files.

## Frontend conventions (Angular)

### Naming & structure

- Filenames: **kebab-case**. Classes: PascalCase.
- Use **standalone components** (Angular 17+ style). Don't create new NgModules unless unavoidable.
- Prefer **Signals** over `BehaviorSubject` for local state.
- When Tailwind class strings get long, extract via `@apply` into a custom utility.
- All HTTP lives in `core/api/`. Components must not call `HttpClient` directly.

### Design & UX (hard rules)

- **Dark mode must work from day one.** Mechanism: `ThemeService` (Signal) toggles a `.dark` class on `<html>`; colors come from CSS variables (`--color-bg`, `--color-primary`, etc.); Tailwind's `dark:` variant assists. Never hard-code dark colors in components (no `bg-gray-900` / `text-white`).
- **Responsive (mobile-first).** Every route MUST work at **375 px** width, then progressively enhance via `sm: / md: / lg: / xl:` up to 1440 px+. Tailwind's default breakpoints are sufficient — don't write CSS for one specific resolution.
- **Modern UX.** CSS transitions over JS animations. Visible `focus-visible` ring. Skeleton placeholders to avoid layout shift. Every interactive element keyboard-reachable. Respect `prefers-reduced-motion`. Loading / empty / error states all need real UI — no plain `"loading…"` text.

### Performance

- **Lazy loading is mandatory.** Every route outside the root shell (shell + login) uses `loadComponent` / `loadChildren`. Each lazy chunk stays **under 200 KB gzipped**; split if larger.
- `<img loading="lazy">` is the default; only above-the-fold first-paint images are exempt.
- The SignalR client is constructed inside a service and **only connects on first subscription** — never at app bootstrap. A user who never opens a game page should never establish a hub connection.

### Architecture — SOLID, easy to extend

- **Single responsibility.** A component does one thing. Container components (fetch data, dispatch events) and presentational components (pure rendering of inputs) are separate — don't mix.
- **Dependency inversion.** When a service has plausible alternative implementations (mock for tests / future API client / different state backend), use an **abstract class as the DI token**. Inject by token, not by concrete class.
- **Open/closed.** Adding a new theme / locale / difficulty MUST be a "drop one config file or one TS file" change — no edits to existing components.
- Composition over inheritance. Cross-cutting behavior goes in directives / pipes, not base classes.
- Components stay under 200 LOC. If larger, extract a service or store.

### Dialogs & overlays

Dialogs / popovers / overlays MUST use **Angular CDK** (`@angular/cdk/dialog` or `@angular/cdk/overlay`). Material's `MatDialog` is fine (it wraps CDK). **Never** hand-roll `<div>` + `*ngIf` modals — focus trap, ESC handling, backdrop, ARIA attributes are all required, and CDK gives them for free.

### i18n

- Use **Transloco** (or Angular i18n + ICU runtime switching).
- Initial locales: `zh-CN` (Simplified Chinese) + `en`. Files at `public/i18n/<locale>.json`. Flat keys / dotted paths (`room.join.button`).
- Templates **MUST NOT hard-code** Chinese or English display strings — always `{{ 'key.path' | transloco }}`. Date / number formatting goes through Angular's `formatDate` / `formatNumber` with a locale parameter.
- The active language is held by `LanguageService` (Signal), persisted to `localStorage`. Resolution order: `localStorage` → `navigator.language` → `en` fallback.
- Adding a new locale = drop a new `i18n/<locale>.json` + add one entry to `LanguageService.supported` + register Angular locale data in `core/i18n/register-locales.ts`. No other file changes.

### Theme switching (Material / System / future)

- Themes are kept in a registry: `ThemeService.register(name, tokens)`. `tokens` is a CSS variable bag (`--color-primary`, `--color-surface`, `--radius-card`, `--shadow-elevated`, …). Switching = setting `data-theme="<name>"` on `<html>` and persisting to `localStorage`.
- The shipped themes are **only** the `[data-theme='…']` blocks in `src/styles/tokens.css` plus one `register('…')` line each — there is no `themes/` directory any more, and no TypeScript object per theme. `npm run lint` prints the live count (`N themes x M tokens`), derived from those selectors.

  Two corrections in a row landed on this one line, which is why it now points at a *mechanism* instead of a place. It first said "two themes ship" and was wrong from the day `ink` landed. It was then fixed to "read the directory `core/theme/themes/`" — and `drop-theme-token-mirrors` **deleted that directory two changes later**. **"Read the directory" only beats enumerating while the directory exists;** pointing at the thing that fails CI beats both.
- **Dark/Light is an orthogonal axis to the theme.** Each theme has light + dark token sets. `ThemeService` exposes two signals (`themeName` and `isDark`) that switch independently.
- Component styles MUST reference CSS variables, never literal colors. "This button uses theme-blue" = `var(--color-primary)`, not `#2962FF`.
- Adding a new theme = drop one tokens file + one `ThemeService.register(...)` call. No component changes.

The same registry pattern applies to **board skins** (`BoardSkinService`, `core/theme/skins/`) and **sound packs** (`SoundService`, `core/sound/packs/index.ts`, lazily `import()`ed — they were 8.69 kB of first paint for audio that cannot play before the first user gesture).

**Neither list is enumerated here on purpose.** The previous version of this line said board skins were "`wood` + `classic`" — wrong since `midnight` shipped, and it went stale while a whole change (`fix-spec-web-shell-pack-count`) was busy hunting two *other* copies of the sound-pack list without looking at this one. Read the directory. A skin or pack that omits a token **fails to compile**, and walking tests derive from `PACK_LOADERS` / the skin registry, so the code cannot disagree with itself — only prose can.

### Frontend tests

- Vitest covers: services / stores with logic, components with conditional branches, the i18n pipe, cross-cutting services like `ThemeService` / `LanguageService` / `SoundService`. Pure presentational components can skip.
- For side-effecting paths (dialogs, route guards, SignalR subscriptions), use TestBed + `ComponentHarness` integration tests.

## Common commands

From `backend/`:

```bash
dotnet build Gewu.slnx                              # build all
dotnet test  Gewu.slnx                              # run all tests
dotnet run   --project src/Gewu.Api                 # http://localhost:5145, https://localhost:7082

dotnet test tests/Gewu.Domain.Tests                 # single project
dotnet test tests/Gewu.Domain.Tests \
  --filter "FullyQualifiedName~WinDetectionTests.Diagonal"   # single test
```

### EF Core migrations

Install once: `dotnet tool install --global dotnet-ef`. Run from `backend/`:

```bash
# Add a migration (name in PascalCase, describes the intent)
dotnet ef migrations add AddUserAndRoom \
  --project src/Gewu.Infrastructure \
  --startup-project src/Gewu.Api \
  --output-dir Persistence/Migrations

# Apply to the configured DB
dotnet ef database update \
  --project src/Gewu.Infrastructure \
  --startup-project src/Gewu.Api

# Roll back the last migration (only before it has been merged / pushed)
dotnet ef migrations remove \
  --project src/Gewu.Infrastructure \
  --startup-project src/Gewu.Api

# Generate a SQL script for review.
#
# NOTE: **--idempotent does not work here.** EF throws
#   NotSupportedException: Generating idempotent scripts for migrations is not
#   currently supported for SQLite
# and writes no file at all. This block used to say `--idempotent`, and it had
# never once worked — measured, not assumed.
dotnet ef migrations script \
  --project src/Gewu.Infrastructure \
  --startup-project src/Gewu.Api \
  -o migrations.sql

# Just the delta between two migrations. This form *is* supported on SQLite.
dotnet ef migrations script AddMoveOrigin AddScoreRuns \
  --project src/Gewu.Infrastructure \
  --startup-project src/Gewu.Api \
  -o delta.sql
```

Rule: never edit a migration that has already been merged to `main` — add a new one instead.

### Frontend

```bash
npm install
npm start           # ng serve (with the dev proxy → :5145)
npm run build
npm test            # Vitest, watch mode
npm run test:ci     # Vitest, single run — this is what CI runs
npm run lint        # ng lint + scripts/check-styles.mjs
```

`npm run lint` also runs `check-styles.mjs`, which asserts the board-skin token sets match, that no stylesheet hardcodes a suit path, that the card table's fan formula has no percentage-valued variable in a `transform`, and — since `fix-primary-label-contrast` — that **every foreground/fill pairing the templates actually write** clears 4.5:1 in all four themes x light/dark (gradients measured at every stop), with no element able to carry two foreground colours at once. It prints the reading count; **a drop in that number means coverage was lost, not that the code got simpler**. It pins those by **filename**, so moving a stylesheet will break it — correctly: the invariants must not silently stop being checked.

## Git — branches & commits

Use **GitHub Flow** plus **Conventional Commits**.

**Branches** — `main` is protected and always deployable. Branch off `main` for every change; PR back to `main`:

```
feat/<slug>      new feature       feat/room-chat
fix/<slug>       bug fix           fix/elo-draw-calc
refactor/<slug>  refactor          refactor/move-validator
docs/<slug>      docs only         docs/signalr-contract
chore/<slug>     build / misc      chore/upgrade-ef-core
test/<slug>      tests only        test/win-detection-edge
```

Slugs are kebab-case. Tie to an OpenSpec change name when one exists (e.g. `feat/add-domain-core`).

**Commits** — [Conventional Commits](https://www.conventionalcommits.org/):

```
<type>(<scope>): <subject>

[optional body — explain WHY, not WHAT]

[optional footer — BREAKING CHANGE: ... / Refs: #123]
```

Types: `feat` `fix` `docs` `style` `refactor` `perf` `test` `build` `ci` `chore` `revert`. Scope is the module (`domain`, `api`, `web`, `infra`, etc.). Subject is in the imperative mood, ≤ 72 chars, no trailing period. Breaking changes get a `!` after the type/scope **and** a `BREAKING CHANGE:` footer.

Examples:

```
feat(domain): add five-in-a-row win detection
fix(api): return 409 instead of 500 when room is full
refactor(web)!: replace BehaviorSubject with Signals in game store
```

A single commit can be in English or Chinese, but pick one — don't mix within a single commit. Don't commit failing tests.

## Code review

All merges to `main` go through a PR. Direct push is forbidden.

### PR requirements

- **Link to the OpenSpec change.** The PR description states the corresponding `openspec/changes/<name>/`. If there is no associated change, explain why one isn't needed (pure docs, build fix, spec-drift correction).
- **Size.** Each PR ideally stays under **400 lines of net change** (excluding auto-generated migration / lock files). Split when larger.
- **CI must be green.** `dotnet build` + `dotnet test` + the web `npm run lint` + `npm test -- --run` must all pass before requesting review.
- **At least 1 approval** to merge. PRs touching architecture, security, or DB schema benefit from 2 approvals.
- Merge style: **Squash merge**. Use Conventional Commits format and keep the PR number (`feat(domain): ... (#42)`).

### Author self-check (before opening a PR)

- [ ] Layer direction intact: `Domain` has no outward dependencies; `Application` only depends on `Domain`; DB access only in `Infrastructure`; `Api` does not directly reference `Domain`
- [ ] No `async void` / `.Result` / `.Wait()` in `Domain` or `Application`
- [ ] SignalR hub is purely a router; business logic lives in handlers
- [ ] Public methods have an XML `<summary>`; interfaces start with `I`
- [ ] Unit tests cover: win detection / ELO / new handlers / web services with logic
- [ ] No secrets / connection strings / sensitive `appsettings.*.json` values committed
- [ ] OpenSpec `tasks.md` is checked off; latest progress reflected in the PR description

### Reviewer focus (in priority order)

1. **Correctness and business logic** — win detection rules, ELO formula, forbidden moves, timeouts, reconnection. Are edge cases tested?
2. **Architecture and dependency direction** — any sneaky cross-layer calls? Are DB / HTTP details leaking into `Application` or `Domain`?
3. **Concurrency and async** — deadlock risk? `async` used correctly? SignalR group / connection lifecycle right?
4. **Security** — input validation (FluentValidation), authn/authz, SQL injection, XSS, JWT verification, spectators must not be able to send move commands.
5. **Test quality** — not just coverage; do tests assert *behavior* rather than *implementation*? Is mocking abused?
6. **Readability** — naming, function length, comments that explain *why* rather than *what*.
7. **Performance** — only raise this when there's a metric or profile to point at. Don't ask for optimization on a hunch.

### Comment etiquette

Prefix review comments so the author knows what's required vs. optional:

- `must:` — must change, otherwise no merge (correctness / security / architectural violation)
- `should:` — strongly suggested change; the author needs a reason to keep their version
- `nit:` — minor / style; the author's call
- `question:` — pure question, not a request for change
- `praise:` — when something's well-done, say so

Authors can decline `should` / `nit` items but should briefly explain why. Don't approve while a `must:` is unresolved.

## Shell

- **A command started in the background has no network egress.** A 1634-request fetch logged **558 failures in a row** while the same `curl` line succeeded (200, 8 kB) in the foreground; the loop kept going and would have ended with a plausible-looking `fetched=` line. **Only the file count showed it.** Long fetches: start them in the foreground and let the tool move them to the background — a process that already has the socket keeps it.

Windows host, bash shell. Use Unix syntax in commands (`/dev/null`, forward slashes), not `NUL` / backslashes.
