/// The server's shapes, as immutable Dart.
///
/// **These exist because `Map<String, dynamic>` has no opinion.** Before this, the
/// screens read `raw['status']` and `move['row'] as int`; a contract change was
/// invisible to the compiler and showed up as an empty board. This repo has already
/// paid that bill once on the web client, when `GameReplayDto` grew a seat list and
/// the replay page silently dropped the third player.
///
/// Rules that hold for every model here:
///
/// - **Immutable.** `final` fields, `const` constructors.
/// - **Parse-only.** `fromJson` and nothing else — no network, no business rules.
///   A model that needs either is not a model.
/// - **Tolerant of absence, never of the wrong type.** Missing optional fields
///   become null; a field that is present with an unexpected type should fail loudly
///   here rather than three layers up.
///
/// Hand-written rather than generated: five models is a few dozen lines, and a code
/// generator wants a build pipeline. **The trigger for revisiting that is written in
/// the proposal** — more than ~12 models, or the first defect caused by a hand-written
/// `fromJson` missing a field.
library;

/// The signed-in player.
class AuthUser {
  const AuthUser({required this.id, required this.username, this.email});

  final String id;
  final String username;
  final String? email;

  /// The username lives under `user`, not at the top level of the auth response.
  /// Reading the wrong field made every room name `mobile-…`, which looked like a
  /// naming choice rather than a bug — the integration test now pins it.
  static AuthUser fromAuthResponse(Map<String, dynamic> json) {
    final user = json['user'] as Map<String, dynamic>?;
    return AuthUser(
      id: '${user?['id'] ?? ''}',
      username: '${user?['username'] ?? ''}',
      email: user?['email'] as String?,
    );
  }
}

/// A pair of tokens. The refresh token **rotates** — see `AuthRepository`.
class AuthTokens {
  const AuthTokens({required this.access, required this.refresh});

  final String access;
  final String refresh;

  factory AuthTokens.fromJson(Map<String, dynamic> json) => AuthTokens(
    access: '${json['accessToken'] ?? ''}',
    refresh: '${json['refreshToken'] ?? ''}',
  );
}

class AuthResult {
  const AuthResult({required this.user, required this.tokens});

  final AuthUser user;
  final AuthTokens tokens;

  factory AuthResult.fromJson(Map<String, dynamic> json) => AuthResult(
    user: AuthUser.fromAuthResponse(json),
    tokens: AuthTokens.fromJson(json),
  );
}

/// One seat at a table. Seat 0 moves first.
class RoomSeat {
  const RoomSeat({required this.index, this.playerId, this.username});

  final int index;
  final String? playerId;
  final String? username;

  bool get isTaken => playerId != null;

  factory RoomSeat.fromJson(Map<String, dynamic> json) {
    final player = json['player'] as Map<String, dynamic>?;
    return RoomSeat(
      index: (json['index'] as num?)?.toInt() ?? 0,
      playerId: player?['id'] as String?,
      username: player?['username'] as String?,
    );
  }
}

class Move {
  const Move({
    required this.row,
    required this.col,
    required this.seat,
    this.fromRow,
    this.fromCol,
  });

  final int row;
  final int col;
  final int seat;

  /// Where the piece came from, for games that move rather than place.
  ///
  /// **Null for 五子棋 and every other placement game**, and that nullability is the
  /// point: a placement game has no origin, so a non-null default would invent one.
  final int? fromRow;
  final int? fromCol;

  bool get isRelocation => fromRow != null && fromCol != null;

  factory Move.fromJson(Map<String, dynamic> json) => Move(
    row: (json['row'] as num?)?.toInt() ?? 0,
    col: (json['col'] as num?)?.toInt() ?? 0,
    seat: (json['seat'] as num?)?.toInt() ?? 0,
    fromRow: (json['fromRow'] as num?)?.toInt(),
    fromCol: (json['fromCol'] as num?)?.toInt(),
  );
}

/// How a game finished. **Parsed by name, not by ordinal** — the server's enum is
/// `Ongoing = 0`, `Decided = 1`, `Draw = 3`, with **no 2**, so counting positions here
/// would be a copy of a gap.
enum GameResult {
  ongoing('Ongoing'),
  decided('Decided'),
  draw('Draw'),
  unknown('');

  const GameResult(this.wire);
  final String wire;

  static GameResult parse(String? value) => GameResult.values.firstWhere(
    (r) => r.wire == value,
    orElse: () => GameResult.unknown,
  );
}

/// Why it finished.
enum GameEndReason {
  decided('Decided'),
  resigned('Resigned'),
  turnTimeout('TurnTimeout'),
  unknown('');

  const GameEndReason(this.wire);
  final String wire;

