# Tasks — fix-spec-web-shell-pack-count

## 1. 确认漂移的是 spec 而不是代码

- [x] 1.1 菜单选项来自 `header.ts` 的 `options: this.sound.availablePacks()` —— 派生的,是对的。
- [x] 1.2 `en.json` / `zh-CN.json` 里 `header.sound-pack.minimal` 两边都在 —— 也是对的。
- [x] 1.3 所以错的只有两条 requirement 的**枚举文字**。

## 2. 两条 requirement 从枚举改成派生

- [x] 2.1 Scenario「下拉列出全部已注册 pack」:断言改成「menuitem 逐项等于 `availablePacks()`」,
      并明写 requirement MUST NOT 点名 pack、MUST NOT 写下项数,附上理由。
- [x] 2.2 i18n requirement:键清单改成「`label` 加 `BUILT_IN_PACKS` 每个 key 一条」。

## 3. 两条断言 —— 之前一条都没有

- [x] 3.1 `header.spec.ts`:开菜单,断言 menuitem 文本逐项等于
      `sound.availablePacks().map(n => 'header.sound-pack.' + n)`。
      **顺带发现那个 Scenario 从来没有实现**:`header.spec.ts` 之前只按
      `aria-label` 找 trigger 和音量滑杆,从没数过菜单项。
      断言里带 `expect(expected.length).toBeGreaterThan(1)` —— 只有一个 pack 时
      「逐项相等」会退化成几乎不检查什么。
- [x] 3.2 `i18n-parity.spec.ts`:那条点名 `header.sound-pack.minimal` 的用例改成遍历
      `BUILT_IN_PACKS`。**`minimal` 的键之所以存在,就是因为那份手写清单点名要了它** ——
      两份手写清单守着同一个事实,第四个 pack 谁都不管。

## 4. 变异验证

| 改坏什么 | 结果 |
| --- | --- |
| `header.ts` 的 `options` 加 `.slice(0, 2)`(菜单少一个 pack) | RED |
| `en.json` 的 `header.sound-pack.minimal` 置空 | RED |

## 5. 刻意不动

`header.board-skin.midnight` 也在那份手写清单里,同样是枚举。板皮清单是 `BoardSkinService`
的事;顺手改会把这次的边界从「pack 清单只有一份」扩成「所有注册表清单都要派生」,
那是另一件事,该有它自己的理由。
