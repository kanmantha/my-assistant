import { useState, type FormEvent } from "react";
import { Modal, Input, Field, Textarea, Select, Button } from "./ui";
import { ENUM_OPTIONS, t, priorityLabel, taskStatusLabel, recurrenceLabel } from "../utils/locale";
import { useSettings } from "../contexts/SettingsContext";
import {
  notesApi,
  tasksApi,
  remindersApi,
  appointmentsApi
} from "../api/endpoints";
import type { Note, Task, Reminder, Appointment } from "../models";

const enumValueToInt = (name: string): number => {
  const map: Record<string, Record<string, number>> = {
    Status: { Pending: 0, InProgress: 1, Completed: 2, Cancelled: 3 },
    Priority: { Low: 0, Medium: 1, High: 2, Urgent: 3 },
    Recurrence: { Once: 0, Daily: 1, Weekly: 2, Monthly: 3, Yearly: 4, Custom: 5 }
  };
  for (const group of Object.values(map)) {
    if (name in group) return group[name];
  }
  return 0;
};

// ===================== NOTE FORM =====================
export function NoteFormModal({
  open,
  onClose,
  note,
  onSaved
}: {
  open: boolean;
  onClose: () => void;
  note?: Note | null;
  onSaved: () => void;
}) {
  const { settings } = useSettings();
  const lang = settings.language;
  const [title, setTitle] = useState(note?.title ?? "");
  const [content, setContent] = useState(note?.content ?? "");
  const [tags, setTags] = useState(note?.tags.join(", ") ?? "");
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  const handleSubmit = async (e: FormEvent) => {
    e.preventDefault();
    setBusy(true);
    setError(null);
    try {
      const tagList = tags.split(",").map((t) => t.trim()).filter(Boolean);
      if (note) {
        await notesApi.update(note.id, { title, content, tags: tagList, isPinned: note.isPinned });
      } else {
        await notesApi.create({ title, content, tags: tagList });
      }
      onSaved();
      onClose();
    } catch (err) {
      setError(err instanceof Error ? err.message : t("failedSaveNote", lang));
    } finally {
      setBusy(false);
    }
  };

  return (
    <Modal open={open} onClose={onClose} title={t(note ? "editNote" : "newNote", lang)}>
      <form onSubmit={handleSubmit} className="space-y-4">
        <Field label={t("titleField", lang)}>
          <Input value={title} onChange={(e) => setTitle(e.target.value)} required />
        </Field>
        <Field label={t("contentField", lang)}>
          <Textarea value={content} onChange={(e) => setContent(e.target.value)} />
        </Field>
        <Field label={t("tagsField", lang)}>
          <Input value={tags} onChange={(e) => setTags(e.target.value)} placeholder={t("tagsPlaceholder", lang)} />
        </Field>
        {error && <p className="text-sm text-rose-600">{error}</p>}
        <div className="flex justify-end gap-2 pt-1">
          <Button type="button" variant="secondary" onClick={onClose}>
            {t("cancel", lang)}
          </Button>
          <Button type="submit" loading={busy}>
            {t("save", lang)}
          </Button>
        </div>
      </form>
    </Modal>
  );
}

