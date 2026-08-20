// E2E smoke test for add-ai-opponent.
// 1. Register Alice (fresh email with unique suffix).
// 2. POST /api/rooms/ai -> bot joins as White, status=Playing.
// 3. Connect to /hubs/match, JoinRoom.
// 4. Alice MakeMove(7,7) -> expect MoveMade for Alice, then MoveMade from bot within ~3s.
// 5. Play several moves on squares read off the live board, verify bot responds each turn.
// 6b. Resign -> the game ends deterministically, so step 7 has a rated result to find.
// 6. GET /api/rooms/{id} at the end to observe final state.
// 7. GET /api/leaderboard -> Alice appears, bot does NOT.
// 8. GET /api/games + per-game leaderboard / profile queries.
// 9. Create-room enforcement for a game with human play but no AI (doudizhu).

using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.SignalR.Client;

// 地址从环境变量取,默认仍是本地开发端口 —— 手跑的用法一个字不变。
// 硬编码它曾是这个 smoke "只能人手跑"的原因之一,而**一个不跑的测试等于没有测试**。
var BaseUrl = Environment.GetEnvironmentVariable("SMOKE_BASE_URL") ?? "http://localhost:5145";
var http = new HttpClient { BaseAddress = new Uri(BaseUrl) };

// 等一手棋最多多久。
//
// 本地 Release 跑完 21 条断言只要十几秒,5 秒绰绰有余;放宽到 10 秒是为了 CI ——
// ubuntu runner 忙的时候冷启动更慢,而这个 smoke 的失败方式一旦变成"偶发红",
// 它就会被当成噪音,那等于又回到没人信它的状态。
//
// 代价是**真卡住时反馈慢一倍**。这个取舍值得,因为这里的假阴性(偶发超时)会侵蚀信任,
// 而假阳性的代价只是多等 5 秒。
var MoveTimeout = TimeSpan.FromSeconds(10);

var passed = 0;
var failed = 0;
void Assert(bool cond, string name)
{
    if (cond) { Console.WriteLine($"  \u2713 {name}"); passed++; }
    else { Console.WriteLine($"  \u2717 {name}"); failed++; }
}

// Unique suffix so we don't collide with previous smoke runs.
var suffix = Guid.NewGuid().ToString("N")[..8];
var aliceEmail = $"alice-{suffix}@example.com";
var aliceUsername = $"Alice{suffix[..4]}";

Console.WriteLine("=== 1. Register Alice ===");
var reg = await http.PostAsJsonAsync("/api/auth/register", new
{
    email = aliceEmail,
    username = aliceUsername,
    password = "Password1",
});
reg.EnsureSuccessStatusCode();
var regBody = await reg.Content.ReadFromJsonAsync<AuthResponse>()
    ?? throw new Exception("register body null");
Assert(regBody.User.Rating == 1200, "Alice.Rating == 1200");
Assert(regBody.User.GamesPlayed == 0, "Alice.GamesPlayed == 0");
http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", regBody.AccessToken);

Console.WriteLine("=== 2. POST /api/rooms/ai ===");
var createResp = await http.PostAsJsonAsync("/api/rooms/ai", new
{
    name = "AI smoke",
    difficulty = "Easy",
    gameKey = "gomoku",
});
createResp.EnsureSuccessStatusCode();
var room = await createResp.Content.ReadFromJsonAsync<RoomStateDto>()
    ?? throw new Exception("room body null");
Assert(room.Status == "Playing", "room.Status == Playing");
Assert(room.Black?.Username == aliceUsername, "black is Alice");
Assert(room.White?.Username == "AI_Easy", "white is AI_Easy");
Assert(room.Game?.CurrentSeat == 0, "currentSeat == 0 (先手坐 0 号，颜色是显示层的事)");
Assert(room.Game?.Moves.Count == 0, "moves empty");

Console.WriteLine("=== 3. Connect SignalR, JoinRoom ===");
var hub = new HubConnectionBuilder()
    .WithUrl($"{BaseUrl}/hubs/match?access_token={regBody.AccessToken}")
    .Build();

var moveQueue = new System.Collections.Concurrent.ConcurrentQueue<MoveMadePayload>();
var moveSignal = new SemaphoreSlim(0);

