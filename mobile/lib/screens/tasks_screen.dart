import 'package:flutter/material.dart';
import 'package:intl/intl.dart';
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
  DateTime _selectedDate = DateTime.now();
  TimeOfDay _selectedTime = TimeOfDay.now();
  bool _hasDueDate = false;

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

  void _showAddSheet() {
    _titleCtrl.clear();
    _descCtrl.clear();
    _priority = 'Medium';
    _selectedDate = DateTime.now();
    _selectedTime = TimeOfDay.now();
    _hasDueDate = false;
    showModalBottomSheet(
      context: context,
      isScrollControlled: true,
      shape: const RoundedRectangleBorder(
        borderRadius: BorderRadius.vertical(top: Radius.circular(20)),
      ),
      builder: (ctx) => StatefulBuilder(
        builder: (ctx, setSheetState) {
          return Padding(
            padding: EdgeInsets.fromLTRB(20, 20, 20, MediaQuery.of(ctx).viewInsets.bottom + 20),
            child: SingleChildScrollView(
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
                      DropdownMenuItem(value: 'Critical', child: Text('Critical')),
                      DropdownMenuItem(value: 'Urgent', child: Text('Urgent')),
                    ],
                    onChanged: (v) => setSheetState(() => _priority = v ?? 'Medium'),
                  ),
                  const SizedBox(height: 12),
                  Row(
                    children: [
                      Expanded(
                        child: OutlinedButton.icon(
                          onPressed: () async {
                            final picked = await showDatePicker(
                              context: ctx,
                              initialDate: _selectedDate,
                              firstDate: DateTime(2020),
                              lastDate: DateTime(2030),
                            );
                            if (picked != null) setSheetState(() { _selectedDate = picked; _hasDueDate = true; });
                          },
                          icon: const Icon(Icons.calendar_today, size: 16),
                          label: Text(_hasDueDate ? DateFormat('dd MMM yyyy').format(_selectedDate) : 'Due date'),
                        ),
                      ),
                      const SizedBox(width: 8),
                      Expanded(
                        child: OutlinedButton.icon(
                          onPressed: () async {
                            final picked = await showTimePicker(
                              context: ctx,
                              initialTime: _selectedTime,
                            );
                            if (picked != null) setSheetState(() { _selectedTime = picked; _hasDueDate = true; });
                          },
                          icon: const Icon(Icons.access_time, size: 16),
                          label: Text(_hasDueDate ? _selectedTime.format(ctx) : 'Due time'),
                        ),
                      ),
                    ],
                  ),
                  const SizedBox(height: 16),
                  FilledButton(
                    onPressed: () async {
                      final title = _titleCtrl.text.trim();
                      if (title.isEmpty) return;
                      String? dueDateStr;
                      if (_hasDueDate) {
                        final dt = DateTime(_selectedDate.year, _selectedDate.month, _selectedDate.day, _selectedTime.hour, _selectedTime.minute);
                        dueDateStr = dt.toIso8601String();
                      }
                      try {
                        await context.read<TasksState>().add(
                          title,
                          description: _descCtrl.text.trim(),
                          priority: _priority,
                          dueDate: dueDateStr,
                        );
                        if (ctx.mounted) Navigator.pop(ctx);
                      } catch (e) {
                        if (ctx.mounted) {
                          ScaffoldMessenger.of(ctx).showSnackBar(
                            SnackBar(content: Text('Failed to save task: $e'), backgroundColor: Colors.red),
                          );
                        }
                      }
                    },
                    child: const Text('Add Task'),
                  ),
                ],
              ),
            ),
          );
        },
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
      'Critical' => Colors.red.shade900,
      'Urgent' => AppTheme.danger,
      'High' => Colors.deepOrange,
      'Low' => AppTheme.success,
      _ => AppTheme.primary,
    };
    final subtitleParts = <String>[];
    if (task.description.isNotEmpty) subtitleParts.add(task.description);
    if (task.dueDate != null && task.dueDate!.isNotEmpty) {
      subtitleParts.add('Due: ${task.dueDate}${task.dueTime != null && task.dueTime!.isNotEmpty ? ' at ${task.dueTime}' : ''}');
    }
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
        subtitle: subtitleParts.isNotEmpty ? Text(subtitleParts.join(' — '), maxLines: 1, overflow: TextOverflow.ellipsis) : null,
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