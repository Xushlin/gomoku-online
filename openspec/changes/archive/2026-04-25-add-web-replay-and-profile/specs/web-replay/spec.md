## ADDED Requirements

### Requirement: `/replay/:id` 路由由 `ReplayPage` 提供,惰性加载 + 鉴权守卫

`app.routes.ts` SHALL 新增 lazy 路由 `/replay/:id`:

- `loadComponent: () => import('./pages/replay/replay-page/replay-page').then((m) => m.ReplayPage)`
- `canMatch: [authGuard]`

`src/app/pages/replay/replay-page/` 目录 SHALL 包含 `replay-page.ts` + `replay-page.html` + spec。组件 standalone、OnPush、`:host { display: block }`。

#### Scenario: 路径解析到 ReplayPage
- **WHEN** 已登录用户导航到 `/replay/abc-123`
- **THEN** 加载 `ReplayPage` lazy chunk;路由参数 `id` 在组件中可读

#### Scenario: 未登录访问被守卫拦截
- **WHEN** 未登录用户访问 `/replay/x`
- **THEN** authGuard 拒绝匹配,导航被重定向到 `/login`(沿用既有守卫语义)

---

### Requirement: `ReplayPage` 初始化时拉取 `GET /api/rooms/{id}/replay`,渲染只读棋盘

`ReplayPage` `ngOnInit` SHALL:

1. 从 `ActivatedRoute.snapshot.paramMap` 读 `id`。
2. 调 `RoomsApiService.getReplay(id)` (新增方法,见 `web-game-board` 修订)。
3. 成功 → 把 `GameReplayDto` 存入本地 `replay = signal<GameReplayDto | null>`,把 `currentPly` 重置为 0。
4. 404 → 渲染翻译后的 `replay.errors.not-found` + 返回大厅链接。
5. 409 → 渲染翻译后的 `replay.errors.still-in-progress` + 链接到 `/rooms/:id` 实时房间页。
6. 其他 → 渲染翻译后的 `replay.errors.generic` + Retry 按钮(再次调 `getReplay`)。

成功状态下,模板 SHALL 引用既存 `Board` 组件并传 `[readonly]="true"`、`[state]="boardState()"`、`[mySide]="'spectator'"`,其中 `boardState` 是一个 `computed` 把 `replay()` + `currentPly()` 合成为一个 `RoomState` 形状对象,`status='Finished'`,`game.moves` 为 `replay.moves.slice(0, currentPly())`。

#### Scenario: 成功获取并初次渲染
- **WHEN** Alice 打开 `/replay/r-1` 且后端返回 200 + `GameReplayDto`(20 步)
- **THEN** `Board` 组件渲染;无落子(currentPly=0);标题区显示对局元信息(房间名、黑白方用户名为链接、`endReason`、`endedAt`)

#### Scenario: 404 处理
- **WHEN** `getReplay` 返回 HTTP 404
- **THEN** 不渲染 `Board`;渲染翻译键 `replay.errors.not-found` + 返回大厅链接;不再发起任何 hub / REST 调用

#### Scenario: 409 处理
- **WHEN** `getReplay` 返回 HTTP 409(`GameNotFinishedException`)
- **THEN** 渲染翻译键 `replay.errors.still-in-progress` + 一个 link `[routerLink]="['/rooms', id]"` 让用户去看实时对局

#### Scenario: 通用错误带 Retry
- **WHEN** `getReplay` 抛出非 404/409 错误(网络 / 500)
- **THEN** 渲染翻译键 `replay.errors.generic` + Retry 按钮;点 Retry 重新调 `getReplay`

---

### Requirement: 复用 `Board` 组件的只读模式渲染,不引入第二个棋盘渲染层

`ReplayPage` SHALL 通过传 `[readonly]="true"` 给现有 `Board` 组件来实现只读渲染;MUST NOT 在 `pages/replay/` 下复制粘贴 board 实现。

`boardState` `computed` SHALL 合成 `RoomState` 形状(synthesised partial)使 `Board` 自然消费 —— `status: 'Finished'` 触发 `Board.cellDisabled` 永远为 true,所以 readonly 边界由两层共同保证(`[readonly]` 输入 + `status !== 'Playing'`)。