// Every square either side has taken, as observed over the wire.
//
// This exists because the smoke used to play a *hardcoded* column — (7,7), (6,7),
// (5,7), (4,7), (3,7) — on a board it does not own. When the bot took one of those
// four, Alice's next `MakeMove` came back `invalid-move` and the run died on an
// assertion about the bot, having caught nothing but its own collision. That is what
// happened in CI: the bot played (6,7), Alice's very next intended square.
//
// **The rate is low, and I first wrote the wrong reason for it here.** I claimed Easy
// plays adjacent to the last stone, so (6,7) was likely. Measured instead — 60 bot
// moves over 12 runs — its choices are scattered across the whole board and *not one*
// landed in that set of four. So the collision is roughly `1 - (1 - 4/225)^4`, about
// 7% per run: a low-rate flake that had simply been passing, until it did not.
//
// The fix does not depend on the rate. This step exists to show "the bot keeps
// replying", and that needs *a* legal move, never a particular one.
var occupied = new HashSet<(int Row, int Col)>();
System.Collections.Concurrent.ConcurrentQueue<string>? arrivalsRef = null;
hub.On<MoveMadePayload>("MoveMade", payload =>
{
    Console.WriteLine($"  <- MoveMade ply={payload.Ply} ({payload.Row},{payload.Col}) seat={payload.Seat}");
    arrivalsRef?.Enqueue($"move:{payload.Ply}");
    lock (occupied)
    {
        occupied.Add((payload.Row, payload.Col));
    }
    moveQueue.Enqueue(payload);
    moveSignal.Release();
});
hub.On<GameEndedPayload>("GameEnded", payload =>
{
    Console.WriteLine($"  <- GameEnded result={payload.Result} winner={payload.WinnerUserId}");
});

// 到达顺序 —— 一条**客户端真的依赖**的契约,此前没人量过。
//
// `handleMoveMade` 曾经自己猜下一手是谁:`move.stone === 'Black' ? 'White' : 'Black'`。
// 那是个两座位假设,而三座位棋种里它是错的。删掉它的理由是"权威状态先到,所以不用猜" ——
// 而那句话在这里被量成断言:`MakeMoveCommandHandler` 先 await `RoomStateChangedAsync`
// 再 await `MoveMadeAsync`,同一个 group、同一条连接,所以带着这一手的 `RoomState`
// MUST 在 `MoveMade` 之前到。
//
// **一个"因为顺序如此所以可以删代码"的论证,必须自己带上那个顺序的证据。**
var arrivals = new System.Collections.Concurrent.ConcurrentQueue<string>();
arrivalsRef = arrivals;
hub.On<RoomStateDto>("RoomState", state =>
{
    var maxPly = state.Game?.Moves.Count > 0 ? state.Game.Moves[^1].Ply : 0;
    arrivals.Enqueue($"state:{maxPly}");
});

await hub.StartAsync();
await hub.InvokeAsync("JoinRoom", room.Id);
Console.WriteLine("  hub connected + joined room");

async Task<MoveMadePayload> NextMoveAsync(TimeSpan timeout)
{
    if (!await moveSignal.WaitAsync(timeout))
        throw new TimeoutException("no MoveMade within " + timeout);
    moveQueue.TryDequeue(out var mv);
    return mv!;
}

Console.WriteLine("=== 4. Alice plays (7,7); wait for bot response ===");
await hub.InvokeAsync("MakeMove", room.Id, 7, 7);
var aliceMove = await NextMoveAsync(MoveTimeout);
Assert(aliceMove.Seat == 0 && aliceMove.Row == 7 && aliceMove.Col == 7, "Alice's move echoed back");
// 第一个提到 ply 1 的帧 MUST 是 RoomState,不是 MoveMade。
var firstMentioningPly1 = arrivals.FirstOrDefault(a => a.EndsWith(":1", StringComparison.Ordinal));
Assert(firstMentioningPly1 == "state:1",
    $"the authoritative RoomState for a move arrives before its MoveMade (first was {firstMentioningPly1 ?? "<none>"})");
var botMove = await NextMoveAsync(MoveTimeout);
Assert(botMove.Seat == 1, "bot responded from seat 1");
Assert(botMove.Ply == 2, "bot move ply == 2");

Console.WriteLine("=== 5. Play several more rounds — verify bot keeps moving ===");

// Pick an empty square rather than trusting a fixed list.
//
// **Even columns only**, and that is structural rather than a coincidence: two of
// Alice's stones can then never be adjacent, so she can never make five in a row
// however many rounds this loop runs. Scanning every cell instead would hand her four
// consecutive ones — one short of winning — and the next person to raise the round
// count would end the game early and break the "bot keeps replying" assertion with no
// hint as to why.
(int Row, int Col) NextFree()
{
    lock (occupied)
    {
        for (var r = 0; r < 15; r++)
        {
            for (var c = 0; c < 15; c += 2)
            {
                if (!occupied.Contains((r, c))) return (r, c);
            }
        }
    }
    throw new InvalidOperationException("no free even-column square — not this smoke's job");
}

