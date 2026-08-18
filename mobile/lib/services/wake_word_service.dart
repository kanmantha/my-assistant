import 'dart:async';
import 'dart:developer' as dev;

import 'package:flutter/foundation.dart' show kIsWeb;
import 'package:speech_to_text/speech_to_text.dart';

/// Continuous on-device wake-word listening built on [SpeechToText].
///
/// Listens in short windows and restarts when a window produces no wake
/// phrase. Uses the same wake phrases as the backend parser (en, hi, te).
class WakeWordService {
  WakeWordService._() : _speech = SpeechToText();

  static final WakeWordService instance = WakeWordService._();

  final SpeechToText _speech;
  bool _initialized = false;
  bool _running = false;
  bool _listening = false;
  Completer<bool>? _currentListen;

  /// Wake phrases — matched against partial + final results.
  static const List<String> _phrases = [
    'hey assistant',
    'hi assistant',
    'okay assistant',
    'ok assistant',
    'hey asistant',
    'hey assistance',
    'hey assistent',
    'hi asistant',
    'hi assistance',
    'hi assistent',
    'ok asistant',
    'okay asistant',
    'assistant',
    'एसिस्टेंट',
    'असिस्टेंट',
    'అసిస్టెంట్',
  ];

  bool get isRunning => _running;
  bool get isListening => _listening;
  bool get isAvailable => _initialized;
  bool get isWeb => kIsWeb;

  Future<bool> initialize() async {
    if (_initialized) return true;
    try {
      _initialized = await _speech.initialize(
        onError: (err) {
          dev.log('[WakeWord] onError: ${err.errorMsg} permanent=${err.permanent}');
          // Never stop the loop on errors — just mark not listening so the
          // next window can retry. Only truly permanent errors (mic blocked)
          // will naturally fail every window until the user re-enables.
          _listening = false;
        },
        onStatus: (status) {
          if (status == SpeechToText.notListeningStatus ||
              status == SpeechToText.doneStatus) {
            _listening = false;
          }
        },
        debugLogging: false,
      );
    } catch (e) {
      dev.log('[WakeWord] initialize failed: $e');
      _initialized = false;
    }
    dev.log('[WakeWord] initialized=$_initialized');
    return _initialized;
  }

  /// Force re-initialization after repeated failures.
  Future<void> _reinitialize() async {
    _initialized = false;
    _listening = false;
    try { await _speech.cancel(); } catch (_) {}
    await Future<void>.delayed(const Duration(milliseconds: 300));
    await initialize();
  }

  /// Starts the wake-word loop. [onWake] fires when a wake phrase is heard.
  Future<void> start({required void Function() onWake}) async {
    if (_running) return;
    if (!await initialize()) return;
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
        // Short pause between windows; longer after a successful wake.
        await Future<void>.delayed(hit
            ? const Duration(milliseconds: 1500)
            : const Duration(milliseconds: 400));
      }
      // After many consecutive failures, try re-initializing the speech engine.
      if (failCount > 20 && _running) {
        dev.log('[WakeWord] re-initializing after $failCount failures');
        failCount = 0;
        await _reinitialize();
      }
    }
  }

  /// Listens for one window. Returns true when a wake phrase was detected.
  Future<bool> _listenOnce(void Function() onWake) async {
    if (!_running) return false;
    final completer = Completer<bool>();
    _currentListen = completer;
    _listening = true;

    try {
      await _speech.listen(
        onResult: (result) {
          if (completer.isCompleted) return;
          final words = result.recognizedWords.toLowerCase().trim();
          if (words.isNotEmpty) {
            dev.log('[WakeWord] heard: "$words" final=${result.finalResult}');
            if (_matches(words)) {
              dev.log('[WakeWord] MATCH detected!');
              completer.complete(true);
              return;
            }
          }
          if (result.finalResult) {
            completer.complete(false);
          }
        },
        listenOptions: SpeechListenOptions(
          partialResults: true,
          listenMode: ListenMode.search,
          listenFor: const Duration(seconds: 15),
          pauseFor: const Duration(seconds: 8),
          cancelOnError: false,
        ),
      );
    } catch (e) {
      dev.log('[WakeWord] listen exception: $e');
      if (!completer.isCompleted) completer.complete(false);
    }

    // Safety net in case the platform never reports window end.
    final hit = await completer.future.timeout(
      const Duration(seconds: 20),
      onTimeout: () {
        dev.log('[WakeWord] window timed out');
        return false;
      },
    );

    try {
      if (_speech.isListening) await _speech.stop();
    } catch (_) {}
    _listening = false;

    if (hit && _running) {
      onWake();
    }
    return hit;
  }

  bool _matches(String words) {
    final compact = words.replaceAll(RegExp(r'[^a-z\u0900-\u097F\u0C00-\u0C7F]'), '');

    // Direct match against all known phrases.
    for (final p in _phrases) {
      final compactPhrase = p.replaceAll(' ', '');
      if (words.contains(p) || compact.contains(compactPhrase)) return true;
    }

    // Fuzzy: the word "assistant" can be mis-transcribed in many ways.
    // Check if any word in the result starts with "ass" or contains "ist".
    final wordList = compact.split(RegExp(r'\s+'));
    for (final w in wordList) {
      if (w.startsWith('ass') || w.contains('ist') || w == 'a') {
        // Found something that looks like "assistant" — check if there's
        // a preceding trigger word ("hey", "hi", "ok", "okay").
        final idx = wordList.indexOf(w);
        if (idx > 0) {
          final prev = wordList[idx - 1];
          if (prev == 'hey' || prev == 'hi' || prev == 'ok' || prev == 'okay' || prev == 'a') {
            return true;
          }
        }
        // Standalone "assistant" (any transcription containing "ist").
        if (w.length >= 5) return true;
      }
    }

    // Very loose: if the entire text contains "hey" + any "st" nearby.
    if (compact.contains('hey') && compact.contains('st')) return true;
    if (compact.contains('hi') && compact.contains('st') && compact.length < 20) return true;

    return false;
  }

  Future<void> stop() async {
    _running = false;
    _listening = false;
    final completer = _currentListen;
    if (completer != null && !completer.isCompleted) {
      completer.complete(false);
    }
    try {
      if (_speech.isListening) await _speech.stop();
    } catch (_) {}
    dev.log('[WakeWord] stopped');
  }
}
