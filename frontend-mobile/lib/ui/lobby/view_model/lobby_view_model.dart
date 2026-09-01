import 'package:flutter/foundation.dart';

import '../../../data/models/models.dart';
import '../../../data/repositories/auth_repository.dart';
import '../../../data/repositories/room_repository.dart';

/// Gomoku's lobby. One game this slice — a picker with one entry would be a picker
/// pretending to be a platform.
const gameKey = 'gomoku';

class LobbyViewModel extends ChangeNotifier {
  LobbyViewModel({required this._rooms, required this._auth});

  final RoomRepository _rooms;
  final AuthRepository _auth;

  List<Room> rooms = const [];
  bool loading = true;
  String? errorKey;

  Future<void> load() async {
    loading = true;
    errorKey = null;
    notifyListeners();
    try {
      rooms = await _rooms.list(gameKey);
    } on RoomFailure {
      errorKey = 'lobby.errors.load-failed';
    } catch (_) {
      errorKey = 'auth.errors.network';
    } finally {
      loading = false;
      notifyListeners();
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
      notifyListeners();
      return null;
    }
  }

  Future<String?> join(String roomId) async {
    try {
      await _rooms.join(roomId);
      return roomId;
    } on RoomFailure {
      errorKey = 'lobby.errors.join-failed';
      notifyListeners();
      return null;
    }
  }
}
