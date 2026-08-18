## Why

`web-shell` 有两条 requirement 把「有哪些音效 pack」**逐个数出来**,而 pack 从两个变成三个之后,两条都错了:

| 位置 | 写的 | 实际 |
| --- | --- | --- |
| Scenario「下拉列出全部已注册 pack」 | 「menu 列出 `wood` 和 `chiptune` **两项**」 | 三项 |
| Requirement「i18n —— `header.sound-pack.*` 双语对齐」 | `label` / `wood` / `chiptune` | 还有 `minimal` |

**代码是对的** —— 菜单选项直接来自 `this.sound.availablePacks()`,两份 i18n 里 `minimal` 也都在。错的只有 spec。这是 `add-web-sound-minimal-pack` 加第三个 pack 时留下的漂移,和 `add-game-sounds` 刚删掉的那条重复 requirement 同一个源头:那次 `web-sound` 里「哪些 pack 是内置的」有两个答案,其中一个从那天起就是错的。

## 一条被枚举掩盖的空缺

那个 Scenario 的标题是「列出**全部**已注册 pack」,而它的断言是「列出两项」—— 标题说的是派生,断言写的是枚举。而且**这条断言根本不存在**:`header.spec.ts` 从来没有数过菜单项。

`minimal` 的 i18n 键之所以在,是因为 `i18n-parity.spec.ts` 里另一份**手写清单**点名要了它。第四个 pack 的键不会有任何东西要求 —— 那份清单和它守的 spec 一样,是同一个形状。

> 一个数字写死在 spec 里,只会在有人恰好去数的时候被发现是错的。这次是我在给 pack 加事件时顺手数了一下。

## What Changes

两条 requirement 都从**枚举**改成**派生**,并且各配一条真的断言:

- Scenario 改成「menu 列出的项数与顺序逐项等于 `sound.availablePacks()`」,MUST NOT 点名任何 pack。
  `header.spec.ts` 补上这条断言 —— 从 stub 的清单派生,不写死数量。
- i18n requirement 改成「`header.sound-pack.label` 加上 `BUILT_IN_PACKS` 每个 key 一条」。
  `i18n-parity.spec.ts` 里那条点名 `header.sound-pack.minimal` 的用例改成遍历 `BUILT_IN_PACKS` ——
  加第四个 pack 而忘记翻译,现在会红。

这不是新行为,是把两条已经为真的行为写对、并且第一次真的钉住。

## 不动的部分

`header.board-skin.midnight` 也在那份手写清单里,同样是枚举。它不在这次范围内 —— 板皮的清单是 `BoardSkinService` 的事,顺手改会把这次的边界从「pack 清单只有一份」扩成「所有注册表清单」。留着,理由写在这里。

## Impact

- `openspec/specs/web-shell/spec.md`:2 条 MODIFIED。
- 代码:两个 spec 文件各一条断言;**生产代码零改动**,后端零改动。
