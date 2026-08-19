import 'dart:async';
import 'dart:developer' as dev;

import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../providers/assistant_provider.dart';
import '../providers/productivity_providers.dart';
import '../providers/wake_word_provider.dart';
import '../services/api_client.dart';
import '../services/backend_client.dart';
import '../services/native_speech_service.dart';
import '../services/speech_service.dart';
import '../services/tts_service.dart';
import '../theme.dart';

enum _VoiceState {
  idle,
  greeting,
  listeningCommand,
  confirming,
  listeningConfirm,
  processing,
}

class AssistantScreen extends StatefulWidget {
  const AssistantScreen({super.key});

  @override
  State<AssistantScreen> createState() => _AssistantScreenState();
}

class _AssistantScreenState extends State<AssistantScreen> {
  final _controller = TextEditingController();
  final _scroll = ScrollController();
  String _partialText = '';
  bool _wasWakeRunning = false;

  _VoiceState _voiceState = _VoiceState.idle;
  String _pendingCommand = '';

  @override
  void initState() {
    super.initState();
    WidgetsBinding.instance.addPostFrameCallback((_) {
      if (!mounted) return;
      context.read<AssistantProvider>().addListener(_onAssistantChange);
    });
  }

  void _onAssistantChange() {
    if (!mounted) return;
    final assistant = context.read<AssistantProvider>();
    if (assistant.consumeAutoListen()) {
      _startConversationalFlow();
    }
  }

  @override
  void dispose() {
    try { context.read<AssistantProvider>().removeListener(_onAssistantChange); } catch (_) {}
    _controller.dispose();
    _scroll.dispose();
    super.dispose();
  }

  // ─── Conversational voice flow ──────────────────────────────────────

  Future<void> _startConversationalFlow() async {
    if (_voiceState != _VoiceState.idle && _voiceState != _VoiceState.greeting) return;

    final wake = context.read<WakeWordProvider>();
    _wasWakeRunning = wake.running;
    if (_wasWakeRunning) {
      dev.log('[Assistant] stopping wake word for mic handoff');
      await wake.stop();
      await Future<void>.delayed(const Duration(milliseconds: 2000));
    }

    if (!mounted) return;
    _setState(_VoiceState.greeting);

    final assistant = context.read<AssistantProvider>();
    final greeting = assistant.language == 'hi-IN'
        ? 'हाँ, मैं आपकी सहायता कैसे कर सकता हूँ?'
        : assistant.language == 'te-IN'
            ? 'అవును, నేను మీకు ఎలా సహాయం చేయగలను?'
            : 'Yes, how can I help you?';
    assistant.addAssistantText(greeting);
    _scrollToBottom();
    await TtsService.instance.speak(greeting);

    await _listenForCommand();
  }

  Future<void> _listenForCommand() async {
    _setState(_VoiceState.listeningCommand);
    final heard = await _listenOnce(label: 'command');
    if (!mounted) return;

    if (heard == null || heard.isEmpty) {
      final assistant = context.read<AssistantProvider>();
      final msg = assistant.language == 'hi-IN'
          ? 'कुछ सुनाई नहीं दिया। कृपया फिर से कहें।'
          : assistant.language == 'te-IN'
              ? 'ఏమీ వినిపించలేదు. దయచేసి మళ్ళీ చెప్పండి.'
              : 'I didn\'t hear anything. Please try again.';
      assistant.addAssistantText(msg);
      _scrollToBottom();
      await TtsService.instance.speak(msg);
      _setState(_VoiceState.idle);
      _restartWakeWord();
      return;
    }

    _pendingCommand = heard;
    _setState(_VoiceState.confirming);
    final assistant = context.read<AssistantProvider>();
    final confirmMsg = assistant.language == 'hi-IN'
        ? 'मैंने सुना: $heard। क्या मैं इसे सहेज दूँ?'
        : assistant.language == 'te-IN'
            ? 'నేను విన్నది: $heard. దీన్ని సేవ్ చేయాలా?'
            : 'I heard: $heard. Should I save this?';
    assistant.addAssistantText(confirmMsg);
    _scrollToBottom();
    await TtsService.instance.speak(confirmMsg);

    _setState(_VoiceState.listeningConfirm);
    final answer = await _listenOnce(label: 'confirmation');

    if (answer == null || answer.isEmpty) {
      assistant.addAssistantText('No answer. Cancelled.');
      await TtsService.instance.speak('No answer. Cancelled.');
      _setState(_VoiceState.idle);
      _restartWakeWord();
      return;
    }

    final a = answer.toLowerCase();
    if (a.contains('yes') || a.contains('हाँ') || a.contains('अवुन') ||
        a.contains('sure') || a.contains('ok') || a.contains('confirm') ||
        a.contains('save') || a.contains('correct') || a.contains('right') ||
        a.contains('haan') || a.contains('acha')) {
      await _processCommand(_pendingCommand);
    } else {
      assistant.addAssistantText('Okay, cancelled.');
      await TtsService.instance.speak('Okay, cancelled.');
    }

    _pendingCommand = '';
    _setState(_VoiceState.idle);
    _restartWakeWord();
  }

