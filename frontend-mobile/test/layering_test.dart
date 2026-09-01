// The layering rules, enforced.
//
// A layering rule written only in a document is the first thing the next person in
// a hurry routes around, and nothing reports it. This walks the real source and
// fails on the import that breaks the boundary.
//
// The file list is **derived from the directory**, never typed: a hand-written list
// of files to check is the defect this repo has fixed eight times, and it fails by
// quietly not covering the file somebody just added.
//
// **Imports are resolved before they are judged**, and that is not tidiness. The
// first version matched the literal text `data/repositories/`, so it caught a
// violation written as `package:gewu_mobile/data/repositories/x.dart` and missed the
// one a real offender writes — `../repositories/x.dart` from inside `data/models/`.
// The positive control below is what found that: three deliberate breakages went red
// and the fourth passed.
import 'dart:io';

import 'package:flutter_test/flutter_test.dart';
import 'package:path/path.dart' as p;

final lib = Directory('lib');

/// One import, as a path relative to `lib/` — however it was written.
class ResolvedImport {
  ResolvedImport(this.file, this.line, this.target);

  /// The importing file, e.g. `data/models/models.dart`.
  final String file;

  /// The original source line, for a failure message that names the culprit.
  final String line;

  /// What it points at: `data/repositories/auth_repository.dart`, or `package:dio`
  /// for anything outside this package.
  final String target;

  @override
  String toString() => '$file -> $target';
}

String _norm(String path) => path.replaceAll(r'\', '/');

List<ResolvedImport> readImports() {
  final out = <ResolvedImport>[];
  for (final entity in lib.listSync(recursive: true)) {
    if (entity is! File || !entity.path.endsWith('.dart')) continue;
    final file = _norm(p.relative(entity.path, from: 'lib'));

    for (final line in entity.readAsLinesSync()) {
      final trimmed = line.trimLeft();
      if (!trimmed.startsWith('import ')) continue;

      final match = RegExp("import\\s+['\"]([^'\"]+)['\"]").firstMatch(trimmed);
      if (match == null) continue;
      final raw = match.group(1)!;

      final String target;
      if (raw.startsWith('package:gewu_mobile/')) {
        target = raw.substring('package:gewu_mobile/'.length);
      } else if (raw.startsWith('package:') || raw.startsWith('dart:')) {
        target = raw;
      } else {
        // Relative — resolve it against the importing file's directory, which is the
        // form an actual boundary violation is written in.
        target = _norm(p.normalize(p.join(p.dirname(file), raw)));
      }
      out.add(ResolvedImport(file, trimmed, target));
    }
  }
  return out;
}

/// Reports every import from files matching [from] that lands on [forbidden].
List<String> offenders(
  List<ResolvedImport> imports, {
  required bool Function(String file) from,
  required bool Function(String target) forbidden,
}) => [
  for (final i in imports)
    if (from(i.file) && forbidden(i.target)) i.toString(),
];

void main() {
  late List<ResolvedImport> imports;
  late List<String> files;

  setUpAll(() {
    imports = readImports();
    files = imports.map((i) => i.file).toSet().toList();
  });

  test('the walk found the source, so the rules below are not vacuous', () {
    // Without this, a moved directory leaves every rule iterating an empty list and
    // passing — the exact shape of the bug they exist for.
    expect(files.length, greaterThan(8), reason: 'files with imports under lib/');
    expect(files.any((f) => f.startsWith('ui/')), isTrue);
    expect(files.any((f) => f.startsWith('data/')), isTrue);
  });

  group('a View never reaches past its ViewModel', () {
    test('ui/** imports no service and no transport', () {
      expect(
        offenders(
          imports,
          from: (f) => f.startsWith('ui/'),
          forbidden: (t) => t.startsWith('data/services/') || t == 'package:dio/dio.dart',
        ),
        equals(<String>[]),
      );
    });

    test('a view imports no repository either — that is its view model\'s job', () {
      expect(
        offenders(
          imports,
          from: (f) => f.contains('/view/'),
          forbidden: (t) => t.startsWith('data/repositories/'),
        ),
        equals(<String>[]),
      );
    });
  });

  test('models sit at the bottom and import nothing above them', () {
    expect(
      offenders(
        imports,
        from: (f) => f.startsWith('data/models/'),
        forbidden: (t) =>
            t.startsWith('data/services/') ||
            t.startsWith('data/repositories/') ||
            t.startsWith('ui/') ||
            t == 'package:dio/dio.dart',
      ),
      equals(<String>[]),
    );
  });

  group('only the data layer knows the transport exists', () {
    test('package:dio appears nowhere else', () {
      expect(
        offenders(
          imports,
          from: (f) => !f.startsWith('data/services/') && !f.startsWith('data/repositories/'),
          forbidden: (t) => t == 'package:dio/dio.dart',
        ),
        equals(<String>[]),
      );
    });

    /// **The other half.** Without it, deleting Dio entirely would pass the rule
    /// above — "nobody imports the transport" is trivially true of a codebase that
    /// has no transport.
    test('and something does import it', () {
      expect(
        imports.where((i) => i.target == 'package:dio/dio.dart').map((i) => i.file).toList(),
        isNotEmpty,
      );
    });
  });

  test('a ViewModel is testable without a widget', () {
    // Holding a BuildContext is what makes a view model need a widget to test, and
    // being testable without one is the entire reason it is a separate object.
    final bad = offenders(
      imports,
      from: (f) => f.contains('/view_model/'),
      forbidden: (t) =>
          t == 'package:flutter/material.dart' || t == 'package:flutter/widgets.dart',
    );

    for (final file in files.where((f) => f.contains('/view_model/'))) {
      // **Code only, not prose.** The first version scanned whole files and flagged
      // the doc comment that says a view model does NOT hold a BuildContext. A
      // checker that fires on the sentence describing the rule is worse than none:
      // the obvious fix is deleting the explanation.
      final code = File(p.join('lib', file)).readAsLinesSync().where((line) {
        final t = line.trimLeft();
        return !t.startsWith('//') && !t.startsWith('*') && !t.startsWith('/*');
      });
      if (code.any((l) => l.contains('BuildContext'))) bad.add('$file uses BuildContext');
    }

    expect(bad, equals(<String>[]));
  });
}
