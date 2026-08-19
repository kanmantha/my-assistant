import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../providers/assistant_provider.dart';
import '../providers/productivity_providers.dart';
import '../providers/wake_word_provider.dart';
import 'appointments_screen.dart';
import 'assistant_screen.dart';
import 'dashboard_screen.dart';
import 'notes_screen.dart';
import 'settings_screen.dart';
import 'tasks_screen.dart';

class HomeShell extends StatefulWidget {
  const HomeShell({super.key});

  @override
  State<HomeShell> createState() => _HomeShellState();
}

class _HomeShellState extends State<HomeShell> {
  int _index = 0;
  WakeWordProvider? _wake;

  static const _screens = [
    DashboardScreen(),
    TasksScreen(),
    AssistantScreen(),
    NotesScreen(),
    AppointmentsScreen(),
    SettingsScreen(),
  ];

  @override
  void initState() {
    super.initState();
    _wake = context.read<WakeWordProvider>();
    _wake?.onWake = _handleWake;
  }

  void _handleWake() {
    if (!mounted) return;
    context.read<AssistantProvider>().requestAutoListen();
    setState(() => _index = 2);
  }

  @override
  void dispose() {
    _wake?.onWake = null;
    _wake = null;
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final wake = context.watch<WakeWordProvider>();
    final isError = wake.status.contains('unavailable') ||
        wake.status.contains('Failed') ||
        wake.status.contains('Disabled') ||
        wake.status.contains('retry');

    return Scaffold(
      body: SafeArea(
        child: Column(
          children: [
            if (wake.enabled)
              GestureDetector(
                onTap: isError ? () => wake.retry() : null,
                child: Container(
                  width: double.infinity,
                  padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 8),
                  color: wake.running && wake.listening
                      ? Colors.green.withValues(alpha: 0.15)
                      : isError
                          ? Colors.orange.withValues(alpha: 0.15)
                          : Colors.deepPurple.withValues(alpha: 0.08),
                  child: Row(
                    children: [
                      Icon(
                        wake.running && wake.listening
                            ? Icons.mic
                            : isError
                                ? Icons.error_outline
                                : Icons.wb_twilight,
                        size: 16,
                        color: wake.running && wake.listening
                            ? Colors.green
                            : isError
                                ? Colors.orange
                                : Colors.deepPurple,
                      ),
                      const SizedBox(width: 8),
                      Expanded(
                        child: Text(
                          wake.status,
                          style: TextStyle(
                            fontSize: 12,
                            color: wake.running && wake.listening
                                ? Colors.green.shade700
                                : isError
                                    ? Colors.orange.shade700
                                    : Colors.deepPurple.shade700,
                            fontWeight: FontWeight.w500,
                          ),
                        ),
                      ),
                      if (isError)
                        Text(
                          'TAP TO RETRY',
                          style: TextStyle(
                            fontSize: 10,
                            fontWeight: FontWeight.bold,
                            color: Colors.orange.shade700,
                          ),
                        ),
                    ],
                  ),
                ),
              ),
            Expanded(
              child: IndexedStack(index: _index, children: _screens),
            ),
          ],
        ),
      ),
      bottomNavigationBar: NavigationBar(
        selectedIndex: _index,
        onDestinationSelected: (i) {
          setState(() => _index = i);
          // Reload data when switching to a CRUD tab
          if (i == 1) context.read<TasksState>().load();
          if (i == 3) context.read<NotesState>().load();
          if (i == 4) context.read<AppointmentsState>().load();
        },
        destinations: const [
          NavigationDestination(icon: Icon(Icons.dashboard_outlined), selectedIcon: Icon(Icons.dashboard), label: 'Home'),
          NavigationDestination(icon: Icon(Icons.checklist_outlined), selectedIcon: Icon(Icons.checklist), label: 'Tasks'),
          NavigationDestination(icon: Icon(Icons.auto_awesome_outlined), selectedIcon: Icon(Icons.auto_awesome), label: 'Assistant'),
          NavigationDestination(icon: Icon(Icons.note_alt_outlined), selectedIcon: Icon(Icons.note_alt), label: 'Notes'),
          NavigationDestination(icon: Icon(Icons.event_outlined), selectedIcon: Icon(Icons.event), label: 'Events'),
          NavigationDestination(icon: Icon(Icons.settings_outlined), selectedIcon: Icon(Icons.settings), label: 'Settings'),
        ],
      ),
      floatingActionButton: wake.running
          ? FloatingActionButton.small(
              onPressed: () => setState(() => _index = 2),
              tooltip: 'Say "Hey Assistant"',
              backgroundColor: wake.listening
                  ? Colors.green.shade100
                  : Colors.deepPurple.shade100,
              child: Icon(
                Icons.wb_twilight,
                color: wake.listening ? Colors.green : Colors.deepPurple,
              ),
            )
          : null,
    );
  }
}
