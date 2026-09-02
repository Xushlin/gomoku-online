/// What to call a seat, per game.
///
/// **Dispatched on the game key, never on the seat count**, and that distinction has
/// already cost this platform once. The original rule was written as「座位数大于二 →
/// 说座位号」, while the *reason* behind it was 「『白方走棋』在一个没有白方的棋种里
/// 是错的」. 象棋 and 五子棋 both happen to have exactly two seats, so the case the
/// reason was about slipped straight through the criterion. **Write the criterion
/// against the reason, not against whatever number is to hand.**
///
/// The other half of the same history: a requirement once put 「象棋读作红 / 黑」 in
/// *parentheses* with no mechanism — no implementation read it, no test guarded it, and
/// a Scenario under the same requirement said the opposite. Three places followed the
/// Scenario. **An exception in parentheses is the same as no exception**; this file is
/// the mechanism that was missing.
library;

import 'board_registry.dart';

/// Seat names by game. Games absent from this map fall back to seat numbers, which is
/// correct for a game whose seats have no colour at all (斗地主 has three).
const _labelsByGame = <String, List<String>>{
  // 五子棋: black moves first.
  gomokuGameKey: ['game.seat.black', 'game.seat.white'],
  // 一字棋: the same reading, because it is the same game at three roads — web's
  // manifest spells it out (`seatLabelKeys: ['game.seat.black', 'game.seat.white']`).
  // **Without this entry it silently fell back to "seat N"**, which is the correct
  // fallback for a game whose seats have no colour and the wrong answer for this one.
  tictactoeGameKey: ['game.seat.black', 'game.seat.white'],
  // 象棋: **red moves first**, so seat 0 is red.
  xiangqiGameKey: ['game.seat.red', 'game.seat.black'],
};

/// The translation key naming [seat] in [gameKey], or null when this game's seats have
/// no name and the caller should say "seat N".
///
/// Returns a **key**, never a formatted string: a ViewModel that formats prose has
/// taken the View's job and the locale with it.
String? seatLabelKey(String gameKey, int seat) {
  final labels = _labelsByGame[gameKey];
  if (labels == null || seat < 0 || seat >= labels.length) return null;
  return labels[seat];
}
