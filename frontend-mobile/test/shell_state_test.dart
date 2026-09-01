// "Which screen is on" has nowhere to hide in the shell.
//
// This used to live in two fields on a `StatefulWidget` — `_authenticated` and
// `_openRoomId` — switched on as a pair. Nothing enforced them, no screen was a route,
// and the system back button had nothing to pop (measured: `canPop()` was **false**
// inside a room). The route table owns that question now.
import 'dart:io';

import 'package:flutter/widgets.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:gewu_mobile/app.dart';

/// **The real guard is this line, and it is checked by the compiler.**
///
/// `GewuApp.new` is assigned to a variable typed as returning a `StatelessWidget`. The
/// day somebody turns the shell back into a `StatefulWidget` to stash a flag, this
/// file stops compiling — which is a stronger promise than any assertion below, and it
/// is why the state cannot come back quietly.
// ignore: unused_element
const StatelessWidget Function({Key? key, required AppDependencies deps}) _shellIsStateless =
    GewuApp.new;

void main() {
  test('the tear-off above is what enforces this, and it resolves', () {
    // A `const` tear-off that nobody reads can be tree-shaken out of a person's
    // attention, so touch it once: this line is the reason the file has to compile.
    expect(_shellIsStateless, isNotNull);
  });

  test('the shell declares no screen state', () {
    // Code only, not prose: the class doc above deliberately names both old fields to
    // explain why they are gone, and a checker that fires on its own explanation
    // invites deleting the explanation.
    final offenders = <String>[];
    final lines = File('lib/app.dart').readAsLinesSync();
    for (final (i, line) in lines.indexed) {
      final t = line.trimLeft();
      if (t.startsWith('//') || t.startsWith('*') || t.startsWith('/*')) continue;
      for (final banned in const ['_authenticated', '_openRoomId', 'setState(']) {
        if (t.contains(banned)) offenders.add('app.dart:${i + 1} $banned');
      }
    }
    expect(offenders, equals(<String>[]));
  });

  test('and the walk read a real file, so the check above is not vacuous', () {
    // Without this, a moved or renamed `app.dart` would leave the loop iterating
    // nothing and passing.
    final source = File('lib/app.dart').readAsStringSync();
    expect(source, contains('class GewuApp extends StatelessWidget'));
    expect(source, contains('routerConfig'));
  });
}
