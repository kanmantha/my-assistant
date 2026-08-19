export interface ApiResponse<T> {
  success: boolean;
  data?: T;
  message?: string;
  errors?: string[];
}

export type EnumValue = number | string;

// ---------- Auth ----------
export interface User {
  id: string;
  email: string;
  firstName: string;
  lastName: string;
  displayName: string;
  organizationId?: string;
  roles: string[];
}

export interface AuthResponse {
  accessToken: string;
  refreshToken: string;
  expiresAt: string;
  user: User;
}

export interface RegisterPayload {
  email: string;
  password: string;
  firstName: string;
  lastName: string;
}

export interface LoginPayload {
  email: string;
  password: string;
}

// ---------- Notes ----------
export interface Note {
  id: string;
  title: string;
  content: string;
  originalLanguage: string;
  tags: string[];
  isPinned: boolean;
  createdAt: string;
  updatedAt?: string;
}

export interface CreateNotePayload {
  title: string;
  content: string;
  originalLanguage?: string;
  tags?: string[];
}

export interface UpdateNotePayload {
  title?: string;
  content?: string;
  tags?: string[];
  isPinned?: boolean;
}

// ---------- Tasks ----------
export interface Task {
  id: string;
  title: string;
  description?: string;
  priority: EnumValue;
  status: EnumValue;
  dueDate?: string;
  dueTime?: string;
  completedDate?: string;
  category?: string;
  createdAt: string;
  updatedAt?: string;
}

export interface CreateTaskPayload {
  title: string;
  description?: string;
  priority?: EnumValue;
  status?: EnumValue;
  dueDate?: string;
  dueTime?: string;
  category?: string;
}

export interface UpdateTaskPayload extends CreateTaskPayload {
  status?: EnumValue;
}

// ---------- Reminders ----------
export interface Reminder {
  id: string;
  title: string;
  message?: string;
  reminderAt: string;
  recurrence: EnumValue;
  recurrenceRule?: string;
  priority: EnumValue;
  isFired: boolean;
  isAcknowledged: boolean;
  createdAt: string;
}

export interface CreateReminderPayload {
  title: string;
  message?: string;
  reminderAt: string;
  recurrence?: EnumValue;
  recurrenceRule?: string;
  priority?: EnumValue;
}

export interface UpdateReminderPayload {
  title: string;
  message?: string;
  reminderAt: string;
  recurrence: EnumValue;
  recurrenceRule?: string;
  priority: EnumValue;
  isAcknowledged?: boolean;
}

// ---------- Appointments ----------
export interface Appointment {
  id: string;
  title: string;
  description?: string;
  startDateTime: string;
  endDateTime: string;
  location?: string;
  participants: string[];
  reminderMinutes: number;
  status: EnumValue;
  createdAt: string;
  updatedAt?: string;
}

export interface CreateAppointmentPayload {
  title: string;
  description?: string;
  startDateTime: string;
  endDateTime?: string;
  location?: string;
  participants?: string[];
  reminderMinutes?: number;
}

export interface UpdateAppointmentPayload {
  title: string;
  description?: string;
  startDateTime: string;
  endDateTime?: string;
  location?: string;
  participants?: string[];
  reminderMinutes?: number;
  status?: EnumValue;
}

// ---------- Dashboard ----------
export interface DashboardData {
  greeting: string;
  userName: string;
  tasksToday: number;
  tasksCompletedToday: number;
  todayTasks: Task[];
  todayAppointments: Appointment[];
  upcomingReminders: Reminder[];
  recentNotes: Note[];
  pendingTasks: number;
  upcomingAppointments: number;
}

// ---------- Search ----------
export interface SearchResultItem {
  id: string;
  type: string;
  title: string;
  snippet?: string;
  date?: string;
  metadata?: Record<string, unknown>;
}

export interface SearchResponse {
  notes: SearchResultItem[];
  tasks: SearchResultItem[];
  appointments: SearchResultItem[];
  reminders: SearchResultItem[];
  totalCount: number;
}

export interface SearchPayload {
  query: string;
  scopes?: string[];
}

// ---------- Settings ----------
export interface UserSettings {
  language: string;
  autoDetectLanguage: boolean;
  assistantVoice?: string;
  speechSpeed: number;
  voiceVolume: number;
  muteAssistantVoice: boolean;
  wakeWordEnabled: boolean;
  wakeWord: string;
  notificationsEnabled: boolean;
  defaultReminderMinutes: number;
  timeZone: string;
  theme: string;
  confirmationMode: boolean;
  reducedMotion: boolean;
  highContrast: boolean;
  fontScale: number;
}

export interface UpdateSettingsPayload {
  language?: string;
  autoDetectLanguage?: boolean;
  assistantVoice?: string;
  speechSpeed?: number;
  voiceVolume?: number;
  muteAssistantVoice?: boolean;
  wakeWordEnabled?: boolean;
  wakeWord?: string;
  notificationsEnabled?: boolean;
  defaultReminderMinutes?: number;
  timeZone?: string;
  theme?: string;
  confirmationMode?: boolean;
  reducedMotion?: boolean;
  highContrast?: boolean;
  fontScale?: number;
}

// ---------- Conversations ----------
export interface Conversation {
  id: string;
  userMessage: string;
  assistantResponse: string;
  language: string;
  intent?: string;
  isVoice: boolean;
  createdAt: string;
}

// ---------- Assistant ----------
export interface AssistantPayload {
  text: string;
  language?: string;
  sessionId?: string;
  isVoice?: boolean;
}

export interface AssistantResponse {
  reply?: string;
  intent?: string;
  language?: string;
  needsConfirmation: boolean;
  confirmationPrompt?: string;
  pendingAction?: string;
  data?: Record<string, unknown>;
  ttsText?: string;
  captureType?: "date" | "category" | "text";
}

// ---------- Subscription / Usage ----------
export interface SubscriptionInfo {
  tier: string;
  status: string;
  startedAt?: string;
  renewalAt?: string;
  usage: {
    notes: number;
    tasks: number;
    reminders: number;
  };
  limits: {
    notes: number;
    tasks: number;
    reminders: number;
    appointments: number;
  };
}