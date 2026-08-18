## Why

关卡产物是**缩进过**提交进仓库的 —— 它要被人肉眼审阅,那是对的。但
`PuzzleLevelSeeder` 用 `JsonElement.GetRawText()` 把它写进数据库,而
`GetRawText()` 返回的是**源文本原样的切片**。于是那份缩进被逐字复制进 `LayoutJson`
列里,再在每一次加载关卡时发给客户端。

实测,不是估算 —— 拿一个真实开发库和一个新建库对比:

| | 存下来的总量 | 最大的一关布局 |
| --- | --- | --- |
| 现状(`GetRawText`) | 75,325 B / 17 关 | 5,618 B |
| 紧凑 | **31,488 B** | **1,854 B** |

同一个构建、同一个端点上的 A/B(旧库 vs 新库):

| 请求 | 现状 | 紧凑 |
| --- | --- | --- |
| `GET /api/games/idiom-crossword/levels/10` | 6,389 B | **2,321 B** |
| `GET /api/games/idiom-crossword/levels/0` | 1,427 B | 539 B |
| `GET /api/games/klotski/levels/0` | 1,621 B | 738 B |

**最重的那一关,玩家每次打开都要等 6.4 kB,其中 4 kB 是缩进。**

## 关键在于「更小」不等于「对」

一个改了内容的压缩是**数据损坏**,而它在体积断言下会显得像成功。所以判据不是字节数,
是 `JsonNode.DeepEquals` 对着产物逐关比 —— 而字节数只是附带确认「真的换了写法」。

## 编码器那一脚有个反直觉的陷阱

`Utf8JsonWriter` 的默认编码器会把**每一个非 ASCII 字符**转义。成语和「曹操」会变成
`\uXXXX` —— 六个字节换一个字符,**比省下的空白还大**,而且数据库浏览器里读不出来。
必须显式用 `JavaScriptEncoder.UnsafeRelaxedJsonEscaping`。

它名字里的 "unsafe" 指的是不转义 HTML 敏感字符(`<` `>` `&` `'` `+`),那在 JSON 被插进
标记语言时才要紧;这份 JSON 以 `application/json` 发出,从不被插进标记。

这一条要**单独一条测试**盯着:换回默认编码器之后,`DeepEquals` 照样绿、「无多余空白」
也照样绿 —— 只有体积会变差。所以体积那条断言在这里不是可选的装饰。

## What Changes

- `PuzzleLevelSeeder` 用 `Utf8JsonWriter` + `Indented = false` +
  `UnsafeRelaxedJsonEscaping` 重新序列化 `layout` / `solution`,取代 `GetRawText()`。
- `puzzle-core` 加一条 requirement:入库形式 MUST 无多余空白、语义 MUST 不变、
  非 ASCII MUST NOT 被转义。
- 五条新测试,三个变异各自能让它们变红。

## Impact

- 受影响代码:`PuzzleLevelSeeder.cs` 一处 + 一个新测试文件。
- **产物文件不动。** 它缩进着提交是对的 —— 它是给人看的;数据库里是给机器发的。
  两者的正确格式本来就不同,而这正是从前那个 bug 的成因:一个函数把两件事当成一件。
- **既有开发库不会自动变紧凑。** seeder 对已有关卡是 no-op(幂等性以
  `(GameKey, LevelIndex)` 判定),所以旧库里那些缩进过的行会留着,直到库被重建。
  这不值得为它写一条数据迁移:本地库随时可以删,而线上没有库。
- 后端 spec 一条 ADDED;前端零改动。
