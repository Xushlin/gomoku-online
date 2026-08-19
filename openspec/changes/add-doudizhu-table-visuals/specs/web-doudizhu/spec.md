# web-doudizhu 的规格变化

## ADDED Requirements

### Requirement: 牌桌画成一张桌子,牌画成牌

`CardTable` SHALL 把局面画成一张环绕的牌桌:一块 felt 桌面,我在下方,另两家在上方两侧,
每家显示头像 / 用户名 / 剩余张数 / 地主标 / 该谁走的高亮。对家的手牌 SHALL 画成**牌背**叠,
张数看得见而牌面看不见 —— 这与服务端逐张裁剪过的事实一致,客户端手上本来就没有那些牌。

一张牌 SHALL 是圆角纸面 + 角标(点数 + 小花色)+ 一个大花色,红黑由花色决定。

**牌面 MUST NOT 用整张牌的位图。** 54 张定死的位图既不跟 app 主题、也不跟棋盘皮肤,而这个仓库
的硬规则是组件里不许写死颜色。判据与 `add-web-xiangqi` 给象棋棋子的那一条相同,连约束一起继承:
**皮肤挑的是深浅,不是色相。** 纸面、边框、角标、牌背、桌面 SHALL 全部走皮肤 token;
**花色的色相是这个游戏的身份**(♥ MUST 是红的),因此花色形状用素材图,而 MUST NOT 由皮肤改色相。

花色图的路径 SHALL 由组件绑成 `--ddz-pip`(`games/doudizhu/card-art.ts`),而样式表 MUST NOT 再写
一份。**这个位置是量出来的,不是选出来的:** 路径写在 CSS 里更符合「CSS 是绘制权威」,但这个仓库的
测试构建**没有 .png 的 loader** —— 绝对路径报 `Could not resolve`,相对路径报
`No loader is configured for ".png"`,两次都让整个测试构建失败。代价是一个 `[style.--ddz-pip]` 绑定
若被清洗掉花色会**静静地不见**,所以 MUST 有一条断言读 inline style 里的 `url(`。
图片放 `public/`(原样拷贝的静态资源,不进打包器),而「这条路径指着一个真存在的文件」由一条走遍
**全部 54 个编码**的测试钉住,它用**惰性** `import.meta.glob` 只取键名、不加载模块。

牌桌的样式表 SHALL 是**组件自己的**,而 MUST NOT 注册进 `angular.json` 的全局样式 —— 全局样式首屏
就要下载,而牌桌只在斗地主房间里画。量到的是:放全局时初始包 474.16 → 484.83 kB,480 kB 的预算
当场报警;搬进组件后是 479.66 kB,且 `anyComponentStyle` 那条 4 kB 的预算也没红。

手牌 SHALL 重叠成扇形,重叠步长由张数决定,使**任意张数在 375 px 下都不横向溢出**:
`--step` 取"一张牌宽的固定比例"与"(容器宽 - 一张牌宽) / (张数 - 1)"中的较小者。张数是快照里
已有的数字,不是规则。

#### Scenario: 对家只看得到牌背
- **WHEN** 渲染另两家
- **THEN** MUST 出现牌背元素,数量等于 `handCounts[seat]` 的可视表示;MUST NOT 出现任何对家的牌面

#### Scenario: 满手牌在 375 px 下不溢出
- **WHEN** 视口 375 px,手上 20 张牌
- **THEN** 页面横向 `overflow` MUST 为 0(此断言 MUST 在**手牌非空**时测量 —— 空手牌下它必然通过)

#### Scenario: 皮肤换深浅,不换花色色相
- **WHEN** 切换任一已注册棋盘皮肤或明暗模式
- **THEN** 纸面 / 边框 / 桌面 MUST 随之变化;♥ / ♦ MUST 仍是红色

### Requirement: 发牌与出牌有动作,而动作由牌的身份驱动

发牌 SHALL 表现为牌从扇形中心散开到各自位置(逐张错开),出牌 SHALL 表现为那手牌从**出牌人的
方位**飞向桌心。

实现 MUST 是 CSS keyframes,MUST NOT 是 JS 动画,也 MUST NOT 依赖计时器或"动画放过了吗"这类状态。
机制是:手牌与桌面牌都以**牌的编码**为 `track` 键,所以一张牌的 DOM 节点只在这张牌第一次出现时
被创建,而 `animation` 写在牌上就恰好在那一刻放一次 —— 之后重排、别人出牌、快照刷新都 MUST NOT
重播。**同一个事实(牌的身份)同时驱动 DOM 与动画,于是没有第二个东西需要被记得去重置。**

散开的几何 SHALL 只用 `--i`(第几张)、`--n`(共几张)、`--step`(重叠步长)三个 CSS 变量算出,
MUST NOT 量 DOM。

出牌方位 SHALL 由纯函数 `relativeSeat(seat, mySeat, total)` → `'self' | 'left' | 'right'` 给出,
三个值各对应一组 CSS 起点变量。**下家在右手边**(出牌逆时针,俯视时下方的逆时针下一位在右)。
它 MUST NOT 量 DOM:桌子换了摆法,方位仍然对。

