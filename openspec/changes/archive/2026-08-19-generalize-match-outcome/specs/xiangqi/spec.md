# xiangqi Specification Delta

## MODIFIED Requirements

### Requirement: 将死与困毙都判负

一步棋走完后，若**对方没有任何合法走法**，本方 MUST 获胜：

- 对方被将军且无法解将（**将死**）；
- 对方未被将军但无子可动（**困毙**）。

象棋与国际象棋在这里不同:**困毙判负,不是和棋**。

结果 MUST 是 `GameResult.Decided`,且 `MoveApplication.WinnerSeat` MUST 是**走子方的座位号**,
由聚合根写入 `GameEndReason.Decided` 与对应的 `WinnerUserId`。

此前这里写的是 `GameResult.BlackWin` / `WhiteWin`,由 `side == Stone.Black ? BlackWin : WhiteWin`
算出 —— 那个颜色恒等于 `side`,即规则把自己的入参重新说了一遍。

#### Scenario: 将死
- **WHEN** 一步将军之后对方无任何合法走法
- **THEN** `Apply` 返回 `(Decided, WinnerSeat: 走子方座位)`

#### Scenario: 困毙同样判负
- **WHEN** 对方未被将军但没有任何合法走法
- **THEN** `Apply` 返回 `(Decided, WinnerSeat: 走子方座位)` —— MUST NOT 是和棋
