import 'package:flutter/material.dart';

import '../api/api_client.dart';
import '../app.dart';

class LoginScreen extends StatefulWidget {
  const LoginScreen({super.key, required this.services, required this.onSignedIn});

  final AppServices services;
  final VoidCallback onSignedIn;

  @override
  State<LoginScreen> createState() => _LoginScreenState();
}

class _LoginScreenState extends State<LoginScreen> {
  final _email = TextEditingController();
  final _username = TextEditingController();
  final _password = TextEditingController();
  bool _registering = false;
  bool _busy = false;
  String? _error;

  @override
  void dispose() {
    _email.dispose();
    _username.dispose();
    _password.dispose();
    super.dispose();
  }

  String _t(String key) => widget.services.strings.t(key);

  Future<void> _submit() async {
    setState(() {
      _busy = true;
      _error = null;
    });
    try {
      final auth = _registering
          ? await widget.services.api.register(
              _email.text.trim(),
              _username.text.trim(),
              _password.text,
            )
          : await widget.services.api.login(_email.text.trim(), _password.text);
      // 认证响应里用户名在 `user` 下,不是顶层。写错的表现是房间名一律叫 `mobile-…`,
      // 而那看起来只是「名字没起好」,不像一个读错了字段的 bug。
      widget.services.username = (auth['user'] as Map<String, dynamic>?)?['username'] as String?;
      if (mounted) widget.onSignedIn();
    } on ApiException catch (e) {
      // The server's code drives the message — never its prose. Falling back to a
      // generic key is honest; guessing meaning from English is what the web client
      // removed.
      setState(() => _error = _messageFor(e));
    } catch (_) {
      setState(() => _error = _t('auth.errors.network'));
    } finally {
      if (mounted) setState(() => _busy = false);
    }
  }

  String _messageFor(ApiException e) {
    const byCode = <String, String>{
      'invalid-credentials': 'auth.login.errors.invalid-credentials',
      'account-inactive': 'auth.login.errors.account-inactive',
      'email-taken': 'auth.register.errors.email-taken',
      'username-taken': 'auth.register.errors.username-taken',
    };
    final key = byCode[e.code];
    return key == null ? _t('auth.errors.generic') : _t(key);
  }

  @override
  Widget build(BuildContext context) {
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
                      _t(_registering ? 'auth.register.title' : 'auth.login.title'),
                      style: Theme.of(context).textTheme.headlineSmall,
                    ),
                    const SizedBox(height: 20),
                    TextField(
                      controller: _email,
                      keyboardType: TextInputType.emailAddress,
                      autofillHints: const [AutofillHints.email],
                      decoration: InputDecoration(labelText: _t('auth.login.email-label')),
                    ),
                    if (_registering) ...[
                      const SizedBox(height: 12),
                      TextField(
                        controller: _username,
                        decoration: InputDecoration(
                          labelText: _t('auth.register.username-label'),
                        ),
                      ),
                    ],
                    const SizedBox(height: 12),
                    TextField(
                      controller: _password,
                      obscureText: true,
                      decoration: InputDecoration(labelText: _t('auth.login.password-label')),
                    ),
                    if (_error != null) ...[
                      const SizedBox(height: 12),
                      Text(_error!, style: TextStyle(color: Theme.of(context).colorScheme.error)),
                    ],
                    const SizedBox(height: 20),
                    FilledButton(
                      onPressed: _busy ? null : _submit,
                      child: Text(
                        _busy
                            ? _t(
                                _registering
                                    ? 'auth.register.submit-loading'
                                    : 'auth.login.submit-loading',
                              )
                            : _t(_registering ? 'auth.register.submit' : 'auth.login.submit'),
                      ),
                    ),
                    const SizedBox(height: 8),
                    TextButton(
                      onPressed: _busy ? null : () => setState(() => _registering = !_registering),
                      child: Text(
                        _t(
                          _registering
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