  Future<void> _processCommand(String text) async {
    _setState(_VoiceState.processing);
    final assistant = context.read<AssistantProvider>();
    assistant.addUserMessage(text);

    // ─── Client-side intent detection ─────────────────────────────
    final lower = text.toLowerCase();

    // Notes: "take a note", "add note", "note down", "create note"
    if (_isNoteIntent(lower)) {
      final title = _extractNoteTitle(text);
      final content = _extractNoteContent(text);
      try {
        await context.read<NotesState>().add(title, content);
        final msg = 'Note created: "$title"';
        assistant.addAssistantText(msg);
        _scrollToBottom();
        await TtsService.instance.speak(msg);
      } catch (e) {
        assistant.addAssistantText('Could not create note: $e');
        await TtsService.instance.speak('Could not create the note.');
      }
      return;
    }

    // Tasks: "create task", "add task", "schedule task", "make a task"
    if (_isTaskIntent(lower)) {
      final title = _extractTaskTitle(text);
      final priority = _extractPriority(lower);
      try {
        await context.read<TasksState>().add(title, priority: priority);
        final msg = 'Task created: "$title"';
        assistant.addAssistantText(msg);
        _scrollToBottom();
        await TtsService.instance.speak(msg);
      } catch (e) {
        assistant.addAssistantText('Could not create task: $e');
        await TtsService.instance.speak('Could not create the task.');
      }
      return;
    }

    // Appointments: "schedule meeting", "book appointment", "set up meeting"
    if (_isAppointmentIntent(lower)) {
      final title = _extractAppointmentTitle(text);
      final dt = _extractDateTime(text);
      try {
        await context.read<AppointmentsState>().add(
          title: title,
          startDateTime: dt.toIso8601String(),
          endDateTime: dt.add(const Duration(hours: 1)).toIso8601String(),
        );
        final timeStr = '${dt.day}/${dt.month} at ${dt.hour}:${dt.minute.toString().padLeft(2, '0')}';
        final msg = 'Appointment "$title" scheduled for $timeStr';
        assistant.addAssistantText(msg);
        _scrollToBottom();
        await TtsService.instance.speak(msg);
      } catch (e) {
        assistant.addAssistantText('Could not create appointment: $e');
        await TtsService.instance.speak('Could not create the appointment.');
      }
      return;
    }

    // List today's appointments
    if (_isListAppointmentsIntent(lower)) {
      await _listTodayAppointments();
      return;
    }

    // List notes
    if (_isListNotesIntent(lower)) {
      await _listNotes();
      return;
    }

    // List tasks
    if (_isListTasksIntent(lower)) {
      await _listTasks();
      return;
    }

    // ─── Fallback: send to backend ──────────────────────────────
    final demoResponse = assistant.demoRespond(text);
    if (demoResponse != null) {
      assistant.addAssistantResponse(demoResponse);
      _scrollToBottom();
      await TtsService.instance.speak(demoResponse.responseText ?? 'Done');
      return;
    }

    assistant.setBusy(true);
    try {
      final backend = context.read<BackendClient>();
      final result = await backend.sendCommand(
        text: text,
        language: assistant.language,
        timezone: 'Asia/Kolkata',
      );
      assistant.addAssistantResponse(result);
      _scrollToBottom();
      await TtsService.instance.speak(result.responseText ?? 'Done');
    } on ApiException catch (e) {
      assistant.addAssistantText('Unable to reach the assistant: ${e.message}');
      await TtsService.instance.speak('Sorry, I could not reach the server.');
    } catch (e) {
      assistant.addAssistantText('Something went wrong: $e');
      await TtsService.instance.speak('Something went wrong.');
    } finally {
      assistant.setBusy(false);
      _scrollToBottom();
    }
  }