for (var round = 0; round < 4; round++)
{
    var (r, c) = NextFree();
    Console.WriteLine($"  -> Alice plays ({r},{c})");
    await hub.InvokeAsync("MakeMove", room.Id, r, c);
    await NextMoveAsync(MoveTimeout); // Alice's echo
    // If Alice just won, there's no bot move. Check by GET state after loop.
    try
    {
        var mv = await NextMoveAsync(MoveTimeout);
        Console.WriteLine($"  after Alice({r},{c}): bot responded ({mv.Row},{mv.Col})");
    }
    catch (TimeoutException)
    {
        Console.WriteLine("  no bot response (likely game ended)");
        break;
    }
}

Console.WriteLine("=== 6. Final state ===");
var finalState = await http.GetFromJsonAsync<RoomStateDto>($"/api/rooms/{room.Id}");
Console.WriteLine($"  status={finalState!.Status} moves={finalState.Game?.Moves.Count} result={finalState.Game?.Result}");
Assert(finalState.Game!.Moves.Count >= 4, "at least 4 moves played");

Console.WriteLine("=== 6b. Resign, so the game ends on purpose ===");
// The leaderboard step below needs a *finished rated game*, and this is how it gets
// one. It used to arrive by accident: Alice played a straight column of five, so she
// won — when the bot did not happen to block her. Two different things were riding on
// one coincidence, which is why a square collision in step 5 surfaced as
// "Alice appears in leaderboard" failing.
//
// Resigning is deterministic, and a loss records ELO exactly as a win does. It also
// covers an endpoint nothing else here touches.
var resign = await http.PostAsync($"/api/rooms/{room.Id}/resign", content: null);
Assert(resign.IsSuccessStatusCode, $"resign accepted (was {(int)resign.StatusCode})");

var afterResign = await http.GetFromJsonAsync<RoomStateDto>($"/api/rooms/{room.Id}");
Console.WriteLine($"  status={afterResign!.Status} result={afterResign.Game?.Result}");
Assert(afterResign.Game?.EndedAt is not null, "game finished after resign");

Console.WriteLine("=== 7. Leaderboard excludes bots ===");
// PagedResult<T>, not a bare array: the endpoint gained paging in
// add-leaderboard-pagination and this line kept deserialising into List<T>,
// which throws. Nobody noticed for months because this project is not in
// Gewu.slnx and CI never runs it — a smoke test outside CI rots silently and
// then lies about coverage. Either wire it in or delete it.
var board = await http.GetFromJsonAsync<PagedResult<LeaderboardEntry>>(
    "/api/leaderboard?pageSize=100");
var hasBot = board!.Items.Any(e => e.Username.StartsWith("AI_"));
Assert(!hasBot, "no AI_* entries in leaderboard");
var aliceEntry = board.Items.FirstOrDefault(e => e.Username == aliceUsername);
Assert(aliceEntry is not null, "Alice appears in leaderboard");

Console.WriteLine("=== 8. Per-game rating ===");
// add-per-game-rating moved ratings into UserGameStats, keyed by (user, game).
// The wire shapes did not change, so the checks above still hold — these two
// pin the parts that are new.
var games = await http.GetFromJsonAsync<List<GameDescriptor>>("/api/games");
Assert(games is not null && games.Any(g => g.GameKey == "gomoku" && g.IsRated), "gomoku is rated");
Assert(games is not null && games.Any(g => g.GameKey == "tictactoe" && !g.IsRated), "tictactoe is not rated");
// enable-xiangqi-human-play 之后:象棋计分,且开放人人对战。
Assert(games is not null && games.Any(g => g.GameKey == "xiangqi" && g.IsRated && g.SupportsHumanVsHuman),
    "xiangqi is rated and open to human play");

