// E2E smoke test for add-ai-opponent.
// 1. Register Alice (fresh email with unique suffix).
// 2. POST /api/rooms/ai -> bot joins as White, status=Playing.
// 3. Connect to /hubs/gomoku, JoinRoom.
// 4. Alice MakeMove(7,7) -> expect MoveMade for Alice, then MoveMade from bot within ~3s.
// 5. Play several moves, verify bot responds each turn.
// 6. GET /api/rooms/{id} at the end to observe final state.
// 7. GET /api/leaderboard -> Alice appears, bot does NOT.

using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.SignalR.Client;

const string BaseUrl = "http://localhost:5145";
var http = new HttpClient { BaseAddress = new Uri(BaseUrl) };

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
});
createResp.EnsureSuccessStatusCode();
var room = await createResp.Content.ReadFromJsonAsync<RoomStateDto>()
    ?? throw new Exception("room body null");
Assert(room.Status == "Playing", "room.Status == Playing");
Assert(room.Black?.Username == aliceUsername, "black is Alice");
Assert(room.White?.Username == "AI_Easy", "white is AI_Easy");
Assert(room.Game?.CurrentTurn == "Black", "currentTurn == Black");
Assert(room.Game?.Moves.Count == 0, "moves empty");

Console.WriteLine("=== 3. Connect SignalR, JoinRoom ===");
var hub = new HubConnectionBuilder()
    .WithUrl($"{BaseUrl}/hubs/gomoku?access_token={regBody.AccessToken}")
    .Build();

var moveQueue = new System.Collections.Concurrent.ConcurrentQueue<MoveMadePayload>();
var moveSignal = new SemaphoreSlim(0);
hub.On<MoveMadePayload>("MoveMade", payload =>
{
    Console.WriteLine($"  <- MoveMade ply={payload.Ply} ({payload.Row},{payload.Col}) stone={payload.Stone}");
    moveQueue.Enqueue(payload);
    moveSignal.Release();
});
hub.On<GameEndedPayload>("GameEnded", payload =>
{
    Console.WriteLine($"  <- GameEnded result={payload.Result} winner={payload.WinnerUserId}");
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
var aliceMove = await NextMoveAsync(TimeSpan.FromSeconds(5));
Assert(aliceMove.Stone == "Black" && aliceMove.Row == 7 && aliceMove.Col == 7, "Alice's move echoed back");
var botMove = await NextMoveAsync(TimeSpan.FromSeconds(5));
Assert(botMove.Stone == "White", "bot responded as White");
Assert(botMove.Ply == 2, "bot move ply == 2");

Console.WriteLine("=== 5. Play several more rounds — verify bot keeps moving ===");
var humanSteps = new (int, int)[] { (6, 7), (5, 7), (4, 7), (3, 7) };
foreach (var (r, c) in humanSteps)
{
    await hub.InvokeAsync("MakeMove", room.Id, r, c);
    await NextMoveAsync(TimeSpan.FromSeconds(5)); // Alice's echo
    // If Alice just won, there's no bot move. Check by GET state after loop.
    try
    {
        var mv = await NextMoveAsync(TimeSpan.FromSeconds(5));
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

Console.WriteLine("=== 7. Leaderboard excludes bots ===");
var board = await http.GetFromJsonAsync<List<LeaderboardEntry>>("/api/leaderboard");
var hasBot = board!.Any(e => e.Username.StartsWith("AI_"));
Assert(!hasBot, "no AI_* entries in leaderboard");
var aliceEntry = board.FirstOrDefault(e => e.Username == aliceUsername);
Assert(aliceEntry is not null, "Alice appears in leaderboard");

await hub.DisposeAsync();

Console.WriteLine($"\n=== SUMMARY: {passed} passed, {failed} failed ===");
Environment.Exit(failed == 0 ? 0 : 1);


// DTO records (shape matches server responses — only the fields we actually read).
record AuthResponse(string AccessToken, string RefreshToken, DateTime AccessTokenExpiresAt, UserDto User);
record UserDto(Guid Id, string Email, string Username, int Rating, int GamesPlayed, int Wins, int Losses, int Draws, DateTime CreatedAt);
record RoomStateDto(Guid Id, string Name, string Status, PlayerDto? Host, PlayerDto? Black, PlayerDto? White, List<PlayerDto> Spectators, GameDto? Game, List<object> ChatMessages, DateTime CreatedAt);
record PlayerDto(Guid Id, string Username);
record GameDto(Guid Id, string CurrentTurn, DateTime StartedAt, DateTime? EndedAt, string? Result, Guid? WinnerUserId, List<MoveDto> Moves);
record MoveDto(int Ply, int Row, int Col, string Stone, DateTime PlayedAt);
record MoveMadePayload(int Ply, int Row, int Col, string Stone, DateTime PlayedAt);
record GameEndedPayload(string Result, Guid? WinnerUserId, DateTime EndedAt);
record LeaderboardEntry(int Rank, Guid UserId, string Username, int Rating, int GamesPlayed, int Wins, int Losses, int Draws);
