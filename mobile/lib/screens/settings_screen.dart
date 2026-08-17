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
import 'onboarding_screen.dart';

class SettingsScreen extends StatefulWidget {
  const SettingsScreen({super.key});

  @override
  State<SettingsScreen> createState() => _SettingsScreenState();
}

class _SettingsScreenState extends State<SettingsScreen> {
  bool _voiceEnabled = true;
  String _language = 'en-IN';
  String _theme = 'System';

  @override
  void initState() {
    super.initState();
  }

  Future<void> _refresh() async {
    final backend = context.read<BackendClient>();
    try {
      final usage = await backend.usage();
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(content: Text('Plan: ${usage.planCode} • AI ${usage.aiRequests}/${usage.aiLimit == -1 ? '∞' : usage.aiLimit}')),
        );
      }
    } on ApiException catch (e) {
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(content: Text('Backend unreachable (${e.message})')),
        );
      }
    } catch (e) {
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text('$e')));
      }
    }
  }

  Future<void> _toggleDemo(bool value) async {
    final auth = context.read<AuthProvider>();
    final assistant = context.read<AssistantProvider>();
    await SecureStore.setDemoMode(value);
    if (value) {
      auth.setDemoAuth(profile: demoProfile);
      await assistant.setDemoMode();
    } else {
      await auth.signOut();
      await assistant.setDemoMode();
    }
    if (mounted) {
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(content: Text(value ? 'Demo mode enabled' : 'Signed out of demo mode')),
      );
    }
  }

  Future<void> _signOut() async {
    final auth = context.read<AuthProvider>();
    final assistant = context.read<AssistantProvider>();
    await SecureStore.clearTokens();
    await SecureStore.setDemoMode(false);
    await auth.signOut();
    await assistant.setDemoMode();
    if (mounted) {
      Navigator.of(context).pushAndRemoveUntil(
        MaterialPageRoute(builder: (_) => const OnboardingScreen()),
        (route) => false,
      );
    }
  }

  @override
  Widget build(BuildContext context) {
    final auth = context.watch<AuthProvider>();
    final profile = auth.profile;
    final theme = Theme.of(context);

    return Scaffold(
      appBar: AppBar(title: const Text('Settings')),
      body: ListView(
        padding: const EdgeInsets.all(16),
        children: [
          Card(
            elevation: 0,
            color: theme.colorScheme.surface,
            shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(16)),
            child: ListTile(
              leading: CircleAvatar(
                backgroundColor: AppTheme.primary,
                child: Text(
                  profile?.fullName.isNotEmpty == true ? profile!.fullName[0].toUpperCase() : '?',
                  style: const TextStyle(color: Colors.white, fontWeight: FontWeight.bold),
                ),
              ),
              title: Text(profile?.fullName ?? 'Guest'),
              subtitle: Text(profile?.email ?? (auth.demoMode ? 'Demo Mode Active' : 'Not signed in')),
              trailing: auth.demoMode
                  ? Chip(label: const Text('DEMO'), backgroundColor: AppTheme.success.withOpacity(0.15), labelStyle: const TextStyle(color: AppTheme.success, fontSize: 12))
                  : null,
            ),
          ),
          const SizedBox(height: 12),
          Text('Preferences', style: TextStyle(fontWeight: FontWeight.w700, color: theme.colorScheme.onSurface)),
          const SizedBox(height: 6),
          _SectionCard(
            children: [
              SwitchListTile(
                value: _theme == 'Light',
                onChanged: (v) => setState(() => _theme = v ? 'Light' : 'Dark'),
                title: const Text('Light theme'),
                secondary: const Icon(Icons.light_mode_outlined),
              ),
              const Divider(height: 1),
              SwitchListTile(
                value: _voiceEnabled,
                onChanged: (v) => setState(() => _voiceEnabled = v),
                title: const Text('Voice assistant'),
                subtitle: const Text('Voice input & text-to-speech'),
                secondary: const Icon(Icons.mic_none),
              ),
              const Divider(height: 1),
              Consumer<WakeWordProvider>(
                builder: (context, wake, _) {
                  final busy = wake.enabled && !wake.running;
                  return Column(
                    children: [
                      SwitchListTile(
                        value: wake.enabled,
                        onChanged: (v) {
                          if (v) {
                            ScaffoldMessenger.of(context).showSnackBar(
                              const SnackBar(
                                content: Text('Listening for "Hey Assistant"… Microphone permission may be requested.'),
                                duration: Duration(seconds: 3),
                              ),
                            );
                          }
                          wake.setEnabled(v);
                        },
                        title: const Text('Wake word'),
                        subtitle: Text(
                          busy
                              ? 'Starting microphone…'
                              : wake.running
                                  ? 'Active — say "Hey Assistant"'
                                  : '"Hey Assistant" (off)',
                        ),
                        secondary: busy
                            ? const SizedBox(
                                height: 20,
                                width: 20,
                                child: CircularProgressIndicator(strokeWidth: 2),
                              )
                            : Icon(
                                wake.running ? Icons.hearing : Icons.speaker,
                                color: wake.running ? AppTheme.success : null,
                              ),
                      ),
                      if (wake.isWeb)
                        const Padding(
                          padding: EdgeInsets.fromLTRB(16, 0, 16, 10),
                          child: Text(
                            'Wake word works best on Android. On web, speech recognition may be interrupted by the browser.',
                            style: TextStyle(fontSize: 12, color: AppTheme.danger),
                          ),
                        ),
                    ],
                  );
                },
              ),
              const Divider(height: 1),
              ListTile(
                leading: const Icon(Icons.translate),
                title: const Text('Assistant language'),
                trailing: DropdownButton<String>(
                  value: _language,
                  underline: const SizedBox.shrink(),
                  items: const [
                    DropdownMenuItem(value: 'en-IN', child: Text('English')),
                    DropdownMenuItem(value: 'hi-IN', child: Text('हिन्दी')),
                    DropdownMenuItem(value: 'te-IN', child: Text('తెలుగు')),
                  ],
                  onChanged: (v) {
                    setState(() => _language = v ?? 'en-IN');
                    context.read<AssistantProvider>().setLanguage(_language);
                    context.read<AssistantProvider>().addAssistantText(
                      _language == 'hi-IN'
                          ? 'भाषा बदल दी है — हिन्दी'
                          : _language == 'te-IN'
                              ? 'భాష మార్చబడింది — తెలుగు'
                              : 'Language switched to English',
                    );
                  },
                ),
              ),
            ],
          ),
          const SizedBox(height: 12),
          Text('Account', style: TextStyle(fontWeight: FontWeight.w700, color: theme.colorScheme.onSurface)),
          const SizedBox(height: 6),
          _SectionCard(
            children: [
              ListTile(
                leading: const Icon(Icons.refresh),
                title: const Text('Refresh subscription'),
                subtitle: const Text('Check plan usage from server'),
                onTap: _refresh,
              ),
              const Divider(height: 1),
              SwitchListTile(
                value: auth.demoMode,
                onChanged: _toggleDemo,
                title: const Text('Demo mode'),
                subtitle: const Text('Offline canned responses'),
                secondary: const Icon(Icons.auto_awesome),
              ),
              const Divider(height: 1),
              ListTile(
                leading: const Icon(Icons.logout),
                title: const Text('Sign out'),
                textColor: AppTheme.danger,
                onTap: _signOut,
              ),
            ],
          ),
          const SizedBox(height: 20),
          Center(
            child: Text(
              'My Assistant v1.0.0\nEnglish • हिन्दी • తెలుగు',
              textAlign: TextAlign.center,
              style: TextStyle(color: theme.colorScheme.onSurfaceVariant, fontSize: 12),
            ),
          ),
        ],
      ),
    );
  }
}

class _SectionCard extends StatelessWidget {
  final List<Widget> children;
  const _SectionCard({required this.children});

  @override
  Widget build(BuildContext context) {
    return Card(
      elevation: 0,
      color: Theme.of(context).colorScheme.surface,
      shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(16)),
      child: Column(children: children),
    );
  }
}