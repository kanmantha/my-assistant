import 'package:flutter/foundation.dart';

import '../models/models.dart';
import '../services/backend_client.dart';

class TasksState extends ChangeNotifier {
  final BackendClient _backend;
  List<TaskItem> _tasks = [];
  bool _busy = false;
  String? _error;

  TasksState(this._backend);

  List<TaskItem> get tasks => List.unmodifiable(_tasks);
  bool get busy => _busy;
  String? get error => _error;

  Future<void> setDemo() async {
    _tasks = [
      const TaskItem(
        id: 'demo-1', title: 'Call the dentist', description: '',
        priority: 'High', status: 'Pending', dueDate: null, dueTime: null, createdAt: '',
      ),
      const TaskItem(
        id: 'demo-2', title: 'Submit project report', description: '',
        priority: 'Urgent', status: 'Pending', dueDate: null, dueTime: null, createdAt: '',
      ),
      const TaskItem(
        id: 'demo-3', title: 'Buy groceries', description: '',
        priority: 'Low', status: 'Completed', dueDate: null, dueTime: null, createdAt: '', completedAt: '',
      ),
    ];
    notifyListeners();
  }

  Future<void> load() async {
    _busy = true;
    _error = null;
    notifyListeners();
    try {
      _tasks = await _backend.tasks();
    } catch (e) {
      _error = e.toString();
    }
    _busy = false;
    notifyListeners();
  }

  Future<void> add(String title, {String description = '', String? priority}) async {
    _tasks.insert(0, await _backend.createTask(title: title, description: description, priority: priority));
    notifyListeners();
  }

  Future<void> toggle(String id) async {
    try {
      final updated = await _backend.completeTask(id);
      _tasks = _tasks.map((t) => t.id == id ? updated : t).toList();
      notifyListeners();
    } catch (_) {}
  }

  Future<void> remove(String id) async {
    await _backend.deleteTask(id);
    _tasks = _tasks.where((t) => t.id != id).toList();
    notifyListeners();
  }
}

class NotesState extends ChangeNotifier {
  final BackendClient _backend;
  List<Note> _notes = [];
  bool _busy = false;
  String? _error;

  NotesState(this._backend);

  List<Note> get notes => List.unmodifiable(_notes);
  bool get busy => _busy;
  String? get error => _error;

  Future<void> setDemo() async {
    _notes = [
      const Note(
        id: 'demo-n1', title: 'Meeting ideas', content: 'Brainstorm new features for the assistant.',
        language: 'en-IN', tags: ['work'], createdAt: '', updatedAt: '',
      ),
      const Note(
        id: 'demo-n2', title: 'Grocery list', content: 'Milk, eggs, bread, dal',
        language: 'en-IN', tags: ['shopping'], createdAt: '', updatedAt: '',
      ),
    ];
    notifyListeners();
  }

  Future<void> load() async {
    _busy = true;
    _error = null;
    notifyListeners();
    try {
      _notes = await _backend.notes();
    } catch (e) {
      _error = e.toString();
    }
    _busy = false;
    notifyListeners();
  }

  Future<void> add(String title, String content) async {
    _notes.insert(0, await _backend.createNote(title: title, content: content));
    notifyListeners();
  }

  Future<void> remove(String id) async {
    await _backend.deleteNote(id);
    _notes = _notes.where((n) => n.id != id).toList();
    notifyListeners();
  }
}