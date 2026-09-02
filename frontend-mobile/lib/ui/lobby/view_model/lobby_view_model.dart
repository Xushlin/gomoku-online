import '../../../data/models/models.dart';
import '../../../data/repositories/auth_repository.dart';
import '../../../data/repositories/game_catalog_repository.dart';
import '../../../data/repositories/room_repository.dart';
import '../../view_model.dart';

/// One game's room list.
///
/// **The game key arrives from the route.** It used to be a top-level
/// `const gameKey = 'gomoku'`, whose own comment said a picker with one entry is a
/// picker pretending to be a platform. The catalogue is that picker, so the constant
/// is gone.
class LobbyViewModel extends ViewModel {
  LobbyViewModel({
    required this._rooms,
    required this._auth,
    required this._catalog,
    required this.gameKey,
  });

  final RoomRepository _rooms;
  final AuthRepository _auth;
  final GameCatalogRepository _catalog;

  /// Which game's rooms this lists.
  final String gameKey;

  List<Room> rooms = const [];
  bool loading = true;
  String? errorKey;

  /// What this game supports, from `GET /api/games`.
  ///
  /// **Both entry points are derived from it, and neither is a list.** `supportsAi` is
  /// projected from the same AI registry `POST /api/rooms/ai` validates against, so what
  /// the client offers and what the server accepts cannot disagree; its own doc says a
  /// hand-written copy shows up as **a button that is always 400**. And this lobby had
  /// one: the create-room button was unconditional, while
  /// `POST /api/rooms {"gameKey":"tictactoe"}` answers
  /// *400 'tictactoe' has no human-vs-human mode on this platform.*
  GameDescriptor? get descriptor => _catalog.of(gameKey);

  bool get canCreateRoom => descriptor?.supportsHumanVsHuman ?? false;
  bool get canPlayAi => descriptor?.supportsAi ?? false;

  /// Creates a room against the machine and returns its id, or null with [errorKey].
  Future<String?> createAiRoom({
    required String difficulty,
    required String humanSide,
  }) async {
    try {
      final name = '${_auth.currentUser?.username ?? 'mobile'}-ai-${DateTime.now().minute}';
      final room = await _rooms.createAiRoom(
        name: name,
        gameKey: gameKey,
        difficulty: difficulty,
        humanSide: humanSide,
      );
      return room.id;
    } catch (_) {
      // A wrong difficulty is a *binding* error, so there is no field-level message to
      // surface — one generic key is the honest answer.
      errorKey = 'lobby.ai-game.errors.generic';
      notifyIfAlive();
      return null;
    }
  }

  Future<void> load() async {
    await _catalog.load();
    loading = true;
    errorKey = null;
    notifyIfAlive();
    try {
      rooms = await _rooms.list(gameKey);
    } on RoomFailure {
      errorKey = 'lobby.errors.generic';
    } catch (_) {
      errorKey = 'auth.errors.network';
    } finally {
      loading = false;
      notifyIfAlive();
    }
  }

  /// Creates a room and returns its id, or null with [errorKey] set.
  Future<String?> create() async {
    try {
      final name = '${_auth.currentUser?.username ?? 'mobile'}-${DateTime.now().minute}';
      final room = await _rooms.create(name, gameKey);
      return room.id;
    } on RoomFailure {
      errorKey = 'lobby.create-room.errors.generic';
      notifyIfAlive();
      return null;
    }
  }

  Future<String?> join(String roomId) async {
    try {
      await _rooms.join(roomId);
      return roomId;
    } on RoomFailure {
      errorKey = 'lobby.errors.generic';
      notifyIfAlive();
      return null;
    }
  }
}
