# tasks — add-idiom-guess

## 0. 先量,再写

- [x] `openspec/changes/` 里没有别的未归档变更(纪律:上一个游戏没归档不开下一个)。
- [x] `openspec validate add-idiom-guess --strict` 绿。**红了先看 SHALL 在不在第一行。**
- [x] 读 `add-tictactoe`(第二个棋种的定价)与 `generalize-puzzle-rules`(接缝为什么是现在这样)。

## 1. 关卡生成器 —— 泄题规则是它的主要工作

- [x] 离线生成器,**显式种子**,产物可复现(与成语纵横同规矩:同种子同产物,测试钉住)。
- [x] **被挖的字 MUST NOT 出现在该条释义里。** 这是本变更的核心判据 —— 2,914 条(23%)
      按天真做法会把答案印在题面上。
- [x] 剔除释义里直接含整条成语的 51 条。
- [x] **正面对照**:关掉泄题规则重跑,MUST 生成出至少一条「答案印在题面上」的题 ——
      否则那条规则可能根本没生效,而两种产物都「看起来像题」。
- [x] 难度由数据给:tier × 挖几个字。12 关,难度 1–12。
- [x] 产物 `backend/data/levels/idiom-guess.json` 提交进仓库,启动时幂等载入。

## 2. 规则实现 —— 而验收判据是改动面

- [x] `IdiomGuessRules : IPuzzleRules`,四个方法。
- [x] `Validate`:N 条全对才算通关。
- [x] `CheckPartial`:对了回出处,**错了 MUST 回 `null`** —— 答错时附带任何内容都是借错误路径泄题。
- [x] **出处缺失要能过**:池子里 252 条没有出处,载荷 MUST 容忍它不存在,MUST NOT 抛。
      写一条专门的测试,用一条真的没出处的成语。
- [x] `Hint`:揭一个空格;缺省或解析不了 MUST 退化到合理默认,MUST NOT 抛。
- [x] `Score`:错误 + 提示 → 1–3 星。
- [x] 一处 DI 注册。**没有加 `GameKeys` 常量** —— 那个类的文档写着它是「平台内置**棋种**的键」,
      而两个既有关卡游戏都在自己的规则类里写字面量。提案里写的是加常量,照着本地惯例改了。
- [x] **头号验收:`git diff --name-only` 里 MUST NOT 出现 `backend/src/Gewu.Domain/Puzzles/`
      下任何既有文件。** 这条是 `puzzle-core` 自己写下的判据,不是我定的。
- [x] 变异:关掉泄题规则 → 生成器那条红;`CheckPartial` 答错也回载荷 → 那条红;
      `Validate` 只查第一条 → 通关那条红。**每条都要看到红,且确认 build 0 error。**

## 3. Web

- [x] `/g/idiom-guess` 懒加载:关卡列表(星级 / 最好用时 / 锁定)+ 关卡页。
- [x] **客户端不持有答案,也不自己计分** —— 与成语纵横同一条。
- [x] 答对显示出处纸条;**没有出处的那些不显示空纸条**。
- [x] manifest `status` → `'available'` + `launchRoute`。平台不变量:available ⇒ launchRoute 非空。
- [x] `game-emblem.ts` 里「Used by exactly two games」改掉 —— 已经三个了。
- [x] i18n 两个 locale;成语内容保持中文(`contentLocales: ['zh-CN']` 已经声明)。
- [x] `npm run test:ci` + `npm run lint` 绿;初始包变化记下来(预算 480 kB)。

## 4. 浏览器里真打一关

- [x] 用**最长的真实内容**量 375 px:释义 p95 是 41 字、最长 **74 字**,拿 74 那条打。
      空数据通过一切布局断言 —— 这仓库四次溢出缺陷里三次是空数据下看不见的。
- [x] 答对一条看出处纸条;答错一条看反馈;用一次提示看星级掉。
- [x] 记得窗格不合成:点完要 `window.ng.applyChanges(...)` 再读 DOM;
      自动消失的提示读**信号**而不是 DOM。

## 5. 文档

- [x] `JOURNAL.md` 一条。**要写清它没证明什么** —— 同家族的第二个实现推翻不了接缝的假设。
- [x] `CLAUDE.md`:游戏数从九改十;类别表里 猜成语 去掉 (planned)。**数目录,别信写着的数。**
- [x] 归档 + `validate --all --strict` 绿。
