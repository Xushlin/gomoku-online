# Tasks — generalize-puzzle-rules

## 1. Domain：接口

- [x] 1.1 新增 `PuzzleScoreInput` record：`(HintsUsed, Mistakes, Duration, LayoutJson, SolutionJson, SubmissionJson)`，三个服务端信号在前。
- [x] 1.2 `IPuzzleRules.Validate` / `CheckPartial` 增加 `layoutJson` 参数；`Score` 改收 `PuzzleScoreInput`。
- [x] 1.3 文档注释写清两件事：布局必须一起传的理由（位置性答案 vs 路径性答案），以及提交进入计分为什么不是漏洞（服务端必须重放才肯接受）。

## 2. Domain：成语纵横

- [x] 2.1 `IdiomCrosswordRules` 三个方法只改签名，**方法体不动**。
- [x] 2.2 `Score` 显式忽略新入参，并在注释里说明这是选择而不是遗漏。
- [x] 2.3 `IdiomCrosswordRulesTests` 只做机械适配（多传一个 `LayoutJson()`、构造 `ScoreInput`），**断言一条不改**。
- [x] 2.4 新增 `Score_ignores_the_level_and_the_submission`：把关卡两半与提交换成 `"{}"`，星级不变。

## 3. Application：两个 handler

- [x] 3.1 `SubmitPuzzleAttemptCommandHandler`：`Validate(solution, layout, submission)`；`Score(new PuzzleScoreInput(...))`。
- [x] 3.2 `CheckPuzzlePartialCommandHandler`：`CheckPartial(solution, layout, partial)`。
- [x] 3.3 既有 handler 测试无需改动 —— 它们走的是 MediatR 入口，签名变化不穿透到那一层。

## 4. 测试用的假实现

- [x] 4.1 `PuzzleLifecycleTests` 的 `FakeRules` / `MarkerRules` 适配新签名。
- [x] 4.2 `FakeRules` 上加了注释：**它证明不了接口通用**。它照着成语纵横的形状写成，而一个 fake 不可能推翻写它时依据的假设 —— 覆盖「新增游戏不改既有文件」那条的正是这个 fake，所以它从未报警。真正的判据在 `add-klotski` 的 `git diff --name-only` 里。

## 5. 验证

- [x] 5.1 `dotnet build Gewu.slnx` —— **0 error, 0 warning**。
- [x] 5.2 `dotnet test Gewu.slnx` 全绿：Domain 514 / Application 210 / Infrastructure 78 = **802**。
- [x] 5.3 `openspec validate generalize-puzzle-rules --strict` 通过。
- [x] 5.4 无新增 migration、无 DTO 改动、无端点改动。

### 5.5 顺带修掉的一个 warning（不属于本变更的范围，说明在此）

`MoveOriginMigrationTests` 用 `ExecuteSqlRawAsync` 拼插值 SQL，编译器为此报 **EF1002**，从 `generalize-match-domain` 起就在。它是我上一轮说「0 warning」时漏掉的 —— 增量构建跳过了那个项目，我没做干净构建就下了结论。

改成 `ExecuteSqlAsync`（参数化）。值全是自己造的，注入风险为零；修它的理由是**一条长期存在的 warning 会让下一条真正要紧的 warning 淹没在噪声里**。

## 6. 归档前必答

- [x] **6.1 成语纵横的行为是否真的一行没变。**

  是。`git diff` 显示 `IdiomCrosswordRules` 只有三处签名与一处 `hintsUsed` → `input.HintsUsed` 的取值改动，判定逻辑、计分公式、JSON 选项全部原样。既有测试的断言零改动，只有调用形状变了。

- [x] **6.2 交给 `add-klotski` 的验收条件。**

  `add-klotski` 的 `git diff --name-only` **MUST NOT** 出现下列任何路径：

  - `backend/src/Gewu.Domain/Puzzles/`
  - `backend/src/Gewu.Application/Features/Puzzles/`
  - `backend/src/Gewu.Api/Controllers/PuzzlesController.cs`（或等价的谜题端点文件）

  这条判据是可执行的，不是断言 —— 与 `generalize-match-domain` 交给 `add-xiangqi` 的那条同一形式，那一条后来收住了。
