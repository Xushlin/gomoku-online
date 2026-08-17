# Tasks — enforce-human-vs-human

## 1. 先证明缺陷存在

- [x] 1.1 对着**真**注册表(`BuiltInGameRules.All`)跑 `CreateRoomCommandValidator`:

  ```
  gomoku       SupportsHvH=True   humanRoomAccepted=True
  tictactoe    SupportsHvH=False  humanRoomAccepted=True   ← 洞
  xiangqi      SupportsHvH=False  humanRoomAccepted=True   ← 洞
  ```

- [x] 1.2 起 API 实调。推断不算数 —— 上一个变更刚为「听起来对的机制说明」付过账。

  ```
  gameKey        declares HvH  POST /api/rooms
  gomoku         True          201
  tictactoe      False         201   *** MISMATCH ***
  xiangqi        False         201   *** MISMATCH ***
  ```

  `declares HvH` 那一列取自**同一个 API 的** `GET /api/games`。服务端在一次响应里声明象棋没有人人对战,下一次请求就开了一间。

- [x] 1.3 再往前走一步:第二个真人账号 `POST /api/rooms/{id}/join` → **200**。

  ```json
  { "gameKey": "xiangqi", "status": "Playing",
    "black": { "username": "hvhbefore" }, "white": { "username": "hvh2" } }
  ```

  不是一条孤儿记录,是一局正在进行的真人象棋。

## 2. 校验规则

- [x] 2.1 `GameKeyValidation.MustSupportHumanVsHuman(registry)`,与 `MustBeARegisteredGameKey` 并列。
- [x] 2.2 `CreateRoomCommandValidator` 挂上它;`CreateAiRoomCommandValidator` **不**挂。
- [x] 2.3 键解析不出规则时本条静默让位 —— `registry.For(key) is not { SupportsHumanVsHuman: false }`,只有"解析出来了且不支持"才失败。同一个字段为同一件事报两条错误,只会让调用方以为要改两处。

## 3. 让测试夹具不再说谎

- [x] 3.1 `GomokuRules.Registry` 改为 `[.. BuiltInGameRules.All]`。
- [x] 3.2 `GomokuRules.GomokuOnly` 保持手写 —— 它**本来**就该是个残缺注册表,现在注释里把这层区别写明了。
- [x] 3.3 删掉「与生产 DI 一致的注册表:五子棋 + 一字棋」那句话,换成事实与它为何曾经是假的。

## 4. 测试

- [x] 4.1 删掉 `xiangqi` 那条"未登记"用例(自 `add-xiangqi` 起就是假的),换成 `"go"` —— 围棋不在七款规划之内,是真的不在平台上。
- [x] 4.2 遍历 `GomokuRules.Registry.All` 断言:真人房校验通过 ⟺ `SupportsHumanVsHuman`。
- [x] 4.3 同一条测试里数两类各有几个,两个计数都必须 > 0。
- [x] 4.4 `A_game_without_human_play_is_refused_a_human_room_but_allowed_an_ai_room` —— 一条用例同时钉住拒绝与放行,免得日后有人"顺手"把规则也挂到 AI 路径上。
- [x] 4.5 新增 `The_test_registry_is_the_one_production_registers`:夹具的键集合必须等于 `BuiltInGameRules.All`。3.1 修的是当下,这条管的是以后。
- [x] 4.6 全量 `dotnet test Gewu.slnx` → **871 passed**(此前 868),没有别的用例依赖"能建一字棋真人房"。

## 5. 规格

- [x] 5.1 `room-and-gameplay`:建房校验要求 + 三处拿 `xiangqi` 当"未登记"举例的过期场景。
- [x] 5.2 `game-rules-registry`:新增"由服务端强制,不只是被声明"要求。
- [x] 5.3 `openspec validate --strict` 通过。

## 6. 验证

- [x] 6.1 `dotnet build` **0 warning**;`dotnet test` **871 passed**。
- [x] 6.2 重跑实调:

  | gameKey | 声明 | 变更前 | 变更后 |
  | --- | --- | --- | --- |
  | gomoku | true | 201 | 201 |
  | tictactoe | false | **201** | **400** |
  | xiangqi | false | **201** | **400** |

- [x] 6.3 400 的响应体点名字段且说清理由:

  ```json
  { "GameKey": ["'xiangqi' has no human-vs-human mode on this platform."] }
  ```

  未登记的键仍然只有一条错误:`{ "GameKey": ["'go' is not a game on this platform."] }`。

- [x] 6.4 AI 路径未受影响:`POST /api/rooms/ai` 对 `xiangqi` / `tictactoe` 均 201,`status=Playing`,白方 `AI_Easy`。
- [x] 6.5 `git status` 中无 `frontend-web/` 文件。

## 7. 明确不做的事,及理由

- [x] 7.1 **只在建房时拦,不在 `JoinRoom` 拦。** 建房是唯一的入口,堵住它就不会再有新的这类房间。为已存在的房间加一道 join 校验,要给 `JoinRoomCommandHandler` 添一个注册表依赖,而它服务的人群是零 —— 本仓库没有部署,本地 SQLite 全部 gitignore,唯一存在过的两间是上面 1.2 亲手建的。
- [x] 7.2 **`?? GameKeys.Gomoku` 那个"向后兼容"缺省没动。** 它属于 `generalize-lobby`:那问的是*客户端要哪个棋种*,与*服务端准不准*是两件事。顺带记下待办发现——那个缺省的理由写的是「已发布的客户端不会送这个字段」,而已发布的客户端有零个;前端从来没送过 `gameKey` 给 `POST /api/rooms` 与 `GET /api/rooms`,所以它不是兼容层,是一处藏起来的硬编码,也是大厅至今仍是五子棋专属的直接原因。
- [x] 7.3 **一字棋 / 象棋仍然不计分。** 本变更让 `IsRated ⇒ SupportsHumanVsHuman` 所依赖的那个"结构性事实"真的成立,结论本身不变。

## 8. 留给下一步

- [x] 8.1 `generalize-lobby` 现在可以放心读 `supportsHumanVsHuman` 决定要不要渲染"创建房间"—— 那是**展示决定**,背后有服务端规则兜着,而不是替代它。