// **这里此前是 `Count(g => !g.IsRated) == 1`("一字棋是唯一不计分的对战棋种"),而斗地主落地
// 那天它红了 —— 尽管同一个事实在 Application 层的断言当天就改过了。**
//
// 一个事实两份副本,只改了一份,而 `dotnet test Gewu.slnx` 1266 条全绿:红的只有这里。
// 这一次的成因不是这个仓库修过三遍的"手写清单冒充注册表",而是**这份副本住在
// `dotnet test` 到不了的地方** —— 它是一个对着活服务器跑的控制台程序。
// 又一次由这个 smoke 自己给出的、把它接进 CI 的理由。
//
// 改法不是把 1 改成 2:那只是把同一颗地雷往前挪一格,第九个棋种落地时再红一次。
// **名册留在 Application 层维护**(那里两个不计分的棋种各写了不同的理由);这里改成钉
// 那条不变量与"两侧都非空",于是它不必随棋种数量改动,而守住的东西一点没少。
var descriptors = games ?? throw new Exception("/api/games returned null");
Assert(descriptors.All(g => !g.IsRated || g.SupportsHumanVsHuman),
    "every rated game is open to human play (IsRated => SupportsHumanVsHuman)");
Assert(descriptors.Any(g => g.IsRated) && descriptors.Any(g => !g.IsRated),
    "both sides are non-empty, so the walk above is not a one-sided no-op");

// 斗地主是第八个棋种,而它在这份契约里的四个事实各有各的理由,所以点名断言 ——
// 遍历守不住某一个棋种的具体取值。
var doudizhu = descriptors.SingleOrDefault(g => g.GameKey == "doudizhu");
Assert(doudizhu is not null, "doudizhu is published in the descriptor list");
Assert(doudizhu?.SupportsHumanVsHuman == true, "doudizhu is open to human play");
// ELO 是两人模型,而它按分结算 —— 一条按分的榜是另一条榜,不是这条。
Assert(doudizhu?.IsRated == false, "doudizhu is unrated, and for a different reason than tictactoe");
// enforce-ai-availability:没有机器人的棋种,`POST /api/rooms/ai` 必须拒绝(见步骤 9)。
Assert(doudizhu?.SupportsAi == false, "doudizhu has no AI");
// generalize-match-payload 开的无盘面分支,此前只有成语接龙走到过。
Assert(doudizhu is { Rows: null, Cols: null }, "doudizhu reports no board");
// generalize-match-payload 开出的无盘面分支,由成语接龙第一次真正走到。
Assert(games is not null && games.Any(g => g.GameKey == "idiom-chain" && g.Rows is null && g.Cols is null),
    "idiom-chain reports no board");
// enforce-ai-availability:成语接龙故意没有机器人。
Assert(games is not null && games.Any(g => g.GameKey == "idiom-chain" && !g.SupportsAi),
    "idiom-chain has no AI");

var tttBoard = await http.GetFromJsonAsync<PagedResult<LeaderboardEntry>>(
    "/api/leaderboard?gameKey=tictactoe");
// Empty, not an error: tic-tac-toe settles no ELO, so it has no stats rows.
Assert(tttBoard!.Total == 0, "tictactoe ladder is empty");

var aliceXiangqi = await http.GetFromJsonAsync<UserPublicProfileDto>(
    $"/api/users/{regBody.User.Id}?gameKey=xiangqi");
// "Exists but has never played this game" answers 200 with initial values —
// 404 would be mis-reported by clients as "user not found".
Assert(aliceXiangqi!.GamesPlayed == 0, "Alice has no xiangqi record, and that is a 200");

Console.WriteLine("=== 9. The two create-room enforcements, against the newest game ===");
// 上面那两条断言(`supportsHumanVsHuman: true` / `supportsAi: false`)是**客户端看到的话**;
// 这两条是**服务端会接受的事**。enforce-human-vs-human 与 enforce-ai-availability 各自的成因
// 都是"结论对 web UI 成立、对 API 不成立",所以这两半必须分别量,不能从一半推另一半。
var humanRoom = await http.PostAsJsonAsync("/api/rooms", new
{
    name = "doudizhu smoke",
    gameKey = "doudizhu",
});
Assert((int)humanRoom.StatusCode == 201, $"POST /api/rooms doudizhu -> 201 (was {(int)humanRoom.StatusCode})");
// 注意返回的是 `RoomSummaryDto` 而不是 `RoomStateDto` —— 建房与"看房间"是两个形状。
//
// **只在 201 时解析,而这个 `if` 是变异测试逼出来的。** 把上面那两个开关反过来之后建房
// 返回 400,而 400 的正文是 `ProblemDetails`(`status` 是数字),于是解析抛异常、整个 smoke
// 在这里崩掉 —— CI 仍然红(退出码非 0),但**后面的断言一条都没报出来**。
// 与本文件上面记的"smoke 死在它自己的落子冲突上,什么都没抓到"是同一种坏掉的报告方式。
var waiting = humanRoom.IsSuccessStatusCode
    ? await humanRoom.Content.ReadFromJsonAsync<RoomSummary>()
    : null;
