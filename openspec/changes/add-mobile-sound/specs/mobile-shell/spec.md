## ADDED Requirements

### Requirement: 声音事件集 SHALL 从 web 的 `SOUND_EVENTS` 派生,MUST NOT 手写

手机端的声音事件名 SHALL 与 `frontend-web/src/app/core/sound/sound.tokens.ts` 的
`SOUND_EVENTS` 完全一致,且该一致性 SHALL 由一条读**那份源码**的测试守住。

**手写一份清单会落后于它,而症状是一个事件永远不响** —— 这个仓已经为「手写清单冒充注册表」
付过九次账。这与 `hub_contract_test` 从服务端源码派生 hub 方法名是同一招。

手机端**只会触发其中一个子集**(它只有落子类与走子类棋种),这不是缺陷:事件集是平台级的,
一个棋种播它需要的那些。

#### Scenario: 事件集与 web 一致
- **WHEN** 走查读取 web 的 `sound.tokens.ts`
- **THEN** 手机端的事件名集合与之相等

#### Scenario: web 新增一个事件会让走查红
- **WHEN** web 的 `SOUND_EVENTS` 多了一个名字而手机端没有
- **THEN** 走查失败

---

### Requirement: 音效 SHALL 在设备上合成,MUST NOT 打包音频文件

音效 SHALL 由纯 Dart 代码合成为 PCM 样本,MUST NOT 以音频资产文件的形式随包分发。

理由有两条,第二条是判据上的:

1. web 端**没有音频文件可同步** —— 它的包是 WebAudio 现场合成的,所以「从 web 拉过来」这条
   路在这里不存在;
2. **合成出来的音频是一串数字,因此是可断言的。** 一个打包好的音频文件只能断言它存在;而
   一段生成的 PCM 可以断言它的长度、峰值幅度与**主频**。

因此每个事件的输出 SHALL 可被测试直接检查,而不必播放。

#### Scenario: 落子音是一段短促的可测样本
- **WHEN** 请求 `move-place` 的样本
- **THEN** 样本时长在 100 ms 以内,峰值幅度非零且不削顶,主频落在设计频率的容差内

#### Scenario: 每个事件都有声音
- **WHEN** 遍历事件集里的每一个事件
- **THEN** 每一个都产出非空、非全零的样本

---

### Requirement: 静音 SHALL 是一个开关,而关掉时 MUST NOT 走到播放层

设置页 SHALL 提供声音开关,复用 `header.sound.label` / `header.sound.on` /
`header.sound.off`,MUST NOT 新增翻译键。该选择 SHALL 持久化,与主题、深浅同一个存储。

关闭时,客户端 MUST NOT 调用播放层 —— 而不是「播一个音量为零的声音」。**后者在一台静音的
设备上和前者看起来一样,却仍然会申请音频焦点、打断别人的音乐。**

#### Scenario: 关掉之后不播
- **WHEN** 声音开关是关,发生一次落子
- **THEN** 播放层 MUST NOT 被调用

#### Scenario: 打开之后播
- **WHEN** 声音开关是开,发生一次落子
- **THEN** 播放层被调用一次(否则上一条是因为整条路都断了才成立的)

#### Scenario: 重启后记得
- **WHEN** 关掉声音并重启应用
- **THEN** 声音仍然是关

---

### Requirement: 声音 MUST NOT 参与任何判断,失败 MUST NOT 影响对局

播放音效 SHALL 是即发即忘的:它的返回值 MUST NOT 被等待,它抛出的异常 MUST NOT 传播到调用点。

**一局棋不能因为音频设备忙而下不下去。** 播放层不可用(无设备、被占用、平台不支持)时,
客户端 SHALL 静默地继续。

#### Scenario: 播放失败不影响落子
- **WHEN** 播放层抛出异常
- **THEN** 落子照常完成,屏幕上 MUST NOT 出现错误