  static GameEndReason parse(String? value) => GameEndReason.values.firstWhere(
    (r) => r.wire == value,
    orElse: () => GameEndReason.unknown,
  );
}

class GameSnapshot {
  const GameSnapshot({
    required this.moves,
    this.currentSeat,
    this.result = GameResult.ongoing,
    this.winnerUserId,
    this.endReason,
  });

  final List<Move> moves;

  /// Whose turn it is, as a seat index. Null before the game starts.
  final int? currentSeat;

  /// **The server has always sent these three, and the client used to drop them.**
  /// The symptom on a real phone was a finished game where the screen just stopped:
  /// the board still there, every tap refused, and nothing saying who won.
  final GameResult result;
  final String? winnerUserId;
  final GameEndReason? endReason;

  bool get isOver => result == GameResult.decided || result == GameResult.draw;

  static const empty = GameSnapshot(moves: <Move>[]);

  factory GameSnapshot.fromJson(Map<String, dynamic> json) => GameSnapshot(
    moves: [
      for (final m in (json['moves'] as List<dynamic>? ?? const []))
        Move.fromJson(m as Map<String, dynamic>),
    ],
    currentSeat: (json['currentSeat'] as num?)?.toInt(),
    // **Null is "not finished", not "unrecognised".** The server's `GameResult?` is null
    // for a game still in play, and collapsing that into `unknown` would throw away the
    // difference between "we know it is ongoing" and "the server said something this
    // client has never heard of" — and the second one is the case where guessing is the
    // wrong thing to do.
    result: json['result'] == null
        ? GameResult.ongoing
        : GameResult.parse(json['result'] as String?),
    winnerUserId: json['winnerUserId']?.toString(),
    endReason: json['endReason'] == null
        ? null
        : GameEndReason.parse(json['endReason'] as String?),
  );
}

/// Room status as the server names it. Unknown values keep their text rather than
/// collapsing to a default — a status nobody handles should be visible, not silently
/// treated as `Waiting`.
enum RoomStatus {
  waiting('Waiting'),
  playing('Playing'),
  finished('Finished'),
  unknown('');

  const RoomStatus(this.wire);
  final String wire;

  static RoomStatus parse(String? value) => RoomStatus.values.firstWhere(
    (s) => s.wire == value,
    orElse: () => RoomStatus.unknown,
  );
}

/// Which conversation a message belongs to.
///
/// **Parsed by name, and an unrecognised value stays unrecognised.** Collapsing it to
/// `room` would take a channel nobody here understands and broadcast it to the table —
/// the spectator channel exists precisely so that some messages do not reach players.
enum ChatChannel {
  room('Room'),
  spectator('Spectator'),
  unknown('');

  const ChatChannel(this.wire);

  final String wire;

  static ChatChannel parse(String? value) => switch (value) {
    'Room' => ChatChannel.room,
    'Spectator' => ChatChannel.spectator,
    _ => ChatChannel.unknown,
  };
}

/// One thing somebody said.
class ChatMessage {
  const ChatMessage({
    required this.id,
    required this.senderUserId,
    required this.senderUsername,
    required this.content,
    required this.channel,
    this.sentAt,
  });

  final String id;
  final String senderUserId;

  /// The name to show. **A snapshot taken by the server when the message was sent** —
  /// so a later rename does not rewrite history.
  final String senderUsername;
  final String content;
  final ChatChannel channel;
  final DateTime? sentAt;

  factory ChatMessage.fromJson(Map<String, dynamic> json) => ChatMessage(
    id: '${json['id'] ?? ''}',
    senderUserId: '${json['senderUserId'] ?? ''}',
    senderUsername: '${json['senderUsername'] ?? ''}',
    content: '${json['content'] ?? ''}',
    channel: ChatChannel.parse(json['channel'] as String?),
    sentAt: DateTime.tryParse('${json['sentAt'] ?? ''}'),
  );
}

class Room {
  const Room({
    required this.id,
    required this.name,
    required this.gameKey,
    required this.status,
    required this.seats,
    required this.game,
    this.hostUsername,
    this.hostId,
    this.seatCount,
    this.chatMessages = const [],
    this.spectators = const [],
  });

  final String id;
  final String name;

  /// Which game this room is playing.
  ///
  /// **Required, and read from the room rather than from the route.** The server's own
  /// DTO doc explains why: there are four ways into a room — a redirect from creating
  /// it, a reload, a bookmarked link, and "my games" — and only the first one leaves
  /// the client already knowing the game. On the other three it holds nothing but a
  /// room id, so "carry the game key in the path" is a shortcut that works on one of
  /// four paths and draws a 10×9 board as 15×15 on the rest.
  final String gameKey;
  final RoomStatus status;
  final List<RoomSeat> seats;
  final GameSnapshot game;
  final String? hostUsername;

