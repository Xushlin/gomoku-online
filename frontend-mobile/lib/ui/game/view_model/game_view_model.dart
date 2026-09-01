import 'package:flutter/foundation.dart';

import '../../../data/models/models.dart';
import '../../../data/repositories/room_repository.dart';

/// One room in play.
///
/// It owns the subscription to the repository's live room and republishes as its own
/// change notification, so the View listens to exactly one thing.
class GameViewModel extends ChangeNotifier {
  GameViewModel({required this._rooms, required this.roomId});

  final RoomRepository _rooms;
  final String roomId;

  Room? room;
  bool sending = false;
  String? errorKey;

  List<Move> get moves => room?.game.moves ?? const [];

  Future<void> open() async {
    _rooms.live.addListener(_onPush);
    try {
      room = await _rooms.open(roomId);
    } on RoomFailure {
      errorKey = 'game.errors.generic';
    } catch (_) {
      errorKey = 'game.errors.network';
    }
    notifyListeners();
  }

  void _onPush() {
    room = _rooms.live.value ?? room;
    notifyListeners();
  }

  /// **No legality check here** — the server owns that (design D2). A second copy of
  /// the rules is a second truth, and the player reads the disagreement as a bug.
  Future<void> place(int row, int col) async {
    if (sending) return;
    sending = true;
    errorKey = null;
    notifyListeners();
    try {
      await _rooms.makeMove(roomId, row, col);
    } catch (_) {
      errorKey = 'game.errors.invalid-move';
    } finally {
      sending = false;
      notifyListeners();
    }
  }

  @override
  void dispose() {
    _rooms.live.removeListener(_onPush);
    super.dispose();
  }
}
