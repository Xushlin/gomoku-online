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

class GameSnapshot {
  const GameSnapshot({required this.moves, this.currentSeat});

  final List<Move> moves;

  /// Whose turn it is, as a seat index. Null before the game starts.
  final int? currentSeat;

  static const empty = GameSnapshot(moves: <Move>[]);

  factory GameSnapshot.fromJson(Map<String, dynamic> json) => GameSnapshot(
    moves: [
      for (final m in (json['moves'] as List<dynamic>? ?? const []))
        Move.fromJson(m as Map<String, dynamic>),
    ],
    currentSeat: (json['currentSeat'] as num?)?.toInt(),
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

  int get takenSeats => seats.where((s) => s.isTaken).length;
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
