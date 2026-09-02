// Two halves of one rule: no ViewModel notifies after it is gone.
//
// The behavioural half proves the guard works. The walk proves every ViewModel is
// behind it — including the one somebody adds next month, because the file list is
// **derived from the directory** and never typed. A hand-written list of files to
// check is the defect this repo has fixed eight times, and it fails by quietly not
// covering the file that was just added.
import 'dart:async';
import 'dart:io';
import 'dart:typed_data';

import 'package:dio/dio.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:path/path.dart' as p;

import 'package:gewu_mobile/data/repositories/game_catalog_repository.dart';
import 'package:gewu_mobile/data/repositories/room_repository.dart';
import 'package:gewu_mobile/data/services/dio_client.dart';
import 'package:gewu_mobile/data/services/match_hub_service.dart';
import 'package:gewu_mobile/data/services/token_store.dart';
import 'package:gewu_mobile/ui/game/view_model/game_view_model.dart';

/// An adapter that holds every request open until [gate] is completed.
///
/// The point is control over *when* the await returns, which is the only way to be
/// standing in the window this test is about. `Future.delayed` would make it a race.
class GatedAdapter implements HttpClientAdapter {
  final gate = Completer<void>();

  @override
  Future<ResponseBody> fetch(
    RequestOptions options,
    Stream<Uint8List>? requestStream,
    Future<void>? cancelFuture,
  ) async {
    await gate.future;
    return ResponseBody.fromString('{"code":"gone"}', 400);
  }

  @override
  void close({bool force = false}) {}
}

void main() {
  group('a ViewModel disposed mid-flight does not notify', () {
    test('GameViewModel.open() resolving after dispose throws nothing', () async {
      final adapter = GatedAdapter();
      final gatedDio = buildDio(
        baseUrl: 'http://example.invalid',
        tokens: MemoryTokenStore(),
        refresh: () async => false,
        adapter: adapter,
      );
      final rooms = RoomRepository(
        dio: gatedDio,
        hub: MatchHub(serverAddress: 'http://example.invalid', accessToken: () => ''),
      );

      final vm = GameViewModel(
        rooms: rooms,
        catalog: GameCatalogRepository(gatedDio),
        roomId: 'r1',
      );
      final open = vm.open();

      // The window: the request is still open, and the view is already gone.
      vm.dispose();
      expect(vm.isDisposed, isTrue, reason: 'precondition — otherwise this proves nothing');

      adapter.gate.complete();

      // Without the guard this is where it throws:
      //   A GameViewModel was used after being disposed.
      await open;
    });
  });

  group('every ViewModel is behind the guard', () {
    late List<File> viewModels;

    setUpAll(() {
      viewModels = Directory('lib/ui')
          .listSync(recursive: true)
          .whereType<File>()
          .where((f) => p.split(f.path).contains('view_model'))
          .where((f) => f.path.endsWith('.dart'))
          .toList();
    });

    test('the walk found them, so the checks below are not vacuous', () {
      // Without this, a renamed directory leaves both checks iterating an empty list
      // and passing — the exact shape of the bug they exist for.
      // **This number changing is the point.** It was 3 (login + lobby + game) and
      // the catalogue makes it 4; the day a fifth lands, this line is what says so.
      expect(viewModels, hasLength(4), reason: 'login + lobby + game + catalog');
    });

    test('each one extends ViewModel', () {
      final offenders = [
        for (final f in viewModels)
          if (!f.readAsStringSync().contains('extends ViewModel'))
            p.relative(f.path, from: 'lib'),
      ];
      expect(offenders, equals(<String>[]));
    });

    test('none of them calls notifyListeners directly', () {
      // **Code only, not prose.** Scanning whole files would flag the doc comment
      // that explains this very rule, and the obvious fix for that is deleting the
      // explanation.
      final offenders = <String>[];
      for (final f in viewModels) {
        for (final (i, line) in f.readAsLinesSync().indexed) {
          final t = line.trimLeft();
          if (t.startsWith('//') || t.startsWith('*') || t.startsWith('/*')) continue;
          if (t.contains('notifyListeners(')) {
            offenders.add('${p.relative(f.path, from: 'lib')}:${i + 1}');
          }
        }
      }
      expect(offenders, equals(<String>[]));
    });

    test('and the guard they call actually exists', () {
      // The other half. Without it, renaming `notifyIfAlive` to something no
      // ViewModel calls would leave "nobody calls notifyListeners" trivially true of
      // code that notifies nothing at all.
      final callers = viewModels
          .where((f) => f.readAsStringSync().contains('notifyIfAlive('))
          .length;
      expect(callers, 4);
      expect(
        File('lib/ui/view_model.dart').readAsStringSync(),
        contains('if (!_disposed) notifyListeners();'),
      );
    });
  });
}
