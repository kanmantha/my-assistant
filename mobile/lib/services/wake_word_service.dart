import 'dart:async';

import 'package:flutter/foundation.dart' show kIsWeb;
import 'package:speech_to_text/speech_to_text.dart';

/// Continuous on-device wake-word listening built on [SpeechToText].
///
/// Listens in short windows and restarts when a window produces no wake
/// phrase, matching free/always-on behavior on devices that support it.
/// Uses the same wake phrases as the backend parser (en, hi, te).
class WakeWordService {
  WakeWordService._() : _speech = SpeechToText();

  static final WakeWordService instance = WakeWordService._();

  final SpeechToText _speech;
  bool _initialized = false;
  bool _running = false;
  bool _listening = false;
  Completer<bool>? _currentListen;

  /// Wake phrases; broadened to tolerate speech-to-text quirks.
  static const List<String> _phrases = [
    'hey assistant',
    'hi assistant',
    'okay assistant',
    'ok assistant',
    'hey asistant',
    'hey assistance',
    'hey assistent',
    'hi asistant',
    'assistant',
    'एसिस्टेंट',
    'असिस्टेंट',
    'అసిస్టెంట్',
  ];

  /// Common mis-transcriptions that still sound like "assistant".
  static const List<String> _fuzzySuffixes = [
    'asistant',
    'assistent',
    'assistance',
    'assist ant',
    'a assistant',
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
          // On permanent errors (e.g. microphone blocked), stop the loop so
          // the UI can show the toggle is off. Transient errors are retried
          // by the next _listenOnce window.
          if (err.permanent && _running) {
            _listening = false;
            unawaited(stop());
          }
        },
        onStatus: (status) {
          if (status == SpeechToText.notListeningStatus) {
            _listening = false;
          }
        },
        debugLogging: false,
      );
    } catch (_) {
      _initialized = false;
    }
    return _initialized;
  }

  /// Starts the wake-word loop. [onWake] fires when a wake phrase is heard.
  Future<void> start({required void Function() onWake}) async {
    if (_running) return;
    if (!await initialize()) return;
    _running = true;
    _loop(onWake);
  }

  Future<void> _loop(void Function() onWake) async {
    while (_running) {
      final hit = await _listenOnce(onWake);
      if (_running) {
        await Future<void>.delayed(hit
            ? const Duration(milliseconds: 1200)
            : const Duration(milliseconds: 350));
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
            if (_matches(words)) {
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
          listenMode: ListenMode.deviceDefault,
          listenFor: const Duration(seconds: 10),
          pauseFor: const Duration(seconds: 5),
          cancelOnError: false,
        ),
      );
    } catch (_) {
      if (!completer.isCompleted) completer.complete(false);
    }

    // Safety net in case the platform never reports window end.
    final hit = await completer.future.timeout(
      const Duration(seconds: 15),
      onTimeout: () => false,
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
    for (final p in _phrases) {
      final compactPhrase = p.replaceAll(' ', '');
      if (words.contains(p) || compact.contains(compactPhrase)) return true;
    }
    // Fuzzy: check if the recognized text ends with a known mis-transcription
    // of "assistant" — catches cases like "hey asistant", "okay assistent".
    for (final suffix in _fuzzySuffixes) {
      if (compact.endsWith(suffix)) return true;
    }
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
  }
}
