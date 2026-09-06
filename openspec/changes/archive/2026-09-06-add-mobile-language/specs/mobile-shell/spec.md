## ADDED Requirements

### Requirement: 可选语言 SHALL 从同步的文案产物派生,MUST NOT 手写一份名单

可选语言 SHALL 由 `assets/i18n/` 下的 locale 文件决定,MUST NOT 在页面或常量表里手写。

`Translations.supported` 目前是一张手写的常量表;它 SHALL 由一条走查钉住,断言它的键集合与
那个目录里的文件名集合**相等**(不是包含)。**「手写清单假装成注册表」是这个仓修过九次的
缺陷**,而两个语言看起来足够稳定,正是它容易再犯一次的地方。

#### Scenario: 名单与产物一致
- **WHEN** 走查列出 `assets/i18n/*.json` 的文件名
- **THEN** `Translations.supported` 的键集合与之**相等**
- **AND** 每一个都 MUST 有 `header.language.<locale>` 的文案,两个 locale 都要有

#### Scenario: web 新增一个 locale 会让走查红
- **WHEN** 同步进来一个新的 locale 文件而常量表没跟上
- **THEN** 走查 MUST 失败,而不是设置页里少一行

---

### Requirement: 语言 SHALL 按「存过的 → 设备语言 → 默认」解析,而设备语言只是回退

启动时的语言 SHALL 按顺序解析:

1. 本地存过的选择;
2. 否则设备语言,**当且仅当它在可选名单里**;
3. 否则 `zh-CN`。

人**选过之后 MUST 以人的选择为准**:设备语言变化 MUST NOT 覆盖它。设备语言是没有选择时的
回退,不是一个持续生效的来源。

#### Scenario: 没选过时跟设备走
- **WHEN** 没有存过语言,设备语言是 `en`
- **THEN** 应用以 `en` 启动

#### Scenario: 设备语言不在名单里就用默认
- **WHEN** 没有存过语言,设备语言是一个未支持的 locale
- **THEN** 应用以 `zh-CN` 启动,MUST NOT 显示原始键

#### Scenario: 选过之后设备语言不再有发言权
- **WHEN** 用户选了 `zh-CN`,而设备语言是 `en`
- **THEN** 应用仍然是 `zh-CN`

---

### Requirement: 切换语言 SHALL 改变屏幕上的字,而判据 MUST NOT 是存下的字符串

切换语言 SHALL 让**已经在屏幕上的界面**改用新语言,不需要重启,也不需要离开当前页。

判据 SHALL 是渲染出来的文本:一条测试 SHALL 在切换前后各查一次同一处界面文案,并要求它
从一种语言变成另一种。

**这一条写成这样是因为同一个错误犯过两次**:主题那一笔把选择存得完美而屏幕纹丝不动;棋盘
颜色那一笔的断言问的是 token 袋而不是屏幕。**一个只断言存储的判据,在整条渲染路径断掉时最绿。**

语言 SHALL 与主题、深浅、声音、皮肤互相独立:切换任何一个 MUST NOT 改变其余四个。

#### Scenario: 切换之后屏幕上的字变了
- **WHEN** 在设置页把语言从 `zh-CN` 换成 `en`
- **THEN** 当前屏幕上的标题变成英文
- **AND** 该断言 MUST 查渲染出来的文本,MUST NOT 只查存下来的 locale 名

#### Scenario: 五个轴互不干扰
- **WHEN** 切换语言
- **THEN** 主题、深浅、声音、皮肤 MUST 都不变;反方向同样成立

#### Scenario: 选择留得住
- **WHEN** 选好语言之后重启应用
- **THEN** MUST 仍然是那个语言