  // ─── Intent detectors ────────────────────────────────────────────

  bool _isNoteIntent(String lower) =>
      lower.contains('take a note') || lower.contains('add note') ||
      lower.contains('note down') || lower.contains('create note') ||
      lower.contains('save note') || lower.contains('write note') ||
      lower.startsWith('note ');

  bool _isTaskIntent(String lower) =>
      lower.contains('create a task') || lower.contains('add a task') ||
      lower.contains('create task') || lower.contains('add task') ||
      lower.contains('schedule task') || lower.contains('make a task') ||
      lower.contains('new task');

  bool _isAppointmentIntent(String lower) =>
      lower.contains('schedule meeting') || lower.contains('book appointment') ||
      lower.contains('set up meeting') || lower.contains('create appointment') ||
      lower.contains('add appointment') || lower.contains('schedule appointment') ||
      lower.contains('new meeting');

  bool _isListAppointmentsIntent(String lower) =>
      lower.contains('what are my appointments') || lower.contains('list appointments') ||
      lower.contains('show appointments') || lower.contains('my appointments') ||
      lower.contains('today\'s appointments') || lower.contains('today appointments') ||
      lower.contains('what meetings') || lower.contains('my meetings') ||
      lower.contains('show meetings');

  bool _isListNotesIntent(String lower) =>
      lower.contains('show notes') || lower.contains('list notes') ||
      lower.contains('what are my notes') || lower.contains('my notes');

  bool _isListTasksIntent(String lower) =>
      lower.contains('show tasks') || lower.contains('list tasks') ||
      lower.contains('what are my tasks') || lower.contains('my tasks');

  // ─── Content extractors ─────────────────────────────────────────

  // Prefixes are ordered longest-first so "take a note" wins over "note".
  // Each ends with `(?:\s+|$)` so a bare command ("add task") still matches
  // and falls through to the "Untitled ..." default.

  /// Note command prefixes.
  static final RegExp _notePrefix = RegExp(
    r'^(take a note|add a note|create a note|save a note|write a note|note down|take note|add note|create note|save note|write note|note)(?:\s+|$)',
    caseSensitive: false,
  );

  /// Task command prefixes.
  static final RegExp _taskPrefix = RegExp(
    r'^(create a task|add a task|make a task|schedule a task|create task|add task|schedule task|make task|new task|task)(?:\s+|$)',
    caseSensitive: false,
  );

  /// Appointment command prefixes.
  static final RegExp _apptPrefix = RegExp(
    r'^(schedule an appointment|create an appointment|book an appointment|set up a meeting|schedule appointment|create appointment|book appointment|add appointment|schedule meeting|create meeting|book meeting|set up meeting|new meeting|new appointment)(?:\s+|$)',
    caseSensitive: false,
  );

  /// Trailing/embedded time phrases stripped from appointment titles.
  static final RegExp _timePhrase = RegExp(
    r'\s*\b(at|from|on)?\s*(tomorrow|today|tonight|next week)?\s*\b\d{1,2}(:\d{2})?\s*(am|pm|oclock|o clock|hours?)\b',
    caseSensitive: false,
  );

  static final RegExp _bareDayPhrase = RegExp(
    r'\s*\b(tomorrow|today|tonight|next week)\b',
    caseSensitive: false,
  );

  static final RegExp _sentenceEnd = RegExp(r'[.!?]');

