import 'dart:async';

import 'package:flutter_tts/flutter_tts.dart';

/// Thin wrapper around [FlutterTts] for the assistant voice prompts.
class TtsService {
  TtsService._();
  static final TtsService instance = TtsService._();

  final FlutterTts _tts = FlutterTts();
  bool _initialized = false;

  Future<void> init() async {
    if (_initialized) return;
    await _tts.setLanguage('en-IN');
    await _tts.setSpeechRate(0.45);
    await _tts.setVolume(1.0);
    await _tts.setPitch(1.0);
    _initialized = true;
  }

  /// Speaks [text] and completes when TTS finishes.
  Future<void> speak(String text) async {
    await init();
    final completer = Completer<void>();
    _tts.setCompletionHandler(() {
      if (!completer.isCompleted) completer.complete();
    });
    await _tts.speak(text);
    await completer.future.timeout(
      const Duration(seconds: 15),
      onTimeout: () {},
    );
  }

  Future<void> stop() async {
    try { await _tts.stop(); } catch (_) {}
  }

  Future<void> setLanguage(String code) async {
    await init();
    await _tts.setLanguage(code);
  }
}
