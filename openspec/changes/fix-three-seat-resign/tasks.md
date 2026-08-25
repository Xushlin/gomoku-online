# tasks — fix-three-seat-resign

## 1. 认输的入口

- [x] 操作条加 `canResign = seatCount === 2`,模板据此渲染认输按钮。
- [x] 判据是 `=== 2` **而不是 `!moreThanTwoSeats()`** —— 后者在描述符缺席时会说「可以认输」。
- [x] 离开与催促不受影响。

## 2. 那个 500

- [x] `ExceptionHandlingMiddleware`:`SeatCountNotSupportedException` 进 409 那一组。
- [x] `dotnet build Gewu.slnx` 0 错误。

## 3. 斗地主的标签

- [x] `DOUDIZHU_TABLE.i18nPrefix`:`game.doudizhu` → `cards.doudizhu`。
- [x] 删掉 `roleLabelKey`(接口 + 两份配置)—— 零读者,而斗地主那份的值在任何前缀下都不存在。
- [x] `card-table-config.ts` 上把「写错一个词的后果」写进注释,并点出钉住它的是哪条测试。

## 4. 测试

- [x] 新增 `card-table-labels.spec.ts`:挂**真的**两份语言文件,判据两条 —— 前缀在两个语言文件里都指向非空子树;叫分那一排上真的画出了语言文件里那句译文(按前缀查出来,不是写死的英文)。
- [x] 操作条:三座位没有认输、两座位有(同一条测试里两头都断言);描述符缺席时也不给。

## 5. 变异

- [x] 前缀改回 `game.doudizhu` → 新测试红 **3 处**,分别点名 `game.doudizhu.no-bid` 缺在 en / zh-CN,以及前缀本身不存在。
- [x] `canResign` 恒真 → 红。
- [x] `canResign` 退化成 `!moreThanTwoSeats()` → **第一次是绿的**,补了「描述符缺席」那条之后红。
- [x] 第一版那条 DOM 断言写成负向的「渲染结果里不含前缀」,而它**恒真**两层:jsdom 下缺失键渲染的不是键本身,而且 `innerText` 在 jsdom 里就是空串。换成**正向**断言(必须出现语言文件里那句译文)才有信号。

## 6. 量出来的东西

- [x] `npm run lint` 0 / `test:ci` **918 绿** / 两个 tsconfig 0 / `build` 0。
- [x] `dotnet build` 0 错误 / `dotnet test` **1482 绿**(311 + 125 + 1046)。
- [x] **真请求确认过**:三人局 `POST /resign` → **HTTP 409** + `ProblemDetails`,`detail` 就是「this room has 3」;而**正面对照**也打了 —— 两人局同一个端点 **HTTP 200**,`{"result":"Decided","endReason":"Resigned"}`。修之前那一发是 500 + 一条未处理异常。
- [x] 初始包 **477.83 kB,一个字节没变** —— 一个 `@if` 和一处字符串。

## 7. 不做的

- [ ] **三家局里「认输」到底算什么** —— 那要点数结算。真正的拆除条件换成:**点数阶梯落地那天**(与 `DoudizhuScoring.Settle` 没有生产调用者是同一件事的两头)。
- [ ] `Gewu.Api.Tests` 不在这次建 —— 它会把中间件的映射表变成可测的,但那是另一个决定。
