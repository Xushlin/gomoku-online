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

/// 中国象棋.
const xiangqiGameKey = 'xiangqi';

const boardRenderers = <String, BoardRenderer>{
  gomokuGameKey: GomokuRenderer(),
  xiangqiGameKey: XiangqiRenderer(),
};

/// The renderer for [gameKey], or null when this client has no board for it.
BoardRenderer? rendererFor(String gameKey) => boardRenderers[gameKey];