  /// Matches "at 3 pm", "at 15:00", "at 3 o'clock", and bare "3pm".
  /// The apostrophe in "o'clock" is optional because STT output varies.
  static final RegExp _timeOfDay = RegExp(
    r"(?:\bat\s+)?\b(\d{1,2})(?::(\d{2}))?\s*(am|pm|o'?\s?clock|hours?)\b"
    r"|\bat\s+(\d{1,2})(?::(\d{2}))?\b",
    caseSensitive: false,
  );

  String _extractNoteTitle(String text) {
    var t = text.trim().replaceFirst(_notePrefix, '').trim();
    if (t.isEmpty) t = 'Untitled note';
    // Use the first sentence as the title; fall back to a word-boundary cut.
    if (t.length > 50) {
      final dotIdx = t.indexOf(_sentenceEnd);
      if (dotIdx > 0 && dotIdx <= 50) {
        return t.substring(0, dotIdx).trim();
      }
      final head = t.substring(0, 50);
      final lastSpace = head.lastIndexOf(' ');
      return (lastSpace > 20 ? head.substring(0, lastSpace) : head).trim();
    }
    return t;
  }

  String _extractNoteContent(String text) {
    final t = text.trim().replaceFirst(_notePrefix, '').trim();
    return t.isEmpty ? 'Untitled note' : t;
  }

  String _extractTaskTitle(String text) {
    var t = text.trim().replaceFirst(_taskPrefix, '').trim();
    // Strip a leading priority qualifier: "high priority call bank" -> "call bank"
    t = t
        .replaceFirst(
          RegExp(r'^(urgent|high|medium|low)\s+priority\s+', caseSensitive: false),
          '',
        )
        .trim();
    if (t.isEmpty) t = 'Untitled task';
    return t;
  }

  String _extractPriority(String lower) {
    if (lower.contains('urgent')) return 'Urgent';
    if (lower.contains('high')) return 'High';
    if (lower.contains('low')) return 'Low';
    return 'Medium';
  }

  String _extractAppointmentTitle(String text) {
    var t = text.trim().replaceFirst(_apptPrefix, '').trim();
    // Strip time references ("at 3 pm", "tomorrow at 10:30 am") from the title.
    t = t.replaceAll(_timePhrase, ' ').replaceAll(_bareDayPhrase, ' ');
    // Collapse whitespace and drop dangling connectors left behind.
    t = t
        .replaceAll(RegExp(r'\s+'), ' ')
        .replaceFirst(RegExp(r'\s*\b(at|from|on|with)\s*$', caseSensitive: false), '')
        .trim();
    if (t.isEmpty) t = 'Meeting';
    return t;
  }

  DateTime _extractDateTime(String text) {
    final lower = text.toLowerCase();
    var dt = DateTime.now();

    if (lower.contains('tomorrow')) {
      dt = dt.add(const Duration(days: 1));
    } else if (lower.contains('next week')) {
      dt = dt.add(const Duration(days: 7));
    }

    // Extract time: "at 3 PM", "at 15:00", "at 3 o'clock", "3pm" (no "at").
    final timeMatch = _timeOfDay.firstMatch(text);
    if (timeMatch != null) {
      // Branch 1 (groups 1-3) has an am/pm/o'clock suffix; branch 2 (groups
      // 4-5) is a bare "at <hour>" with no suffix.
      final hasPeriod = timeMatch.group(1) != null;
      var hour = int.parse((hasPeriod ? timeMatch.group(1) : timeMatch.group(4))!);
      final minuteRaw = hasPeriod ? timeMatch.group(2) : timeMatch.group(5);
      final minute = minuteRaw != null ? int.parse(minuteRaw) : 0;
      final period = timeMatch.group(3)?.toLowerCase().replaceAll(RegExp(r"['\s]"), '');

      if (period == 'pm' && hour < 12) {
        hour += 12;
      } else if (period == 'am' && hour == 12) {
        hour = 0;
      } else if (period != 'am' && hour >= 1 && hour <= 7) {
        // No explicit am/pm (or "o'clock"): 1-7 almost always means afternoon.
        hour += 12;
      }

      // Guard against nonsense like "at 25" so DateTime doesn't roll over.
      if (hour >= 0 && hour <= 23 && minute >= 0 && minute <= 59) {
        dt = DateTime(dt.year, dt.month, dt.day, hour, minute);
      }
    }

    return dt;
  }

