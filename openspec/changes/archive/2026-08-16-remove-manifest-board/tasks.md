# Tasks — remove-manifest-board

## 1. 删字段

- [x] 1.1 `GameManifest` 去掉 `board?` 及其那段长注释。
- [x] 1.2 gomoku / tictactoe / xiangqi 三份 manifest 去掉 `board`。

## 2. 换来源

- [x] 2.1 `boardSizeFor(capabilities, gameKey)` 读 `GameCapabilitiesService.of()` 的 `rows` / `cols`；解析不出、或描述符里的数字非正，仍退回 `DEFAULT_BOARD`。
- [x] 2.2 `RoomPage`：注入 `GameCapabilitiesService`，`ngOnInit` 调 `ensureLoaded()`，新增 `loadingBoard = loading() || !loaded()` 并接到模板的加载分支。
- [x] 2.3 `ReplayPage`：同上。

`DEFAULT_BOARD` 的语义收窄了，注释里写清了：它是给「这个客户端没听说过的棋种」用的，**不是**给「描述符还没到」用的 —— 后者由调用方压住加载态处理。两者混在一起，就会用一个兜底值去画一块客户端马上就会知道尺寸的棋盘。

## 3. 测试

- [x] 3.1 `board-size.spec.ts` 改用能力桩；新增「描述符数字非正也退回」「描述符未到达时退回」两条。
- [x] 3.2 删掉「每个可玩对战棋种都要声明 board」那条不变量，换成**清单里不得存在 `board` 属性**。
- [x] 3.3 `registry.spec.ts` 去掉象棋的 `board` 断言。
- [x] 3.4 `room-page.spec.ts` / `replay-page.spec.ts` 换成能力桩；房间页新增两条：一字棋房间按服务端描述符画 9 格、**描述符未到达时只画骨架屏不画棋盘**。
- [x] 3.5 `StubGameCapabilities` 增加 `sized()` 与 `pending()` 两个工厂。

## 4. 验证

- [x] 4.1 `npm run lint` 全绿；`npm run test:ci` **453 passed**（本变更前 449）。
- [x] 4.2 `npm run build` 成功。
- [x] 4.3 浏览器实测（独立后端 + 全新 scratch 数据库，三个房间各建一个）：
  - 一字棋房间 **9 格**
  - 五子棋房间 **225 格**
  - 象棋房间 **90 个交叉点 / 32 枚子** —— 不受影响，它本来就硬编码自己的 10×9
- [x] 4.4 `openspec validate remove-manifest-board --strict` 通过。

## 5. 归档前必答

- [x] **5.1 加载态真的覆盖住了吗？**

  两条证据，强度不同，都如实说：

  - **确定性的那条是单元测试**：`holds the skeleton until the descriptors arrive` 用一个 `loaded() === false` 的桩挂载房间页，断言 `app-board` 与 `app-xiangqi-board` 都不存在、骨架屏在。这条是可复现的。
  - **浏览器只能佐证**：冷加载一字棋房间后连续采样棋盘格数，**只观察到 9，从未观察到 225**。但采样是在页面加载**之后**才开始的（工具的一次往返比那个请求还慢），所以它不能证明「第一帧」没问题 —— 它只能说明在可观测的分辨率下没有闪烁。

  把这两条分开写，是因为「我没看见」和「它不会发生」不是一回事。

- [x] **5.2 后端零改动。** `git diff --name-only` 里没有 `backend/` 下的任何文件。

## 6. 顺带记下的一件事

回放页的加载门是新代码，浏览器实测**没有**覆盖到它（需要一局已结束的对局，而验证用的是全新数据库）。它由既有的回放页单元测试覆盖 —— 那些用例断言棋盘会渲染，所以「门永远关着」这种坏法会红。不过这仍然是单元测试而不是实测，写在这里而不是假装它被测过了。
