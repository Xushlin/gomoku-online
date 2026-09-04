## ADDED Requirements

### Requirement: 棋盘皮肤 SHALL 是第四个独立的轴,而皮肤名单 SHALL 从同步产物派生

设置页 SHALL 让人选棋盘皮肤,并持久化到与主题、深浅、声音同一个存储。

可选皮肤 SHALL 从同步产物 `skinTokens` 的键派生,MUST NOT 在页面或注册表里手写一份名单。
该产物由 `tool/sync_shared.dart` 从 web 的 `styles/board-skins.css` 生成、由
`shared_sync_test` 钉住 —— **「手写清单假装成注册表」是这个仓修过九次的缺陷**,而三个皮肤名
看起来足够稳定,正是它容易再犯一次的地方。

皮肤 SHALL 与主题、深浅、声音**互相独立**:切换任何一个 MUST NOT 改变另外三个。

#### Scenario: 每一个皮肤都有名字
- **WHEN** 走查遍历 `skinTokens` 的键
- **THEN** 每一个 MUST 有 `header.board-skin.<key>` 的文案,两个 locale 都要有
- **AND** 这条走查 MUST 从产物派生 —— web 加一个皮肤同步过来时它 MUST 红,而不是
  页面上多一个渲成原始键的选项

#### Scenario: 四个轴互不干扰
- **WHEN** 切换皮肤
- **THEN** 主题名、深浅、声音开关 MUST 都不变
- **AND** 反方向同样 MUST 成立:切主题 MUST NOT 换掉皮肤

#### Scenario: 选择留得住
- **WHEN** 选好皮肤之后重启应用
- **THEN** MUST 仍然是那个皮肤

---

### Requirement: 棋盘的颜色 SHALL 全部来自皮肤,renderer 里 MUST NOT 写死颜色

棋盘背景、格线、星位、两种棋子及其描边的颜色 SHALL 全部取自当前皮肤。

任何 `BoardRenderer` 的实现 MUST NOT 内联颜色字面量 —— 在这一笔之前棋子的黑白是
`0xFF1A1A1A` / `0xFFF5F5F5` 两个写死的值,而**写死的颜色正是皮肤存在的理由**:它们让
「换了皮肤」变成「背景变了、棋子没变」。

这条 SHALL 由一条读 renderer 源码的走查守住,且该走查 MUST 只扫代码不扫注释 ——
一个会命中「解释这条规则的那句注释」的检查比没有更糟。

#### Scenario: 换皮肤,棋子也跟着换
- **WHEN** 皮肤从一个换到另一个
- **THEN** 棋盘背景、格线与棋子颜色 MUST 都随之改变

#### Scenario: renderer 里没有颜色字面量
- **WHEN** 走查扫描 `board_renderer.dart` 及各棋种的 renderer(只扫代码)
- **THEN** MUST NOT 出现 `Color(0x…)` 字面量

---

### Requirement: 皮肤搬的是调色板,渐变纹理 MUST NOT 被声称一致

web 的皮肤块里有多层 CSS 渐变(木纹、暗角、交叉纹理)。Flutter 端 MUST NOT 声称与之像素级
一致,SHALL 只保证**颜色值**取自同一份产物。

产物 SHALL 完整保留每个皮肤的全部声明(包括渐变字符串),而 Dart 端 SHALL 只消费其中
**可解析成颜色**的那些;哪些被消费 SHALL 是显式且被测的,MUST NOT 是「碰巧能用的那些」。

#### Scenario: 渐变值同步过来但不假装能画
- **WHEN** 产物里某个皮肤的某个变量是渐变字符串
- **THEN** 它 MUST 出现在产物里(否则漂移无从检测)
- **AND** Dart 端解析它为颜色 MUST 失败得可观测,而不是悄悄退回一个默认色

## MODIFIED Requirements

### Requirement: 主题与深色模式 SHALL 可切换,而主题列表 SHALL 从同步产物派生

设置页 SHALL 让人选主题与深浅色,并持久化到本地。

可选主题 SHALL 从 `themeTokens` 的键派生,MUST NOT 在页面里手写一份名单。那份产物由 `tool/sync_shared.dart` 从 web 同步、由 `shared_sync_test` 钉住;**「手写清单假装成注册表」是这个仓库修过八次的缺陷**,而四个主题名字看起来足够稳定,正是它容易再犯一次的地方。

主题与深浅 SHALL 是**两个正交的轴**,MUST NOT 合并成一个八选一的列表 —— 与 web 端同一个模型。

#### Scenario: 每一个主题都有名字
- **WHEN** 走查遍历 `themeTokens` 的键
- **THEN** 每一个 MUST 有 `header.theme.<key>` 的文案,两个 locale 都要有
- **AND** 这条走查 MUST 从 `themeTokens` 派生 —— 下次 web 加一套主题同步过来,
  它 MUST 红,而不是页面上多一个渲成原始键的选项

#### Scenario: 两个轴各自独立
- **WHEN** 切换深色模式
- **THEN** 主题名 MUST 不变;反之切换主题时深浅 MUST 不变
- **AND** 两个方向 MUST 同时被测:少了任何一半,一个「切一个就重置另一个」的实现
  都能通过剩下那半

#### Scenario: 选择留得住
- **WHEN** 选好之后重启应用
- **THEN** MUST 仍然是那个主题和那个深浅
- **AND** MUST NOT 存进放刷新令牌的那个安全存储 —— 主题名不是秘密

#### Scenario: 棋盘颜色 SHALL 由皮肤决定,而不再由主题决定
- **WHEN** 主题改变
- **THEN** 棋盘的颜色 MUST NOT 因此改变 —— 它由皮肤那一个轴决定
- **AND** 这条**取代**了原先「棋盘颜色跟着主题走,而手机端 MUST NOT 另建皮肤轴」那一句:
  那句话写着它自己的拆除条件(「哪天需要独立皮肤轴」),而需求把它触发了。
  **一条被推翻的 live 要求 MUST 被改写,MUST NOT 留着与代码相反** —— 这个仓为
  「live 规格与已发布代码正好相反」付过 36 个提交的账。
