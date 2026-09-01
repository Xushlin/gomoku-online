/// The base every ViewModel extends.
///
/// **It exists because of one measured assertion, not out of caution:**
///
/// ```
/// A GameViewModel was used after being disposed.
/// GameViewModel.open (game_view_model.dart:31)
/// ```
///
/// Every ViewModel here does the same thing — `await` something, then
/// `notifyListeners()`. If the view is gone by the time the await returns (tap into a
/// room, tap straight back out), that notification lands on a dead notifier.
///
/// **It only crashes in debug.** `ChangeNotifier.debugAssertNotDisposed` is an
/// `assert`, so in a release build the call is simply swallowed. A bug that crashes
/// for us and goes silent for the user is the worse kind, not the milder one.
///
/// The guard is a flag rather than `hasListeners`: a live notifier that nobody happens
/// to be listening to also reports false, so `hasListeners` would swallow legitimate
/// notifications and the symptom would be a screen that just does not update.
library;

import 'package:flutter/foundation.dart';

abstract class ViewModel extends ChangeNotifier {
  bool _disposed = false;

  /// Whether [dispose] has run. Exposed so a test can assert the guard is live
  /// rather than infer it from the absence of a crash.
  bool get isDisposed => _disposed;

  /// Notifies, unless this ViewModel is already gone.
  ///
  /// Subclasses call this and never `notifyListeners()` directly —
  /// `test/view_model_notify_test.dart` walks `ui/**/view_model/` and fails on a
  /// bare call, so the rule is not a convention anybody has to remember.
  @protected
  void notifyIfAlive() {
    if (!_disposed) notifyListeners();
  }

  @override
  void dispose() {
    _disposed = true;
    super.dispose();
  }
}