// ================= TASK FORM =================
export function TaskFormModal({
  open,
  onClose,
  task,
  onSaved
}: {
  open: boolean;
  onClose: () => void;
  task?: Task | null;
  onSaved: () => void;
}) {
  const { settings } = useSettings();
  const lang = settings.language;
  const [title, setTitle] = useState(task?.title ?? "");
  const [description, setDescription] = useState(task?.description ?? "");
  const [category, setCategory] = useState(task?.category ?? "");
  const [dueDate, setDueDate] = useState((task?.dueDate ?? "").slice(0, 10));
  const [dueTime, setDueTime] = useState(task?.dueTime ? (task.dueTime as string).slice(0, 5) : "");
  const [priority, setPriority] = useState<TaskEnumName>(
    task ? (typeof task.priority === "string" ? (task.priority as TaskEnumName) : (ENUM_OPTIONS.Priority[task.priority as number] as TaskEnumName)) : "Medium"
  );
  const [status, setStatus] = useState<TaskEnumName>(
    task ? (typeof task.status === "string" ? (task.status as TaskEnumName) : (ENUM_OPTIONS.Status[task.status as number] as TaskEnumName)) : "Pending"
  );
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  const handleSubmit = async (e: FormEvent) => {
    e.preventDefault();
    setBusy(true);
    setError(null);
    try {
      const payload = {
        title,
        description: description || undefined,
        category: category || undefined,
        dueDate: dueDate || undefined,
        dueTime: dueTime ? `${dueTime}:00` : undefined,
        priority: enumValueToInt(priority),
        status: enumValueToInt(status)
      };
      if (task) {
        await tasksApi.update(task.id, payload);
      } else {
        await tasksApi.create(payload);
      }
      onSaved();
      onClose();
    } catch (err) {
      setError(err instanceof Error ? err.message : t("failedSaveTask", lang));
    } finally {
      setBusy(false);
    }
  };

  return (
    <Modal open={open} onClose={onClose} title={t(task ? "editTask" : "newTask", lang)}>
      <form onSubmit={handleSubmit} className="space-y-4">
        <Field label={t("titleField", lang)}>
          <Input value={title} onChange={(e) => setTitle(e.target.value)} required />
        </Field>
        <Field label={t("description", lang)}>
          <Textarea value={description} onChange={(e) => setDescription(e.target.value)} />
        </Field>
        <div className="grid grid-cols-2 gap-3">
          <Field label={t("dueDate", lang)}>
            <Input type="date" value={dueDate} onChange={(e) => setDueDate(e.target.value)} />
          </Field>
          <Field label={t("dueTime", lang)}>
            <Input type="time" value={dueTime} onChange={(e) => setDueTime(e.target.value)} />
          </Field>
        </div>
        <div className="grid grid-cols-3 gap-3">
          <Field label={t("priority", lang)}>
            <Select value={priority} onChange={(e) => setPriority(e.target.value as TaskEnumName)}>
              {ENUM_OPTIONS.Priority.map((p) => (
                <option key={p} value={p}>{priorityLabel(p, lang)}</option>
              ))}
            </Select>
          </Field>
          <Field label={t("statusField", lang)}>
            <Select value={status} onChange={(e) => setStatus(e.target.value as TaskEnumName)}>
              {ENUM_OPTIONS.Status.map((s) => (
                <option key={s} value={s}>{taskStatusLabel(s, lang)}</option>
              ))}
            </Select>
          </Field>
          <Field label={t("category", lang)}>
            <Input value={category} onChange={(e) => setCategory(e.target.value)} />
          </Field>
        </div>
        {error && <p className="text-sm text-rose-600">{error}</p>}
        <div className="flex justify-end gap-2 pt-1">
          <Button type="button" variant="secondary" onClick={onClose}>
            {t("cancel", lang)}
          </Button>
          <Button type="submit" loading={busy}>
            {t("save", lang)}
          </Button>
        </div>
      </form>
    </Modal>
  );
}

type TaskEnumName = "Pending" | "InProgress" | "Completed" | "Cancelled" | "Low" | "Medium" | "High" | "Urgent";

// ================= REMINDER FORM =================
export function ReminderFormModal({
  open,
  onClose,
  reminder,
  onSaved
}: {
  open: boolean;
  onClose: () => void;
  reminder?: Reminder | null;
  onSaved: () => void;
}) {
  const { settings } = useSettings();
  const lang = settings.language;
  const [title, setTitle] = useState(reminder?.title ?? "");
  const [message, setMessage] = useState(reminder?.message ?? "");
  const [date, setDate] = useState(reminder ? new Date(reminder.reminderAt).toISOString().slice(0, 10) : new Date().toISOString().slice(0, 10));
  const [time, setTime] = useState(reminder ? new Date(reminder.reminderAt).toTimeString().slice(0, 5) : "09:00");
  const [recurrence, setRecurrence] = useState<string>(reminder ? (typeof reminder.recurrence === "string" ? reminder.recurrence : ENUM_OPTIONS.Recurrence[reminder.recurrence as number]) : "Once");
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  const handleSubmit = async (e: FormEvent) => {
    e.preventDefault();
    setBusy(true);
    setError(null);
    try {
      const reminderAt = new Date(`${date}T${time}:00`).toISOString();
      const payload = {
        title,
        message: message || undefined,
        reminderAt,
        recurrence: enumValueToInt(recurrence),
        priority: reminder ? (typeof reminder.priority === "number" ? reminder.priority : enumValueToInt(reminder.priority)) : 1
      };
      if (reminder) {
        await remindersApi.update(reminder.id, { ...payload, isAcknowledged: reminder.isAcknowledged });
      } else {
        await remindersApi.create(payload);
      }
      onSaved();
      onClose();
    } catch (err) {
      setError(err instanceof Error ? err.message : t("failedSaveReminder", lang));
    } finally {
      setBusy(false);
    }
  };

  return (
    <Modal open={open} onClose={onClose} title={t(reminder ? "editReminder" : "newReminder", lang)}>
      <form onSubmit={handleSubmit} className="space-y-4">
        <Field label={t("titleField", lang)}>
          <Input value={title} onChange={(e) => setTitle(e.target.value)} required />
        </Field>
        <Field label={t("messageField", lang)}>
          <Input value={message} onChange={(e) => setMessage(e.target.value)} />
        </Field>
        <div className="grid grid-cols-2 gap-3">
          <Field label={t("dateField", lang)}>
            <Input type="date" value={date} onChange={(e) => setDate(e.target.value)} />
          </Field>
          <Field label={t("timeField", lang)}>
            <Input type="time" value={time} onChange={(e) => setTime(e.target.value)} />
          </Field>
        </div>
        <Field label={t("repeatField", lang)}>
          <Select value={recurrence} onChange={(e) => setRecurrence(e.target.value)}>
            {ENUM_OPTIONS.Recurrence.map((r) => (
              <option key={r} value={r}>{recurrenceLabel(r, lang)}</option>
            ))}
          </Select>
        </Field>
        {error && <p className="text-sm text-rose-600">{error}</p>}
        <div className="flex justify-end gap-2 pt-1">
          <Button type="button" variant="secondary" onClick={onClose}>
            {t("cancel", lang)}
          </Button>
          <Button type="submit" loading={busy}>
            {t("save", lang)}
          </Button>
        </div>
      </form>
    </Modal>
  );
}

