# Tasks — generalize-match-payload

## 1. 载荷:位置 或 文本

- [x] 1.1 `MoveIntent`:`To` 改为 `Position?`,新增 `Text`;工厂加 `Say(text)`。
- [x] 1.2 `PlayedMove` 同形,并加 `Positional` / `Said` 两个工厂。
- [x] 1.3 「恰好一种载荷」由 `MovePayload.Validate` 单点实现,三处(`MoveIntent` / `PlayedMove` / `Move` 实体)共用。两种都给、都不给、文本空白、文本带起点,全部抛 `InvalidMoveException`。
- [x] 1.4 `Move` 实体:`Row` / `Col` 改可空,新增 `Text`。
- [x] 1.5 `RequirePosition()` / `RequireText()`:棋盘类规则的第一件事。一个成语落到五子棋规则里,得到的是一句说得清的拒绝,不是空引用。
- [x] 1.6 `MovePayloadTests` **直接调主构造器**枚举非法组合,不走三个工厂 —— 工厂是约定,构造器检查才是机制。14 条。

## 2. 盘面属于 `IBoardGameRules`

- [x] 2.1 新增 `IBoardGameRules : IGameRules` 承载 `Rows` / `Cols`,从 `IGameRules` 移除。
- [x] 2.2 `INInARowRules : IBoardGameRules`;`XiangqiRules` 实现 `IBoardGameRules`。
- [x] 2.3 `GameDescriptorDto.Rows` / `Cols` 改 `int?`,投影按 `r as IBoardGameRules` 填。
- [x] 2.4 遍历注册表的断言:声明了尺寸的规则必须真是 `IBoardGameRules`,且至少有一个是。

## 3. 迁移

- [x] 3.1 `AddMoveTextPayload`:`Row` / `Col` 加宽为可空 + 新增 `Text`(`maxLength: 64`,词典里最长的成语 15 字)。

- [x] 3.2 **第一版迁移是错的,而且看不出来。** 把 CLR 类型改成 `int?` 之后生成迁移,EF 只加了 `Text` 一列 —— 因为 `MoveConfiguration` 上有 `.IsRequired()`,**显式配置压过 CLR 可空性**。类型改完、编译通过、迁移干净,而数据库仍然是 `NOT NULL`,会在插入第一条文本类记录时才拒绝。删掉那两行 `.IsRequired()` 才是本变更真正动到 schema 的地方。

- [x] 3.3 **EF 生成的 `Down` 会静默毁数据**,如提案所料:`AlterColumn(nullable: false, defaultValue: 0)` 加 `DropColumn("Text")` —— 每一步成语变成一步下在 (0,0) 的棋,内容随列消失。与 `add-per-game-rating` 那次是同一个错误。手写为:先用一张带 `CHECK` 的临时表做断言,存在文本类记录时 INSERT 违反约束、迁移中止。**表名即错误信息**(`__rollback_refused_AddMoveTextPayload_would_destroy_textual_moves`),约束失败时 SQLite 会把它打出来。

- [x] 3.4 `MoveTextPayloadMigrationTests` 四条:既有落子一字不变、文本类可存(此前会被 `NOT NULL` 拒绝)、无文本时回滚正常、**有文本时回滚抛异常且数据仍在**。

## 4. 前端

- [x] 4.1 `MoveDto.row` / `col` 改 `number | null`,新增 `text`;`GameDescriptor.rows` / `cols` 同。
- [x] 4.2 `boardSizeFor` 返回 `BoardSize | null`,三条分支互不冒充。
- [x] 4.3 房间页 / 回放页改 `@else if (boardSize(); as size)` —— 没有盘面就不渲染棋盘。**没有用 `!` 断言**:那会把刚刚分出来的第三种情况又抹平。
- [x] 4.4 `board.ts` / `position.ts` 跳过没有格子的一步:载荷不对说明挂错了组件,而"画得不对好过白屏"这条规则照旧。
- [x] 4.5 `StubGameCapabilities.boardless()` + 两条测试。**今天没有任何棋种会走到这条分支**,浏览器里看不到它,所以单测是唯一托住它的东西。

## 5. 验证

- [x] 5.1 `dotnet build` **0 warning**;`dotnet test` **889 passed**(此前 871:+14 载荷不变量、+4 迁移)。
- [x] 5.2 `npm run lint` 全绿;`npm run test:ci` **480 passed**(此前 478);bundle 500.34 kB(未回退)。
- [x] 5.3 浏览器实跑,两种位置类载荷都原样往返:

  | 棋种 | 落库结果 |
  | --- | --- |
  | 五子棋 | `row: 7, col: 7, fromRow: null, fromCol: null, text: null` |
  | 中国象棋 | `fromRow: 6, fromCol: 0 → row: 5, col: 0, text: null`,AI 随即应招 |

  `GET /api/games` 三个棋种仍各自带尺寸(它们都有盘面),没有一个变成 `null`。

- [x] 5.4 **本变更不加成语接龙。** 下一个变更 `add-idiom-chain` 的验收条件沿用前两次:`git diff --name-only` 里没有内核文件。

## 6. 顺带修的漂移

- [x] 6.1 `platform-catalog` 有三处仍提到已被 `remove-manifest-board` 删掉的 `GameManifest.board`,其中一条**场景断言 `board === { rows: 3, cols: 3 }`** —— 那个字段已经不存在。同一份 spec 第 36 行同时写着「MUST NOT 存在 `board` 属性」,前后自相矛盾。纯 spec 修正,代码本来就是对的。

## 7. 一处仍未被证明的事

- [x] 7.1 这个接缝是**照着一个还不存在的游戏**塑形的:成语接龙的规则写在提案里,还没有实现。`generalize-match-domain`(对着象棋)与 `generalize-puzzle-rules`(对着华容道)都是这么做的,两次都成立 —— 但都要等游戏落地才算被证明。这里同样:`add-idiom-chain` 是唯一能检验它的东西。
