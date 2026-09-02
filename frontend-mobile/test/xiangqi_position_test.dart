// The opening setup, and the replay that has no rules in it.
//
// The setup is a deliberate copy of the server's `XiangqiBoard.Initial()`. These tests
// check it against **invariants of the game** rather than against a second transcription
// of the same 32 placements — a copy compared to a copy can be wrong together. The
// check that compares it to the authority is in `integration_test/xiangqi_test.dart`:
// it plays a real opening (炮二平五) and requires the *server* to accept it.
import 'dart:io';

import 'package:flutter_test/flutter_test.dart';
import 'package:gewu_mobile/data/models/models.dart';
import 'package:gewu_mobile/ui/game/xiangqi/position.dart';

Move relocation(int fromRow, int fromCol, int row, int col, int seat) =>
    Move(row: row, col: col, seat: seat, fromRow: fromRow, fromCol: fromCol);

int countWhere(XiangqiPosition p, bool Function(XiangqiPiece) test) =>
    p.whereType<XiangqiPiece>().where(test).length;

void main() {
  group('the opening setup', () {
    final start = initialPosition();

    test('32 pieces, 16 a side', () {
      expect(start, hasLength(xiangqiRows * xiangqiCols));
      expect(countWhere(start, (_) => true), 32);
      expect(countWhere(start, (p) => p.isRed), 16);
      expect(countWhere(start, (p) => !p.isRed), 16);
    });

    test('each side has exactly the right pieces', () {
      const expected = {
        XiangqiPieceType.general: 1,
        XiangqiPieceType.advisor: 2,
        XiangqiPieceType.elephant: 2,
        XiangqiPieceType.horse: 2,
        XiangqiPieceType.chariot: 2,
        XiangqiPieceType.cannon: 2,
        XiangqiPieceType.soldier: 5,
      };
      for (final seat in const [redSeat, blackSeat]) {
        for (final entry in expected.entries) {
          expect(
            countWhere(start, (p) => p.seat == seat && p.type == entry.key),
            entry.value,
            reason: 'seat $seat ${entry.key.name}',
          );
        }
      }
    });

    test('mirror-symmetric about the general file', () {
      // The invariant that catches a transposed back rank — which is what a typo in a
      // hand-written setup looks like, and it renders as a plausible-looking board.
      for (var row = 0; row < xiangqiRows; row++) {
        for (var col = 0; col < 4; col++) {
          expect(
            pieceAt(start, row, col),
            pieceAt(start, row, xiangqiCols - 1 - col),
            reason: 'row $row, col $col vs ${xiangqiCols - 1 - col}',
          );
        }
      }
    });

    test('red is seat ZERO, written as the literal the server uses', () {
      // **The constants are pinned to a number here, deliberately.** Every other
      // assertion in this file is written in terms of `redSeat` / `blackSeat`, so
      // swapping the two constants relabels the whole world coherently and nothing goes
      // red — measured: a positive control that swapped them reddened exactly one test.
      // What actually matters is the tie to the server's seat numbering: `Game` opens on
      // seat 0 and 象棋 is red-first, so seat 0 **is** red. If that ever stops being
      // true, the board draws red while the sidebar says it is black's move.
      expect(redSeat, 0);
      expect(blackSeat, 1);
      expect(pieceAt(initialPosition(), 9, 4)?.seat, 0, reason: 'red general, seat 0');
      expect(pieceAt(initialPosition(), 0, 4)?.seat, 1, reason: 'black general, seat 1');
    });

    test('red is at the bottom and moves first; black is at the top', () {
      // Seat 0 is red, and red's back rank is row 9. Getting this pair backwards is
      // the defect the named constants exist to prevent.
      expect(pieceAt(start, 9, 4), const XiangqiPiece(XiangqiPieceType.general, redSeat));
      expect(pieceAt(start, 0, 4), const XiangqiPiece(XiangqiPieceType.general, blackSeat));
      expect(countWhere(start, (p) => p.isRed), 16);
      for (var row = 0; row <= 4; row++) {
        for (var col = 0; col < xiangqiCols; col++) {
          final piece = pieceAt(start, row, col);
          if (piece != null) expect(piece.isRed, isFalse, reason: 'row $row is black\'s half');
        }
      }
    });

    test('the cannons and soldiers are on their ranks', () {
      expect(pieceAt(start, 7, 1)?.type, XiangqiPieceType.cannon);
      expect(pieceAt(start, 7, 7)?.type, XiangqiPieceType.cannon);
      expect(pieceAt(start, 2, 1)?.type, XiangqiPieceType.cannon);
      expect(pieceAt(start, 2, 7)?.type, XiangqiPieceType.cannon);

      for (var col = 0; col < xiangqiCols; col++) {
        final red = pieceAt(start, 6, col);
        final black = pieceAt(start, 3, col);
        if (col.isEven) {
          expect(red?.type, XiangqiPieceType.soldier, reason: 'red soldier at col $col');
          expect(black?.type, XiangqiPieceType.soldier, reason: 'black soldier at col $col');
        } else {
          expect(red, isNull, reason: 'no red soldier at col $col');
          expect(black, isNull);
        }
      }
    });

    test('rows 4 and 5 are empty — the river', () {
      for (var col = 0; col < xiangqiCols; col++) {
        expect(pieceAt(start, 4, col), isNull);
        expect(pieceAt(start, 5, col), isNull);
      }
    });
  });

  group('replaying moves', () {
    test('a relocation moves the piece and leaves the origin empty', () {
      // 炮二平五 — red's right cannon to the centre file.
      final after = positionAfter([relocation(7, 7, 7, 4, redSeat)]);
      expect(pieceAt(after, 7, 7), isNull);
      expect(pieceAt(after, 7, 4), const XiangqiPiece(XiangqiPieceType.cannon, redSeat));
      expect(countWhere(after, (_) => true), 32, reason: 'nothing was captured');
    });

    test('a capture removes the piece that was there', () {
      // Red's cannon takes black's centre soldier: 炮 (7,4) -> (3,4).
      final after = positionAfter([relocation(7, 4, 3, 4, redSeat)], from: positionAfter([
        relocation(7, 7, 7, 4, redSeat),
      ]));
      expect(pieceAt(after, 3, 4), const XiangqiPiece(XiangqiPieceType.cannon, redSeat));
      expect(countWhere(after, (_) => true), 31, reason: 'one fewer piece on the board');
      expect(countWhere(after, (p) => !p.isRed), 15);
      expect(countWhere(after, (p) => p.isRed), 16);
    });

    test('a placement ply is skipped rather than guessed at', () {
      // A ply with no origin is what a *placement* game's move looks like. Treating it
      // as a relocation from (0, 0) would silently corrupt the board, and the symptom
      // would read as a replay bug rather than as bad input.
      final after = positionAfter([const Move(row: 5, col: 5, seat: redSeat)]);
      expect(after, equals(initialPosition()));
    });

    test('an out-of-bounds ply is skipped', () {
      final after = positionAfter([relocation(7, 7, 99, 4, redSeat)]);
      expect(after, equals(initialPosition()));
    });

    test('the original position is not mutated', () {
      final start = initialPosition();
      positionAfter([relocation(7, 7, 7, 4, redSeat)], from: start);
      expect(pieceAt(start, 7, 7)?.type, XiangqiPieceType.cannon);
    });

    test('replaying an empty history is the opening setup', () {
      expect(positionAfter(const []), equals(initialPosition()));
    });

    test('the replay never looks at what kind of piece it is moving', () {
      // **The mechanism behind "replaying is not judging".** Every rule in 象棋 depends
      // on the piece's kind — a horse moves unlike a cannon. Moving one does not. So the
      // invariant is mechanical and checkable: the body of `positionAfter` must not
      // mention `type` at all. A comment claiming "no rules here" is not a mechanism.
      final source = File('lib/ui/game/xiangqi/position.dart').readAsLinesSync();
      final start = source.indexWhere((l) => l.startsWith('XiangqiPosition positionAfter('));
      expect(start, greaterThan(0), reason: 'positionAfter must be findable');

      final body = source.skip(start).takeWhile((l) => l != '}').where((l) {
        final t = l.trimLeft();
        return !t.startsWith('//') && !t.startsWith('///');
      }).toList();

      expect(body, isNotEmpty, reason: 'or this walks nothing');
      expect(
        body.where((l) => l.contains('type')),
        equals(<String>[]),
        reason: 'knowing the kind of piece is what a rule needs; moving one does not',
      );
    });
  });
}