// 三个座位:开局要坐满三个人,所以一个人建完房之后它 MUST 留在 Waiting。
// 这是 add-room-seats 的 `_seats.Count == rules.SeatCount` 第一次被真 HTTP 走到 ——
// 两人棋种在这里是 Waiting 是因为"还差一个",而这里是"还差两个",同一段代码不同的数。
Assert(waiting?.Status == "Waiting", "a one-player three-seat room stays Waiting");
Assert(waiting?.GameKey == "doudizhu", "and the room remembers which game it is");
// **这两条钉的仍是一笔债,而这段注释此前把它记错了。** 它写的是「add-doudizhu-visibility
// 付这笔账(DTO 加座位字段,`SeatWire` 删除)」—— `SeatWire` 确实删了
// (generalize-match-contract),`RoomStateDto.Seats` 也确实有了(add-doudizhu-visibility),
// 而这两条断言**照样是绿的**。原因是它们看的是另一个 DTO:
//
// **这笔账已经付了(fix-lobby-seats):`RoomSummaryDto` 现在也有 `Seats`。**
// 所以下面那条 `White: null` 不再是「第三个座位无处可去」,而是一条更窄、更诚实的断言:
// **`White` 仍然只是 1 号座位**,而三号座位在 `Seats` 里。两句话同时成立,才说明那个字段
// 是加上去的、不是把旧字段改了意思。
//
// (这段注释被改过两次,而两次都是同一个教训:它上一版说「add-doudizhu-visibility 付这笔账」,
// 而那个变更改的是 `RoomStateDto` —— **一条描述另一个 DTO 的注释,会在自己这个 DTO 被修好
// 之后继续错着**。)
//
// 写成 `waiting is { White: null }` 而不是 `waiting?.White is null`:后者在 `waiting` 本身
// 为 null 时**空转通过** —— 上面那次变异跑里,它是建房失败之后唯一还绿着的断言。
Assert(waiting?.Black?.Username == aliceUsername, "seat 0 shows up as Black");
Assert(waiting is { White: null }, "White is still just seat 1, not \"the last player\"");
Assert(waiting?.Seats?.Count == 1, "and the lobby summary now carries the seat list itself");

var botRoom = await http.PostAsJsonAsync("/api/rooms/ai", new
{
    name = "doudizhu bot smoke",
    difficulty = "Easy",
    gameKey = "doudizhu",
});
// 400,不是 201。这条路径此前对成语接龙返回过 201,而 65 秒后那个调用方白拿了 +46 分。
Assert((int)botRoom.StatusCode == 400,
    $"POST /api/rooms/ai doudizhu -> 400 (was {(int)botRoom.StatusCode})");

Console.WriteLine("=== 10. 挖坑 —— 第九个棋种,先手由发牌决定 ===");
// 与斗地主同一组四条描述符事实 + 两条建房事实。**两半都量**,因为
// enforce-human-vs-human 与 enforce-ai-availability 的成因都是从一半推另一半。
var wakeng = descriptors.SingleOrDefault(g => g.GameKey == "wakeng");
Assert(wakeng is not null, "wakeng is published in the descriptor list");
Assert(wakeng?.SupportsHumanVsHuman == true, "wakeng is open to human play");
// ELO 是两人模型,而挖坑按分结算 —— 与斗地主同一条结构性理由。
Assert(wakeng?.IsRated == false, "wakeng is unrated, structurally (points, not ELO)");
// 一个能算牌的机器人在没有炸弹、跟牌必须同型同张的牌型下强得离谱 —— 所以没有 AI,
// 而那不需要任何新代码:不在 BuiltInGameAis.All 里就够了。
Assert(wakeng?.SupportsAi == false, "wakeng has no AI");
Assert(wakeng is { Rows: null, Cols: null }, "wakeng reports no board");
// publish-seat-count:座位数是结构性事实,而客户端此前只能拿「坐了几个人」当它用。
Assert(wakeng?.SeatCount == 3, $"wakeng seats three (was {wakeng?.SeatCount})");
Assert(doudizhu?.SeatCount == 3, $"doudizhu seats three (was {doudizhu?.SeatCount})");
Assert(descriptors.Single(g => g.GameKey == "gomoku").SeatCount == 2, "gomoku seats two");
// **两侧都要有样本** —— 一条只走到 2 的遍历,在一个恒返回 2 的实现下是绿的。
Assert(descriptors.Any(g => g.SeatCount == 2) && descriptors.Any(g => g.SeatCount > 2),
    "seat counts cover both two and more than two");
