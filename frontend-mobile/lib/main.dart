import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter/services.dart' show rootBundle;

import 'app.dart';

Future<void> main() async {
  WidgetsFlutterBinding.ensureInitialized();
  final deps = await AppDependencies.build(rootBundle);

  // The silent resume: a stored refresh token means "log in without asking".
  //
  // **Deliberately not awaited.** Awaiting it holds the launch screen for a whole
  // round trip, and for an unreachable server that is the 10 s connect timeout —
  // during which the app looks hung. Not awaiting is safe because the router reads
  // `signedIn` as a VALUE: if this lands before the first `redirect` runs, the value
  // is simply already true; if it lands after, `refreshListenable` re-runs it. An
  // event-shaped signal would have a gap here.
  unawaited(deps.auth.refresh());

  runApp(GewuApp(deps: deps));
}