#### Scenario: 落子按钮永远禁用
- **WHEN** ReplayPage 渲染任意 currentPly
- **THEN** Board 的全部 225 个 `<button>` 都 `disabled`;点击不触发任何事件

#### Scenario: 最后一步高亮跟着 scrubber
- **WHEN** `currentPly` 从 5 移到 7
- **THEN** Board 的 last-move 高亮自动从第 5 步落点移到第 7 步落点(因为 `boardState` 重新合成了 `moves.slice`)

---

### Requirement: 移动 scrubber —— 上一/下一步、首/末、播放/暂停、速度选择

ReplayPage SHALL 渲染一个 scrubber 控件,包含以下 UI 元素(全部 `| transloco` 文本,token-themed):

- **▶ 播放 / ⏸ 暂停** 按钮:点击切换 `playing` signal
- **⏮ 首步**:`currentPly.set(0)`;若正在播放则继续从 0 播
- **⏪ 上一步**:`currentPly` 减 1,边界 0;暂停播放
- **⏩ 下一步**:`currentPly` 加 1,边界 `moves.length`;暂停播放
- **⏭ 末步**:`currentPly.set(moves.length)`;暂停播放
- **进度滑块**:`<input type="range" min="0" max="moves.length" step="1" [value]="currentPly()" (input)="onSeek($event)">`,拖动直接 set `currentPly`,自动暂停
- **速度选择**(0.5× / 1× / 2× 的简单按钮组或 select)

播放间隔 = `700 / speed` 毫秒,通过 `effect` 驱动的 `setInterval`(随 `playing` / `speed` 变化重建)。

到达 `currentPly === moves.length` 时,自动 `playing.set(false)`,主按钮文案变为"重播"(再次点击重置 `currentPly` 到 0 并恢复播放)。

#### Scenario: 下一步前进
- **WHEN** `currentPly === 3`,用户点 ⏩
- **THEN** `currentPly === 4`;Board 显示前 4 步落子;`playing` 强制为 false

#### Scenario: 边界禁用
- **WHEN** `currentPly === 0`
- **THEN** ⏪ 和 ⏮ 按钮 `disabled`;⏭ 和 ⏩ 启用

#### Scenario: 自动播放到末尾自动停
- **WHEN** 用户从 ply 0 点 ▶ 播放,`moves.length === 12`
- **THEN** 大约 12 × (700/speed) 毫秒后 `currentPly === 12`,`playing` 自动变 false,主按钮显示"重播"

#### Scenario: 速度切换无 jitter
- **WHEN** 播放中用户从 1× 切到 2×
- **THEN** 旧 setInterval 立即清除,新 setInterval 以 350ms 间隔继续(无双重计时);Board 不闪烁

#### Scenario: 拖动滑块跳转
- **WHEN** 用户拖动滑块到值 9
- **THEN** `currentPly === 9`;`playing` 强制为 false;Board 立即渲染前 9 步

---

### Requirement: 标题区元信息使用用户名链接组件

ReplayPage 标题区 SHALL 渲染:

- 房间名(纯文本)
- "黑方:" + 黑方 username(`<a [routerLink]="['/users', black.id]" class="username-link">`)
- "白方:" + 白方 username(同上)
- 状态徽章:`endReason` 翻译(`game.ended.reason-connected-5` / `.reason-resigned` / `.reason-timeout`)
- 结束时间(`endedAt`,通过 Angular `formatDate` 按当前 locale 显示)

#### Scenario: 用户名是链接
- **WHEN** 渲染标题区
- **THEN** 黑白方 username 文本是 `<a>`,`href` 解析为 `/users/<userId>`;有 `username-link` class

---

### Requirement: 仅页面内状态,无 URL 深链(v1)

`currentPly` / `playing` / `speed` SHALL 全部是组件内 signal;MUST NOT 同步到 URL query string。

#### Scenario: 刷新页面重置 scrubber
- **WHEN** 用户在 ply 7 暂停后刷新
- **THEN** 重新 fetch `getReplay`,`currentPly` 回到 0;不读取或写入 `?ply=`(将来 `add-replay-share` 改动再加)