`prefers-reduced-motion: reduce` 下**全部动画 MUST 关闭**。一桌 20 张牌加上出牌是这个 app 里
动得最多的一屏,这不是可选项。

#### Scenario: 一张牌只在到手时动一次
- **WHEN** 手上已有的牌因为别人出牌而收到新快照
- **THEN** 已在屏幕上的牌 MUST NOT 重新播放入场动画;新到手的牌(如抢到地主后进手的底牌)MUST 播放

#### Scenario: 出牌从出牌人的方位飞来
- **WHEN** `tableSeat` 是我的下家 / 上家 / 我自己
- **THEN** 桌面那手牌 MUST 分别带 `right` / `left` / `self` 三组起点之一

#### Scenario: 降低动效时不动
- **WHEN** `prefers-reduced-motion: reduce`
- **THEN** 发牌与出牌 MUST 无位移动画(牌直接就位)

### Requirement: 每家身边显示当前一轮的动作,而这一轮是算出来的

`table-layout.ts` SHALL 提供纯函数 `currentTrick(moves)`,返回当前一轮里每个座位的动作
(`bid` 叫分 / `pass` 不要 / `play` 出牌),牌桌把 `pass` 与 `bid` 显示在对应座位旁边。

**这一轮是算出来的,不需要任何规则知识:从最后一手 `play:` 起到末尾的那一段就是当前一轮** ——
桌上那手牌就是最后一手非 pass 的出牌,它之后只可能是 pass。叫分阶段取全部 `bid:`。
新一轮开始时上一轮的「不要」自己消失,因为那一段的起点前移了。

它 MUST NOT 用于任何判断:要压的那手牌仍然只认服务端的 `tableCards`。这是把**已经公开的
`moves`** 换一个位置显示,而不是在客户端重建局面。

#### Scenario: 新一轮清掉上一轮的不要
- **WHEN** 一手新的 `play:` 追加到 `moves` 之后
- **THEN** 上一轮的 `pass` MUST NOT 再显示

#### Scenario: 叫分阶段显示叫了几分
- **WHEN** `moves` 是 `bid:2` / `bid:0`
- **THEN** 两个座位旁 MUST 分别显示「叫 2 分」与「不叫」

### Requirement: 扇形的重叠公式要求容器有确定宽度

`.ddz-fan` 的重叠步长 SHALL 取「一张牌宽的固定比例」与「容器正好装得下」中的较小者,并 SHALL 带一个
下限(如 `2px`)。**装着扇形的容器 MUST NOT 是 shrink-to-fit**(`align-items: flex-end` / `center`、
或任何让它按内容定宽的写法)。

原因是量出来的,而且踩了三次:那个「装得下」的表达式里有 `100%`,而 shrink-to-fit 的容器宽度尚未
定下来,于是它解算成 0 —— 步长先变成**负数**(牌背反向叠,`scrollWidth 18 > clientWidth 0`),
加了下限之后又被压到 2px(17 张牌挤成 50px 的一条)。**只有当扇形的宽度就是轨道宽度时,
「装得下」才有意义。**

同理,发牌动画的横向散开量 MUST NOT 引用那个带 `100%` 的变量:**百分比是在用它的地方解算的** ——
在 `margin-left` 里对着容器,而在 `transform: translate()` 里对着元素自己,`(34px - 34px) / 16 = 0`,
于是整段横向位移静静地变成 0,牌只往下掉不散开,而动画照样在放。它 SHALL 用一个只由长度构成的变量。

jsdom 没有排版引擎,量不到这两件事;所以源码级的那一半 SHALL 由 `scripts/check-styles.mjs` 钉住
(那段 keyframe 里不许出现带百分比的那个变量),而几何的那一半只能在真浏览器里量。

#### Scenario: 任意张数在 375 px 下都不溢出
- **WHEN** 视口 375 px,手上 20 张牌、两家各 17 张牌背、桌上一手牌
- **THEN** **没有任何元素** `scrollWidth > clientWidth`(页面级的 `scrollWidth - clientWidth === 0`
  **不够** —— 三次溢出里有两次在页面级检查下是 0)

#### Scenario: 散开量不是 0
- **WHEN** 发牌动画的第一帧
- **THEN** 第一张牌的 `transform` 横向位移 MUST NOT 是 0

### Requirement: 侧栏在座位多于两个时列出每一个人

房间侧栏的座位名单 SHALL 在 `seats.length > 2` 时逐个列出 `seats`(座位号 + 用户名),而不是只列
「黑方 / 白方」。

**这是同一个缺陷的第二处。** `add-web-doudizhu` 把「轮到谁」从「白方走棋」改成了座位号,却没看
旁边这份名单:一桌三个人时它只列黑白两个,**2 号座位上的人在自己的房间里根本不出现**。
判据仍是 `seats.length`,MUST NOT 是棋种键。

#### Scenario: 三座位列三个人
- **WHEN** `seats.length === 3`
- **THEN** 三个用户名 MUST 都出现;「黑方 / 白方」两个标签 MUST NOT 出现

#### Scenario: 两座位一字不变
- **WHEN** `seats.length === 2`
- **THEN** 仍然是「黑方 / 白方」两行
