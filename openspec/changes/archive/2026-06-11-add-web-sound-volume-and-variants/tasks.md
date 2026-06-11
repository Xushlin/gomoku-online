# Tasks: add-web-sound-volume-and-variants

## 1. SoundService 音量

- [x] 1.1 `sound.service.ts`:抽象类加 `volume: Signal<number>` + `setVolume(volume: number)`;`DefaultSoundService` 实现 —— clamp/取整、`gomoku:sound-volume` 持久化、垃圾值 fall back 100
- [x] 1.2 增益应用:`ensureContext()` 用 `(volume/100)²` 初始化 master gain(替换字面量 `1`);`setVolume` 在 ctx 已存在时同步更新 `masterGain.gain.value`
- [x] 1.3 `play()` 早返条件加 `volume() === 0`(与 muted 并列)
- [x] 1.4 Vitest:volume 默认 100 / setVolume clamp(150→100、-5→0、33.7→34)/ 持久化 + 重建恢复 / 垃圾 localStorage 值 fall back / volume 0 早返不建 ctx / 感知曲线 gain=(50/100)²=0.25 / mute↔volume 互不干扰

## 2. `minimal` 音效包

- [x] 2.1 新建 `core/sound/packs/minimal.ts`:sine-only 合成 5 个事件,peak gain ≈ wood 的 50%,时长上限按 spec(move-place ≤ 80ms,单事件 ≤ 400ms),节点全部 `stop(when)` 调度
- [x] 2.2 `DefaultSoundService` 构造函数加 `register('minimal', minimalPack)`
- [x] 2.3 Vitest:availablePacks 含三者 / activate('minimal') 持久化 / 5 个事件各创建 ≥1 个 sine OscillatorNode 接到 masterGain / 静态断言无 square|triangle|sawtooth、无外部资源引用

## 3. `midnight` 棋盘皮肤

- [x] 3.1 `src/styles/board-skins.css` 追加 `[data-board-skin='midnight']` 块:深蓝灰石板面(非纯黑)、淡冷灰网格/星位、黑子高光+亮缘、冷 rim 白子、非红高饱和 last-move ring;无 `.dark` override
- [x] 3.2 新建 `core/theme/skins/midnight.ts` 导出 `midnightSkin: BoardSkinTokens`(镜像 CSS 字面量)
- [x] 3.3 `DefaultBoardSkinService` 构造函数加 `register('midnight', midnightSkin)`
- [x] 3.4 Vitest:availableSkins 含 'midnight' / activate 设置 `data-board-skin` 并持久化
- [x] 3.5 验证扩展点:diff 确认 `Board` 组件与 `header.html` 菜单结构零改动

## 4. Header 音量滑杆

- [x] 4.1 `header.html`:音效皮肤菜单底部加非 `cdkMenuItem` 行 —— label(`header.sound.volume`)+ `<input type="range" min="0" max="100" step="1">`,绑定 `sound.volume()`、`(change)` → `onVolumeChange`,`[attr.aria-label]`,`accent-color: var(--color-primary)`
- [x] 4.2 `header.ts`:`onVolumeChange(value: string)` —— `setVolume(+value)`,未静音时播放一次 `'move-place'` 试听
- [x] 4.3 Vitest(TestBed):拖动/释放调 setVolume 一次且菜单不关 / 未静音释放播放试听、静音不播 / aria-label 存在

## 5. i18n

- [x] 5.1 `public/i18n/en.json` + `zh-CN.json` 同步加 `header.sound.volume`("Volume"/"音量")、`header.sound-pack.minimal`("Minimal"/"极简")、`header.board-skin.midnight`("Midnight"/"午夜")
- [x] 5.2 跑 i18n parity 测试确认两 locale key 集合一致

## 6. 验收

- [x] 6.1 `npm run lint` + `npm test -- --run` 全绿;无新增色值字面量违规(midnight CSS 块与 skins/*.ts 属于 token 定义层,豁免范围与 wood/classic 相同)
- [x] 6.2 手动验证:三档音量试听差异明显、0 静音;midnight 在浅/深两主题下外观一致、375px 下黑子轮廓清晰;minimal 五个事件音量明显低于 wood
- [x] 6.3 `openspec validate add-web-sound-volume-and-variants` 通过
