import 'dart:async';

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

  /// Wake phrases; low-confidence inserts like "hey" are tolerated so the
  /// recognition does not need to be perfect.
  static const List<String> _phrases = [
    'hey assistant',
    'hi assistant',
    'okay assistant',
    'ok assistant',
    'assistant',
    'एसिस्टेंट',
    'असिस्टेंट',
    'అసిస్టెంట్',
  ];

  bool get isRunning => _running;
  bool get isListening => _listening;
  bool get isAvailable => _initialized;

  /// Initializes speech recognition (requesting mic permission if needed).
  /// Returns false when speech recognition is unavailable on this device.
  Future<bool> initialize() async {
    if (_initialized) return true;
    try {
      _initialized = await _speech.initialize(
        onError: (err) {
          if (err.permanent && _running) {
            unawaited(stop());
          }
        },
        onStatus: (status) {
          if (status == SpeechToText.notListeningStatus) _listening = false;
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
    if (_running || !await initialize()) return;
    _running = true;
    _loop(onWake);
  }

  Future<void> _loop(void Function() onWake) async {
    while (_running) {
      final hit = await _listenOnce(onWake);
      // A short pause between windows lets the recognizer reset cleanly;
      // _running may have been set false by stop() during listen.
      if (_running && !hit) {
        await Future<void>.delayed(const Duration(milliseconds: 400));
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
          final words = result.recognizedWords.toLowerCase();
          if (_matches(words)) {
            completer.complete(true);
          }
        },
        listenOptions: SpeechListenOptions(
          partialResults: true,
          listenMode: ListenMode.confirmation,
          listenFor: const Duration(seconds: 5),
          pauseFor: const Duration(seconds: 5),
          cancelOnError: true,
        ),
      );
      // Wait for a result-bearing stop: either our match, or the window
      // timing out (final/no-match) after ~listenFor + pauseFor.
      final hit = await completer.future.timeout(
        const Duration(seconds: 12),
        onTimeout: () => false,
      );
      if (hit) {
        _listening = false;
        await _stopListen();
        if (_running) {
          // A real match: fire the wake event on a microtask so the
          // caller can toggle UI before the loop exits.
          onWake();
        }
        _running = false;
        return true;
      }
    } catch (_) {
      // Cancelled or transient error; treat as a no-match window.
    } finally {
      _listening = false;
    }
    if (!_running) return true;
    await _stopListen();
    return false;
  }

  bool _matches(String words) {
    final compact = words.replaceAll(RegExp(r'[^a-z\u0900-\u097F\u0C00-\u0C7F]'), '');
    for (final p in _phrases) {
      final compactPhrase = p.replaceAll(' ', '');
      if (words.contains(p) || compact.contains(compactPhrase)) return true;
    }
    return false;
  }

  Future<void> _stopListen() async {
    try {
      if (_speech.isListening) await _speech.stop();
    } catch (_) {}
  }

  Future<void> stop() async {
    _running = false;
    _listening = false;
    final completer = _currentListen;
    if (completer != null && !completer.isCompleted) {
      completer.complete(false);
    }
    await _stopListen();
  }
}