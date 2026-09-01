import '../../../data/repositories/auth_repository.dart';
import '../../view_model.dart';

/// Login/register state and intent.
///
/// **No `BuildContext` here.** Holding one would mean this cannot be tested without
/// a widget, and being testable without a widget is the entire reason it exists.
class LoginViewModel extends ViewModel {
  LoginViewModel(this._auth);

  final AuthRepository _auth;

  bool registering = false;
  bool busy = false;

  /// A translation KEY, not a message. The View turns it into words — a ViewModel
  /// that formats prose has quietly taken on the View's job and the locale with it.
  String? errorKey;

  void toggleMode() {
    registering = !registering;
    errorKey = null;
    notifyIfAlive();
  }

  /// Returns true when the caller should move on.
  Future<bool> submit({
    required String email,
    required String username,
    required String password,
  }) async {
    busy = true;
    errorKey = null;
    notifyIfAlive();
    try {
      if (registering) {
        await _auth.register(email.trim(), username.trim(), password);
      } else {
        await _auth.login(email.trim(), password);
      }
      return true;
    } on AuthFailure catch (e) {
      errorKey = _keyFor(e.code);
      return false;
    } finally {
      busy = false;
      notifyIfAlive();
    }
  }

  static String _keyFor(String code) => switch (code) {
    'invalid-credentials' => 'auth.login.errors.invalid-credentials',
    'account-inactive' => 'auth.login.errors.account-inactive',
    'email-taken' => 'auth.register.errors.email-taken',
    'username-taken' => 'auth.register.errors.username-taken',
    'network' => 'auth.errors.network',
    _ => 'auth.errors.generic',
  };
}