  // ─── List queries ────────────────────────────────────────────────

  Future<void> _listTodayAppointments() async {
    final assistant = context.read<AssistantProvider>();
    final state = context.read<AppointmentsState>();
    await state.load();

    final now = DateTime.now();
    final today = state.appointments.where((a) {
      return a.startDateTime.year == now.year &&
          a.startDateTime.month == now.month &&
          a.startDateTime.day == now.day;
    }).toList();

    String msg;
    if (today.isEmpty) {
      msg = 'You have no appointments today.';
    } else {
      final items = today.map((a) =>
          '${a.title} at ${a.startDateTime.hour}:${a.startDateTime.minute.toString().padLeft(2, '0')}').join(', ');
      msg = 'Today you have ${today.length} appointment${today.length > 1 ? 's' : ''}: $items';
    }

    assistant.addAssistantText(msg);
    _scrollToBottom();
    await TtsService.instance.speak(msg);
  }

  Future<void> _listNotes() async {
    final assistant = context.read<AssistantProvider>();
    final state = context.read<NotesState>();
    await state.load();

    if (state.notes.isEmpty) {
      final msg = 'You have no notes yet.';
      assistant.addAssistantText(msg);
      await TtsService.instance.speak(msg);
    } else {
      final titles = state.notes.map((n) => n.title).join(', ');
      final msg = 'You have ${state.notes.length} notes: $titles';
      assistant.addAssistantText(msg);
      await TtsService.instance.speak(msg);
    }
    _scrollToBottom();
  }

  Future<void> _listTasks() async {
    final assistant = context.read<AssistantProvider>();
    final state = context.read<TasksState>();
    await state.load();

    if (state.tasks.isEmpty) {
      final msg = 'You have no tasks yet.';
      assistant.addAssistantText(msg);
      await TtsService.instance.speak(msg);
    } else {
      final pending = state.tasks.where((t) => t.status != 'Completed').toList();
      final titles = pending.map((t) => t.title).join(', ');
      final msg = 'You have ${pending.length} pending tasks: $titles';
      assistant.addAssistantText(msg);
      await TtsService.instance.speak(msg);
    }
    _scrollToBottom();
  }

  // ─── Low-level speech ──────────────────────────────────────────────

  Future<String?> _listenOnce({required String label}) async {
    final assistant = context.read<AssistantProvider>();
    assistant.setListening(true);
    setState(() => _partialText = '');
    dev.log('[Assistant] _listenOnce($label) starting');

    if (!NativeSpeechService.instance.isSupported) {
      final result = await _listenOnceFallback(label);
      assistant.setListening(false);
      setState(() => _partialText = '');
      return result;
    }

    // Use the shared stream — no new EventChannel subscription.
    final completer = Completer<String?>();
    late StreamSubscription<dynamic> sub;
    sub = NativeSpeechService.instance.onEvent.listen((event) {
      if (event is NativeSpeechResultEvent) {
        if (event.text.isNotEmpty) {
          setState(() => _partialText = event.text);
        }
        if (event.isFinal) {
          dev.log('[Assistant] $label final: "${event.text}"');
          if (!completer.isCompleted) completer.complete(event.text.isEmpty ? null : event.text);
        }
      } else if (event is NativeSpeechErrorEvent) {
        dev.log('[Assistant] $label error: ${event.error}');
        if (!completer.isCompleted) completer.complete(null);
      }
    });

    final started = await NativeSpeechService.instance.startListening(
      language: assistant.language,
    );
    if (!started) {
      sub.cancel();
      assistant.setListening(false);
      setState(() => _partialText = '');
      return null;
    }

    final result = await completer.future.timeout(
      const Duration(seconds: 18),
      onTimeout: () {
        dev.log('[Assistant] $label timed out');
        return null;
      },
    );

    sub.cancel();
    await NativeSpeechService.instance.stopListening();
    assistant.setListening(false);
    setState(() => _partialText = '');
    return result;
  }

