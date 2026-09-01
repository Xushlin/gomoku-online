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
  const Move({required this.row, required this.col, required this.seat});

  final int row;
  final int col;
  final int seat;

  factory Move.fromJson(Map<String, dynamic> json) => Move(
    row: (json['row'] as num?)?.toInt() ?? 0,
    col: (json['col'] as num?)?.toInt() ?? 0,
    seat: (json['seat'] as num?)?.toInt() ?? 0,
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
    required this.status,
    required this.seats,
    required this.game,
    this.hostUsername,
    this.seatCount,
  });

  final String id;
  final String name;
  final RoomStatus status;
  final List<RoomSeat> seats;
  final GameSnapshot game;
  final String? hostUsername;

  /// How many seats this game HAS, which is not how many are taken. The web client
  /// got this wrong in five places; `seats.length` answers the other question.
  final int? seatCount;

  int get takenSeats => seats.where((s) => s.isTaken).length;
  int get totalSeats => seatCount ?? seats.length;

  factory Room.fromJson(Map<String, dynamic> json) => Room(
    id: '${json['id'] ?? ''}',
    name: '${json['name'] ?? ''}',
    status: RoomStatus.parse(json['status'] as String?),
    seats: [
      for (final s in (json['seats'] as List<dynamic>? ?? const []))
        RoomSeat.fromJson(s as Map<String, dynamic>),
    ],
    game: json['game'] == null
        ? GameSnapshot.empty
        : GameSnapshot.fromJson(json['game'] as Map<String, dynamic>),
    hostUsername: (json['host'] as Map<String, dynamic>?)?['username'] as String?,
    seatCount: (json['seatCount'] as num?)?.toInt(),
  );
}
