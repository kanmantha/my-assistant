import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:intl/intl.dart';
import 'package:provider/provider.dart';

import '../providers/auth_provider.dart';
import '../services/chrome_launcher.dart';
import '../theme.dart';
import 'assistant_screen.dart';
import 'notes_screen.dart';

class DashboardScreen extends StatelessWidget {
  const DashboardScreen({super.key});

  @override
  Widget build(BuildContext context) {
    final auth = context.watch<AuthProvider>();
    final profile = auth.profile;
    final hour = DateTime.now().hour;
    final greeting = hour < 12 ? 'Good Morning' : hour < 17 ? 'Good Afternoon' : 'Good Evening';

    return Scaffold(
      appBar: AppBar(
        title: Text(greeting),
        actions: [
          if (profile != null)
            Padding(
              padding: const EdgeInsets.only(right: 16),
              child: CircleAvatar(
                backgroundColor: AppTheme.primary,
                child: Text(
                  profile.fullName.isNotEmpty ? profile.fullName[0].toUpperCase() : '?',
                  style: const TextStyle(color: Colors.white, fontWeight: FontWeight.bold),
                ),
              ),
            ),
        ],
      ),
      body: ListView(
        padding: const EdgeInsets.all(16),
        children: [
          _OrbCard(greeting: greeting, name: profile?.fullName ?? 'Guest'),
          const SizedBox(height: 16),
          if (profile != null) _PlanCard(profile: profile),
          const SizedBox(height: 16),
          _TodaySummaryIcons(),
          const SizedBox(height: 16),
          _QuickActions(),
        ],
      ),
    );
  }
}

class _OrbCard extends StatelessWidget {
  final String greeting;
  final String name;

  const _OrbCard({required this.greeting, required this.name});

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.all(20),
      decoration: BoxDecoration(
        gradient: const LinearGradient(colors: [AppTheme.primary, Color(0xFF8A7BFF)]),
        borderRadius: BorderRadius.circular(22),
      ),
      child: Row(
        children: [
          Container(
            width: 84,
            height: 84,
            decoration: BoxDecoration(
              shape: BoxShape.circle,
              color: Colors.white.withOpacity(0.15),
              border: Border.all(color: Colors.white.withOpacity(0.35), width: 2),
              boxShadow: [BoxShadow(color: Colors.white.withOpacity(0.2), blurRadius: 24)],
            ),
            child: const Icon(Icons.auto_awesome, color: Colors.white, size: 40),
          ),
          const SizedBox(width: 18),
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text('$greeting,',
                    style: const TextStyle(color: Colors.white70, fontSize: 15)),
                Text('$name!',
                    style: const TextStyle(color: Colors.white, fontSize: 22, fontWeight: FontWeight.w700)),
                const SizedBox(height: 6),
                Text('How can I help you today? Tap the assistant orb.',
                    style: TextStyle(color: Colors.white.withOpacity(0.85), fontSize: 13)),
              ],
            ),
          ),
        ],
      ),
    );
  }
}

class _PlanCard extends StatelessWidget {
  final dynamic profile;

  const _PlanCard({required this.profile});

  @override
  Widget build(BuildContext context) {
    final code = profile.planCode ?? 'FREE';
    final used = profile.usageAi ?? 0;
    final limit = profile.usageAiLimit ?? 0;
    final isUnlimited = limit <= 0;
    final percent = isUnlimited ? 0.0 : (used / limit).clamp(0.0, 1.0);

    final planColor = switch (code) {
      'PREMIUM' => AppTheme.primary,
      'PRO' => const Color(0xFF00B894),
      _ => Colors.blueGrey,
    };

    return Container(
      padding: const EdgeInsets.all(18),
      decoration: BoxDecoration(
        color: Theme.of(context).colorScheme.surface,
        borderRadius: BorderRadius.circular(18),
        border: Border.all(color: Color(0xFFE3E7EE)),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(
            children: [
              Icon(Icons.stars, color: planColor),
              const SizedBox(width: 8),
              Text('$code Plan', style: const TextStyle(fontWeight: FontWeight.w700, fontSize: 16)),
              const Spacer(),
              Chip(
                label: Text('AI: ${isUnlimited ? "Unlimited" : "$used / $limit"}'),
                backgroundColor: planColor.withOpacity(0.1),
                labelStyle: TextStyle(color: planColor, fontSize: 12),
              ),
            ],
          ),
          const SizedBox(height: 10),
          if (!isUnlimited) ...[
            ClipRRect(
              borderRadius: BorderRadius.circular(6),
              child: LinearProgressIndicator(value: percent, minHeight: 6, backgroundColor: Colors.black12, color: planColor),
            ),
            const SizedBox(height: 6),
            Text('$used of $limit AI requests used this month',
                style: const TextStyle(fontSize: 12, color: Colors.black54)),
          ],
        ],
      ),
    );
  }
}

class _TodaySummaryIcons extends StatelessWidget {
  @override
  Widget build(BuildContext context) {
    final now = DateFormat('EEE, d MMM').format(DateTime.now());
    return Card(
      elevation: 0,
      color: Theme.of(context).colorScheme.surface,
      shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(18)),
      child: Padding(
        padding: const EdgeInsets.all(16),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Row(
              children: [
                const Icon(Icons.calendar_today, size: 18, color: AppTheme.primary),
                const SizedBox(width: 8),
                Text(now, style: const TextStyle(fontWeight: FontWeight.w600)),
              ],
            ),
            const SizedBox(height: 12),
            Row(
              mainAxisAlignment: MainAxisAlignment.spaceAround,
              children: [
                _CountItem(icon: Icons.checklist, label: 'Tasks', count: 0, color: AppTheme.primary),
                _CountItem(icon: Icons.alarm, label: 'Reminders', count: 0, color: const Color(0xFFE17055)),
                _CountItem(icon: Icons.event, label: 'Appointments', count: 0, color: const Color(0xFF00B894)),
              ],
            ),
          ],
        ),
      ),
    );
  }
}

