# tasks

## 0. 先修那个 dispose 缺陷 —— 路由化会放大它

- [x] `GameViewModel` / `LobbyViewModel` / `LoginViewModel`:`await` 之后再 `notifyListeners()` 的每一处都先确认没被 dispose。
      **不要用 `hasListeners` 当判据** —— 一个没人监听但还活着的 notifier 也是 false,那会把正常的通知也吞掉。
- [x] 单测:`open()` 飞行中 dispose,MUST NOT 抛。
- [x] **正面对照:把守卫删掉,看它红。** 一条从没红过的守卫等于没有。
- [x] 顺手 grep 三个 ViewModel 里 `await` 后面所有的 `notifyListeners()`,一个也别漏 ——
      「我刚修了这一类问题」是该多看两眼的理由,不是该放松的理由。

## 1. 路由表

- [x] `pubspec.yaml` 加 `go_router`。
- [x] `lib/ui/router.dart`:`/login`、`/`、`/rooms/:id` 三条,`GameView` 从 `state.pathParameters['id']` 取房间号。
- [x] `ChangeNotifierProvider` 仍然在每条路由的 `builder` 里创建,`GameViewModel` 仍然按房间号 key —— 换了别的房间要一个新的 ViewModel,不是复用一个指着上一间的。
- [x] `redirect`:未认证 → `/login`;已认证且目标是 `/login` → `/`。
- [x] `refreshListenable`:一个「认证状态变了」的 `Listenable`,由 `AuthRepository` 提供,失效时让 router 重跑 `redirect`。
      **接口装在 repository 上,不装在拦截器上** —— 拦截器在 `data/services/`,让它认识路由会把分层倒过来。

## 2. 拆掉旧的那两个字段

- [x] `GewuApp` 改成 `StatelessWidget`,删掉 `_authenticated` 与 `_openRoomId`。
- [x] `LoginView.onSignedIn` / `LobbyView.onOpenRoom` / `GameView.onLeave` 改成 `context.go(...)`。
      `GameView` 的 `AppBar.leading` 换成 go_router 自己的返回(它知道能不能弹)。
- [x] `MaterialApp` → `MaterialApp.router`。
- [x] **删干净的判据:grep 不到那两个字段名,且 `GewuApp` 是 `StatelessWidget`。**
      最好的机制是能被删掉的那种 —— 它俩消失就是路由表真的接手了。

## 3. 走查与测试

- [x] `test/layering_test.dart`:`router.dart` 在 `ui/` 下,所以它 MUST NOT import `data/services/**` 或 `package:dio`。
      现有四条规则应当**自动覆盖**它 —— 确认一遍,别假设。
- [x] widget 测试(不需要后端):未认证进 `/rooms/x` → 落在登录页;已认证进 `/login` → 落在大厅。**两个方向都要。**
- [x] `integration_test`:进房间 → `canPop()` MUST 为 true → 一次 `popRoute` MUST 回大厅;
      **大厅里 `canPop()` MUST 为 false**(缺了这一条,「什么都 pop」也能通过)。
- [x] `integration_test`:两个 token 都弄成不可用 → 点刷新 → MUST 落在登录页。
      **保留前面那次的正面对照**:弄死之前先证明这个会话是活的,否则这条断言在「一开始就没登上」时也是绿的。
- [x] `flutter analyze` 零问题;`flutter test` 全绿。

## 4. 端到端不回归

- [x] `integration_test/play_a_move_test.dart` MUST 通过,且**每一个匹配器与期望值逐字未变**。
      导航从回调变成 `context.go`,所以那几处 `tap` 的**接收者**可能要动 ——
      动了就逐条说得出动到哪了,期望值一个都不许改。

## 5. 把上一笔留在 live spec 里的过去时收掉

- [x] `refactor-mobile-mvvm` 归档后,live `mobile-shell` 里多了一条叫
      **「重构 MUST NOT 改变任何行为,而判据是既有的端到端切片」** 的要求 ——
      它的**规则**是通用的(拿既有切片当判据、期望值逐字不变),它的**主语**是一次过去的变更。
      这一笔正好第二次用到同一条规则,所以在这里用一个 `RENAMED` 块把它改成通用措辞。
      **不加 `RENAMED` 的话 archive 会直接报错**,这不是可选项。
- [x] 理由写进要求本身:一条主语是「上次那笔」的 live 要求,下一个人读到时无从判断它还管不管。

## 6. 收尾

- [x] `JOURNAL.md` 一条。
- [x] `CLAUDE.md`:手机端那一节加**一行**,说清「哪一屏由路由表决定,`GewuApp` 是 `StatelessWidget`,这一条由类型守」。
      **只加一行** —— 这个文件每次会话整份加载。
- [x] 归档顺序:先 `refactor-mobile-mvvm`,再这一笔。
