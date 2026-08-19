# Tasks — add-room-seats

## 1. 座位成为集合

- [x] 1.1 `RoomSeat(RoomId, Index, UserId)`,主键 `(RoomId, Index)`,外加 `(RoomId, UserId)` 唯一索引。
- [x] 1.2 `Room._seats` + `Seats`(按 `Index` 升序)+ `PlayerAt(index)`;`SeatOf` 改读集合。
- [x] 1.3 `BlackPlayerId` / `WhitePlayerId` 变成**派生**读法。87 处调用点一行未动 ——
      而这不是镜像:镜像是两份能各自漂移的存储,派生只有一份。
- [x] 1.4 `Create` 让 host 坐 0 号;`SwapPlayers` 换的是 0 号与 1 号两行。

## 2. `JoinAsPlayer` 收规则

- [x] 2.1 签名加 `IGameRules rules`,与 `PlayMove` 一致。**座位数不存在 `Room` 上** ——
      存一份就是规则事实的第二份副本,而它错了的表现("永远开不了局"/"少一个人就开局")
      不会有人立刻发现。
- [x] 2.2 坐满才开局;没坐满留在 `Waiting`。两人棋种下逐步等价。
- [x] 2.3 两个生产调用点(`JoinRoom`、`CreateAiRoom`)注入 `IGameRulesRegistry`,
      未知棋种的处理与落子路径一致(那是损坏的房间记录,不是非法加入)。
- [x] 2.4 26 处测试调用点补上参数。

## 3. 查询

- [x] 3.1 五处取房间的路径全部 `.Include("_seats")`。
- [x] 3.2 「这人参与的房间」从 `BlackPlayerId == u || WhitePlayerId == u` 改成查 `RoomSeats` 表。
      **不能写 `r.Seats.Any(...)`** —— `Seats` 是派生属性,EF 翻不成 SQL:要么运行时抛,
      要么静静退化成客户端求值,把整张 Rooms 拉进内存再过滤。后者不报错,只在数据变多的
      那天变慢,而那时没人记得这行改过。
- [x] 3.3 **「当前回合是 bot 的房间」这条查询改完更简单了。** 此前 JOIN 两次 `Users`
      (黑方一次、白方一次)+ 两个分支各写一遍;现在一次 JOIN + `s.Index == g.CurrentTurn`,
      座位数从查询里消失了。那两个分支正是三座位下要加第三个的形状。

## 4. 迁移

- [x] 4.1 `AddRoomSeats`:**先建表 → 再回填 → 最后删两列**。
- [x] 4.2 **EF 生成的版本两处错、两处都不报错**,所以两个方向都手写:
      - 它**先删列再建表**,回填无从下手(它自己提示了 "may result in the loss of data",
        而生成的代码对此什么都没做);
      - 它的 `Down` 用 `defaultValue: Guid.Empty` 把 `BlackPlayerId` 加回来 —— 每个房间的
        黑方变成空 GUID。同 `AddRoomGameKey` 的 `defaultValue: ""` 与
        `DropUserRatingColumns` 的 `defaultValue: 0`。
- [x] 4.3 回填只为非空的 `WhitePlayerId` 建 1 号座位(空座位不存行,而 `UserId` 非空)。
- [x] 4.4 `Down` 把数据搬回列里再删表;没有 0 号座位的房间会让 `UPDATE` 写 NULL 进非空列而失败 ——
      那是想要的。

### 4.5 提案说要两个迁移(expand → contract),实际做成了一个,理由记在这

`add-per-game-rating` 的 expand→contract 有两个好处:把风险最大的数据搬动单独落一次,
以及**让测试能停在中间站观察回填**。这里第一条不适用(没有部署,读者在同一个 PR 里全改了),
而第二条不需要拆:**回填从目的地一侧就可观察** —— 在上一个迁移点按旧形状造数据、跑迁移、
断言 `RoomSeats` 的内容。拆开只会多一份要手写 Designer 的成本,换不到新的可观测性。

## 5. 测试

- [x] 5.1 `RoomSeatsTests`(8 条):host 坐 0 号;两人满座开局;**三人棋种坐第二个人仍 Waiting**;
      **三人轮转走满一圈 `0→1→2→0`,走的是真聚合**;满座后拒人;重复入座按座位报错;
      `SwapPlayers` 换前两个座位;座位按号升序。
- [x] 5.2 `RoomSeatsMigrationTests`(4 条):两个玩家 → 0/1 号;等人的房间只有 0 号;
      回滚搬回真人而不是空 GUID;回滚后等人的房间 white 仍为 NULL。
- [x] 5.3 上一个变更只能用假规则证明取模算术(2 号座位没人坐得下)。**现在坐得下了。**

## 6. 变异验证 —— 其中一条揪出了一个真空缺

| 改坏什么 | 结果 |
| --- | --- |
| 迁移退回 EF 的顺序(先删列再建表) | RED |
| 回填不带 `WHERE`,空座位也插一行 | RED |
| `Down` 不把数据搬回来(= EF 生成的那一版) | RED |
| 坐满两个就开局,不问规则要几个座位 | RED |
| **取房间时不带上座位** | **第一次是 GREEN** |

最后一条:把五处 `.Include("_seats")` 全删掉,整个 Infrastructure 套件**还是绿的**。
而那种状态下,任何一次加载房间再读 `BlackPlayerId` 都会抛 `Single()` —— 座位集合是空的。
也就是说**当时没有任何测试加载过一个房间再读它的座位**。

补了 `RoomRepositorySeatsTests`(4 条,打真 SQLite):`FindById` 带回座位、等人的房间只有 0 号、
「我参与的房间」按座位查得到、大厅返回的每个房间都读得出自己的座位。补完之后这条变异变红。

## 7. 结果

- 后端 **1085** 条全绿(改动前 1069,新增 16)。
- **前端零改动,线上格式零改动** —— `black` / `white` 仍从座位 0/1 投影。
  第三个座位在 DTO 里还看不见,而现在没有三座位棋种注册,所以这不是能被观察到的缺失。
  **触发条件:`add-doudizhu`。**
