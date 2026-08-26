# restyle-klotski-board

## Why

用户说「华容道的 UI 太丑了」。**下面四条不是审美意见,是在浏览器里量出来的。**

### 1. 它是唯一没接进棋盘皮肤层的棋盘

把 `data-board-skin` 在 `wood` / `classic` / `midnight` 之间切,读 `.kt-board` 与 `.kt-piece` 的计算背景:

| skin | `.kt-board` | `.kt-piece` |
| --- | --- | --- |
| wood | `srgb(0.908 0.872 0.785)` | `srgb(0.918 0.800 0.710)` |
| classic | **同上** | **同上** |
| midnight | **同上** | **同上** |

**三个皮肤,一模一样。** 原因是 `.kt-*` 用的是 shell 的 `--color-surface` / `--color-text`,而不是 `--board-bg-color` / `--board-bg-image` / `--board-shadow` 那一套。五子棋、象棋、牌桌都接了(`global.css` 与 `card-table.css` 里到处是 `var(--board-*)` / `var(--felt-*)`),而 `web-board-skins` 那条要求列消费者时写的是 `.board-grid` / `.board-stone` / `.xq-*` / `.ddz-*` —— **`.kt-*` 从来不在里面**。

所以玩家换皮肤,华容道纹丝不动。**它看起来不像这个大厅里的棋盘,因为它确实不是。**

### 2. 一个滑块游戏,不滑

`.kt-piece` 的计算值:

```
transition-property: box-shadow, transform
```

**`grid-area` 不在里面**,而且 grid 的行列线本来就不可动画。所以棋子是**瞬移**的。

而 `global.css` 里那段注释写着「The CSS transition is on `grid-area`'s resolved position, which browsers animate as a layout change」—— **这句话是假的**,它描述了一件浏览器不做的事。这正是本仓库那条「机制描述听起来对和量过,只在要紧的时候不一样」。

### 3. 六个棋子,五个同色

第 1 关读出来:张飞 / 马超 / 赵云 / 黄忠 / 关羽 全是 `rgb(248 239 216)`,只有曹操淡淡地偏了一点。**区分角色的只有那两个字。** 而华容道本来就有四类棋子(曹操 2×2、竖将 1×2、关羽 2×1、兵 1×1),它们在盘上的意义完全不同。

### 4. 桌面上是张小卡片

1280 视口下棋盘 **360×450**,而页面容器是 `max-w-md`(448px)。棋盘的 `max-width: 360px` 是写死的。

### 另外:一处规格漂移,顺手记下

`web-board-skins` 现在仍然要求「每个 skin SHALL 有一个 `src/app/core/theme/skins/<name>.ts` token 文件」,而 `drop-board-skin-mirrors` **已经把那个目录整个删了**。规格和代码矛盾着,`validate --strict` 照样绿(它验形状)。这次一并改掉。

## What Changes

- **接进皮肤层。** `.kt-*` 改为消费 `--board-*` 与新增的 `--kt-*` skin 变量。新变量加进 `wood` 块之后,`classic` / `midnight` **不补就 lint 红** —— 这是既有机制(基准取默认皮肤块的变量集),不需要谁记得。
- **让它真的滑。** 棋子定位从 `grid-area` 换成 `transform: translate()`,格子尺寸仍由 grid 算。于是 `transform` 可动画,而它已经在 transition 列表里。尊重 `prefers-reduced-motion`。
- **四类棋子四种面**,而**角色从 `width × height` 推**,不新增字段、不写名单:`2×2 + target` → 曹操;`1×2` → 竖将;`2×1` → 关羽;`1×1` → 兵。
- **桌面放大**:窄屏仍是一列,`sm` 以上棋盘跟着容器长大。
- 出口、合法落点、选中态跟着新的材质重画。

## Non-goals

- **不动任何规则、模型、API 或键盘交互。** 两步交互、合法落点高亮、路径提交、提示,全部按现有要求原样保留。
- 不加新关卡、不改关卡数据格式。
- 不引入图片资源 —— 皮肤层已有的机制是 CSS 渐变与 `--board-bg-image`。
