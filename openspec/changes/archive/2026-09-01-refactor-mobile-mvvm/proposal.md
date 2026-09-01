# refactor-mobile-mvvm

手机端换成 **Dio + Provider + MVVM**,并把分层规则提炼进 `CLAUDE.md`。

## Why

用户要的。而这个重构的收益**不在这一屏,在第十一屏** —— 现在这 1403 行是能跑的,所以理由必须写清楚,否则下一个人会以为它是审美。

### 三个具体的问题,不是三个偏好

1. **没有模型层。** 现在到处是 `Map<String, dynamic>`:`raw['status']`、`move['row'] as int`、`(game?['moves'] as List<dynamic>?) ?? const []`。后端契约一改,**编译器一个字都不会说** —— 而这个仓库刚为 `GameReplayDto` 的座位表付过一次账,那次 web 端也是照着旧形状读的。
2. **业务逻辑长在 `State` 里。** `_GameScreenState` 同时负责取快照、连 hub、判状态、发落子、渲染。测它就得起一个 widget。
3. **依赖靠手传。** `AppServices` 一路 `widget.services` 传下去;第四屏开始这会变成噪声。

### Dio 与 Provider 各自买到什么

- **Dio** —— 拦截器。token 附加与 401 静默刷新现在是 `_send` 里的手写分支;拦截器是这类逻辑的既定位置,而且能单独测(`http_mock_adapter`)。另外拿到取消、超时、类型化错误。
- **Provider** —— 依赖注入 + 按需重建。它是 web 端「Signals 优先,NgRx 只在真正复杂的流程上」的对应物:轻,不引入 store/action/reducer 那套。

### 分层照 Flutter 官方架构指南

Flutter 团队给的推荐架构就是 MVVM:**UI 层(View + ViewModel)/ 数据层(Repository + Service)**,模型不可变,`ChangeNotifier` 承载状态。不自创一套。

## 目标分层

```
lib/
  main.dart                    应用入口,只做 bootstrap
  app.dart                     MaterialApp + Provider 图
  config/                      构建期事实(服务器地址)
  data/
    models/                    不可变;只会 fromJson;没有网络、没有业务规则
    services/                  裸 IO:DioClient / MatchHubService / SecureTokenStore
    repositories/              ViewModel 唯一能碰的东西;JSON→模型只在这里发生
  ui/
    <feature>/
      view/                    只渲染 + 转发意图,没有业务逻辑
      view_model/              ChangeNotifier;不持有 BuildContext
  theme/ i18n/                 横切
```

## 五条规则,而其中一条要有机制

1. **View 不许碰 Service 或 Dio。** View → ViewModel → Repository,一层都不许跳。
2. **模型不可变,且只解析。** 模型里出现网络或业务规则,说明它该是别的东西。
3. **ViewModel 是 `ChangeNotifier`,不持有 `BuildContext`。** 持有了就不能在无 widget 的情况下测,而那正是它存在的理由。
4. **JSON → 模型只在 Repository 里发生。** 别处出现 `as Map<String, dynamic>` 就是漏了一层。
5. **Repository 之外没有人知道 Dio 存在。**

**第 1 条与第 5 条 MUST 由一条走查测试强制**,读源文件断言 import 边界 —— 一条写在文档里的分层规则,是下一个赶时间的人第一个绕过的东西。这个仓库为「注释里的待办不是机制」付过很多次账。

## 验收:行为一个字节不变

**这是重构,不是功能。** 判据是那条端到端切片测试(注册 → 建房 → 对手加入 → 落子 → 服务端记下 (7,7))**断言一条不改地通过**。

它已经存在,所以不用新写 —— 它就是「什么都没变」的可执行形式,与 `play-from-position` 当初用「既有象棋测试一条不改地通过」是同一手。

## What changes

- `dio` + `provider` 进 `pubspec`;`http` 退到只有测试用。
- `data/models/`:`AuthUser`、`Room`、`RoomSeat`、`GameSnapshot`、`Move`。
- `data/services/`:`DioClient`(含 auth / refresh 拦截器)、`MatchHubService`、`SecureTokenStore`。
- `data/repositories/`:`AuthRepository`、`RoomRepository`。
- `ui/login|lobby|game/`:每个一个 View + 一个 ViewModel。
- 一条 **import 边界走查**测试。
- `CLAUDE.md` 增一段手机端约定 —— 与 Web 那段等长,只写**规则**不写教程。

## Non-goals

- **不加功能。** 不加棋种、不加主题选择器、不动音效。
- **不碰 Web / 桌面 / 后端。**
- **不引入代码生成**(`freezed` / `json_serializable`)。五个模型手写 `fromJson` 是几十行;生成器要一条 build 流水线,而它的收益在模型数量上来之后才成立。**触发条件:模型超过 ~12 个,或第一次因为手写 `fromJson` 漏字段出错。**
- **不做离线缓存层。** Repository 现在直连服务,缓存是另一个决定。
