## MODIFIED Requirements

### Requirement: i18n —— `lobby.*` 翻译树同步扩充

`public/i18n/en.json` 与 `public/i18n/zh-CN.json` SHALL 同步新增 `lobby.*` 键集合:

- `lobby.hero.{welcome, online-count-label, online-count-empty}`
- `lobby.rooms.{title, create-button, empty, loading-retry, join, watch, status-waiting, status-playing, status-finished, seat-black, seat-white, seat-empty, host, spectators}`
- `lobby.my-rooms.{title, empty, resume, you-are-black, you-are-white, you-are-spectator}`
- `lobby.leaderboard.{title, empty, rank, rating, wins, losses, draws, tier-gold, tier-silver, tier-bronze}`
- `lobby.create-room.{dialog-title, name-label, name-placeholder, submit, submit-loading, cancel}`
- `lobby.create-room.errors.{min-length, max-length, whitespace-only, generic, network}`
- `lobby.errors.{generic, network, retry}`
- `lobby.placeholder.{coming-soon, leave-room, room-not-found, back-to-lobby}`

两份 JSON 的 flattened key 集合 MUST 完全相等。

#### Scenario: 键集合一致
- **WHEN** 对比 flattened 后的 `en.json` 与 `zh-CN.json`
- **THEN** 差集为空

#### Scenario: 模板零硬编码
- **WHEN** 在 `src/app/pages/lobby/**/*.html` 与 `src/app/pages/rooms/**/*.html` 中搜索 CJK 字符或 ≥ 3 字母的显示英文字符串
- **THEN** 0 匹配(技术 test-id 等非展示字符串除外)
