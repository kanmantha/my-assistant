import 'dart:async';
import 'dart:developer' as dev;

import 'package:flutter/foundation.dart' show kIsWeb;

import 'native_speech_service.dart';
import 'speech_service.dart';

/// Continuous on-device wake-word listening built on the native speech channel.
/// Falls back to the speech_to_text plugin on web / iOS.
class WakeWordService {
  WakeWordService._();

  static final WakeWordService instance = WakeWordService._();

  bool _running = false;
  bool _listening = false;

  static const List<String> _phrases = [
    'hey assistant', 'hi assistant', 'okay assistant', 'ok assistant',
    'hey asistant', 'hey assistance', 'hey assistent',
    'hi asistant', 'hi assistance', 'hi assistent',
    'ok asistant', 'okay asistant', 'assistant',
  ];

  bool get isRunning => _running;
  bool get isListening => _listening;
  bool get isAvailable => !kIsWeb && NativeSpeechService.instance.isSupported;
  bool get isWeb => kIsWeb;

  Future<bool> initialize() async {
    if (kIsWeb) return SpeechService.instance.init();
    return NativeSpeechService.instance.isAvailable();
  }

  Future<String> testListen() async {
    if (kIsWeb) return _testListenFallback();

    final available = await NativeSpeechService.instance.isAvailable();
    if (!available) return '[Speech engine not available on this device]';

    final result = await NativeSpeechService.instance.listenOnce(
      language: 'en-IN',
      timeout: const Duration(seconds: 8),
    );
    return result ?? '[No speech detected]';
  }

  Future<String> _testListenFallback() async {
    final ok = await SpeechService.instance.init();
    if (!ok) return '[Speech engine unavailable]';

    final completer = Completer<String>();
    final started = await SpeechService.instance.start(
      onResult: (r) {
        if (r.finalResult) completer.complete(r.recognizedWords);
      },
      listenFor: const Duration(seconds: 8),
      pauseFor: const Duration(seconds: 5),
    );
    if (!started) return '[Could not start listening]';

    final result = await completer.future.timeout(
      const Duration(seconds: 10),
      onTimeout: () => '[No response]',
    );
    await SpeechService.instance.stop();
    return result.isEmpty ? '[No speech detected]' : result;
  }

  Future<void> start({required void Function() onWake}) async {
    if (_running) return;
    final ok = await initialize();
    if (!ok) return;
    _running = true;
    dev.log('[WakeWord] loop started');
    _loop(onWake);
  }

  Future<void> _loop(void Function() onWake) async {
    int failCount = 0;
    while (_running) {
      final hit = await _listenOnce(onWake);
      if (hit) {
        failCount = 0;
      } else {
        failCount++;
      }
      if (_running) {
        // On failure, wait longer to give the SpeechRecognizer time to reset.
        final delay = hit
            ? const Duration(milliseconds: 1500)
            : failCount < 3
                ? const Duration(seconds: 3)
                : const Duration(seconds: 5);
        await Future<void>.delayed(delay);
      }
      if (failCount > 10 && _running) {
        dev.log('[WakeWord] re-init after $failCount failures');
        failCount = 0;
        await NativeSpeechService.instance.stopListening();
        await Future<void>.delayed(const Duration(seconds: 3));
      }
    }
  }

  Future<bool> _listenOnce(void Function() onWake) async {
    if (!_running) return false;
    _listening = true;

    if (kIsWeb) return _listenOnceFallback(onWake);

    // Native Android path.
    final completer = Completer<bool>();

    StreamSubscription<dynamic>? sub;
    sub = NativeSpeechService.instance.onEvent.listen((event) {
      if (completer.isCompleted) return;
      if (event is NativeSpeechResultEvent) {
        final words = event.text.toLowerCase().trim();
        dev.log('[WakeWord] heard: "$words" final=${event.isFinal}');
        if (words.isNotEmpty && _matches(words)) {
          dev.log('[WakeWord] MATCH!');
          completer.complete(true);
          return;
        }
        if (event.isFinal) {
          completer.complete(false);
        }
      } else if (event is NativeSpeechErrorEvent) {
        dev.log('[WakeWord] error: ${event.error}');
        if (!completer.isCompleted) completer.complete(false);
      }
    });

    final started = await NativeSpeechService.instance.startListening(
      language: 'en-IN',
    );
    if (!started) {
      sub.cancel();
      _listening = false;
      return false;
    }

    final hit = await completer.future.timeout(
      const Duration(seconds: 18),
      onTimeout: () {
        dev.log('[WakeWord] window timed out');
        return false;
      },
    );

    sub.cancel();
    await NativeSpeechService.instance.stopListening();
    _listening = false;

    if (hit && _running) {
      onWake();
    }
    return hit;
  }

  Future<bool> _listenOnceFallback(void Function() onWake) async {
    // speech_to_text fallback for web/iOS.
    final ok = await SpeechService.instance.init();
    if (!ok) { _listening = false; return false; }

    final completer = Completer<bool>();
    final started = await SpeechService.instance.start(
      onResult: (r) {
        if (completer.isCompleted) return;
        final words = r.recognizedWords.toLowerCase().trim();
        if (words.isNotEmpty && _matches(words)) {
          completer.complete(true);
          return;
        }
        if (r.finalResult) completer.complete(false);
      },
      listenFor: const Duration(seconds: 12),
      pauseFor: const Duration(seconds: 8),
    );
    if (!started) { _listening = false; return false; }

    final hit = await completer.future.timeout(
      const Duration(seconds: 18),
      onTimeout: () => false,
    );

    await SpeechService.instance.stop();
    _listening = false;

    if (hit && _running) onWake();
    return hit;
  }

  bool _matches(String words) {
    final compact = words.replaceAll(RegExp(r'[^a-z]'), '');

    for (final p in _phrases) {
      if (words.contains(p) || compact.contains(p.replaceAll(' ', ''))) return true;
    }

    final wordList = compact.split(RegExp(r'\s+'));
    for (var i = 0; i < wordList.length; i++) {
      final w = wordList[i];
      if (w.startsWith('ass') || (w.contains('ist') && w.length >= 5)) {
        if (i > 0 && ['hey', 'hi', 'ok', 'okay', 'a'].contains(wordList[i - 1])) return true;
        return true;
      }
    }

    if (compact.contains('hey') && compact.contains('st')) return true;
    if (compact.contains('hi') && compact.contains('st') && compact.length < 20) return true;

    return false;
  }

  Future<void> stop() async {
    _running = false;
    _listening = false;
    if (!kIsWeb) {
      await NativeSpeechService.instance.stopListening();
    } else {
      await SpeechService.instance.release();
    }
    dev.log('[WakeWord] stopped');
  }
}
