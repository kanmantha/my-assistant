import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../models/models.dart';
import '../providers/auth_provider.dart';
import '../providers/productivity_providers.dart';
import '../theme.dart';

class TasksScreen extends StatefulWidget {
  const TasksScreen({super.key});

  @override
  State<TasksScreen> createState() => _TasksScreenState();
}

class _TasksScreenState extends State<TasksScreen> {
  final _titleCtrl = TextEditingController();
  final _descCtrl = TextEditingController();
  String _priority = 'Medium';

  @override
  void initState() {
    super.initState();
WidgetsBinding.instance.addPostFrameCallback((_) {
      final state = context.read<TasksState>();
      if (!state.busy) {
        if (context.read<AuthProvider>().demoMode) {
          state.setDemo();
        } else {
          state.load();
        }
      }
    });
  }

  @override
  void dispose() {
    _titleCtrl.dispose();
    _descCtrl.dispose();
    super.dispose();
  }

  Future<void> _add() async {
    final title = _titleCtrl.text.trim();
    if (title.isEmpty) return;
    await context.read<TasksState>().add(title, description: _descCtrl.text.trim(), priority: _priority);
    if (mounted) {
      _titleCtrl.clear();
      _descCtrl.clear();
      Navigator.of(context).pop();
    }
  }

  void _showAddSheet() {
    _titleCtrl.clear();
    _descCtrl.clear();
    _priority = 'Medium';
    showModalBottomSheet(
      context: context,
      isScrollControlled: true,
      builder: (context) => Padding(
        padding: EdgeInsets.only(bottom: MediaQuery.of(context).viewInsets.bottom),
        child: SingleChildScrollView(
          padding: const EdgeInsets.all(20),
          child: Column(
            mainAxisSize: MainAxisSize.min,
            crossAxisAlignment: CrossAxisAlignment.stretch,
            children: [
              const Text('New Task', style: TextStyle(fontSize: 20, fontWeight: FontWeight.bold)),
              const SizedBox(height: 16),
              TextField(
                controller: _titleCtrl,
                decoration: const InputDecoration(labelText: 'Task title'),
              ),
              const SizedBox(height: 12),
              TextField(
                controller: _descCtrl,
                decoration: const InputDecoration(labelText: 'Description (optional)'),
              ),
              const SizedBox(height: 12),
              DropdownButtonFormField<String>(
                initialValue: _priority,
                decoration: const InputDecoration(labelText: 'Priority'),
                items: const [
                  DropdownMenuItem(value: 'Low', child: Text('Low')),
                  DropdownMenuItem(value: 'Medium', child: Text('Medium')),
                  DropdownMenuItem(value: 'High', child: Text('High')),
                  DropdownMenuItem(value: 'Urgent', child: Text('Urgent')),
                ],
                onChanged: (v) => setState(() => _priority = v ?? 'Medium'),
              ),
              const SizedBox(height: 16),
              ElevatedButton(onPressed: _add, child: const Text('Add Task')),
            ],
          ),
        ),
      ),
    );
  }

  @override
  Widget build(BuildContext context) {
    final state = context.watch<TasksState>();
    return Scaffold(
      appBar: AppBar(title: const Text('Tasks')),
      floatingActionButton: FloatingActionButton(
        backgroundColor: AppTheme.primary,
        onPressed: _showAddSheet,
        child: const Icon(Icons.add),
      ),
      body: state.busy && state.tasks.isEmpty
          ? const Center(child: CircularProgressIndicator())
          : state.tasks.isEmpty
              ? const Center(child: Text('No tasks yet. Tap + to add one.'))
              : RefreshIndicator(
                  onRefresh: state.load,
                  child: ListView.builder(
                    padding: const EdgeInsets.all(12),
                    itemCount: state.tasks.length,
                    itemBuilder: (context, i) {
                      final t = state.tasks[i];
                      return _TaskTile(
                        task: t,
                        onToggle: () => state.toggle(t.id),
                        onDelete: () => state.remove(t.id),
                      );
                    },
                  ),
                ),
    );
  }
}

class _TaskTile extends StatelessWidget {
  final TaskItem task;
  final VoidCallback onToggle;
  final VoidCallback onDelete;

  const _TaskTile({required this.task, required this.onToggle, required this.onDelete});

  @override
  Widget build(BuildContext context) {
    final done = task.status == 'Completed';
    final color = switch (task.priority) {
      'Urgent' => AppTheme.danger,
      'High' => Colors.deepOrange,
      'Low' => AppTheme.success,
      _ => AppTheme.primary,
    };
    return Card(
      elevation: 0,
      color: Theme.of(context).colorScheme.surface,
      margin: const EdgeInsets.symmetric(vertical: 5),
      shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(14)),
      child: ListTile(
        leading: IconButton(
          onPressed: onToggle,
          icon: Icon(done ? Icons.check_circle : Icons.radio_button_unchecked, color: done ? AppTheme.success : Colors.black38),
        ),
        title: Text(
          task.title,
          style: TextStyle(decoration: done ? TextDecoration.lineThrough : null, color: done ? Colors.black38 : null),
        ),
        subtitle: task.description.isNotEmpty ? Text(task.description, maxLines: 1, overflow: TextOverflow.ellipsis) : null,
        trailing: Row(
          mainAxisSize: MainAxisSize.min,
          children: [
            Container(
              padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 3),
              decoration: BoxDecoration(color: color.withValues(alpha: 0.12), borderRadius: BorderRadius.circular(8)),
              child: Text(task.priority, style: TextStyle(color: color, fontSize: 11, fontWeight: FontWeight.w600)),
            ),
            IconButton(
              onPressed: onDelete,
              icon: const Icon(Icons.delete_outline, size: 20, color: Colors.black38),
            ),
          ],
        ),
      ),
    );
  }
}