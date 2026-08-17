import 'dart:async';

import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../models/models.dart';
import '../providers/assistant_provider.dart';
import '../providers/auth_provider.dart';
import '../providers/wake_word_provider.dart';
import '../services/api_client.dart';
import '../services/backend_client.dart';
import '../services/secure_store.dart';
import '../theme.dart';
import 'home_shell.dart';

class AuthShell extends StatefulWidget {
  const AuthShell({super.key});

  @override
  State<AuthShell> createState() => _AuthShellState();
}

enum _AuthMode { login, register }

class _AuthShellState extends State<AuthShell> {
  _AuthMode _mode = _AuthMode.login;
  final _formKey = GlobalKey<FormState>();
  final _nameCtrl = TextEditingController();
  final _emailCtrl = TextEditingController();
  final _passwordCtrl = TextEditingController();
  final _phoneCtrl = TextEditingController();
  String _language = 'en-IN';
  bool _obscure = true;
  bool _loading = false;

  @override
  void dispose() {
    _nameCtrl.dispose();
    _emailCtrl.dispose();
    _passwordCtrl.dispose();
    _phoneCtrl.dispose();
    super.dispose();
  }

  Future<void> _submit() async {
    if (!_formKey.currentState!.validate()) return;
    setState(() => _loading = true);
    final auth = context.read<AuthProvider>();
    final backend = context.read<BackendClient>();
    final api = context.read<ApiClient>();
    final wake = context.read<WakeWordProvider>();
    final assistant = context.read<AssistantProvider>();
    try {
      AuthResult result;
      if (_mode == _AuthMode.login) {
        result = await backend.login(email: _emailCtrl.text.trim(), password: _passwordCtrl.text);
      } else {
        result = await backend.register(
          fullName: _nameCtrl.text.trim(),
          email: _emailCtrl.text.trim(),
          password: _passwordCtrl.text,
          phone: _phoneCtrl.text.trim(),
          language: _language,
        );
      }
      await SecureStore.saveTokens(result.accessToken, result.refreshToken);
      api.setTokens(result.accessToken, result.refreshToken);
      await SecureStore.setDemoMode(false);
      auth.setSessionFromAuth(result);
      await assistant.setDemoMode();
      // Pull the saved server setting now that the token is set, so the wake
      // word toggle reflects what was stored server-side (e.g. after reinstall).
      unawaited(wake.restore());
      if (mounted) {
        Navigator.of(context).pushAndRemoveUntil(
          MaterialPageRoute(builder: (_) => const HomeShell()),
          (route) => false,
        );
      }
    } catch (e) {
      if (mounted) {
        final msg = e is ApiException ? e.message : e.toString();
        ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(content: Text(msg)),
        );
      }
    } finally {
      if (mounted) setState(() => _loading = false);
    }
  }

  Future<void> _enterDemo() async {
    final auth = context.read<AuthProvider>();
    final assistant = context.read<AssistantProvider>();
    await SecureStore.setDemoMode(true);
    auth.setDemoAuth(profile: demoProfile);
    await assistant.setDemoMode();
    if (mounted) {
      Navigator.of(context).pushAndRemoveUntil(
        MaterialPageRoute(builder: (_) => const HomeShell()),
        (route) => false,
      );
    }
  }

  @override
  Widget build(BuildContext context) {
    final isLogin = _mode == _AuthMode.login;
    return Scaffold(
      appBar: AppBar(
        title: Text(isLogin ? 'Welcome back' : 'Create your account'),
        centerTitle: true,
      ),
      body: SingleChildScrollView(
        padding: const EdgeInsets.all(24),
        child: Form(
          key: _formKey,
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.stretch,
            children: [
              const SizedBox(height: 8),
              Icon(Icons.auto_awesome, color: AppTheme.primary, size: 56),
              const SizedBox(height: 24),
              if (!isLogin) ...[
                TextFormField(
                  controller: _nameCtrl,
                  decoration: const InputDecoration(labelText: 'Full name', prefixIcon: Icon(Icons.person_outline)),
                  validator: (v) => (v == null || v.trim().isEmpty) ? 'Full name is required' : null,
                ),
                const SizedBox(height: 14),
              ],
              TextFormField(
                controller: _emailCtrl,
                keyboardType: TextInputType.emailAddress,
                decoration: const InputDecoration(labelText: 'Email', prefixIcon: Icon(Icons.email_outlined)),
                validator: (v) => (v == null || !v.contains('@')) ? 'Enter a valid email' : null,
              ),
              const SizedBox(height: 14),
              TextFormField(
                controller: _passwordCtrl,
                obscureText: _obscure,
                decoration: InputDecoration(
                  labelText: 'Password',
                  prefixIcon: const Icon(Icons.lock_outline),
                  suffixIcon: IconButton(
                    icon: Icon(_obscure ? Icons.visibility_off : Icons.visibility),
                    onPressed: () => setState(() => _obscure = !_obscure),
                  ),
                ),
                validator: (v) => isLogin
                    ? (v == null || v.isEmpty) ? 'Password is required' : null
                    : (v == null || v.length < 8) ? 'Password must be at least 8 characters' : null,
              ),
              if (!isLogin) ...[
                const SizedBox(height: 14),
                TextFormField(
                  controller: _phoneCtrl,
                  keyboardType: TextInputType.phone,
                  decoration: const InputDecoration(labelText: 'Phone (optional)', prefixIcon: Icon(Icons.phone_outlined)),
                ),
                const SizedBox(height: 14),
                DropdownButtonFormField<String>(
                  initialValue: _language,
                  decoration: const InputDecoration(labelText: 'Preferred language', prefixIcon: Icon(Icons.translate)),
                  items: const [
                    DropdownMenuItem(value: 'en-IN', child: Text('English')),
                    DropdownMenuItem(value: 'hi-IN', child: Text('Hindi (हिन्दी)')),
                    DropdownMenuItem(value: 'te-IN', child: Text('Telugu (తెలుగు)')),
                  ],
                  onChanged: (v) => setState(() => _language = v ?? 'en-IN'),
                ),
              ],
              const SizedBox(height: 24),
              ElevatedButton(
                onPressed: _loading ? null : _submit,
                child: _loading
                    ? const SizedBox(height: 20, width: 20, child: CircularProgressIndicator(strokeWidth: 2, color: Colors.white))
                    : Text(isLogin ? 'Sign In' : 'Create Account'),
              ),
              const SizedBox(height: 10),
              TextButton(
                onPressed: () {
                  setState(() {
                    _mode = isLogin ? _AuthMode.register : _AuthMode.login;
                  });
                },
                child: Text(isLogin ? "Don't have an account? Register" : 'Already have an account? Sign in'),
              ),
              const SizedBox(height: 8),
              TextButton.icon(
                onPressed: _enterDemo,
                icon: const Icon(Icons.auto_awesome),
                label: const Text('Continue with Demo Mode'),
              ),
            ],
          ),
        ),
      ),
    );
  }
}