Assert(descriptors.All(g => g.SeatCount >= 2), "no versus game seats fewer than two");

var wakengRoom = await http.PostAsJsonAsync("/api/rooms", new
{
    name = "wakeng smoke",
    gameKey = "wakeng",
});
Assert((int)wakengRoom.StatusCode == 201,
    $"POST /api/rooms wakeng -> 201 (was {(int)wakengRoom.StatusCode})");
// 只在 2xx 时解析 —— 400 的正文是 ProblemDetails,解析它会抛,而那会让后面的断言一条都不报。
var wakengWaiting = wakengRoom.IsSuccessStatusCode
    ? await wakengRoom.Content.ReadFromJsonAsync<RoomSummary>()
    : null;
// `is { Status: ... }` 而不是 `?.Status ==`:后者在建房失败时空转通过。
Assert(wakengWaiting is { Status: "Waiting" }, "a one-player three-seat wakeng room stays Waiting");
Assert(wakengWaiting?.GameKey == "wakeng", "and the room remembers which game it is");

var wakengBotRoom = await http.PostAsJsonAsync("/api/rooms/ai", new
{
    name = "wakeng bot smoke",
    difficulty = "Easy",
    gameKey = "wakeng",
});
Assert((int)wakengBotRoom.StatusCode == 400,
    $"POST /api/rooms/ai wakeng -> 400 (was {(int)wakengBotRoom.StatusCode})");

await hub.DisposeAsync();

Console.WriteLine($"\n=== SUMMARY: {passed} passed, {failed} failed ===");
Environment.Exit(failed == 0 ? 0 : 1);


// DTO records (shape matches server responses — only the fields we actually read).
record AuthResponse(string AccessToken, string RefreshToken, DateTime AccessTokenExpiresAt, UserDto User);
record UserDto(Guid Id, string Email, string Username, int Rating, int GamesPlayed, int Wins, int Losses, int Draws, DateTime CreatedAt);
record RoomStateDto(Guid Id, string Name, string Status, PlayerDto? Host, PlayerDto? Black, PlayerDto? White, List<PlayerDto> Spectators, GameDto? Game, List<object> ChatMessages, DateTime CreatedAt);
record PlayerDto(Guid Id, string Username);
record GameDto(Guid Id, int CurrentSeat, DateTime StartedAt, DateTime? EndedAt, string? Result, Guid? WinnerUserId, List<MoveDto> Moves);
record MoveDto(int Ply, int Row, int Col, int Seat, DateTime PlayedAt);
record MoveMadePayload(int Ply, int Row, int Col, int Seat, DateTime PlayedAt);
record GameEndedPayload(string Result, Guid? WinnerUserId, DateTime EndedAt);
record LeaderboardEntry(int Rank, Guid UserId, string Username, int Rating, int GamesPlayed, int Wins, int Losses, int Draws);
record PagedResult<T>(List<T> Items, int Total, int Page, int PageSize);
// `POST /api/rooms` 回的是 summary,不是 state —— 只声明这里真读的字段。
record RoomSummary(Guid Id, string Name, string GameKey, string Status, PlayerDto Host, PlayerDto? Black, PlayerDto? White, List<SeatDto>? Seats, int SpectatorCount);
record SeatDto(int Index, PlayerDto Player);
// Rows / Cols 必须可空:generalize-match-payload 起,无盘面的棋种(成语接龙)报 null。
// SupportsAi 是 enforce-ai-availability 加的。
//
// **这一行此前是 `int Rows, int Cols`,于是本 smoke 在步骤 8 运行时崩溃** —— 编译得过,
// 因为它只是个 DTO;跑起来就炸,因为 /api/games 里真有 null。没人发现,因为它不在 CI 里。
// 这正是把它接进 CI 的最强论据,而且它是自己给出的。
record GameDescriptor(
    string GameKey, bool IsRated, bool SupportsHumanVsHuman, bool SupportsAi, int SeatCount, int? Rows, int? Cols);
record UserPublicProfileDto(Guid Id, string Username, int Rating, int GamesPlayed, int Wins, int Losses, int Draws, DateTime CreatedAt);
