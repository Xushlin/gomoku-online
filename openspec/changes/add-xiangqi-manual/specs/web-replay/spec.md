## MODIFIED Requirements

### Requirement: 移动 scrubber —— 上一/下一步、首/末、播放/暂停、速度选择

scrubber SHALL 是一个**独立的展示组件**,由 ReplayPage 与棋谱学习页共用;它渲染以下 UI 元素(全部 `| transloco` 文本,token-themed):

- **▶ 播放 / ⏸ 暂停** 按钮:点击切换 `playing` signal
- **⏮ 首步**:`currentPly.set(0)`;若正在播放则继续从 0 播
- **⏪ 上一步**:`currentPly` 减 1,边界 0;暂停播放
- **⏩ 下一步**:`currentPly` 加 1,边界 `moves.length`;暂停播放
- **⏭ 末步**:`currentPly.set(moves.length)`;暂停播放
- **进度滑块**:`<input type="range" min="0" max="moves.length" step="1" [value]="currentPly()" (input)="onSeek($event)">`,拖动直接 set `currentPly`,自动暂停
- **速度选择**(0.5× / 1× / 2× 的简单按钮组或 select)

播放间隔 = `700 / speed` 毫秒,通过 `effect` 驱动的 `setInterval`(随 `playing` / `speed` 变化重建)。

到达 `currentPly === moves.length` 时,自动 `playing.set(false)`,主按钮文案变为"重播"(再次点击重置 `currentPly` 到 0 并恢复播放)。

**它抽成组件而不是留在页面里,理由是第二个消费者已经到了**(`web-xiangqi-manual` 的学习页),而复制一份的代价是可测的:上面那些边界行为 —— 边界禁用、到末尾自动停、切速度不 jitter —— 在这里有 Scenario 钉着,而**复制品的那几条不会跟着红**。所以:

- 组件 SHALL 是纯展示的:输入是 `totalMoves` 与 `currentPly`,输出是「请求跳到第 N 手」;它 MUST NOT 注入任何服务,也 MUST NOT 知道招法从哪来;
- 播放的计时 SHALL 留在组件内(它是这个控件自己的行为),而**当前半手的真源 SHALL 在页面上** —— 页面还要用它选招法切片喂棋盘;
- 下面每一条 Scenario 对**两个**消费者都成立,而 MUST 有一条断言证明两边用的是同一个组件,否则「共用」只是一句注释。

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

#### Scenario: 两个页面共用同一个 scrubber
- **WHEN** 检索回放页与棋谱学习页的模板
- **THEN** 两者 MUST 都引用同一个 scrubber 组件;两个模板里 MUST NOT 各自出现 `type="range"`
