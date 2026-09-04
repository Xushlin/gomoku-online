# tasks

## 1. 名单派生

- [x] 走查:`Translations.supported` 的键 == `assets/i18n/*.json` 的文件名(**相等**)。
- [x] 走查:每个 locale 都有 `header.language.<locale>` 文案,两个 locale 都要有。
- [x] **正面对照:从 `supported` 里删掉一个,看走查红。**

## 2. 解析与持久化

- [x] `AppSettings` 第五个字段 `locale`;`PreferencesStore` 存。
- [x] 解析顺序:存过的 → 设备语言(在名单里才算) → `zh-CN`。
- [x] **设备语言只是回退** —— 选过之后不被它覆盖。三条都要测。

## 3. 让整棵树拿到新实例

- [x] 外壳持有可监听的 `Translations`,切换时 `load` 之后整树重建。
- [x] `Provider<Translations>.value` 换成随之更新的那一个。
- [x] 加载期间 MUST NOT 出现空屏或半截翻译。

## 4. 设置页

- [x] 第五组单选,复用 `header.language.*`。**零新增键。**
- [x] 页面又长了一截 —— 既有测试里那个 `reach()`(滚动到可见)要覆盖到新控件。

## 5. 判据

- [x] **widget 测试:切换后 `find.text` 找到的是另一种语言的那句话。**
      这是主判据 —— 只断言存储的版本在渲染路径断掉时最绿(已经犯过两次)。
- [x] 单测:五个轴两两独立(每个方向)。
- [x] 单测:重启后记得。
- [x] **正面对照:让切换只写存储不重载 `Translations`,看屏幕那条红。**
- [x] **正面对照:让设备语言覆盖已存的选择,看那条红。**
- [x] 集成测试:在真外壳里切一次语言,读屏幕。**跑过了,而且第一次是红的** ——
      断言假设了「先中文后英文」,但这一笔之后没存过选择时跟**设备语言**走,
      而测试宿主报 `en-US`,于是应用一开始就是英文的。改成不假设起始语言。

## 6. 不回归

- [x] `flutter analyze` 零问题;`flutter test` 全绿;`shared_sync_test` 绿(零新增键)。
- [x] 既有集成测试跑过(settings / play_a_move / router)。
- [x] Android 构建。

## 7. 收尾

- [x] `JOURNAL.md` 一条;CLAUDE.md 手机端那节的轴数从四改五。
