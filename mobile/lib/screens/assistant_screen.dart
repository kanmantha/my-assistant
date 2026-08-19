import 'dart:async';
import 'dart:convert';
import 'dart:developer' as dev;
import 'dart:io';
import 'dart:math' as math;

import 'package:flutter/material.dart';
import 'package:provider/provider.dart';
import 'package:url_launcher/url_launcher.dart';

import '../providers/assistant_provider.dart';
import '../providers/productivity_providers.dart';
import '../models/models.dart';
import '../providers/wake_word_provider.dart';
import '../services/api_client.dart';
import '../services/backend_client.dart';
import '../services/native_speech_service.dart';
import '../services/speech_service.dart';
import '../services/tts_service.dart';
import '../theme.dart';
import '../widgets/confirmation_dialog.dart';

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
    if (!mounted) return;
    // Siri-like: auto-listen for the next command after responding
    await _listenForCommand();
  }

  Future<void> _processCommand(String text) async {
    _setState(_VoiceState.processing);
    final assistant = context.read<AssistantProvider>();
    assistant.addUserMessage(text);

    // ─── Client-side intent detection ─────────────────────────────
    final lower = text.toLowerCase();

    // Notes, Tasks, Appointments → show confirmation dialog
    if (_isNoteIntent(lower) || _isTaskIntent(lower) || _isAppointmentIntent(lower)) {
      final detectedType = _isNoteIntent(lower)
          ? 'note'
          : _isTaskIntent(lower)
              ? 'task'
              : 'appointment';

      String title;
      String content;
      String? priority;
      DateTime? dateTime;
      String? location;

      if (detectedType == 'note') {
        title = _extractNoteTitle(text);
        content = _extractNoteContent(text);
      } else if (detectedType == 'task') {
        title = _extractTaskTitle(text);
        content = '';
        priority = _extractPriority(lower);
        dateTime = _extractDateTime(text);
      } else {
        title = _extractAppointmentTitle(text);
        content = '';
        dateTime = _extractDateTime(text);
      }

      final result = await showModalBottomSheet<ConfirmationResult>(
        context: context,
        isScrollControlled: true,
        shape: const RoundedRectangleBorder(
          borderRadius: BorderRadius.vertical(top: Radius.circular(20)),
        ),
        builder: (_) => ConfirmationDialog(
          initialType: detectedType,
          initialTitle: title,
          initialContent: content,
          initialPriority: priority,
          initialDateTime: dateTime,
          initialLocation: location,
        ),
      );

      if (result == null) {
        assistant.addAssistantText('Cancelled.');
        _scrollToBottom();
        return;
      }

      try {
        if (!mounted) return;
        if (result.type == 'note') {
          await context.read<NotesState>().add(result.title, result.content);
        } else if (result.type == 'task') {
          await context.read<TasksState>().add(
            result.title,
            description: result.content,
            priority: result.priority,
            dueDate: result.dateTime?.toIso8601String(),
          );
        } else {
          final dt = result.dateTime ?? DateTime.now().add(const Duration(hours: 1));
          await context.read<AppointmentsState>().add(
            title: result.title,
            startDateTime: dt.toIso8601String(),
            endDateTime: dt.add(const Duration(hours: 1)).toIso8601String(),
            location: result.location,
          );
        }
        if (!mounted) return;
        final msg = '${_capitalize(result.type)} created: "${result.title}"';
        assistant.addAssistantText(msg);
        _scrollToBottom();
        await TtsService.instance.speak(msg);
      } catch (e) {
        if (!mounted) return;
        assistant.addAssistantText('Could not create ${result.type}: $e');
        await TtsService.instance.speak('Could not create the ${result.type}.');
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

    // Today's schedule query
    if (_isTodayScheduleIntent(lower)) {
      await _listTodayAppointments();
      return;
    }

    // Reminders → send to backend (no confirmation dialog)
    if (_isReminderIntent(lower)) {
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
      return;
    }

    // Delete/Update/Complete → send to backend
    if (_isDeleteIntent(lower) || _isUpdateIntent(lower) || _isCompleteIntent(lower)) {
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
      return;
    }

    // Help
    if (_isHelpIntent(lower)) {
      final msg = 'I can help you with notes, tasks, appointments, reminders, '
          'weather, web search, timers, calculations, jokes, and more. '
          'Try saying: "What\'s the weather?", "Search for Flutter docs", '
          '"Set a timer for 5 minutes", or "What\'s 2 plus 3?"';
      assistant.addAssistantText(msg);
      _scrollToBottom();
      await TtsService.instance.speak(msg);
      return;
    }

    // Weather
    if (_isWeatherIntent(lower)) {
      await _handleWeather(text);
      return;
    }

    // Timer / Countdown
    if (_isTimerIntent(lower)) {
      await _handleTimer(lower);
      return;
    }

    // Math calculator
    if (_isMathIntent(lower)) {
      await _handleMath(lower);
      return;
    }

    // Joke
    if (_isJokeIntent(lower)) {
      await _handleJoke();
      return;
    }

    // Quote
    if (_isQuoteIntent(lower)) {
      await _handleQuote();
      return;
    }

    // Unit conversion
    if (_isConvertIntent(lower)) {
      await _handleConversion(lower);
      return;
    }

    // Navigation / Maps
    if (_isNavigationIntent(lower)) {
      await _handleNavigation(text);
      return;
    }

    // Quick actions (open app, flashlight)
    if (_isQuickActionIntent(lower)) {
      await _handleQuickAction(lower);
      return;
    }

    // Web search / General Q&A passthrough
    if (_isWebSearchIntent(lower)) {
      await _handleWebSearch(text);
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

  // ─── Siri-like handlers ───────────────────────────────────────────

  Future<void> _handleWeather(String text) async {
    final assistant = context.read<AssistantProvider>();
    assistant.setBusy(true);
    try {
      // Default to Bengaluru; extract city if mentioned
      double lat = 12.9716, lon = 77.5946;
      final cityMatch = RegExp(r'weather (?:in|at|of|for)\s+(.+)', caseSensitive: false)
          .firstMatch(text.toLowerCase());
      String cityName = 'Bengaluru';
      if (cityMatch != null) {
        cityName = cityMatch.group(1)?.trim() ?? 'Bengaluru';
        // Geocode via Open-Meteo
        final geoUrl = Uri.parse(
            'https://geocoding-api.open-meteo.com/v1/search?name=${Uri.encodeComponent(cityName)}&count=1&language=en');
        final geoResp = await _httpGet(geoUrl);
        if (geoResp != null) {
          final geoData = jsonDecode(geoResp);
          final results = geoData['results'] as List?;
          if (results != null && results.isNotEmpty) {
            lat = results[0]['latitude'] ?? lat;
            lon = results[0]['longitude'] ?? lon;
            cityName = results[0]['name'] ?? cityName;
          }
        }
      }

      final url = Uri.parse(
          'https://api.open-meteo.com/v1/forecast?latitude=$lat&longitude=$lon'
          '&current=temperature_2m,relative_humidity_2m,weather_code,wind_speed_10m'
          '&daily=weather_code,temperature_2m_max,temperature_2m_min,precipitation_probability_max'
          '&timezone=Asia%2FKolkata&forecast_days=3');
      final resp = await _httpGet(url);
      if (resp == null) {
        assistant.addAssistantText('Could not fetch weather data.');
        await TtsService.instance.speak('Sorry, I could not get the weather right now.');
        return;
      }
      final data = jsonDecode(resp);
      final current = data['current'] as Map<String, dynamic>?;
      final daily = data['daily'] as Map<String, dynamic>?;
      if (current == null) {
        assistant.addAssistantText('No weather data available for $cityName.');
        return;
      }
      final temp = current['temperature_2m'];
      final humidity = current['relative_humidity_2m'];
      final wind = current['wind_speed_10m'];
      final wmo = current['weather_code'] as int? ?? 0;
      final condition = _wmoToText(wmo);

      var msg = 'In $cityName, it\'s currently $temp°C with $condition. '
          'Humidity is $humidity% and wind speed is $wind km/h.';

      if (daily != null) {
        final maxTemps = daily['temperature_2m_max'] as List?;
        final minTemps = daily['temperature_2m_min'] as List?;
        final precip = daily['precipitation_probability_max'] as List?;
        if (maxTemps != null && maxTemps.length >= 3) {
          msg += ' Tomorrow: ${minTemps != null ? minTemps[1] : '?'}° to ${maxTemps[1]}°, '
              'rain chance ${precip != null ? precip[1] : 0}%. '
              'Day after: ${minTemps != null ? minTemps[2] : '?'}° to ${maxTemps[2]}°.';
        }
      }
      assistant.addAssistantText(msg);
      _scrollToBottom();
      await TtsService.instance.speak(msg);
    } catch (e) {
      assistant.addAssistantText('Could not fetch weather: $e');
      await TtsService.instance.speak('Sorry, I could not get the weather.');
    } finally {
      assistant.setBusy(false);
      _scrollToBottom();
    }
  }

  String _wmoToText(int code) {
    const descriptions = {
      0: 'clear sky', 1: 'mainly clear', 2: 'partly cloudy', 3: 'overcast',
      45: 'foggy', 48: 'rime fog', 51: 'light drizzle', 53: 'moderate drizzle',
      55: 'dense drizzle', 56: 'freezing drizzle', 57: 'dense freezing drizzle',
      61: 'slight rain', 63: 'moderate rain', 65: 'heavy rain',
      66: 'freezing rain', 67: 'heavy freezing rain',
      71: 'slight snow', 73: 'moderate snow', 75: 'heavy snow', 77: 'snow grains',
      80: 'slight showers', 81: 'moderate showers', 82: 'violent showers',
      85: 'slight snow showers', 86: 'heavy snow showers',
      95: 'thunderstorm', 96: 'thunderstorm with hail', 99: 'heavy thunderstorm with hail',
    };
    return descriptions[code] ?? 'unknown conditions';
  }

  Future<void> _handleTimer(String lower) async {
    final assistant = context.read<AssistantProvider>();
    // Extract minutes from the text
    final minMatch = RegExp(r'(\d+)\s*(?:minutes?|mins?)').firstMatch(lower);
    final secMatch = RegExp(r'(\d+)\s*(?:seconds?|secs?)').firstMatch(lower);
    final hourMatch = RegExp(r'(\d+)\s*(?:hours?|hrs?)').firstMatch(lower);

    int totalSeconds = 0;
    if (hourMatch != null) totalSeconds += int.parse(hourMatch.group(1)!) * 3600;
    if (minMatch != null) totalSeconds += int.parse(minMatch.group(1)!) * 60;
    if (secMatch != null) totalSeconds += int.parse(secMatch.group(1)!);
    if (totalSeconds == 0) totalSeconds = 300; // default 5 min

    final mins = totalSeconds ~/ 60;
    final secs = totalSeconds % 60;
    final durationText = mins > 0 ? '$mins minute${mins > 1 ? "s" : ""}' : '$secs seconds';

    assistant.addAssistantText('Starting a $durationText timer...');
    _scrollToBottom();
    await TtsService.instance.speak('Timer started for $durationText.');

    // Show countdown in a dialog
    if (!mounted) return;
    showDialog(
      context: context,
      barrierDismissible: true,
      builder: (_) => _TimerCountdown(totalSeconds: totalSeconds),
    ).then((_) {
      TtsService.instance.speak('Timer finished!');
    });
  }

  Future<void> _handleMath(String lower) async {
    final assistant = context.read<AssistantProvider>();
    try {
      // Normalize the expression
      var expr = lower
          .replaceAll('what is ', '').replaceAll('what s ', '').replaceAll('whats ', '')
          .replaceAll('calculate ', '').replaceAll('compute ', '').replaceAll('solve ', '')
          .replaceAll('plus', '+').replaceAll('minus', '-')
          .replaceAll('times', '*').replaceAll('multiplied by', '*')
          .replaceAll('divided by', '/').replaceAll('over', '/')
          .replaceAll('modulus', '%').replaceAll('mod ', '%')
          .replaceAll(RegExp(r'[^0-9+\-*/().%\s]'), '')
          .trim();

      if (expr.isEmpty) {
        assistant.addAssistantText('I couldn\'t understand the math expression.');
        await TtsService.instance.speak('Sorry, I couldn\'t understand the expression.');
        return;
      }

      // Simple safe eval (supports +, -, *, /, %, parentheses)
      final result = _evalMath(expr);
      final resultStr = result == result.toInt().toDouble()
          ? result.toInt().toString()
          : result.toStringAsFixed(4).replaceFirst(RegExp(r'0+$'), '').replaceFirst(RegExp(r'\.$'), '');

      final msg = '$expr = $resultStr';
      assistant.addAssistantText(msg);
      _scrollToBottom();
      await TtsService.instance.speak('The answer is $resultStr');
    } catch (e) {
      assistant.addAssistantText('Could not calculate: $e');
      await TtsService.instance.speak('Sorry, I could not calculate that.');
    }
  }

  double _evalMath(String expr) {
    expr = expr.replaceAll(' ', '');
    int pos = 0;

    late final double Function() parseExpr;
    late final double Function() parseTerm;

    double parseAtom() {
      if (pos < expr.length && expr[pos] == '(') {
        pos++;
        final result = parseExpr();
        if (pos < expr.length && expr[pos] == ')') pos++;
        return result;
      }
      int sign = 1;
      if (pos < expr.length && expr[pos] == '-') {
        sign = -1;
        pos++;
      } else if (pos < expr.length && expr[pos] == '+') {
        pos++;
      }
      int start = pos;
      while (pos < expr.length &&
          ((expr[pos].codeUnitAt(0) >= 48 && expr[pos].codeUnitAt(0) <= 57) || expr[pos] == '.')) {
        pos++;
      }
      return sign * double.parse(expr.substring(start, pos));
    }

    parseTerm = () {
      double left = parseAtom();
      while (pos < expr.length && (expr[pos] == '*' || expr[pos] == '/' || expr[pos] == '%')) {
        final op = expr[pos++];
        final right = parseAtom();
        if (op == '*') {
          left *= right;
        } else if (op == '/') {
          left /= right;
        } else {
          left %= right;
        }
      }
      return left;
    };

    parseExpr = () {
      double left = parseTerm();
      while (pos < expr.length && (expr[pos] == '+' || expr[pos] == '-')) {
        final op = expr[pos++];
        final right = parseTerm();
        left = op == '+' ? left + right : left - right;
      }
      return left;
    };

    return parseExpr();
  }

  Future<void> _handleJoke() async {
    final assistant = context.read<AssistantProvider>();
    const jokes = [
      'Why do programmers prefer dark mode? Because light attracts bugs!',
      'Why was the JavaScript developer sad? Because he didn\'t Node how to Express himself.',
      'A SQL query walks into a bar, sees two tables and asks... "Can I JOIN you?"',
      'Why do Java developers wear glasses? Because they can\'t C#.',
      'What\'s a programmer\'s favorite hangout place? Foo Bar.',
      'Why do programmers hate nature? It has too many bugs.',
      'How many programmers does it take to change a light bulb? None, that\'s a hardware problem.',
      'Why did the developer go broke? Because he used up all his cache.',
      'What\'s a computer\'s favorite snack? Microchips!',
      'Why did the computer go to the doctor? Because it had a virus!',
      'What do you call a computer that sings? A-Dell!',
      'Why was the computer cold? It left its Windows open!',
    ];
    final joke = jokes[math.Random().nextInt(jokes.length)];
    assistant.addAssistantText(joke);
    _scrollToBottom();
    await TtsService.instance.speak(joke);
  }

  Future<void> _handleQuote() async {
    final assistant = context.read<AssistantProvider>();
    const quotes = [
      'The only way to do great work is to love what you do. — Steve Jobs',
      'Innovation distinguishes between a leader and a follower. — Steve Jobs',
      'Stay hungry, stay foolish. — Steve Jobs',
      'Life is what happens when you\'re busy making other plans. — John Lennon',
      'The future belongs to those who believe in the beauty of their dreams. — Eleanor Roosevelt',
      'It is during our darkest moments that we must focus to see the light. — Aristotle',
      'The best time to plant a tree was 20 years ago. The second best time is now. — Chinese Proverb',
      'Your time is limited, don\'t waste it living someone else\'s life. — Steve Jobs',
      'If you look at what you have in life, you\'ll always have more. — Oprah Winfrey',
      'The only impossible journey is the one you never begin. — Tony Robbins',
      'Success is not final, failure is not fatal: it is the courage to continue that counts. — Winston Churchill',
      'Believe you can and you\'re halfway there. — Theodore Roosevelt',
    ];
    final quote = quotes[math.Random().nextInt(quotes.length)];
    assistant.addAssistantText(quote);
    _scrollToBottom();
    await TtsService.instance.speak(quote);
  }

  Future<void> _handleConversion(String lower) async {
    final assistant = context.read<AssistantProvider>();
    // Extract number
    final numMatch = RegExp(r'([\d.]+)').firstMatch(lower);
    if (numMatch == null) {
      assistant.addAssistantText('Please specify a number to convert, like "convert 5 miles to km".');
      await TtsService.instance.speak('Please specify a number to convert.');
      return;
    }
    final num = double.parse(numMatch.group(1)!);
    String result;
    String from, to;

    if (lower.contains('miles') && (lower.contains('km') || lower.contains('kilometer'))) {
      result = (num * 1.60934).toStringAsFixed(2);
      from = 'miles'; to = 'kilometers';
    } else if ((lower.contains('km') || lower.contains('kilometer')) && lower.contains('miles')) {
      result = (num / 1.60934).toStringAsFixed(2);
      from = 'kilometers'; to = 'miles';
    } else if (lower.contains('kg') && lower.contains('pound')) {
      result = (num * 2.20462).toStringAsFixed(2);
      from = 'kg'; to = 'pounds';
    } else if (lower.contains('pound') && lower.contains('kg')) {
      result = (num / 2.20462).toStringAsFixed(2);
      from = 'pounds'; to = 'kg';
    } else if (lower.contains('kg') && lower.contains('gram')) {
      result = (num * 1000).toStringAsFixed(2);
      from = 'kg'; to = 'grams';
    } else if (lower.contains('gram') && lower.contains('kg')) {
      result = (num / 1000).toStringAsFixed(2);
      from = 'grams'; to = 'kg';
    } else if (lower.contains('celsius') && lower.contains('fahrenheit')) {
      result = ((num * 9 / 5) + 32).toStringAsFixed(1);
      from = '°C'; to = '°F';
    } else if (lower.contains('fahrenheit') && lower.contains('celsius')) {
      result = ((num - 32) * 5 / 9).toStringAsFixed(1);
      from = '°F'; to = '°C';
    } else if (lower.contains('inch') && lower.contains('cm')) {
      result = (num * 2.54).toStringAsFixed(2);
      from = 'inches'; to = 'cm';
    } else if (lower.contains('cm') && lower.contains('inch')) {
      result = (num / 2.54).toStringAsFixed(2);
      from = 'cm'; to = 'inches';
    } else {
      assistant.addAssistantText('Supported conversions: miles/km, kg/pounds/grams, °C/°F, inches/cm.');
      await TtsService.instance.speak('I can convert miles, kilometers, kilograms, pounds, grams, celsius, fahrenheit, inches, and centimeters.');
      return;
    }
    final msg = '$num $from = $result $to';
    assistant.addAssistantText(msg);
    _scrollToBottom();
    await TtsService.instance.speak('$num $from equals $result $to');
  }

  Future<void> _handleNavigation(String text) async {
    final assistant = context.read<AssistantProvider>();
    final destMatch = RegExp(r'(?:to|at|of)\s+(.+)', caseSensitive: false).firstMatch(text);
    final destination = destMatch?.group(1)?.trim() ?? text;
    final url = Uri.parse('https://www.google.com/maps/search/?api=1&query=${Uri.encodeComponent(destination)}');
    if (await canLaunchUrl(url)) {
      await launchUrl(url, mode: LaunchMode.externalApplication);
      assistant.addAssistantText('Opening maps for "$destination"');
      await TtsService.instance.speak('Opening directions to $destination');
    } else {
      assistant.addAssistantText('Could not open maps.');
      await TtsService.instance.speak('Sorry, I could not open maps.');
    }
    _scrollToBottom();
  }

  Future<void> _handleQuickAction(String lower) async {
    final assistant = context.read<AssistantProvider>();
    if (lower.contains('flashlight') || lower.contains('torch')) {
      assistant.addAssistantText('Flashlight toggle requires native platform support. Use the quick settings panel.');
      await TtsService.instance.speak('Please use the quick settings panel to toggle the flashlight.');
    } else {
      // Map app names to package URIs
      String? package;
      if (lower.contains('settings')) {
        package = 'package:com.android.settings';
      } else if (lower.contains('calculator')) {
        package = 'package:com.google.android.calculator';
      } else if (lower.contains('clock')) {
        package = 'package:com.google.android.deskclock';
      } else if (lower.contains('calendar')) {
        package = 'package:com.google.android.calendar';
      } else if (lower.contains('camera')) {
        package = 'package:com.android.camera';
      } else if (lower.contains('youtube')) {
        package = 'package:com.google.android.youtube';
      } else if (lower.contains('chrome') || lower.contains('browser')) {
        package = 'package:com.android.chrome';
      }

      if (package != null) {
        final url = Uri.parse('intent://$package#Intent;end');
        if (await canLaunchUrl(url)) {
          await launchUrl(url, mode: LaunchMode.externalApplication);
          assistant.addAssistantText('Opening ${lower.replaceAll('open ', '')}...');
          await TtsService.instance.speak('Opening ${lower.replaceAll('open ', '')}');
        } else {
          // Fallback: search Play Store
          final searchUrl = Uri.parse('https://play.google.com/store/apps/details?id=$package');
          await launchUrl(searchUrl, mode: LaunchMode.externalApplication);
          assistant.addAssistantText('Opening Play Store for this app.');
          await TtsService.instance.speak('Opening Play Store.');
        }
      } else {
        assistant.addAssistantText('I can open: settings, calculator, clock, calendar, camera, YouTube, Chrome, and maps.');
        await TtsService.instance.speak('Please specify which app to open.');
      }
    }
    _scrollToBottom();
  }

  Future<void> _handleWebSearch(String text) async {
    final assistant = context.read<AssistantProvider>();
    // Extract the query
    var query = text
        .replaceAll(RegExp(r'^(search for |search |look up |google |find |look for )', caseSensitive: false), '')
        .replaceAll(RegExp(r'^(what is |who is |who was |what was |where is |where was |when was |when is |why is |why does |how do |how to |how does |tell me about |explain |define )', caseSensitive: false), '')
        .trim();
    if (query.isEmpty) query = text;

    final url = Uri.parse('https://www.google.com/search?q=${Uri.encodeComponent(query)}');
    if (await canLaunchUrl(url)) {
      await launchUrl(url, mode: LaunchMode.externalApplication);
      assistant.addAssistantText('Searching for "$query"...');
      await TtsService.instance.speak('Here are the results for $query');
    } else {
      assistant.addAssistantText('Could not open web search.');
      await TtsService.instance.speak('Sorry, I could not perform the search.');
    }
    _scrollToBottom();
  }

  Future<String?> _httpGet(Uri url) async {
    try {
      final httpClient = HttpClient();
      final request = await httpClient.getUrl(url);
      final httpResponse = await request.close();
      final body = await httpResponse.transform(utf8.decoder).join();
      httpClient.close();
      return body;
    } catch (e) {
      dev.log('[HTTP] GET error: $e');
      return null;
    }
  }

  // ─── Intent detectors ────────────────────────────────────────────

  bool _isNoteIntent(String lower) =>
      lower.contains('take a note') || lower.contains('add note') ||
      lower.contains('note down') || lower.contains('create note') ||
      lower.contains('save note') || lower.contains('write note') ||
      lower.contains('i need to remember') || lower.contains('put this down') ||
      lower.contains('remember this') || lower.contains('jot down') ||
      lower.contains('make a note') || lower.contains('set a note') ||
      lower.startsWith('note ');

  bool _isTaskIntent(String lower) =>
      lower.contains('create a task') || lower.contains('add a task') ||
      lower.contains('create task') || lower.contains('add task') ||
      lower.contains('schedule task') || lower.contains('make a task') ||
      lower.contains('new task') || lower.contains('i need to') ||
      lower.contains('i have to') || lower.contains('i must') ||
      lower.contains('got to') || lower.contains('need to do') ||
      lower.contains('set a task') || lower.contains('set up a task');

  bool _isAppointmentIntent(String lower) =>
      lower.contains('schedule meeting') || lower.contains('book appointment') ||
      lower.contains('set up meeting') || lower.contains('create appointment') ||
      lower.contains('add appointment') || lower.contains('schedule appointment') ||
      lower.contains('new meeting') || lower.contains('book a meeting') ||
      lower.contains('plan a meeting') || lower.contains('arrange a meeting') ||
      lower.contains('set meeting') || lower.contains('schedule a call') ||
      lower.contains('set up appointment') || lower.contains('new appointment');

  bool _isDeleteIntent(String lower) =>
      lower.contains('delete') || lower.contains('remove') ||
      lower.contains('cancel') || lower.contains('get rid of') ||
      lower.contains('trash') || lower.contains('dump');

  bool _isUpdateIntent(String lower) =>
      lower.contains('update') || lower.contains('change') ||
      lower.contains('modify') || lower.contains('reschedule') ||
      lower.contains('edit') || lower.contains('move');

  bool _isCompleteIntent(String lower) =>
      lower.contains('complete') || lower.contains('mark as done') ||
      lower.contains('finish') || lower.contains('done with') ||
      lower.contains('cross off') || lower.contains('tick off') ||
      lower.contains('achieve');

  bool _isListAppointmentsIntent(String lower) =>
      lower.contains('what are my appointments') || lower.contains('list appointments') ||
      lower.contains('show appointments') || lower.contains('my appointments') ||
      lower.contains('today\'s appointments') || lower.contains('today appointments') ||
      lower.contains('what meetings') || lower.contains('my meetings') ||
      lower.contains('show meetings') || lower.contains('what\'s on my calendar') ||
      lower.contains('what\'s my schedule') || lower.contains('what do i have today') ||
      lower.contains('do i have any meetings') || lower.contains('any meetings today') ||
      lower.contains('upcoming appointments') || lower.contains('what\'s coming up') ||
      lower.contains('my schedule today') || lower.contains('today\'s schedule') ||
      lower.contains('tomorrow\'s schedule') || lower.contains('what\'s tomorrow');

  bool _isListNotesIntent(String lower) =>
      lower.contains('show notes') || lower.contains('list notes') ||
      lower.contains('what are my notes') || lower.contains('my notes') ||
      lower.contains('all my notes') || lower.contains('open notes') ||
      lower.contains('go to notes') || lower.contains('see my notes') ||
      lower.contains('how many notes');

  bool _isListTasksIntent(String lower) =>
      lower.contains('show tasks') || lower.contains('list tasks') ||
      lower.contains('what are my tasks') || lower.contains('my tasks') ||
      lower.contains('all my tasks') || lower.contains('pending tasks') ||
      lower.contains('what\'s left to do') || lower.contains('what do i need to do') ||
      lower.contains('open tasks') || lower.contains('go to tasks') ||
      lower.contains('see my tasks') || lower.contains('how many tasks');

  bool _isReminderIntent(String lower) =>
      lower.contains('remind me') || lower.contains('set a reminder') ||
      lower.contains('create reminder') || lower.contains('add reminder') ||
      lower.contains('don\'t forget') || lower.contains('don\'t let me forget') ||
      lower.contains('remember to') || lower.contains('reminder about') ||
      lower.contains('remind me to') || lower.contains('remind me about');

  bool _isTodayScheduleIntent(String lower) =>
      lower.contains('what\'s my day like') || lower.contains('what do i have today') ||
      lower.contains('how does my day look') || lower.contains('what\'s planned for today') ||
      lower.contains('today\'s agenda') || lower.contains('my day today') ||
      lower.contains('what\'s on today') || lower.contains('schedule for today');

  bool _isHelpIntent(String lower) =>
      lower.contains('help') || lower.contains('what can you do') ||
      lower.contains('what do you do') || lower.contains('how do you work') ||
      lower.contains('commands') || lower.contains('features');

  // ─── Siri-like intent detectors ─────────────────────────────────

  bool _isWeatherIntent(String lower) =>
      lower.contains('weather') || lower.contains('temperature') ||
      lower.contains('is it raining') || lower.contains('is it cold') ||
      lower.contains('is it hot') || lower.contains('forecast') ||
      lower.contains('will it rain') || lower.contains('climate') ||
      lower.contains('हवा') || lower.contains('मौसम') || lower.contains('వాతావరణం');

  bool _isWebSearchIntent(String lower) =>
      lower.startsWith('search for ') || lower.startsWith('search ') ||
      lower.startsWith('look up ') || lower.startsWith('google ') ||
      lower.startsWith('find ') || lower.startsWith('look for ') ||
      lower.contains('what is ') || lower.contains('who is ') ||
      lower.contains('who was ') || lower.contains('what was ') ||
      lower.contains('where is ') || lower.contains('where was ') ||
      lower.contains('when was ') || lower.contains('when is ') ||
      lower.contains('why is ') || lower.contains('why does ') ||
      lower.contains('how do ') || lower.contains('how to ') ||
      lower.contains('how does ') || lower.contains('tell me about') ||
      lower.contains('explain ') || lower.contains('define ');

  bool _isTimerIntent(String lower) =>
      lower.contains('set a timer') || lower.contains('start a timer') ||
      lower.contains('timer for') || lower.contains('countdown') ||
      lower.contains('set timer') || lower.contains('start timer') ||
      lower.contains('remind me in') || lower.contains('countdown for');

  bool _isMathIntent(String lower) =>
      (lower.contains('what is ') || lower.contains('what s ') || lower.contains('whats ')) && _hasMathOperator(lower) ||
      lower.contains('calculate') || lower.contains('compute') ||
      lower.contains('solve') ||
      lower.contains('plus') || lower.contains('minus') ||
      lower.contains('times') || lower.contains('divided by') ||
      lower.contains('square root') || lower.contains('percent');

  bool _isJokeIntent(String lower) =>
      lower.contains('tell me a joke') || lower.contains('joke') ||
      lower.contains('make me laugh') || lower.contains('something funny') ||
      lower.contains('चुटकुला') || lower.contains('సరదా');

  bool _isQuoteIntent(String lower) =>
      lower.contains('inspire me') || lower.contains('motivation') ||
      lower.contains('quote') || lower.contains('saying') ||
      lower.contains('मुहावरा') || lower.contains('సామెత');

  bool _isConvertIntent(String lower) =>
      lower.contains('convert') || lower.contains('how many miles') ||
      lower.contains('how many kilometers') || lower.contains('how many km') ||
      lower.contains('how many kg') || lower.contains('how many pounds') ||
      lower.contains('how many grams') || lower.contains('how many ounces') ||
      lower.contains('celsius to fahrenheit') || lower.contains('fahrenheit to celsius');

  bool _isNavigationIntent(String lower) =>
      lower.contains('navigate to') || lower.contains('open maps') ||
      lower.contains('directions to') || lower.contains('take me to') ||
      lower.contains('how to get to') || lower.contains('open google maps') ||
      lower.contains('show on map') || lower.contains('location of');

  bool _isQuickActionIntent(String lower) =>
      lower.contains('open settings') || lower.contains('open camera') ||
      lower.contains('open calculator') || lower.contains('open clock') ||
      lower.contains('open calendar') || lower.contains('open gallery') ||
      lower.contains('open chrome') || lower.contains('open browser') ||
      lower.contains('open youtube') || lower.contains('open maps') ||
      lower.contains('turn on flashlight') || lower.contains('turn off flashlight') ||
      lower.contains('toggle flashlight') || lower.contains('flashlight on') ||
      lower.contains('flashlight off');

  bool _hasMathOperator(String lower) =>
      lower.contains('+') || lower.contains('-') || lower.contains('*') ||
      lower.contains('/') || lower.contains('plus') || lower.contains('minus') ||
      lower.contains('times') || lower.contains('multiplied') ||
      lower.contains('divided') || lower.contains('modulus') || lower.contains('%');

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
    if (lower.contains('critical')) return 'Critical';
    if (lower.contains('urgent')) return 'Urgent';
    if (lower.contains('high')) return 'High';
    if (lower.contains('low')) return 'Low';
    return 'Medium';
  }

  String _capitalize(String s) => s[0].toUpperCase() + s.substring(1);

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
                  ? _EmptyState(assistant: assistant, listening: assistant.listening)
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
                // Siri-like: auto-listen for next command after responding
                _setState(_VoiceState.listeningCommand);
                await _listenForCommand();
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

class _EmptyState extends StatefulWidget {
  final AssistantProvider assistant;
  final bool listening;
  const _EmptyState({required this.assistant, this.listening = false});

  @override
  State<_EmptyState> createState() => _EmptyStateState();
}

class _EmptyStateState extends State<_EmptyState> with TickerProviderStateMixin {
  late final AnimationController _pulseController;
  late final AnimationController _rotateController;
  late final Animation<double> _pulseAnim;
  late final Animation<double> _rotateAnim;

  @override
  void initState() {
    super.initState();
    _pulseController = AnimationController(vsync: this, duration: const Duration(milliseconds: 1500))..repeat(reverse: true);
    _rotateController = AnimationController(vsync: this, duration: const Duration(seconds: 8))..repeat();
    _pulseAnim = Tween<double>(begin: 0.8, end: 1.0).animate(CurvedAnimation(parent: _pulseController, curve: Curves.easeInOut));
    _rotateAnim = Tween<double>(begin: 0, end: 2 * math.pi).animate(_rotateController);
  }

  @override
  void dispose() {
    _pulseController.dispose();
    _rotateController.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final isListening = widget.listening;
    final colors = isListening
        ? [const Color(0xFF10B981), const Color(0xFF34D399), const Color(0xFF6EE7B7)]
        : [const Color(0xFF6366F1), const Color(0xFF8B5CF6), const Color(0xFFEC4899)];
    final glowColor = isListening ? const Color(0xFF10B981) : const Color(0xFF6366F1);

    return Center(
      child: Padding(
        padding: const EdgeInsets.all(32),
        child: Column(
          mainAxisAlignment: MainAxisAlignment.center,
          children: [
            AnimatedBuilder(
              animation: Listenable.merge([_pulseAnim, _rotateAnim]),
              builder: (context, child) {
                return Transform.scale(
                  scale: _pulseAnim.value,
                  child: Transform.rotate(
                    angle: _rotateAnim.value,
                    child: Container(
                      width: 120, height: 120,
                      decoration: BoxDecoration(
                        shape: BoxShape.circle,
                        gradient: LinearGradient(
                          begin: Alignment.topLeft,
                          end: Alignment.bottomRight,
                          colors: colors,
                        ),
                        boxShadow: [
                          BoxShadow(color: glowColor.withValues(alpha: 0.5), blurRadius: 40, spreadRadius: 5),
                          BoxShadow(color: glowColor.withValues(alpha: 0.3), blurRadius: 60, spreadRadius: 10),
                        ],
                      ),
                      child: Icon(
                        isListening ? Icons.graphic_eq : Icons.mic,
                        color: Colors.white,
                        size: 56,
                      ),
                    ),
                  ),
                );
              },
            ),
            const SizedBox(height: 32),
            Text(
              widget.assistant.demoGreeting,
              textAlign: TextAlign.center,
              style: const TextStyle(fontSize: 18, fontWeight: FontWeight.w500, height: 1.5),
            ),
            const SizedBox(height: 24),
            _ProactiveSuggestions(),
          ],
        ),
      ),
    );
  }
}

class _ProactiveSuggestions extends StatelessWidget {
  @override
  Widget build(BuildContext context) {
    final hour = DateTime.now().hour;
    String greeting;
    List<String> suggestions;

    if (hour < 12) {
      greeting = 'Good Morning';
      suggestions = ['Schedule a meeting for today', 'Create a task to review emails', 'Take a note for morning agenda'];
    } else if (hour < 17) {
      greeting = 'Good Afternoon';
      suggestions = ['Schedule a team standup', 'Create a task to send report', 'Remind me about lunch'];
    } else {
      greeting = 'Good Evening';
      suggestions = ['Take a note about today\'s progress', 'Create a task for tomorrow', 'Schedule a meeting for next week'];
    }

    return Column(
      children: [
        Text(greeting, style: TextStyle(fontSize: 14, fontWeight: FontWeight.w600, color: Colors.grey.shade600)),
        const SizedBox(height: 12),
        ...suggestions.map((s) => Padding(
          padding: const EdgeInsets.only(bottom: 8),
          child: Container(
            width: double.infinity,
            padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 12),
            decoration: BoxDecoration(
              color: Theme.of(context).colorScheme.surfaceContainerHighest.withValues(alpha: 0.5),
              borderRadius: BorderRadius.circular(12),
            ),
            child: Row(
              children: [
                Icon(Icons.auto_awesome, size: 16, color: Theme.of(context).colorScheme.primary),
                const SizedBox(width: 12),
                Expanded(child: Text(s, style: TextStyle(fontSize: 13, color: Theme.of(context).colorScheme.onSurface))),
              ],
            ),
          ),
        )),
      ],
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

class _TimerCountdown extends StatefulWidget {
  final int totalSeconds;
  const _TimerCountdown({required this.totalSeconds});

  @override
  State<_TimerCountdown> createState() => _TimerCountdownState();
}

class _TimerCountdownState extends State<_TimerCountdown>
    with SingleTickerProviderStateMixin {
  late int _remaining;
  late AnimationController _pulseController;
  late Animation<double> _pulseAnim;
  Timer? _timer;

  @override
  void initState() {
    super.initState();
    _remaining = widget.totalSeconds;
    _pulseController = AnimationController(vsync: this, duration: const Duration(milliseconds: 800))
      ..repeat(reverse: true);
    _pulseAnim = Tween<double>(begin: 0.95, end: 1.05).animate(
        CurvedAnimation(parent: _pulseController, curve: Curves.easeInOut));
    _startCountdown();
  }

  void _startCountdown() {
    _timer = Timer.periodic(const Duration(seconds: 1), (t) {
      if (_remaining <= 0) {
        t.cancel();
        _pulseController.stop();
        if (mounted) {
          Navigator.of(context).pop();
          TtsService.instance.speak('Timer finished!');
        }
        return;
      }
      if (mounted) setState(() => _remaining--);
    });
  }

  @override
  void dispose() {
    _timer?.cancel();
    _pulseController.dispose();
    super.dispose();
  }

  String _format(int s) {
    final h = s ~/ 3600;
    final m = (s % 3600) ~/ 60;
    final sec = s % 60;
    if (h > 0) return '${h.toString().padLeft(2, '0')}:${m.toString().padLeft(2, '0')}:${sec.toString().padLeft(2, '0')}';
    return '${m.toString().padLeft(2, '0')}:${sec.toString().padLeft(2, '0')}';
  }

  @override
  Widget build(BuildContext context) {
    final progress = _remaining / widget.totalSeconds;
    return Dialog(
      shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(24)),
      child: Padding(
        padding: const EdgeInsets.all(32),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            const Text('Timer', style: TextStyle(fontSize: 16, fontWeight: FontWeight.w600)),
            const SizedBox(height: 24),
            AnimatedBuilder(
              animation: _pulseAnim,
              builder: (context, child) {
                return Transform.scale(
                  scale: _pulseAnim.value,
                  child: SizedBox(
                    width: 150, height: 150,
                    child: Stack(
                      alignment: Alignment.center,
                      children: [
                        SizedBox(
                          width: 150, height: 150,
                          child: CircularProgressIndicator(
                            value: progress,
                            strokeWidth: 8,
                            backgroundColor: Colors.grey.shade200,
                            color: _remaining <= 10 ? Colors.red : AppTheme.primary,
                          ),
                        ),
                        Text(
                          _format(_remaining),
                          style: TextStyle(
                            fontSize: 32,
                            fontWeight: FontWeight.bold,
                            color: _remaining <= 10 ? Colors.red : AppTheme.primary,
                          ),
                        ),
                      ],
                    ),
                  ),
                );
              },
            ),
            const SizedBox(height: 24),
            TextButton(
              onPressed: () {
                _timer?.cancel();
                Navigator.of(context).pop();
              },
              child: const Text('Cancel Timer'),
            ),
          ],
        ),
      ),
    );
  }
}
