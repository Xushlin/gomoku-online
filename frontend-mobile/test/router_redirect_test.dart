// The redirect, walked exhaustively.
//
// It is a pure function precisely so this can be a table rather than four widget
// tests: the question "signed in, at /login — where do I go?" has nothing to do with
// widgets, and a test that needs a widget to ask it would not get written for the
// third case.
import 'package:flutter_test/flutter_test.dart';
import 'package:gewu_mobile/ui/router.dart';

void main() {
  // Every (signedIn, location) the app can be in, and the one answer each.
  //
  // **Exactly this table, not "at least these".** A row that stops being right should
  // turn red, and "exactly" is what does that.
  const cases = <({bool signedIn, String location, String? goes})>[
    // Signed out: everything but the login page bounces to it.
    (signedIn: false, location: '/login', goes: null),
    (signedIn: false, location: '/', goes: '/login'),
    (signedIn: false, location: '/games/gomoku/rooms/abc', goes: '/login'),

    // Signed in: the login page bounces the OTHER way. Without this half, an
    // implementation that sent everything to /login would pass the three above.
    (signedIn: true, location: '/login', goes: '/'),
    (signedIn: true, location: '/', goes: null),
    (signedIn: true, location: '/games/gomoku/rooms/abc', goes: null),
  ];

  test('both directions are present in the sample', () {
    // A one-sided walk asserts nothing. This pins that the table actually contains a
    // redirect each way and at least one pass-through, so none of the branches below
    // is being checked against an empty set.
    expect(cases.where((c) => c.goes == '/login'), hasLength(2));
    expect(cases.where((c) => c.goes == '/'), hasLength(1));
    expect(cases.where((c) => c.goes == null), hasLength(3));
  });

  for (final c in cases) {
    test('signedIn=${c.signedIn} at ${c.location} -> ${c.goes ?? "stays"}', () {
      expect(
        redirectFor(signedIn: c.signedIn, location: c.location),
        c.goes,
      );
    });
  }

  test('the route constants are what the table is written against', () {
    // The table above hardcodes '/login' and '/' so it reads like the URLs a person
    // types. If the constants move, this is the line that says so instead of six
    // tests failing for a reason that looks unrelated.
    expect(loginRoute, '/login');
    expect(catalogRoute, '/');
    expect(lobbyRouteFor('gomoku'), '/games/gomoku');
    expect(roomRouteFor('gomoku', 'abc'), '/games/gomoku/rooms/abc');
  });
}