// ================= APPOINTMENT FORM =================
export function AppointmentFormModal({
  open,
  onClose,
  appointment,
  onSaved,
  defaultDate
}: {
  open: boolean;
  onClose: () => void;
  appointment?: Appointment | null;
  onSaved: () => void;
  defaultDate?: string;
}) {
  const { settings } = useSettings();
  const lang = settings.language;
  const [title, setTitle] = useState(appointment?.title ?? "");
  const [description, setDescription] = useState(appointment?.description ?? "");
  const [location, setLocation] = useState(appointment?.location ?? "");
  const [participants, setParticipants] = useState(appointment?.participants.join(", ") ?? "");
  const [date, setDate] = useState(
    appointment
      ? new Date(appointment.startDateTime).toISOString().slice(0, 10)
      : defaultDate ?? new Date().toISOString().slice(0, 10)
  );
  const [startTime, setStartTime] = useState(appointment ? new Date(appointment.startDateTime).toTimeString().slice(0, 5) : "09:00");
  const [endTime, setEndTime] = useState(appointment ? new Date(appointment.endDateTime).toTimeString().slice(0, 5) : "09:30");
  const [reminderMinutes, setReminderMinutes] = useState(appointment?.reminderMinutes ?? 15);
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  const handleSubmit = async (e: FormEvent) => {
    e.preventDefault();
    setBusy(true);
    setError(null);
    try {
      const startDateTime = new Date(`${date}T${startTime}:00`).toISOString();
      const endDateTime = new Date(`${date}T${endTime}:00`).toISOString();
      const payload = {
        title,
        description: description || undefined,
        location: location || undefined,
        participants: participants.split(",").map((p) => p.trim()).filter(Boolean),
        startDateTime,
        endDateTime,
        reminderMinutes
      };
      if (appointment) {
        await appointmentsApi.update(appointment.id, payload);
      } else {
        await appointmentsApi.create(payload);
      }
      onSaved();
      onClose();
    } catch (err) {
      setError(err instanceof Error ? err.message : t("failedSaveAppointment", lang));
    } finally {
      setBusy(false);
    }
  };

  return (
    <Modal open={open} onClose={onClose} title={t(appointment ? "editAppointment" : "newAppointment", lang)} wide>
      <form onSubmit={handleSubmit} className="space-y-4">
        <Field label={t("titleField", lang)}>
          <Input value={title} onChange={(e) => setTitle(e.target.value)} required />
        </Field>
        <div className="grid grid-cols-2 gap-3">
          <Field label={t("dateField", lang)}>
            <Input type="date" value={date} onChange={(e) => setDate(e.target.value)} />
          </Field>
          <Field label={t("remindBefore", lang)}>
            <Input
              type="number"
              min={0}
              value={reminderMinutes}
              onChange={(e) => setReminderMinutes(Number(e.target.value))}
            />
          </Field>
        </div>
        <div className="grid grid-cols-2 gap-3">
          <Field label={t("startTime", lang)}>
            <Input type="time" value={startTime} onChange={(e) => setStartTime(e.target.value)} />
          </Field>
          <Field label={t("endTime", lang)}>
            <Input type="time" value={endTime} onChange={(e) => setEndTime(e.target.value)} />
          </Field>
        </div>
        <div className="grid grid-cols-2 gap-3">
          <Field label={t("location", lang)}>
            <Input value={location} onChange={(e) => setLocation(e.target.value)} />
          </Field>
          <Field label={t("participantsField", lang)}>
            <Input value={participants} onChange={(e) => setParticipants(e.target.value)} placeholder={t("participantsPlaceholder", lang)} />
          </Field>
        </div>
        <Field label={t("description", lang)}>
          <Textarea value={description} onChange={(e) => setDescription(e.target.value)} />
        </Field>
        {error && <p className="text-sm text-rose-600">{error}</p>}
        <div className="flex justify-end gap-2 pt-1">
          <Button type="button" variant="secondary" onClick={onClose}>
            {t("cancel", lang)}
          </Button>
          <Button type="submit" loading={busy}>
            {t("save", lang)}
          </Button>
        </div>
      </form>
    </Modal>
  );
}