  /// The host's user id.
  ///
  /// **Needed because leaving takes a different route for the host of a waiting room**
  /// — the server refuses `/leave` there (`HostCannotLeaveWaitingRoom`) and wants
  /// `/dissolve`. Compared by id rather than by username: a username is a display
  /// name, and two of this platform's bugs have come from treating one as an identity.
  final String? hostId;

  /// How many seats this game HAS, which is not how many are taken. The web client
  /// got this wrong in five places; `seats.length` answers the other question.
  final int? seatCount;

  /// What has been said in this room so far.
  ///
  /// **It rides on the room snapshot**, so opening a room already has the history and
  /// there is no second endpoint to call. Pushes carry **one** message each and are
  /// appended by the repository — never used to replace this list.
  final List<ChatMessage> chatMessages;

  /// Who is watching. Ids and names, as the server reports them.
  final List<RoomSeat> spectators;

  int get takenSeats => seats.where((s) => s.isTaken).length;

  /// Whether [userId] is watching rather than playing. **By id, never by username.**
  bool isSpectator(String? userId) =>
      userId != null && spectators.any((s) => s.playerId == userId);
  int get totalSeats => seatCount ?? seats.length;

  factory Room.fromJson(Map<String, dynamic> json) => Room(
    id: '${json['id'] ?? ''}',
    name: '${json['name'] ?? ''}',
    gameKey: '${json['gameKey'] ?? ''}',
    status: RoomStatus.parse(json['status'] as String?),
    seats: [
      for (final s in (json['seats'] as List<dynamic>? ?? const []))
        RoomSeat.fromJson(s as Map<String, dynamic>),
    ],
    game: json['game'] == null
        ? GameSnapshot.empty
        : GameSnapshot.fromJson(json['game'] as Map<String, dynamic>),
    hostUsername: (json['host'] as Map<String, dynamic>?)?['username'] as String?,
    hostId: (json['host'] as Map<String, dynamic>?)?['id']?.toString(),
    seatCount: (json['seatCount'] as num?)?.toInt(),
    chatMessages: [
      for (final m in (json['chatMessages'] as List<dynamic>? ?? const []))
        ChatMessage.fromJson(m as Map<String, dynamic>),
    ],
    // The server sends spectators as bare users, not as seats — reuse `RoomSeat` for
    // the id/name pair rather than adding a model whose only job is to hold two
    // strings. `index` is meaningless here and is never read.
    spectators: [
      for (final u in (json['spectators'] as List<dynamic>? ?? const []))
        RoomSeat(
          index: -1,
          playerId: (u as Map<String, dynamic>)['id']?.toString(),
          username: u['username'] as String?,
        ),
    ],
  );
}

/// One versus game, as the server describes it — a read-only projection of the
/// server's rules registry (`GET /api/games`).
///
/// **This is the whole reason the client keeps no game table of its own.** The
/// server's DTO doc spells out the test for whether a copy is acceptable: not how
/// small it is, but whether being wrong would ever be noticed. A wrong board size
/// paints visibly wrong and the server's bounds check catches the move anyway; a wrong
/// `isRated` shows up as **a leaderboard that is always empty**, which looks exactly
/// like a game nobody has played yet. So none of it is copied.
class GameDescriptor {
  const GameDescriptor({
    required this.gameKey,
    required this.isRated,
    required this.supportsHumanVsHuman,
    required this.supportsAi,
    required this.seatCount,
    this.rows,
    this.cols,
  });

  final String gameKey;

  /// Whether a finished game settles ELO — i.e. whether this game has a leaderboard.
  final bool isRated;

  final bool supportsHumanVsHuman;
  final bool supportsAi;

  /// How many seats this game HAS, which is not how many are taken.
  ///
  /// **Non-null, and that is the difference from [rows] / [cols]:** every game with
  /// rules has a seat count, so "not applicable" does not exist — whereas 成语接龙
  /// genuinely has no board.
  final int seatCount;

  final int? rows;
  final int? cols;

  bool get hasBoard => rows != null && cols != null;

  factory GameDescriptor.fromJson(Map<String, dynamic> json) => GameDescriptor(
    gameKey: '${json['gameKey'] ?? ''}',
    isRated: json['isRated'] == true,
    supportsHumanVsHuman: json['supportsHumanVsHuman'] == true,
    supportsAi: json['supportsAi'] == true,
    seatCount: (json['seatCount'] as num?)?.toInt() ?? 0,
    rows: (json['rows'] as num?)?.toInt(),
    cols: (json['cols'] as num?)?.toInt(),
  );
}
