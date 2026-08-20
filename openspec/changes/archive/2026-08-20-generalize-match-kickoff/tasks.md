# tasks — generalize-match-kickoff

- [x] 1. `IFirstSeatRules.FirstSeat(MatchState) → int`,与另三个 seam 同一形状。
- [x] 2. `Game` 构造函数收 `firstSeat`;`Room` 在坐满那一刻算它,默认 `Game.FirstSeat`。
- [x] 3. 越界抛 `InvalidFirstSeatException`(code `invalid-first-seat`),**房间留在 Waiting、
      `Game` 仍是 null** —— 一局谁都动不了的棋 MUST NOT 被开出来。
- [x] 4. 十条测试:默认不变、规则能指名、规则看得到设置、开局时历史是空的、三个越界值、
      失败后房间没开局、以及两条注册表走查(没有内置棋种实现它 + 每个内置棋种仍从 0 号开始)。
- [x] 5. **变异三处,两个方向都红**:
      - 忽略 seam(永远 0 号)→ **7 红**;
      - 默认改成 1 号(等价于「把它当必选」)→ **2 红**(`Without_the_seam` 与
        `Every_built_in_game_still_starts_at_seat_zero`);
      - 去掉范围校验 → **4 红**。
      第二条是 `generalize-turn-flow` 给 `NextSeat` 留下的教训:**一个带默认含义的东西,
      只钉一边会让「默认被当成必选」悄悄通过。**
- [x] 6. `dotnet test Gewu.slnx` **1304** 绿(此前 1294,新增 10),五个现有棋种一行不动。

## 走查那两条为什么是两条

`No_built_in_game_picks_its_first_seat_yet` 钉的是「没有人实现这个接口」;
`Every_built_in_game_still_starts_at_seat_zero` 钉的是「于是每一局都从 0 号开始」。
**接口没被实现,和默认没被改坏,是两件事** —— 第二条在上面那个「默认改成 1 号」的变异下会红,
而第一条不会。

第一条的注释里写明了挖坑落地那天把它改成「恰好一个」,与
`Exactly_one_built_in_game_deals_a_setup` 走过的同一条路(那一条也是从「还没有棋种实现它」
改过来的)。**「恰好一个」比「至少一个」有牙**:第二个出现时它会红,而那正是该问
「这两个棋种的先手真是同一种东西吗」的时刻。
