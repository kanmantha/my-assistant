import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../providers/wake_word_provider.dart';
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
    SettingsScreen(),
  ];

  @override
  void initState() {
    super.initState();
    _wake = context.read<WakeWordProvider>();
    WidgetsBinding.instance.addPostFrameCallback((_) {
      // Wake word heard → bring the user to the Assistant.
      _wake?.onWake = () {
        if (!mounted) return;
        setState(() => _index = 2);
      };
    });
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
    return Scaffold(
      body: IndexedStack(index: _index, children: _screens),
      bottomNavigationBar: NavigationBar(
        selectedIndex: _index,
        onDestinationSelected: (i) => setState(() => _index = i),
        destinations: const [
          NavigationDestination(icon: Icon(Icons.dashboard_outlined), selectedIcon: Icon(Icons.dashboard), label: 'Home'),
          NavigationDestination(icon: Icon(Icons.checklist_outlined), selectedIcon: Icon(Icons.checklist), label: 'Tasks'),
          NavigationDestination(icon: Icon(Icons.auto_awesome_outlined), selectedIcon: Icon(Icons.auto_awesome), label: 'Assistant'),
          NavigationDestination(icon: Icon(Icons.note_alt_outlined), selectedIcon: Icon(Icons.note_alt), label: 'Notes'),
          NavigationDestination(icon: Icon(Icons.settings_outlined), selectedIcon: Icon(Icons.settings), label: 'Settings'),
        ],
      ),
      floatingActionButton: wake.running && !wake.listening
          ? FloatingActionButton.small(
              onPressed: () => setState(() => _index = 2),
              tooltip: 'Wake word active — say "Hey Assistant"',
              backgroundColor: Colors.deepPurple.shade100,
              child: const Icon(Icons.wb_twilight, color: Colors.deepPurple),
            )
          : null,
    );
  }
}