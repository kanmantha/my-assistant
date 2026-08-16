import '../models/models.dart';
import 'api_client.dart';

class BackendClient {
  BackendClient(this._api);

  final ApiClient _api;

  Future<AuthResult> register({
    required String fullName,
    required String email,
    required String password,
    String? phone,
    String? language,
  }) async {
    final r = await _api.post('/api/auth/register', {
      'fullName': fullName,
      'email': email,
      'password': password,
      'phone': phone ?? '',
      if (language != null) 'preferredLanguage': language,
      'timezone': 'Asia/Kolkata',
    });
    final data = r['data'] as Map<String, dynamic>? ?? {};
    return AuthResult.fromJson(data);
  }

  Future<AuthResult> login({required String email, required String password}) async {
    final r = await _api.post('/api/auth/login', {
      'email': email,
      'password': password,
    });
    return AuthResult.fromJson(r['data'] as Map<String, dynamic>? ?? {});
  }

  Future<AuthResult> refreshToken(String refresh) async {
    final r = await _api.post('/api/auth/refresh', {'refreshToken': refresh});
    return AuthResult.fromJson(r['data'] as Map<String, dynamic>? ?? {});
  }

Future<List<Plan>> plans() async {
    final r = await _api.get('/api/plans');
    final data = r['data'] as List? ?? const [];
    return data.map((e) => Plan.fromJson(e as Map<String, dynamic>)).toList();
  }

  Future<AssistantCommandResult> assistantCommand({
    required String text,
    String? language,
    String? timezone,
  }) async {
    final r = await _api.post('/api/assistant/command', {
      'text': text,
      if (language != null) 'language': language,
      if (timezone != null) 'timezone': timezone,
    });
    return AssistantCommandResult.fromJson(r);
  }

  /// Convenience wrapper used by the chat UI.
  Future<AssistantCommandResult> sendCommand({
    required String text,
    String? language,
    String? timezone,
  }) =>
      assistantCommand(text: text, language: language, timezone: timezone);

  Future<DashboardData> dashboard() async {
    final r = await _api.get('/api/dashboard');
    return DashboardData.fromJson(r['data'] as Map<String, dynamic>? ?? {});
  }

  Future<UsageInfo> usage() async {
    final r = await _api.get('/api/usage');
    return UsageInfo.fromJson(r['data'] as Map<String, dynamic>? ?? {});
  }

  Future<SubscriptionInfo> subscription() async {
    final r = await _api.get('/api/subscription');
    return SubscriptionInfo.fromJson(r['data'] as Map<String, dynamic>? ?? {});
  }

  Future<UserSettings> userSettings() async {
    final r = await _api.get('/api/user/settings');
    return UserSettings.fromJson(r['data'] as Map<String, dynamic>? ?? {});
  }

  Future<UserSettings> updateUserSettings({
    String? language,
    bool? voiceEnabled,
    bool? wakeWordEnabled,
    bool? notificationsEnabled,
    int? defaultReminderMinutes,
    String? timezone,
  }) async {
    final r = await _api.put('/api/user/settings', {
      if (language != null) 'language': language,
      if (voiceEnabled != null) 'voiceEnabled': voiceEnabled,
      if (wakeWordEnabled != null) 'wakeWordEnabled': wakeWordEnabled,
      if (notificationsEnabled != null) 'notificationsEnabled': notificationsEnabled,
      if (defaultReminderMinutes != null) 'defaultReminderMinutes': defaultReminderMinutes,
      if (timezone != null) 'timezone': timezone,
    });
    return UserSettings.fromJson(r['data'] as Map<String, dynamic>? ?? {});
  }

  Future<Note> createNote({required String title, String content = '', List<String>? tags}) async {
    final r = await _api.post('/api/notes', {
      'title': title,
      'content': content,
      if (tags != null) 'tags': tags,
    });
    return Note.fromJson(r['data'] as Map<String, dynamic>? ?? {});
  }

  Future<List<Note>> notes() async {
    final r = await _api.get('/api/notes');
    final data = r['data'] as List? ?? const [];
    return data.map((e) => Note.fromJson(e as Map<String, dynamic>)).toList();
  }

  Future<void> deleteNote(String id) => _api.delete('/api/notes/$id');

  Future<TaskItem> createTask({
    required String title,
    String description = '',
    String? priority,
    String? dueDate,
    String? dueTime,
  }) async {
    final r = await _api.post('/api/tasks', {
      'title': title,
      'description': description,
      if (priority != null) 'priority': priority,
      if (dueDate != null) 'dueDate': dueDate,
      if (dueTime != null) 'dueTime': dueTime,
    });
    return TaskItem.fromJson(r['data'] as Map<String, dynamic>? ?? {});
  }

  Future<List<TaskItem>> tasks() async {
    final r = await _api.get('/api/tasks');
    final data = r['data'] as List? ?? const [];
    return data.map((e) => TaskItem.fromJson(e as Map<String, dynamic>)).toList();
  }

  Future<TaskItem> completeTask(String id) async {
    final r = await _api.post('/api/tasks/$id/complete', {});
    return TaskItem.fromJson(r['data'] as Map<String, dynamic>? ?? {});
  }

  Future<void> deleteTask(String id) => _api.delete('/api/tasks/$id');

  Future<List<Reminder>> reminders() async {
    final r = await _api.get('/api/reminders');
    final data = r['data'] as List? ?? const [];
    return data.map((e) => Reminder.fromJson(e as Map<String, dynamic>)).toList();
  }

  Future<List<Appointment>> appointments() async {
    final r = await _api.get('/api/appointments');
    final data = r['data'] as List? ?? const [];
    return data.map((e) => Appointment.fromJson(e as Map<String, dynamic>)).toList();
  }
}