# puzzle-core Specification Delta

## ADDED Requirements

### Requirement: 关卡产物入库时去掉多余空白,而语义一字不变

`LayoutJson` 与 `SolutionJson` 入库时 SHALL 是**紧凑**的 JSON:结构部分 MUST NOT 含多余空白。
入库文本与产物中对应的值 MUST 语义相同(`JsonNode.DeepEquals` 为真)—— 这是重排版,MUST NOT 是转换。

产物文件本身**保持缩进**提交。那份是给人审阅的,数据库里那份是给机器下发的,两者的正确格式
本来就不同。从前的缺陷正是把这两件事当成了一件:seeder 用 `JsonElement.GetRawText()`
取值,而它返回**源文本原样的切片**,于是产物的缩进被逐字复制进列里,再在每次加载关卡时发出去。
实测一个真实开发库:存下来的字节 **58% 是空白**,最重的一关从 6,389 B 降到 2,321 B。

非 ASCII 字符 MUST NOT 被转义。`Utf8JsonWriter` 的默认编码器会把每个非 ASCII 字符写成
`\uXXXX` —— 六字节换一个字,**比省下的空白更大**,而且入库文本在数据库浏览器里不可读。
必须显式选一个不转义非 ASCII 的编码器。

这两条 MUST 各有断言,而且**体积那条不是装饰**:把编码器换回默认值之后,「语义相同」与
「无多余空白」都仍然成立,只有体积会变差 —— 只测语义的话,那是一次全绿的退步。

#### Scenario: 入库文本与产物语义相同
- **WHEN** 用一份**缩进过**的产物灌库,取出该关的 `LayoutJson` / `SolutionJson`
- **THEN** 两者与产物中对应的值 `JsonNode.DeepEquals` 为真

#### Scenario: 结构部分没有多余空白
- **WHEN** 把入库文本里的字符串字面量挖掉,只看结构部分
- **THEN** MUST NOT 含换行或连续空格

#### Scenario: 中文以字符存在
- **WHEN** 产物里含中文(成语、棋子名)
- **THEN** 入库文本里它们仍是字符,MUST NOT 出现转义码点

#### Scenario: 紧凑一定比转义小
- **WHEN** 比较入库文本与「把同一个值按转义非 ASCII 的方式序列化」的结果
- **THEN** 入库文本 MUST 更短
