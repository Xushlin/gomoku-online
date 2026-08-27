# web-xiangqi-manual Specification

## Purpose
TBD - created by archiving change add-xiangqi-manual. Update Purpose after archive.
## Requirements
### Requirement: `/g/xiangqi/manual` 是目录,`/g/xiangqi/manual/:lineId` 是学习页,两者惰性加载

两个路由 SHALL 用 `loadComponent` 惰性加载,并 SHALL 挂上离开守卫之外的默认路由配置(它们没有进行中的对局,`leaveWarningKey()` 返回 null,所以守卫放行)。入口 SHALL 从象棋大厅进入;`xiangqiManifest` MUST NOT 因此改变 `launchRoute`。

目录页 SHALL 按局分组渲染(8 局,每局 1–6 条变化),局内条目显示标题与半手数。空态、加载态、错误态 SHALL 各有真实 UI。

#### Scenario: 375px 可用
- **WHEN** 视口 375px,渲染**标题最长**的那一条
- **THEN** 页面 `scrollWidth` MUST NOT 大于 `clientWidth`

#### Scenario: 未登录也能看
- **WHEN** 未登录访问目录与任意一条谱
- **THEN** 都正常渲染,MUST NOT 被重定向到登录页

### Requirement: 学习页复用只读象棋棋盘与共享 scrubber,自己不写渲染也不写 scrubber

学习页 SHALL 用 `XiangqiBoard` 的只读模式渲染局面,并 SHALL 用从回放页抽出的共享 scrubber 组件控制当前半手。它 MUST NOT 自己写棋盘渲染,也 MUST NOT 复制一份 scrubber 的按钮与边界逻辑。

理由:两份 scrubber 会各自漂 —— 边界禁用、到末尾自动停、切速度不 jitter 这些行为在回放页有断言钉着,而复制品的那几条不会跟着红。

#### Scenario: 与回放页共用同一个组件
- **WHEN** 检索学习页模板
- **THEN** 它 MUST 引用共享 scrubber 组件,且模板里 MUST NOT 出现 `type="range"` 或播放/暂停按钮的自有标记

### Requirement: 注解跟着当前半手走,没有注解时不留空洞

当前半手有注解时,学习页 SHALL 显示它;没有注解时 SHALL 显示上一条仍然生效的注解或一个稳定的占位,而 MUST NOT 让那块区域高度跳动 —— 否则每走一步棋盘都会往上下弹。

#### Scenario: 无注解不跳动
- **WHEN** 从一个带注解的半手走到一个不带注解的半手
- **THEN** 注解区域的高度 MUST 不变

#### Scenario: 注解为空的谱也能看
- **WHEN** 一条谱没有任何注解
- **THEN** 学习页正常渲染,注解区显示占位文案,MUST NOT 报错

### Requirement: 结果显示为**谱评**,MUST NOT 说成「将死」

学习页 SHALL 把 `result` 呈现为谱主的评断(如「谱评:黑胜」),而 MUST NOT 用「将死」「绝杀」之类描述招法列表实际走到的状态。

理由是实测:31 局里只有 11 局以杀棋收,**20 局走到「优势已成」就停**。把谱评说成将死,在那 20 局上是**错的**,而错的样子和对的样子在界面上完全一样 —— 没有任何断言会红。

#### Scenario: 未走到将死的谱不说将死
- **WHEN** 打开一条最后一手之后局面仍未终局的谱,走到末手
- **THEN** 界面 MUST 显示谱评文案;MUST NOT 出现「将死」类文案

#### Scenario: 两种谱都要出现在样本里
- **WHEN** 测试遍历谱目录
- **THEN** 样本 MUST 同时含「以杀棋收」与「未终局」两类,否则这条断言会在单一类别的样本上恒真

### Requirement: 目录支持多部谱,分组来自数据

古谱入口 SHALL 先列**谱**(六辑残局 + 梅花谱),再进单谱的目录。谱的清单与每谱的局数 MUST 来自服务端,而 MUST NOT 在客户端写死 —— 加一辑是加一份数据文件,前端不改。

一部谱的目录 SHALL 按它自己的分组键排列:梅花谱是「第N局」,而六辑残局没有这一层,所以分组 MUST 允许**只有一层**,而 MUST NOT 为了形状一致给残局编造一个局号。

#### Scenario: 加一辑不动前端
- **WHEN** 服务端多返回一部谱
- **THEN** 谱的列表多一项,前端源码 MUST 无 diff

#### Scenario: 单层目录不塞假分组
- **WHEN** 打开一部没有分组层的谱
- **THEN** 页面 MUST 直接列变化,MUST NOT 出现「第1局」之类由客户端造出来的标题

### Requirement: 学习页渲染任意起始局面,而首帧 MUST 是那个局面

学习页 SHALL 用线路自己的起始局面作为第 0 手的棋盘,而 MUST NOT 从标准开局起。

**判据是首帧的子数等于起始局面的子数** —— 而不是「代码里传了起始局面」。改这条之前学习页把 `RoomState.game.moves` 交给共享棋盘,棋盘**从标准开局重放**,所以一条 10 子的残局会渲染成 32 子加几步棋:**一个看起来完全正常的、错的盘面**。

#### Scenario: 残局的首帧是残局
- **WHEN** 打开一条 10 子的线路,停在第 0 手
- **THEN** 棋盘上 MUST 恰好 10 个子

#### Scenario: 走到末手再回到第 0 手
- **WHEN** 从第 0 手走到末手,再回到第 0 手
- **THEN** 棋盘 MUST 与首帧逐格相同 —— 局面是每帧重建的,不是就地改的

#### Scenario: 两类起始局面都要在样本里
- **WHEN** 测试遍历样本
- **THEN** 标准开局与残局起始局面 MUST 都出现,否则这条断言在单一类别上恒真

### Requirement: 界面 MUST NOT 出现任何「你解对了」的判定

在长将 / 长捉 / 重复局面规则落地之前,界面 SHALL 只做**研习**:显示谱的招法与谱评,而 MUST NOT 出现「解对了 / 解错了 / 你和对了」这类判定,也 MUST NOT 提供「从这里接着自己走」的入口。

理由不是工期:

- 领域里**没有任何重复局面 / 长将 / 长捉规则**,而**正和的定义就在这些规则里**(抽样 30 局里 9 局是正和);
- 没有它们,「和对了」没有机制可判;而守方能用长将逃命 —— 真规则禁止 —— 于是一部分**红胜**的题会看起来解不开;
- `IBoardGameAi.SelectMove` 只收走子历史(六个实现),从残局出发 AI 会按标准开局重建棋盘。

**一个判错的「解对了」比没有判定更糟** —— 它教错棋,而错的样子和对的样子在界面上完全一样。

拆除条件写进 `CLAUDE.md` 的延期表:**长将 / 重复局面规则落地,且 AI 能从给定局面走棋**。

#### Scenario: 研习页没有判定
- **WHEN** 检索学习页模板与 i18n 键
- **THEN** MUST NOT 出现表示「解对 / 解错 / 判定成功」的文案键

#### Scenario: 没有接着走的入口
- **WHEN** 打开一条线路
- **THEN** 页面 MUST NOT 提供任何进入可交互对局的按钮

