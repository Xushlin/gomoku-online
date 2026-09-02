/// 中国象棋's position, replayed from the move list.
///
/// **The server does not send a position.** `GameSnapshotDto` carries only `Moves`, so
/// a client that wants to draw a 象棋 board has to replay them. 五子棋 needs none of
/// this: every ply *places* a stone of a known colour, so its history **is** its board.
/// 象棋's plies are `from → to`, which say nothing about where anything started.
///
/// **Replaying is not judging.** Nothing here knows how a horse moves, and that is the
/// point — the platform rule is that this client MUST NOT decide legality (design D2),
/// and the two are easy to confuse because both end in "the client knows the board".
/// The distinction is mechanical: [positionAfter] moves whatever is at `from` to `to`
/// and drops whatever was there. Ask it whether a move is legal and it has no opinion.
library;

import '../../../data/models/models.dart';

const xiangqiRows = 10;
const xiangqiCols = 9;
const _cellCount = xiangqiRows * xiangqiCols;

enum XiangqiPieceType { general, advisor, elephant, horse, chariot, cannon, soldier }

/// A side, as a seat index.
///
/// **Seat 0 is 红, and that is not a typo.** `Game` opens on seat 0 and 象棋 is
/// red-first, so seat 0 reads as red — the same reading the server and the web client
/// use. These are named constants precisely so that no file ends up with a bare
/// `seat == 0` standing in for "red": that bare comparison is where somebody would
/// eventually "fix" this, and the platform has already paid for that once — a
/// requirement wrote 「象棋读作红 / 黑」 in *parentheses*, nothing read it, and three
/// places went on calling red "black" for a long time.
const redSeat = 0;
const blackSeat = 1;

class XiangqiPiece {
  const XiangqiPiece(this.type, this.seat);

  final XiangqiPieceType type;

  /// [redSeat] or [blackSeat].
  final int seat;

  bool get isRed => seat == redSeat;

  @override
  bool operator ==(Object other) =>
      other is XiangqiPiece && other.type == type && other.seat == seat;

  @override
  int get hashCode => Object.hash(type, seat);

  @override
  String toString() => '${isRed ? "red" : "black"} ${type.name}';
}

/// Row-major, length `xiangqiRows * xiangqiCols`. Null is an empty intersection.
typedef XiangqiPosition = List<XiangqiPiece?>;

int cellIndex(int row, int col) => row * xiangqiCols + col;

bool inBounds(int row, int col) =>
    row >= 0 && row < xiangqiRows && col >= 0 && col < xiangqiCols;

XiangqiPiece? pieceAt(XiangqiPosition position, int row, int col) =>
    inBounds(row, col) ? position[cellIndex(row, col)] : null;

/// Back rank, left to right. **Mirror-symmetric about column 4** — the general's file.
///
/// Written once and mirrored rather than as 32 placements: a hand-typed board is 32
/// chances to transpose two pieces, and a transposition looks like a rendering bug.
const _backRank = <XiangqiPieceType>[
  XiangqiPieceType.chariot,
  XiangqiPieceType.horse,
  XiangqiPieceType.elephant,
  XiangqiPieceType.advisor,
  XiangqiPieceType.general,
  XiangqiPieceType.advisor,
  XiangqiPieceType.elephant,
  XiangqiPieceType.horse,
  XiangqiPieceType.chariot,
];

/// The opening setup — 32 pieces.
///
/// **A deliberate copy of the server's `XiangqiBoard.Initial()`, and the only one.**
/// This repo's test for an acceptable copy is not how small it is but whether being
/// wrong would ever be noticed: being wrong here paints the whole board wrong on move
/// zero, and the server rejects any move that only looks legal on a wrong board. Two
/// nets, both loud.
///
/// It is also not server *state*: the opening setup of 象棋 is a rule of the game, as
/// public and as fixed as "the board is 10×9".
XiangqiPosition initialPosition() {
  final cells = XiangqiPosition.filled(_cellCount, null);

  void placeSide(int backRankRow, int cannonRow, int soldierRow, int seat) {
    for (var col = 0; col < _backRank.length; col++) {
      cells[cellIndex(backRankRow, col)] = XiangqiPiece(_backRank[col], seat);
    }
    cells[cellIndex(cannonRow, 1)] = XiangqiPiece(XiangqiPieceType.cannon, seat);
    cells[cellIndex(cannonRow, 7)] = XiangqiPiece(XiangqiPieceType.cannon, seat);
    for (var col = 0; col < xiangqiCols; col += 2) {
      cells[cellIndex(soldierRow, col)] = XiangqiPiece(XiangqiPieceType.soldier, seat);
    }
  }

  // 黑方 on top: row 0 is its back rank. 红方 at the bottom: row 9 is its back rank,
  // and red moves first. Same orientation as the server's `XiangqiBoard.Initial()`.
  placeSide(0, 2, 3, blackSeat);
  placeSide(9, 7, 6, redSeat);

  return cells;
}

/// The position after applying [moves] to [from] — the opening setup by default.
///
/// **Two statements of state change, and no rule.** A ply whose origin is missing is
/// skipped rather than guessed at: that is what a *placement* ply looks like, and
/// silently treating one as a relocation from (0,0) would corrupt the board in a way
/// that reads as a replay bug.
XiangqiPosition positionAfter(List<Move> moves, {XiangqiPosition? from}) {
  final cells = XiangqiPosition.of(from ?? initialPosition());

  for (final move in moves) {
    final fromRow = move.fromRow;
    final fromCol = move.fromCol;
    if (fromRow == null || fromCol == null) continue;
    if (!inBounds(fromRow, fromCol) || !inBounds(move.row, move.col)) continue;

    cells[cellIndex(move.row, move.col)] = cells[cellIndex(fromRow, fromCol)];
    cells[cellIndex(fromRow, fromCol)] = null;
  }

  return cells;
}
