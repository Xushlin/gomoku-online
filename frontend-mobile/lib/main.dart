import 'package:flutter/material.dart';
import 'package:flutter/services.dart' show rootBundle;

import 'app.dart';

Future<void> main() async {
  WidgetsFlutterBinding.ensureInitialized();
  final deps = await AppDependencies.build(rootBundle);
  runApp(GewuApp(deps: deps));
}