  Future<String?> _listenOnceFallback(String label) async {
    final ok = await SpeechService.instance.init();
    if (!ok) return null;

    final completer = Completer<String?>();
    final started = await SpeechService.instance.start(
      onResult: (result) {
        if (result.recognizedWords.isNotEmpty) {
          setState(() => _partialText = result.recognizedWords);
        }
        if (result.finalResult) {
          if (!completer.isCompleted) completer.complete(result.recognizedWords);
        }
      },
      listenFor: const Duration(seconds: 15),
      pauseFor: const Duration(seconds: 8),
    );

    if (!started) {
      if (!completer.isCompleted) completer.complete(null);
    }

    final r = await completer.future.timeout(
      const Duration(seconds: 20),
      onTimeout: () => null,
    );
    await SpeechService.instance.stop();
    return r;
  }

  void _setState(_VoiceState s) {
    _voiceState = s;
    dev.log('[Assistant] voiceState → $s');
    setState(() {});
  }

  void _scrollToBottom() {
    WidgetsBinding.instance.addPostFrameCallback((_) {
      if (_scroll.hasClients) {
        _scroll.animateTo(_scroll.position.maxScrollExtent,
            duration: const Duration(milliseconds: 250), curve: Curves.easeOut);
      }
    });
  }

  void _restartWakeWord() {
    if (!_wasWakeRunning) return;
    Future<void>.delayed(const Duration(milliseconds: 2500), () {
      if (mounted) {
        dev.log('[Assistant] restarting wake word');
        context.read<WakeWordProvider>().start();
      }
    });
  }

  // ─── Manual mic button ─────────────────────────────────────────────

  Future<void> _onMicTap() async {
    if (_voiceState != _VoiceState.idle) return;

    final wake = context.read<WakeWordProvider>();
    _wasWakeRunning = wake.running;
    if (_wasWakeRunning) {
      await wake.stop();
      await Future<void>.delayed(const Duration(milliseconds: 2000));
    }

    if (!mounted) return;
    final assistant = context.read<AssistantProvider>();
    assistant.addAssistantText(
      assistant.language == 'hi-IN'
          ? 'बोलिए, मैं सुन रहा हूँ।'
          : assistant.language == 'te-IN'
              ? 'చెప్పండి, నేను వింటున్నాను.'
              : 'I\'m listening. Speak your command.',
    );
    _scrollToBottom();
    await _listenForCommand();
  }

  // ─── UI ──────────────────────────────────────────────────────────────

  String _statusText() {
    switch (_voiceState) {
      case _VoiceState.idle: return '';
      case _VoiceState.greeting: return 'Speaking…';
      case _VoiceState.listeningCommand: return 'Listening for your command…';
      case _VoiceState.confirming: return 'Speaking…';
      case _VoiceState.listeningConfirm: return 'Say "Yes" to save or "No" to cancel…';
      case _VoiceState.processing: return 'Processing…';
    }
  }

