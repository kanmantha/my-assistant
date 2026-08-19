class UserProfile {
  final String id;
  final String fullName;
  final String email;
  final String phone;
  final String preferredLanguage;
  final String timezone;
  final String role;
  final String planCode;
  final int usageAi;
  final int usageAiLimit;
  final int usageVoice;
  final int usageVoiceLimit;

  const UserProfile({
    required this.id,
    required this.fullName,
    required this.email,
    required this.phone,
    required this.preferredLanguage,
    required this.timezone,
    required this.role,
    required this.planCode,
    required this.usageAi,
    required this.usageAiLimit,
    required this.usageVoice,
    required this.usageVoiceLimit,
  });

  factory UserProfile.fromJson(Map<String, dynamic> j) {
    return UserProfile(
      id: j['id']?.toString() ?? '',
      fullName: j['fullName']?.toString() ?? '',
      email: j['email']?.toString() ?? '',
      phone: j['phone']?.toString() ?? '',
      preferredLanguage: j['preferredLanguage']?.toString() ?? 'en-IN',
      timezone: j['timezone']?.toString() ?? 'Asia/Kolkata',
      role: j['role']?.toString() ?? 'User',
      planCode: j['planCode']?.toString() ?? 'FREE',
      usageAi: _toInt(j['usageAi']),
      usageAiLimit: _toInt(j['usageAiLimit']),
      usageVoice: _toInt(j['usageVoice']),
      usageVoiceLimit: _toInt(j['usageVoiceLimit']),
    );
  }

  Map<String, dynamic> toJson() => {
        'id': id,
        'fullName': fullName,
        'email': email,
        'phone': phone,
        'preferredLanguage': preferredLanguage,
        'timezone': timezone,
        'role': role,
        'planCode': planCode,
        'usageAi': usageAi,
        'usageAiLimit': usageAiLimit,
        'usageVoice': usageVoice,
        'usageVoiceLimit': usageVoiceLimit,
      };

  static int _toInt(dynamic v) {
    if (v == null) return 0;
    if (v is int) return v;
    return int.tryParse(v.toString()) ?? 0;
  }
}

class AuthResult {
  final String accessToken;
  final String refreshToken;
  final UserProfile profile;

  const AuthResult({
    required this.accessToken,
    required this.refreshToken,
    required this.profile,
  });

  factory AuthResult.fromJson(Map<String, dynamic> j) => AuthResult(
        accessToken: j['accessToken']?.toString() ?? '',
        refreshToken: j['refreshToken']?.toString() ?? '',
        profile: UserProfile.fromJson(j['profile'] as Map<String, dynamic>? ?? {}),
      );
}

/// Offline demo profile so demo mode works fully offline.
const UserProfile demoProfile = UserProfile(
  id: 'demo-user',
  fullName: 'Demo User',
  email: 'demo@myassistant.in',
  phone: '',
  preferredLanguage: 'en-IN',
  timezone: 'Asia/Kolkata',
  role: 'User',
  planCode: 'PREMIUM',
  usageAi: 3,
  usageAiLimit: -1,
  usageVoice: 1,
  usageVoiceLimit: -1,
);

class Note {
  final String id;
  final String title;
  final String content;
  final String language;
  final List<String> tags;
  final String createdAt;
  final String updatedAt;

  const Note({
    required this.id,
    required this.title,
    required this.content,
    required this.language,
    required this.tags,
    required this.createdAt,
    required this.updatedAt,
  });

  factory Note.fromJson(Map<String, dynamic> j) => Note(
        id: j['id']?.toString() ?? '',
        title: j['title']?.toString() ?? '',
        content: j['content']?.toString() ?? '',
        language: j['language']?.toString() ?? 'en-IN',
        tags: (j['tags'] as List?)?.map((e) => e.toString()).toList() ?? const [],
        createdAt: j['createdAt']?.toString() ?? '',
        updatedAt: j['updatedAt']?.toString() ?? '',
      );
}

