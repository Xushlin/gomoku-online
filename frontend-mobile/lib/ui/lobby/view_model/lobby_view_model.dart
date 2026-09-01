import '../../../data/models/models.dart';
import '../../../data/repositories/auth_repository.dart';
import '../../../data/repositories/room_repository.dart';
import '../../view_model.dart';

/// Gomoku's lobby. One game this slice — a picker with one entry would be a picker
/// pretending to be a platform.
const gameKey = 'gomoku';

class LobbyViewModel extends ViewModel {
  LobbyViewModel({required this._rooms, required this._auth});

  final RoomRepository _rooms;
  final AuthRepository _auth;

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
      errorKey = 'lobby.errors.load-failed';
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
      errorKey = 'lobby.errors.create-failed';
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
      errorKey = 'lobby.errors.join-failed';
      notifyIfAlive();
      return null;
    }
  }
}