  @override
  Widget build(BuildContext context) {
    final assistant = context.watch<AssistantProvider>();
    final messages = assistant.messages;
    final isActive = _voiceState != _VoiceState.idle;

    return Scaffold(
      appBar: AppBar(
        title: const Text('Assistant'),
        actions: [
          Padding(
            padding: const EdgeInsets.only(right: 16),
            child: Center(
              child: Container(
                padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 4),
                decoration: BoxDecoration(
                  color: assistant.demoMode
                      ? AppTheme.success.withValues(alpha: 0.15)
                      : AppTheme.primary.withValues(alpha: 0.1),
                  borderRadius: BorderRadius.circular(8),
                ),
                child: Text(
                  assistant.demoMode ? 'DEMO' : 'LIVE',
                  style: TextStyle(
                    fontSize: 11,
                    fontWeight: FontWeight.bold,
                    color: assistant.demoMode ? AppTheme.success : AppTheme.primary,
                  ),
                ),
              ),
            ),
          ),
        ],
      ),
      body: SafeArea(
        child: Column(
          children: [
            if (isActive)
              Container(
                width: double.infinity,
                padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 8),
                color: _voiceState == _VoiceState.listeningCommand ||
                        _voiceState == _VoiceState.listeningConfirm
                    ? Colors.green.withValues(alpha: 0.12)
                    : AppTheme.primary.withValues(alpha: 0.08),
                child: Row(
                  children: [
                    if (_voiceState == _VoiceState.listeningCommand ||
                        _voiceState == _VoiceState.listeningConfirm)
                      SizedBox(
                        width: 14, height: 14,
                        child: CircularProgressIndicator(
                            strokeWidth: 2, color: Colors.green.shade700),
                      )
                    else
                      Icon(Icons.volume_up, size: 16, color: AppTheme.primary),
                    const SizedBox(width: 8),
                    Expanded(
                      child: Text(
                        _statusText(),
                        style: TextStyle(
                          fontSize: 13,
                          fontWeight: FontWeight.w500,
                          color: _voiceState == _VoiceState.listeningCommand ||
                                  _voiceState == _VoiceState.listeningConfirm
                              ? Colors.green.shade700
                              : AppTheme.primary,
                        ),
                      ),
                    ),
                  ],
                ),
              ),
            Expanded(
              child: messages.isEmpty && _partialText.isEmpty
                  ? _EmptyState(assistant: assistant)
                  : ListView.builder(
                      controller: _scroll,
                      padding: const EdgeInsets.all(16),
                      itemCount: messages.length + (_partialText.isNotEmpty ? 1 : 0),
                      itemBuilder: (context, i) {
                        if (i < messages.length) return _Bubble(message: messages[i]);
                        return _PartialBubble(text: _partialText);
                      },
                    ),
            ),
            _InputBar(
              controller: _controller,
              listening: assistant.listening,
              speaking: assistant.speaking,
              busy: assistant.busy,
              voiceActive: isActive,
              onSend: () async {
                final text = _controller.text.trim();
                if (text.isEmpty) return;
                _controller.clear();
                setState(() => _partialText = '');
                await _processCommand(text);
                if (!mounted) return;
                _setState(_VoiceState.idle);
                _restartWakeWord();
              },
              onMic: _onMicTap,
              onStop: () async {
                await NativeSpeechService.instance.stopListening();
                await SpeechService.instance.stop();
                if (!context.mounted) return;
                context.read<AssistantProvider>().setListening(false);
                setState(() => _partialText = '');
              },
            ),
          ],
        ),
      ),
    );
  }
}

// ─── Helper widgets ──────────────────────────────────────────────────

class _EmptyState extends StatelessWidget {
  final AssistantProvider assistant;
  const _EmptyState({required this.assistant});

  @override
  Widget build(BuildContext context) {
    return Center(
      child: Padding(
        padding: const EdgeInsets.all(32),
        child: Column(
          mainAxisAlignment: MainAxisAlignment.center,
          children: [
            Container(
              width: 100, height: 100,
              decoration: BoxDecoration(
                shape: BoxShape.circle,
                gradient: const LinearGradient(
                    colors: [AppTheme.primary, Color(0xFF8A7BFF)]),
                boxShadow: [
                  BoxShadow(color: AppTheme.primary.withValues(alpha: 0.4), blurRadius: 30)
                ],
              ),
              child: const Icon(Icons.mic, color: Colors.white, size: 48),
            ),
            const SizedBox(height: 24),
            Text(assistant.demoGreeting,
                textAlign: TextAlign.center,
                style: const TextStyle(fontSize: 16, height: 1.5)),
            const SizedBox(height: 16),
            Text(
              'Say "Hey Assistant" to get started.\n\n'
              'Then try:\n'
              '• "Schedule a meeting with Ram at 3 PM"\n'
              '• "Create a task to buy groceries"\n'
              '• "Remind me to call mom tomorrow"\n'
              '• "Take a note about project deadline"',
              textAlign: TextAlign.center,
              style: TextStyle(
                  fontSize: 13,
                  color: Theme.of(context).colorScheme.onSurfaceVariant,
                  height: 1.5),
            ),
          ],
        ),
      ),
    );
  }
}