class _CountItem extends StatelessWidget {
  final IconData icon;
  final String label;
  final int count;
  final Color color;

  const _CountItem({required this.icon, required this.label, required this.count, required this.color});

  @override
  Widget build(BuildContext context) {
    return Column(
      children: [
        Container(
          padding: const EdgeInsets.all(12),
          decoration: BoxDecoration(color: color.withOpacity(0.12), borderRadius: BorderRadius.circular(14)),
          child: Icon(icon, color: color, size: 26),
        ),
        const SizedBox(height: 6),
        Text('$count', style: const TextStyle(fontWeight: FontWeight.bold, fontSize: 16)),
        Text(label, style: const TextStyle(fontSize: 12, color: Colors.black54)),
      ],
    );
  }
}

class _QuickActions extends StatelessWidget {
  @override
  Widget build(BuildContext context) {
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Text('Quick actions', style: TextStyle(fontWeight: FontWeight.w600, color: Theme.of(context).colorScheme.onSurface)),
        const SizedBox(height: 10),
        Row(
          children: [
            Expanded(
              child: OutlinedButton.icon(
                onPressed: () => Navigator.of(context).push(
                  MaterialPageRoute(builder: (_) => const AssistantScreen()),
                ),
                icon: const Icon(Icons.mic),
                label: const Text('Ask Assistant'),
              ),
            ),
            const SizedBox(width: 10),
            Expanded(
              child: OutlinedButton.icon(
                onPressed: () => Navigator.of(context).push(
                  MaterialPageRoute(builder: (_) => const NotesScreen()),
                ),
                icon: const Icon(Icons.note_add_outlined),
                label: const Text('New Note'),
              ),
            ),
            const SizedBox(width: 10),
            Expanded(
              child: OutlinedButton.icon(
                onPressed: () => Navigator.of(context).push(
                  MaterialPageRoute(builder: (_) => const WebSearchScreen()),
                ),
                icon: const Icon(Icons.search),
                label: const Text('Search Web'),
              ),
            ),
          ],
        ),
      ],
    );
  }
}

/// Dedicated "Search the web" screen: type a query, open it in Chrome ONLY.
/// Chrome-only opening avoids the multi-browser chooser dialog.
class WebSearchScreen extends StatefulWidget {
  const WebSearchScreen({super.key});

  @override
  State<WebSearchScreen> createState() => _WebSearchScreenState();
}

class _WebSearchScreenState extends State<WebSearchScreen> {
  final _query = TextEditingController();
  bool _opening = false;
  String? _lastError;
  final List<String> _history = [];

  @override
  void dispose() {
    _query.dispose();
    super.dispose();
  }

  Future<void> _search(String query) async {
    final q = query.trim();
    if (q.isEmpty) return;
    setState(() {
      _opening = true;
      _lastError = null;
    });
    final url = 'https://www.google.com/search?q=${Uri.encodeQueryComponent(q)}';
    try {
      final ok = await ChromeOnlyLauncher.open(url);
      if (ok && mounted) {
        setState(() {
          if (!_history.contains(query)) _history.insert(0, query);
          if (_history.length > 5) _history.removeLast();
        });
      } else if (mounted) {
        setState(() => _lastError = 'No browser could open this link.');
      }
    } on PlatformException catch (e) {
      if (mounted) {
        setState(() => _lastError = 'Open failed: ${e.message ?? e.code}');
      }
    } on MissingPluginException {
      if (mounted) {
        setState(() => _lastError = 'Chrome opener is unavailable.');
      }
    } finally {
      if (mounted) setState(() => _opening = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(title: const Text('Search the web')),
      body: SafeArea(
        child: Padding(
          padding: const EdgeInsets.all(16),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.stretch,
            children: [
              TextField(
                controller: _query,
                autofocus: true,
                textInputAction: TextInputAction.search,
                onSubmitted: _search,
                decoration: InputDecoration(
                  hintText: 'Search via Chrome…',
                  prefixIcon: const Icon(Icons.search),
                  border: OutlineInputBorder(borderRadius: BorderRadius.circular(14)),
                ),
              ),
              const SizedBox(height: 12),
              ElevatedButton.icon(
                onPressed: _opening ? null : () => _search(_query.text),
                icon: _opening
                    ? const SizedBox(height: 18, width: 18, child: CircularProgressIndicator(strokeWidth: 2, color: Colors.white))
                    : const Icon(Icons.open_in_browser),
                label: const Text('Open in Chrome'),
              ),
              if (_lastError != null) ...[
                const SizedBox(height: 12),
                Text(_lastError!, style: const TextStyle(color: AppTheme.danger, fontSize: 13)),
              ],
              const SizedBox(height: 12),
              const Text(
                'Links are opened only in Google Chrome.',
                style: TextStyle(fontSize: 12, color: Colors.black54),
              ),
              if (_history.isNotEmpty) ...[
                const SizedBox(height: 12),
                Text('Recent', style: TextStyle(fontWeight: FontWeight.w600, color: Theme.of(context).colorScheme.onSurface)),
                const SizedBox(height: 4),
                for (final h in _history)
                  ListTile(
                    dense: true,
                    contentPadding: EdgeInsets.zero,
                    leading: const Icon(Icons.history),
                    title: Text(h, maxLines: 1, overflow: TextOverflow.ellipsis),
                    onTap: () => _search(h),
                  ),
              ],
            ],
          ),
        ),
      ),
    );
  }
}