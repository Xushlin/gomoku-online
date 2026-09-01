import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../../../i18n/translations.dart';
import '../view_model/login_view_model.dart';

/// Renders and forwards intent. No business logic, no repository, no Dio.
class LoginView extends StatefulWidget {
  const LoginView({super.key});

  @override
  State<LoginView> createState() => _LoginViewState();
}

class _LoginViewState extends State<LoginView> {
  final _email = TextEditingController();
  final _username = TextEditingController();
  final _password = TextEditingController();

  @override
  void dispose() {
    _email.dispose();
    _username.dispose();
    _password.dispose();
    super.dispose();
  }

  /// No navigation here on purpose. A successful login flips
  /// `AuthRepository.signedIn`, the router's `redirect` sees "signed in, but at
  /// /login" and sends us to the lobby. A `context.go` here as well would be a second
  /// answer to the same question, and the two would disagree the first time one of
  /// them changed.
  Future<void> _submit(LoginViewModel vm) async {
    await vm.submit(
      email: _email.text,
      username: _username.text,
      password: _password.text,
    );
  }

  @override
  Widget build(BuildContext context) {
    final vm = context.watch<LoginViewModel>();
    final t = context.read<Translations>();

    return Scaffold(
      body: Center(
        child: SingleChildScrollView(
          padding: const EdgeInsets.all(24),
          child: ConstrainedBox(
            constraints: const BoxConstraints(maxWidth: 420),
            child: Card(
              child: Padding(
                padding: const EdgeInsets.all(24),
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.stretch,
                  children: [
                    Text(
                      t.t(vm.registering ? 'auth.register.title' : 'auth.login.title'),
                      style: Theme.of(context).textTheme.headlineSmall,
                    ),
                    const SizedBox(height: 20),
                    TextField(
                      controller: _email,
                      keyboardType: TextInputType.emailAddress,
                      autofillHints: const [AutofillHints.email],
                      decoration: InputDecoration(labelText: t.t('auth.login.email-label')),
                    ),
                    if (vm.registering) ...[
                      const SizedBox(height: 12),
                      TextField(
                        controller: _username,
                        decoration: InputDecoration(
                          labelText: t.t('auth.register.username-label'),
                        ),
                      ),
                    ],
                    const SizedBox(height: 12),
                    TextField(
                      controller: _password,
                      obscureText: true,
                      decoration: InputDecoration(labelText: t.t('auth.login.password-label')),
                    ),
                    if (vm.errorKey != null) ...[
                      const SizedBox(height: 12),
                      // The ViewModel hands over a KEY; turning it into words is the
                      // View's job, which is what keeps the locale out of the model.
                      Text(
                        t.t(vm.errorKey!),
                        style: TextStyle(color: Theme.of(context).colorScheme.error),
                      ),
                    ],
                    const SizedBox(height: 20),
                    FilledButton(
                      onPressed: vm.busy ? null : () => _submit(vm),
                      child: Text(
                        vm.busy
                            ? t.t(
                                vm.registering
                                    ? 'auth.register.submit-loading'
                                    : 'auth.login.submit-loading',
                              )
                            : t.t(vm.registering ? 'auth.register.submit' : 'auth.login.submit'),
                      ),
                    ),
                    const SizedBox(height: 8),
                    TextButton(
                      onPressed: vm.busy ? null : vm.toggleMode,
                      child: Text(
                        t.t(
                          vm.registering
                              ? 'auth.register.already-have-account-cta'
                              : 'auth.login.no-account-cta',
                        ),
                      ),
                    ),
                  ],
                ),
              ),
            ),
          ),
        ),
      ),
    );
  }
}