class _PartialBubble extends StatelessWidget {
  final String text;
  const _PartialBubble({required this.text});

  @override
  Widget build(BuildContext context) {
    return Align(
      alignment: Alignment.centerRight,
      child: Container(
        margin: const EdgeInsets.symmetric(vertical: 5),
        padding: const EdgeInsets.symmetric(horizontal: 14, vertical: 10),
        constraints: BoxConstraints(maxWidth: MediaQuery.of(context).size.width * 0.78),
        decoration: BoxDecoration(
          color: AppTheme.primary.withValues(alpha: 0.5),
          borderRadius: BorderRadius.circular(16),
        ),
        child: Row(
          mainAxisSize: MainAxisSize.min,
          children: [
            SizedBox(
              width: 14, height: 14,
              child: CircularProgressIndicator(
                  strokeWidth: 2, color: Colors.white.withValues(alpha: 0.8)),
            ),
            const SizedBox(width: 8),
            Flexible(
              child: Text('$text…',
                  style: const TextStyle(
                      color: Colors.white, fontSize: 15, fontStyle: FontStyle.italic)),
            ),
          ],
        ),
      ),
    );
  }
}

class _Bubble extends StatelessWidget {
  final ChatMessage message;
  const _Bubble({required this.message});

  @override
  Widget build(BuildContext context) {
    final isUser = message.role == 'user';
    return Align(
      alignment: isUser ? Alignment.centerRight : Alignment.centerLeft,
      child: Container(
        margin: const EdgeInsets.symmetric(vertical: 5),
        padding: const EdgeInsets.symmetric(horizontal: 14, vertical: 10),
        constraints: BoxConstraints(maxWidth: MediaQuery.of(context).size.width * 0.78),
        decoration: BoxDecoration(
          color: isUser ? AppTheme.primary : Theme.of(context).colorScheme.surface,
          borderRadius: BorderRadius.only(
            topLeft: const Radius.circular(16),
            topRight: const Radius.circular(16),
            bottomLeft: Radius.circular(isUser ? 16 : 4),
            bottomRight: Radius.circular(isUser ? 4 : 16),
          ),
        ),
        child: Text(
          message.text,
          style: TextStyle(
              color: isUser ? Colors.white : Theme.of(context).colorScheme.onSurface,
              fontSize: 15, height: 1.35),
        ),
      ),
    );
  }
}

class _InputBar extends StatelessWidget {
  final TextEditingController controller;
  final bool listening;
  final bool speaking;
  final bool busy;
  final bool voiceActive;
  final VoidCallback onSend;
  final VoidCallback onMic;
  final VoidCallback onStop;

  const _InputBar({
    required this.controller,
    required this.listening,
    required this.speaking,
    required this.busy,
    required this.voiceActive,
    required this.onSend,
    required this.onMic,
    required this.onStop,
  });

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.fromLTRB(12, 8, 12, 10),
      decoration: BoxDecoration(
        color: Theme.of(context).colorScheme.surface,
        boxShadow: [BoxShadow(color: Colors.black.withValues(alpha: 0.05), blurRadius: 8)],
      ),
      child: SafeArea(
        top: false,
        child: Row(
          children: [
            IconButton(
              onPressed: busy || voiceActive ? null : onMic,
              icon: Icon(
                listening ? Icons.graphic_eq : Icons.mic,
                color: listening ? Colors.red : voiceActive ? Colors.grey : AppTheme.primary,
              ),
              tooltip: 'Tap to speak',
            ),
            Expanded(
              child: TextField(
                controller: controller,
                textInputAction: TextInputAction.send,
                onSubmitted: (_) => onSend(),
                decoration: const InputDecoration(
                    hintText: 'Type a command…',
                    isDense: true, border: InputBorder.none, filled: false),
              ),
            ),
            IconButton.filled(
              onPressed: busy ? null : onSend,
              icon: Icon(busy ? Icons.hourglass_top : Icons.send),
            ),
          ],
        ),
      ),
    );
  }
}