class TaskItem {
  final String id;
  final String title;
  final String description;
  final String priority;
  final String status;
  final String? dueDate;
  final String? dueTime;
  final String createdAt;
  final String? completedAt;

  const TaskItem({
    required this.id,
    required this.title,
    required this.description,
    required this.priority,
    required this.status,
    this.dueDate,
    this.dueTime,
    required this.createdAt,
    this.completedAt,
  });

  factory TaskItem.fromJson(Map<String, dynamic> j) => TaskItem(
        id: j['id']?.toString() ?? '',
        title: j['title']?.toString() ?? '',
        description: j['description']?.toString() ?? '',
        priority: j['priority']?.toString() ?? 'Medium',
        status: j['status']?.toString() ?? 'Pending',
        dueDate: j['dueDate']?.toString(),
        dueTime: j['dueTime']?.toString(),
        createdAt: j['createdAt']?.toString() ?? '',
        completedAt: j['completedAt']?.toString(),
      );
}

class Reminder {
  final String id;
  final String title;
  final String description;
  final DateTime reminderDateTime;
  final String timezone;
  final String recurrence;
  final bool isCompleted;
  final String createdAt;

  const Reminder({
    required this.id,
    required this.title,
    required this.description,
    required this.reminderDateTime,
    required this.timezone,
    required this.recurrence,
    required this.isCompleted,
    required this.createdAt,
  });

  factory Reminder.fromJson(Map<String, dynamic> j) => Reminder(
        id: j['id']?.toString() ?? '',
        title: j['title']?.toString() ?? '',
        description: j['description']?.toString() ?? '',
        reminderDateTime: DateTime.tryParse(j['reminderDateTime']?.toString() ?? '')?.toLocal() ?? DateTime.now(),
        timezone: j['timezone']?.toString() ?? 'Asia/Kolkata',
        recurrence: j['recurrence']?.toString() ?? 'Once',
        isCompleted: j['isCompleted'] == true,
        createdAt: j['createdAt']?.toString() ?? '',
      );
}

class Appointment {
  final String id;
  final String title;
  final String description;
  final DateTime startDateTime;
  final DateTime endDateTime;
  final String location;
  final List<String> participants;
  final int reminderMinutes;
  final String status;
  final String createdAt;

  const Appointment({
    required this.id,
    required this.title,
    required this.description,
    required this.startDateTime,
    required this.endDateTime,
    required this.location,
    required this.participants,
    required this.reminderMinutes,
    required this.status,
    required this.createdAt,
  });

  factory Appointment.fromJson(Map<String, dynamic> j) => Appointment(
        id: j['id']?.toString() ?? '',
        title: j['title']?.toString() ?? '',
        description: j['description']?.toString() ?? '',
        startDateTime: DateTime.tryParse(j['startDateTime']?.toString() ?? '')?.toLocal() ?? DateTime.now(),
        endDateTime: DateTime.tryParse(j['endDateTime']?.toString() ?? '')?.toLocal() ?? DateTime.now(),
        location: j['location']?.toString() ?? '',
        participants: (j['participants'] as List?)?.map((e) => e.toString()).toList() ?? const [],
        reminderMinutes: j['reminderMinutes'] is int ? j['reminderMinutes'] as int : 15,
        status: j['status']?.toString() ?? 'Scheduled',
        createdAt: j['createdAt']?.toString() ?? '',
      );
}

class Plan {
  final String id;
  final String name;
  final String code;
  final double priceMonthly;
  final double priceYearly;
  final String currency;
  final int maxAiRequestsPerMonth;
  final int maxVoiceRequestsPerMonth;
  final bool allowsVoice;
  final bool allowsCalendar;
  final bool allowsCloudBackup;
  final bool allowsAdvancedAi;
  final List<String> features;
  final int displayOrder;

  const Plan({
    required this.id,
    required this.name,
    required this.code,
    required this.priceMonthly,
    required this.priceYearly,
    required this.currency,
    required this.maxAiRequestsPerMonth,
    required this.maxVoiceRequestsPerMonth,
    required this.allowsVoice,
    required this.allowsCalendar,
    required this.allowsCloudBackup,
    required this.allowsAdvancedAi,
    required this.features,
    required this.displayOrder,
  });

