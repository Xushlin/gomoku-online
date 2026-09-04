# tasks

## 1. 同步产物

- [x] `tool/sync_shared.dart` 加一个 `[data-board-skin='…'](.dark)?` 的解析,
      生成 `lib/theme/skin_tokens.g.dart`。**保留全部声明**,渐变字符串照抄。
- [x] `shared_sync_test` 覆盖它:产物与 CSS 不一致就红。

## 2. 皮肤

- [x] `BoardSkin`:背景 / 格线 / 星位 / 深浅两种棋子 / 棋子描边。
- [x] 从产物取色;**解析不出颜色的值(渐变)MUST 可观测地失败**,不悄悄退回默认。
- [x] 皮肤名单从 `skinTokens.keys` 派生并排序。

## 3. 画笔

- [x] `BoardRenderer.paintDecoration` / `paintOccupants` 收 `BoardSkin`。
- [x] **删掉 renderer 里所有颜色字面量**(棋子黑白、描边、象棋子色)。
- [x] 走查:renderer 源码里不出现 `Color(0x…)`(**只扫代码**)。

## 4. 设置与持久化

- [x] `AppSettings` 第四个字段 `skinName`;`PreferencesStore` 存。
- [x] 设置页一组单选,复用 `header.board-skin.*`。**零新增键。**
- [x] 棋盘从 `ThemeData` 的扩展里取皮肤(view 里 MUST NOT 出现皮肤名 —— 沿用
      `BoardColors` 那条已有的走查)。

## 5. 判据

- [x] 走查:每个皮肤都有两个 locale 的文案(从产物派生)。
- [x] 单测:三个皮肤画出的**背景 / 格线 / 棋子颜色互不相同**(集合大小 == 皮肤数)。
- [x] 单测:四个轴两两独立(**每个方向都测**)。
- [x] 单测:重启后记得皮肤。
- [x] 单测:renderer 里没有颜色字面量(只扫代码)。
- [x] widget 测试:换皮肤之后 `GameBoard` 拿到的颜色真的变了(**判据是画出来的东西**,
      不是存下来的字符串 —— #188 就是这么栽的)。
- [x] **正面对照:让皮肤忽略产物、恒返回同一套色,看「三个皮肤互不相同」红。**
- [x] **正面对照:把一个颜色字面量塞回 renderer,看那条走查红。**
- [x] **正面对照:切皮肤时顺手重置主题,看对应方向红。**

## 6. 不回归

- [x] `flutter analyze` 零问题;`flutter test` 全绿;`shared_sync_test` 绿(零新增键)。
- [x] 既有集成测试逐个跑。
- [x] Android 构建通过(128 s)。
- [ ] **没做:真机安装。** `adb devices` 此刻无设备。

## 7. 收尾

- [x] `JOURNAL.md` 一条;CLAUDE.md 手机端那节改**已有那一行**(它现在说「棋盘颜色不是
      第三个轴」—— 那句话被这一笔推翻了)。
