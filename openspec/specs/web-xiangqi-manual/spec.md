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

