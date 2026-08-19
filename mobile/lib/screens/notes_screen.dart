import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../models/models.dart';
import '../providers/auth_provider.dart';
import '../providers/productivity_providers.dart';
import '../theme.dart';

class NotesScreen extends StatefulWidget {
  const NotesScreen({super.key});

  @override
  State<NotesScreen> createState() => _NotesScreenState();
}

class _NotesScreenState extends State<NotesScreen> {
  final _titleCtrl = TextEditingController();
  final _contentCtrl = TextEditingController();

  @override
  void initState() {
    super.initState();
    WidgetsBinding.instance.addPostFrameCallback((_) {
      final state = context.read<NotesState>();
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
    _contentCtrl.dispose();
    super.dispose();
  }

  Future<void> _add() async {
    final title = _titleCtrl.text.trim();
    final content = _contentCtrl.text.trim();
    if (title.isEmpty && content.isEmpty) return;
    try {
      await context.read<NotesState>().add(title.isEmpty ? 'Untitled' : title, content);
      if (mounted) {
        _titleCtrl.clear();
        _contentCtrl.clear();
        Navigator.of(context).pop();
      }
    } catch (e) {
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(content: Text('Failed to save note: $e'), backgroundColor: Colors.red),
        );
      }
    }
  }

  void _showAddSheet() {
    _titleCtrl.clear();
    _contentCtrl.clear();
    showModalBottomSheet(
      context: context,
      isScrollControlled: true,
      builder: (ctx) => Padding(
        padding: EdgeInsets.only(bottom: MediaQuery.of(context).viewInsets.bottom),
        child: SingleChildScrollView(
          padding: const EdgeInsets.all(20),
          child: Column(
            mainAxisSize: MainAxisSize.min,
            crossAxisAlignment: CrossAxisAlignment.stretch,
            children: [
              const Text('New Note', style: TextStyle(fontSize: 20, fontWeight: FontWeight.bold)),
              const SizedBox(height: 16),
              TextField(controller: _titleCtrl, decoration: const InputDecoration(labelText: 'Title')),
              const SizedBox(height: 12),
              TextField(
                controller: _contentCtrl,
                maxLines: 6,
                decoration: const InputDecoration(labelText: 'Content'),
              ),
              const SizedBox(height: 16),
              ElevatedButton(onPressed: _add, child: const Text('Save Note')),
            ],
          ),
        ),
      ),
    );
  }

  void _openNote(Note note) {
    _titleCtrl.text = note.title;
    _contentCtrl.text = note.content;
    showModalBottomSheet(
      context: context,
      isScrollControlled: true,
      builder: (ctx) => Padding(
        padding: EdgeInsets.only(bottom: MediaQuery.of(context).viewInsets.bottom),
        child: SingleChildScrollView(
          padding: const EdgeInsets.all(20),
          child: Column(
            mainAxisSize: MainAxisSize.min,
            crossAxisAlignment: CrossAxisAlignment.stretch,
            children: [
              Text(note.title, style: const TextStyle(fontSize: 18, fontWeight: FontWeight.bold)),
              const SizedBox(height: 8),
              Text(note.content, style: const TextStyle(fontSize: 15, height: 1.5)),
              if (note.tags.isNotEmpty) ...[
                const SizedBox(height: 12),
                Wrap(
                  spacing: 6,
                  children: note.tags.map((t) => Chip(label: Text(t), visualDensity: VisualDensity.compact)).toList(),
                ),
              ],
            ],
          ),
        ),
      ),
    );
  }

  @override
  Widget build(BuildContext context) {
    final state = context.watch<NotesState>();
    return Scaffold(
      appBar: AppBar(title: const Text('Notes')),
      floatingActionButton: FloatingActionButton.extended(
        backgroundColor: AppTheme.primary,
        onPressed: _showAddSheet,
        icon: const Icon(Icons.add),
        label: const Text('New'),
      ),
      body: state.busy && state.notes.isEmpty
          ? const Center(child: CircularProgressIndicator())
          : state.notes.isEmpty
              ? const Center(child: Text('No notes yet. Tap New to write one.'))
              : ListView.builder(
                  padding: const EdgeInsets.fromLTRB(12, 12, 12, 80),
                  itemCount: state.notes.length,
                  itemBuilder: (context, i) {
                    final note = state.notes[i];
                    return Card(
                      elevation: 0,
                      color: Theme.of(context).colorScheme.surface,
                      margin: const EdgeInsets.symmetric(vertical: 5),
                      shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(14)),
                      child: ListTile(
                        onTap: () => _openNote(note),
                        title: Text(note.title, style: const TextStyle(fontWeight: FontWeight.w600)),
                        subtitle: Text(
                          note.content,
                          maxLines: 2,
                          overflow: TextOverflow.ellipsis,
                          style: const TextStyle(fontSize: 13),
                        ),
                        trailing: IconButton(
                          icon: const Icon(Icons.delete_outline, color: Colors.black38),
                          onPressed: () => state.remove(note.id),
                        ),
                      ),
                    );
                  },
                ),
    );
  }
}