  factory Plan.fromJson(Map<String, dynamic> j) => Plan(
        id: j['id']?.toString() ?? '',
        name: j['name']?.toString() ?? '',
        code: j['code']?.toString() ?? 'FREE',
        priceMonthly: (j['priceMonthly'] as num?)?.toDouble() ?? 0,
        priceYearly: (j['priceYearly'] as num?)?.toDouble() ?? 0,
        currency: j['currency']?.toString() ?? 'INR',
        maxAiRequestsPerMonth: j['maxAiRequestsPerMonth'] is int ? j['maxAiRequestsPerMonth'] as int : -1,
        maxVoiceRequestsPerMonth: j['maxVoiceRequestsPerMonth'] is int ? j['maxVoiceRequestsPerMonth'] as int : -1,
        allowsVoice: j['allowsVoice'] == true,
        allowsCalendar: j['allowsCalendar'] == true,
        allowsCloudBackup: j['allowsCloudBackup'] == true,
        allowsAdvancedAi: j['allowsAdvancedAi'] == true,
        features: (j['features'] as List?)?.map((e) => e.toString()).toList() ?? const [],
        displayOrder: j['displayOrder'] is int ? j['displayOrder'] as int : 0,
      );
}

class UsageInfo {
  final int aiRequests;
  final int aiLimit;
  final int voiceRequests;
  final int voiceLimit;
  final int notes;
  final int tasks;
  final int reminders;
  final int appointments;
  final String planCode;

  const UsageInfo({
    required this.aiRequests,
    required this.aiLimit,
    required this.voiceRequests,
    required this.voiceLimit,
    required this.notes,
    required this.tasks,
    required this.reminders,
    required this.appointments,
    required this.planCode,
  });

  factory UsageInfo.fromJson(Map<String, dynamic> j) => UsageInfo(
        aiRequests: j['aiRequests'] is int ? j['aiRequests'] as int : 0,
        aiLimit: j['aiLimit'] is int ? j['aiLimit'] as int : 0,
        voiceRequests: j['voiceRequests'] is int ? j['voiceRequests'] as int : 0,
        voiceLimit: j['voiceLimit'] is int ? j['voiceLimit'] as int : 0,
        notes: j['notes'] is int ? j['notes'] as int : 0,
        tasks: j['tasks'] is int ? j['tasks'] as int : 0,
        reminders: j['reminders'] is int ? j['reminders'] as int : 0,
        appointments: j['appointments'] is int ? j['appointments'] as int : 0,
        planCode: j['planCode']?.toString() ?? 'FREE',
      );

  double get aiPercent => aiLimit > 0 ? (aiRequests / aiLimit).clamp(0.0, 1.0) : 0.0;
  bool get aiUnlimited => aiLimit < 0;
}

class SubscriptionInfo {
  final String planCode;
  final String planName;
  final String status;
  final String billingPeriod;
  final String? renewalDate;
  final String? cancelAt;
  final String? provider;
  final double price;
  final String currency;
  final UsageInfo usage;

  const SubscriptionInfo({
    required this.planCode,
    required this.planName,
    required this.status,
    required this.billingPeriod,
    this.renewalDate,
    this.cancelAt,
    this.provider,
    required this.price,
    required this.currency,
    required this.usage,
  });

  factory SubscriptionInfo.fromJson(Map<String, dynamic> j) => SubscriptionInfo(
        planCode: j['planCode']?.toString() ?? 'FREE',
        planName: j['planName']?.toString() ?? '',
        status: j['status']?.toString() ?? 'Active',
        billingPeriod: j['billingPeriod']?.toString() ?? 'Monthly',
        renewalDate: j['renewalDate']?.toString(),
        cancelAt: j['cancelAt']?.toString(),
        provider: j['provider']?.toString(),
        price: (j['price'] as num?)?.toDouble() ?? 0,
        currency: j['currency']?.toString() ?? 'INR',
        usage: UsageInfo.fromJson(j['usage'] as Map<String, dynamic>? ?? {}),
      );
}

