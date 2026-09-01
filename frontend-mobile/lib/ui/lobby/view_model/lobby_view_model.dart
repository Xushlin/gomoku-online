import '../../../data/models/models.dart';
import '../../../data/repositories/auth_repository.dart';
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
    required this.gameKey,
  });

  final RoomRepository _rooms;
  final AuthRepository _auth;

  /// Which game's rooms this lists.
  final String gameKey;

  List<Room> rooms = const [];
  bool loading = true;
  String? errorKey;

  Future<void> load() async {
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

  /// Ends the session. **No navigation here** — the router watches
  /// `AuthRepository.signedIn` and redirects, so a ViewModel that also pushed a route
  /// would be a second answer to the same question.
  Future<void> signOut() => _auth.logout();

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
