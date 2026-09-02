# tasks

## 0. 先把那个说谎的参数修好

- [x] `size: int` 拆成 `rows` / `cols`。**它有两处不成立:假设正方形,且星位写死 `[3, 7, 11]`。**
- [x] 抽出几何:格距、半格内缩、交叉点 ↔ 像素。**共用几何,不共用装饰**(五子棋有星位没河界,
      象棋反过来)—— 所以是两个画笔,不是一个画笔加 `if (gameKey == …)`。
- [x] 修类文档那句公式:`size / (n - 1)` 是错的,代码是 `side / size` 加半格内缩。**代码对、注释错。**
- [x] 测试两个方向:`rows: 10, cols: 9` 无出界装饰且保持 10:9;`15×15` 星位仍在该在的位置。
      **只测非正方形的话,「永不画装饰」也能通过。**
- [x] **正面对照:把星位改回无条件绘制,看非正方形那条红。**
- [x] 组件改名 —— 它即将画两个棋种。**重点不是名字好看,是别留一个名字说它只服务一个棋种。**
- [x] 在类注释里写下那笔欠账:`rows != cols` 这一笔之后**还没有生产调用方**,
      指名的调用方是下一笔的象棋 10×9;**下一笔不做,这一半就该退回。**

## 1. 棋种目录

- [x] `GameDescriptor` 模型:`gameKey / isRated / supportsHumanVsHuman / supportsAi / seatCount / rows? / cols?`。
      **`rows` / `cols` 可空,`seatCount` 不可空** —— 服务端 DTO 文档明写的区别,别抹平。
- [x] `GameCatalogRepository`:`GET /api/games`,一次拉全,缓存在内存里。
- [x] 棋盘注册表:`gameKey → 画笔`。**「哪些画得出来」只有这一处真源。**
- [x] 目录屏 `ui/catalog/`:列出服务端返回的**每一个**,注册表里没有的显示禁用态。
- [x] **客户端 MUST NOT 有任何「棋种 → 尺寸」的表。** 写完 grep 一遍 `15, 15` 和棋种键字面量,
      确认除了测试没有第二处。
- [x] `Room` 模型加 `gameKey`(`RoomStateDto` 第 96 行早就有,只是没解析)。
- [x] `Move` 模型加 `fromRow` / `fromCol`(可空 —— 五子棋没有)。**趁模型这一趟一起加,
      下一笔就不用再动模型层。**

## 2. 路由三层化

- [x] `/` 变成棋种目录,大厅移到 `/games/:key`,一局移到 `/games/:key/rooms/:id`,**三者嵌套**。
- [x] 删掉 `const gameKey = 'gomoku'`,`LobbyViewModel` 从路由参数收棋种键。
- [x] `redirect` 不变 —— 它按 `matchedLocation == '/login'` 判断,与新增的层无关。**确认一遍,别假设。**
- [x] 一局屏按**房间快照的 `gameKey`** 查目录拿尺寸,**不用路由里的 `:key`**。
- [x] 集成测试:目录 → 大厅 → 一局,每一级 `canPop()`,**外加目录里 `canPop()` 为 false**。
- [x] **正面对照:把 `games/:key` 改成顶层路由,看大厅那条 `canPop` 红。**

## 3. 走查与不回归

- [x] 目录走查**从 `GET /api/games` 的响应派生**,不迭代手打的键清单。
      断言:启用条数 == 棋盘注册表条数;**至少一个禁用**;外加一个**当下的具体数字
      (恰好 1 个启用)**,它在下一笔落地时该红。
- [x] **正面对照:往棋盘注册表里塞一个假条目,看「启用条数」那条红。**
- [x] `test/layering_test.dart`:`GameCatalogRepository` 在 `data/repositories/`,目录屏在 `ui/catalog/`。
      现有四条规则应当自动覆盖 —— **确认,别假设**;顺手断言 `ui/catalog/...` 进了走查样本。
- [x] `test/view_model_notify_test.dart` 里「恰好 3 个 ViewModel」会变成 4 个。
      **那正是它该红的时刻**,改数字时确认新那个也 `extends ViewModel`。
- [x] `integration_test/play_a_move_test.dart` MUST 通过。**这次它会改**:首屏从大厅变成目录,
      多一次点击。判据仍是**每一个匹配器与期望值逐字未变**,接收者与新增点击逐条说得出为什么。
- [x] `integration_test/router_test.dart` 里的 `/rooms/:id` 断言跟着路径走。
- [x] `flutter analyze` 零问题;`flutter test` 全绿。

## 4. 收尾

- [x] `JOURNAL.md` 一条。
- [x] `CLAUDE.md`:手机端那节改**一行** —— 目录从 `GET /api/games` 来,客户端没有第二份表。
      **只改一行**,这个文件每次会话整份加载。
- [x] **归档顺序:这一笔先,`add-mobile-xiangqi` 后。** 两笔都只 ADDED、没有 MODIFIED,
      所以不需要手工合 —— 但顺序仍然是顺序。
- [x] PR 里报净改动行数,超了 400 就说清为什么没拆。
