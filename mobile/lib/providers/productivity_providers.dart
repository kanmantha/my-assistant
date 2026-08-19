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
    try {
      _tasks.insert(0, await _backend.createTask(title: title, description: description, priority: priority));
      _error = null;
      notifyListeners();
    } catch (e) {
      _error = e.toString();
      notifyListeners();
      rethrow;
    }
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
    try {
      _notes.insert(0, await _backend.createNote(title: title, content: content));
      _error = null;
      notifyListeners();
    } catch (e) {
      _error = e.toString();
      notifyListeners();
      rethrow;
    }
  }

  Future<void> remove(String id) async {
    await _backend.deleteNote(id);
    _notes = _notes.where((n) => n.id != id).toList();
    notifyListeners();
  }
}

class AppointmentsState extends ChangeNotifier {
  final BackendClient _backend;
  List<Appointment> _appointments = [];
  bool _busy = false;
  String? _error;

  AppointmentsState(this._backend);

  List<Appointment> get appointments => List.unmodifiable(_appointments);
  bool get busy => _busy;
  String? get error => _error;

  Future<void> setDemo() async {
    final now = DateTime.now();
    _appointments = [
      Appointment(
        id: 'demo-a1', title: 'Team standup', description: 'Daily sync',
        startDateTime: DateTime(now.year, now.month, now.day, 10, 0),
        endDateTime: DateTime(now.year, now.month, now.day, 10, 30),
        location: 'Zoom', participants: const ['Team'], reminderMinutes: 15,
        status: 'Scheduled', createdAt: now.toIso8601String(),
      ),
      Appointment(
        id: 'demo-a2', title: 'Doctor appointment', description: 'Annual checkup',
        startDateTime: DateTime(now.year, now.month, now.day, 15, 0),
        endDateTime: DateTime(now.year, now.month, now.day, 16, 0),
        location: 'City Hospital', participants: const [], reminderMinutes: 30,
        status: 'Scheduled', createdAt: now.toIso8601String(),
      ),
    ];
    notifyListeners();
  }

  Future<void> load() async {
    _busy = true;
    _error = null;
    notifyListeners();
    try {
      _appointments = await _backend.appointments();
    } catch (e) {
      _error = e.toString();
    }
    _busy = false;
    notifyListeners();
  }

  Future<void> add({
    required String title,
    String description = '',
    required String startDateTime,
    String? endDateTime,
    String? location,
    String? participants,
  }) async {
    try {
      final appt = await _backend.createAppointment(
        title: title,
        description: description,
        startDateTime: startDateTime,
        endDateTime: endDateTime,
        location: location,
        participants: participants,
      );
      _appointments.insert(0, appt);
      _error = null;
      notifyListeners();
    } catch (e) {
      _error = e.toString();
      notifyListeners();
      rethrow;
    }
  }

  Future<void> remove(String id) async {
    await _backend.deleteAppointment(id);
    _appointments = _appointments.where((a) => a.id != id).toList();
    notifyListeners();
  }
}