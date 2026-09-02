/// Which games this client can draw. **The only source of that fact.**
///
/// Deliberately **not** under `view/`: it is a capability map, not painting code, and
/// a ViewModel has to be able to ask it ("is this game playable here") without
/// reaching into a view folder.
///
/// The catalogue screen shows every game the server returns and disables the ones it
/// cannot render — and "cannot render" is read from here, not from a second list
/// beside the catalogue. A hand-written list posing as a registry is the defect this
/// repo has fixed **eight** times, and it fails by quietly not covering the entry
/// somebody just added.
///
/// These string keys are not a copy of the server's catalogue: they are the *keys of
/// this map*, i.e. the client's answer to "do I have a painter for that". The server
/// remains the only source of what games exist, how big their boards are and how many
/// seats they have.
library;

import 'view/board_renderer.dart';
import 'view/xiangqi_renderer.dart';

/// 五子棋.
const gomokuGameKey = 'gomoku';

/// 一字棋 — **the same board as 五子棋, three roads instead of fifteen.**
///
/// The server registers it as `NInARowRules("tictactoe", 3, 3, 3)` and writes no
/// win-detection of its own; the platform's own words are 「一字棋是缩小的五子棋,
/// 同一套读法」. Its star points are already derived from the size, and 3×3 derives to
/// none.
const tictactoeGameKey = 'tictactoe';

/// 中国象棋.
const xiangqiGameKey = 'xiangqi';

/// Named so that two keys can share **one** renderer explicitly.
///
/// Dart would canonicalise two `const GomokuRenderer()` expressions to the same
/// instance anyway — which is precisely why it needs a name: an invariant that holds by
/// accident of the language is not an invariant the next reader can rely on.
const _nInARow = GomokuRenderer();

const boardRenderers = <String, BoardRenderer>{
  gomokuGameKey: _nInARow,
  tictactoeGameKey: _nInARow,
  xiangqiGameKey: XiangqiRenderer(),
};

/// The renderer for [gameKey], or null when this client has no board for it.
BoardRenderer? rendererFor(String gameKey) => boardRenderers[gameKey];