class AssistantCommandResult {
  final bool success;
  final String? intent;
  final String? responseText;
  final String? responseLanguage;
  final bool needsClarification;
  final String? clarificationQuestion;
  final int? usageAiRequests;
  final int? usageAiLimit;
  final String? error;

  const AssistantCommandResult({
    required this.success,
    this.intent,
    this.responseText,
    this.responseLanguage,
    required this.needsClarification,
    this.clarificationQuestion,
    this.usageAiRequests,
    this.usageAiLimit,
    this.error,
  });

  factory AssistantCommandResult.fromJson(Map<String, dynamic> j) {
    final inner = j['success'] == false
        ? <String, dynamic>{'success': false, 'responseText': j['message'], 'error': j['errorCode']}
        : (j['data'] as Map<String, dynamic>? ?? {});
    return AssistantCommandResult(
      success: inner['success'] == true,
      intent: inner['intent']?.toString(),
      responseText: inner['responseText']?.toString() ?? j['message']?.toString(),
      responseLanguage: inner['responseLanguage']?.toString(),
      needsClarification: inner['needsClarification'] == true,
      clarificationQuestion: inner['clarificationQuestion']?.toString(),
      usageAiRequests: inner['usageAiRequests'] as int?,
      usageAiLimit: inner['usageAiLimit'] as int?,
      error: inner['error']?.toString(),
    );
  }
}

class UserSettings {
  final String? language;
  final bool? voiceEnabled;
  final bool? wakeWordEnabled;
  final bool? notificationsEnabled;
  final int? defaultReminderMinutes;
  final String? timezone;

  const UserSettings({
    this.language,
    this.voiceEnabled,
    this.wakeWordEnabled,
    this.notificationsEnabled,
    this.defaultReminderMinutes,
    this.timezone,
  });

  factory UserSettings.fromJson(Map<String, dynamic> j) => UserSettings(
        language: j['language']?.toString(),
        voiceEnabled: j['voiceEnabled'] == true,
        wakeWordEnabled: j['wakeWordEnabled'] == true,
        notificationsEnabled: j['notificationsEnabled'] == true,
        defaultReminderMinutes: j['defaultReminderMinutes'] as int?,
        timezone: j['timezone']?.toString(),
      );
}

class DashboardData {
  final String greeting;
  final List<Appointment> todayAppointments;
  final List<TaskItem> todayTasks;
  final List<Reminder> todayReminders;
  final UsageInfo usage;
  final SubscriptionInfo subscription;

  const DashboardData({
    required this.greeting,
    required this.todayAppointments,
    required this.todayTasks,
    required this.todayReminders,
    required this.usage,
    required this.subscription,
  });

  factory DashboardData.fromJson(Map<String, dynamic> j) => DashboardData(
        greeting: j['greeting']?.toString() ?? 'Good Morning',
        todayAppointments: (j['todayAppointments'] as List?)
                ?.map((e) => Appointment.fromJson(e as Map<String, dynamic>))
                .toList() ??
            const [],
        todayTasks: (j['todayTasks'] as List?)
                ?.map((e) => TaskItem.fromJson(e as Map<String, dynamic>))
                .toList() ??
            const [],
        todayReminders: (j['todayReminders'] as List?)
                ?.map((e) => Reminder.fromJson(e as Map<String, dynamic>))
                .toList() ??
            const [],
        usage: UsageInfo.fromJson(j['usage'] as Map<String, dynamic>? ?? {}),
        subscription: SubscriptionInfo.fromJson(j['subscription'] as Map<String, dynamic>? ?? {}),
      );
}

class ConfirmationResult {
  final String type;
  final String title;
  final String content;
  final String? priority;
  final DateTime? dateTime;
  final String? location;

  const ConfirmationResult({
    required this.type,
    required this.title,
    this.content = '',
    this.priority,
    this.dateTime,
    this.location,
  });
}