# tasks — add-api-base-url

## 0. 先量

- [x] `openspec/changes/` 里没有别的未归档变更。
- [x] `openspec validate add-api-base-url --strict` 绿。
- [x] 记下改前的前端测试数 —— 「一条不改地通过」要有一个改前的数字才说得清。

## 1. 一个 token,一处默认

- [x] `API_BASE_URL` token,默认 `''`。
- [x] `core/api/` 下所有服务取它做前缀;`GAME_HUB_URL` 的消费者同样。
- [x] **确认调用点是收敛的**:HTTP 只在 `core/api/` 里发是既有硬规则,先 grep 证实,
      别假定它还成立。

## 2. 不变量:Web 端行为不变

- [x] **既有 1042 条测试断言一条不改地通过。** 它们里的 `expectOne('/api/rooms')`
      就是这条不变量的可执行形式 —— 不用新写。
- [x] 变异:把默认值改成 `'http://x'` → 那批精确 URL 断言必须**大面积**红。
      只红一两条说明覆盖面比以为的窄,那本身是要查的事。
- [x] 一条新测试:注入一个非空 base,断言 REST 与 hub **两者**都带上了前缀。
      两个都要 —— 只测 REST 的话,hub 那一半(实时连接)没有任何东西守着。

## 3. 收尾

- [x] `npm run test:ci` + `npm run lint` 绿;初始包变化记下来。
- [x] `JOURNAL.md` 一条。
- [x] 归档 + `validate --all --strict` 绿。
