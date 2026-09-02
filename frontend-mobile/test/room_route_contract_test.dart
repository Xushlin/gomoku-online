// The room routes this client calls exist on the server, with the verb it uses.
//
// **This exists because the client called a route that does not exist, and its unit
// test confirmed the mistake.** `leave()` posted to `/api/rooms/{id}/dissolve`; dissolve
// is `DELETE /api/rooms/{id}`. The fake adapter beside it accepted any POST and the
// assertion read `expect(posts, ['/api/rooms/A/dissolve'])` — **derived from the client's
// own code**, so it asserted that the client sent what the client was written to send.
// Only the live server said 404.
//
// So the legal set is derived from the **controller's own attributes**, the same way
// `hub_contract_test.dart` derives hub method names from the notifier. It is the same
// class of check one layer down: `shared_sync_test` reading `frontend-web/` is the
// precedent for looking across packages.
import 'dart:io';

import 'package:flutter_test/flutter_test.dart';

final controller = File('../backend/src/Gewu.Api/Controllers/RoomsController.cs');
final repository = File('lib/data/repositories/room_repository.dart');

/// `VERB template` for every action on `RoomsController`, e.g. `DELETE {id:guid}`.
///
/// The controller is routed at `[Route("api/rooms")]`, so a template is relative to
/// that; an empty template means the collection itself.
Set<String> serverRoutes(File source) {
  final attribute = RegExp(r'\[Http(Get|Post|Put|Delete|Patch)\(?"?([^")\]]*)"?\)?\]');
  final found = <String>{};
  for (final line in source.readAsLinesSync()) {
    final trimmed = line.trimLeft();
    if (trimmed.startsWith('//')) continue;
    final m = attribute.firstMatch(trimmed);
    if (m != null) found.add('${m.group(1)!.toUpperCase()} ${m.group(2)!}');
  }
  return found;
}

/// `VERB path` for every call the repository makes, with ids collapsed to a template.
///
/// **Scanned over the whole file, not line by line.** The first version was line-scoped
/// and therefore missed
///
/// ```dart
/// await _dio.post<dynamic>(
///   '/api/rooms/ai',
/// ```
///
/// — a real call, formatted the way a call with a body gets formatted. A pattern that
/// only matches the *simplest* shape reports nothing about the others, and its output is
/// indistinguishable from "they are all fine". The non-vacuity test below pins the
/// multi-line one so it cannot narrow again.
Set<String> clientCalls(File source) {
  final call = RegExp(
    r"_dio\.(get|post|put|delete|patch)<[^>]*>\(\s*'([^']+)'",
    multiLine: true,
    dotAll: true,
  );
  final found = <String>{};
  for (final m in call.allMatches(source.readAsStringSync())) {
    found.add('${m.group(1)!.toUpperCase()} ${m.group(2)!}');
  }
  return found;
}

/// Turns a client path into the controller's template shape:
/// `/api/rooms/$roomId/leave` -> `{id:guid}/leave`, `/api/rooms` -> ``.
String? asTemplate(String path) {
  const prefix = '/api/rooms';
  if (!path.startsWith(prefix)) return null;
  final rest = path.substring(prefix.length);
  if (rest.isEmpty) return '';
  // The only interpolation in these paths is the room id.
  return rest.replaceFirst(RegExp(r'^/\$\w+'), '{id:guid}').replaceFirst(RegExp(r'^/'), '');
}

void main() {
  test('both sources are readable, so the comparison is not vacuous', () {
    // Without this, a moved file leaves both sets empty and "every route is valid" is
    // trivially true — the shape of the bug this test exists for.
    expect(controller.existsSync(), isTrue, reason: controller.path);
    expect(repository.existsSync(), isTrue, reason: repository.path);
    expect(serverRoutes(controller).length, greaterThanOrEqualTo(5));
    expect(clientCalls(repository), isNotEmpty);
    // The multi-line call the first version of the pattern missed. Missing it looked
    // exactly like the file being clean.
    expect(clientCalls(repository), contains('POST /api/rooms/ai'));
  });

  test('every room route the client calls exists on the controller', () {
    final routes = serverRoutes(controller);
    final offenders = <String>[];

    for (final call in clientCalls(repository)) {
      final parts = call.split(' ');
      final template = asTemplate(parts[1]);
      if (template == null) continue; // not a /api/rooms route
      if (!routes.contains('${parts[0]} $template')) offenders.add(call);
    }

    expect(
      offenders.toList()..sort(),
      equals(<String>[]),
      reason: 'the server has no such route+verb. Measured once as a real 404: '
          'dissolve is DELETE /api/rooms/{id}, not POST /api/rooms/{id}/dissolve',
    );
  });

  test('and the two routes this change depends on are the measured ones', () {
    // Named rather than inferred, because these two are the pair that was wrong, and
    // an inferred check would go quiet if the client stopped calling either.
    final routes = serverRoutes(controller);
    expect(routes, contains('DELETE {id:guid}'), reason: 'dissolve');
    expect(routes, contains('POST {id:guid}/leave'), reason: 'leave');
    expect(
      routes.where((r) => r.contains('dissolve')),
      isEmpty,
      reason: 'there is no /dissolve path; that was the wrong guess',
    );

    final calls = clientCalls(repository);
    expect(calls, contains('DELETE /api/rooms/\$roomId'));
    expect(calls, contains('POST /api/rooms/\$roomId/leave'));
    // The AI room, checked against the controller *before* it was written this time.
    expect(routes, contains('POST ai'), reason: 'the AI room');
    expect(calls, contains('POST /api/rooms/ai'));
  });
}
