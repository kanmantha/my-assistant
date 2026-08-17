import { http } from "./http";
import type {
  ApiResponse,
  Appointment,
  AssistantPayload,
  AssistantResponse,
  AuthResponse,
  Conversation,
  CreateAppointmentPayload,
  CreateNotePayload,
  CreateReminderPayload,
  CreateTaskPayload,
  DashboardData,
  LoginPayload,
  Note,
  RegisterPayload,
  Reminder,
  SearchPayload,
  SearchResponse,
  SubscriptionInfo,
  Task,
  UpdateAppointmentPayload,
  UpdateNotePayload,
  UpdateReminderPayload,
  UpdateSettingsPayload,
  UpdateTaskPayload,
  User,
  UserSettings
} from "../models";

export const authApi = {
  register: (payload: RegisterPayload) =>
    http<AuthResponse>("/auth/register", { method: "POST", body: payload, auth: false }),
  login: (payload: LoginPayload) =>
    http<AuthResponse>("/auth/login", { method: "POST", body: payload, auth: false }),
  profile: () => http<User>("/auth/profile"),
  updateProfile: (payload: { firstName: string; lastName: string }) =>
    http<User>("/auth/profile", { method: "PUT", body: payload }),
  forgotPassword: (email: string) =>
    http<{ message: string }>("/auth/forgot-password", { method: "POST", body: { email }, auth: false }),
  resetPassword: (payload: { email: string; token: string; newPassword: string }) =>
    http<{ message: string }>("/auth/reset-password", { method: "POST", body: payload, auth: false }),
  changePassword: (payload: { currentPassword: string; newPassword: string }) =>
    http<{ message: string }>("/auth/change-password", { method: "POST", body: payload })
};

export const notesApi = {
  list: () => http<Note[]>("/notes"),
  create: (payload: CreateNotePayload) => http<Note>("/notes", { method: "POST", body: payload }),
  update: (id: string, payload: UpdateNotePayload) =>
    http<Note>(`/notes/${id}`, { method: "PUT", body: payload }),
  remove: (id: string) => http<{ message: string }>(`/notes/${id}`, { method: "DELETE" })
};

export const tasksApi = {
  list: (status?: number) => http<Task[]>(`/tasks${status !== undefined ? `?status=${status}` : ""}`),
  create: (payload: CreateTaskPayload) => http<Task>("/tasks", { method: "POST", body: payload }),
  update: (id: string, payload: UpdateTaskPayload) =>
    http<Task>(`/tasks/${id}`, { method: "PUT", body: payload }),
  updateStatus: (id: string, status: number) =>
    http<Task>(`/tasks/${id}/status`, { method: "PATCH", body: { status } }),
  remove: (id: string) => http<{ message: string }>(`/tasks/${id}`, { method: "DELETE" })
};

export const remindersApi = {
  list: () => http<Reminder[]>("/reminders"),
  create: (payload: CreateReminderPayload) => http<Reminder>("/reminders", { method: "POST", body: payload }),
  update: (id: string, payload: UpdateReminderPayload) =>
    http<Reminder>(`/reminders/${id}`, { method: "PUT", body: payload }),
  acknowledge: (id: string) =>
    http<Reminder>(`/reminders/${id}/acknowledge`, { method: "PATCH" }),
  remove: (id: string) => http<{ message: string }>(`/reminders/${id}`, { method: "DELETE" })
};

export const appointmentsApi = {
  list: (from?: string, to?: string) => {
    const params = new URLSearchParams();
    if (from) params.set("start", from);
    if (to) params.set("end", to);
    const qs = params.toString();
    return http<Appointment[]>(`/appointments/range${qs ? `?${qs}` : ""}`);
  },
  create: (payload: CreateAppointmentPayload) => http<Appointment>("/appointments", { method: "POST", body: payload }),
  update: (id: string, payload: UpdateAppointmentPayload) =>
    http<Appointment>(`/appointments/${id}`, { method: "PUT", body: payload }),
  remove: (id: string) => http<{ message: string }>(`/appointments/${id}`, { method: "DELETE" })
};

export const dashboardApi = {
  get: () => http<DashboardData>("/dashboard")
};

export const searchApi = {
  search: (payload: SearchPayload) => {
    const params = new URLSearchParams({ q: payload.query });
    payload.scopes?.forEach((s) => params.append("scopes", s));
    return http<SearchResponse>(`/search?${params.toString()}`);
  }
};

export const settingsApi = {
  get: () => http<UserSettings>("/settings"),
  update: (payload: UpdateSettingsPayload) => http<UserSettings>("/settings", { method: "PUT", body: payload })
};

export const conversationsApi = {
  list: () => http<Conversation[]>("/conversations"),
  clear: () => http<{ message: string }>("/conversations", { method: "DELETE" })
};

export const assistantApi = {
  command: (payload: AssistantPayload) =>
    http<AssistantResponse>("/assistant/command", { method: "POST", body: payload }),
  transcribe: (payload: { audio: Blob; language?: string }) => {
    const form = new FormData();
    form.append("audio", payload.audio);
    if (payload.language) {
      form.append("language", payload.language);
    }
    return http<{ text: string; language: string }>("/assistant/transcribe", {
      method: "POST",
      body: form
    });
  }
};

export const subscriptionApi = {
  get: () => http<SubscriptionInfo>("/subscription")
};