import 'dart:async';
import 'dart:developer' as dev;

import 'package:flutter/services.dart';
import 'package:speech_to_text/speech_to_text.dart';

/// Singleton that wraps a single [SpeechToText] instance shared between the
/// wake-word listener and the assistant screen.
///
/// Android only allows ONE [SpeechRecognizer] at a time. Using two separate
/// [SpeechToText] objects causes the second one to fail silently. This class
/// serialises access so only one consumer uses the mic at any moment.
class SpeechService {
  SpeechService._();
  static final SpeechService instance = SpeechService._();

  static const _permChannel = MethodChannel('myassistant/permissions');

  final SpeechToText _speech = SpeechToText();
  bool _initialized = false;

  bool get isListening => _speech.isListening;
  bool get isAvailable => _initialized;

  /// Check if RECORD_AUDIO permission is granted (Android only).
  Future<bool> checkMicPermission() async {
    try {
      final result = await _permChannel.invokeMethod<bool>('checkMicPermission');
      return result ?? false;
    } catch (_) {
      return true; // On web/iOS assume OK
    }
  }

  /// Request mic permission. Returns true if granted after request.
  Future<bool> requestMicPermission() async {
    try {
      final result = await _permChannel.invokeMethod<bool>('requestMicPermission');
      return result ?? false;
    } catch (_) {
      return true;
    }
  }

  /// Check if a speech recognition service is installed on the device.
  Future<bool> hasSpeechService() async {
    try {
      final result = await _permChannel.invokeMethod<bool>('hasSpeechService');
      return result ?? false;
    } catch (_) {
      return true;
    }
  }

  /// Open the Android app settings screen so the user can grant permissions.
  Future<void> openAppSettings() async {
    try {
      await _permChannel.invokeMethod('openAppSettings');
    } catch (_) {}
  }

  /// Diagnose the full chain: permission → speech service → init.
  /// Returns a human-readable status string.
  Future<String> diagnose() async {
    final hasPerm = await checkMicPermission();
    if (!hasPerm) {
      final granted = await requestMicPermission();
      if (!granted) return 'MIC_PERMISSION_DENIED';
    }

    final hasService = await hasSpeechService();
    if (!hasService) return 'NO_SPEECH_SERVICE';

    final ok = await init();
    if (!ok) return 'INIT_FAILED';

    return 'OK';
  }

  /// One-time init — safe to call multiple times.
  Future<bool> init() async {
    if (_initialized && _speech.isAvailable) return true;
    try {
      _initialized = await _speech.initialize(
        onError: (e) => dev.log('[SpeechService] onError: ${e.errorMsg} perm=${e.permanent}'),
        onStatus: (s) => dev.log('[SpeechService] onStatus: $s'),
        debugLogging: true,
      );
    } catch (e) {
      dev.log('[SpeechService] init error: $e');
      _initialized = false;
    }
    return _initialized;
  }

  /// Force a full stop + cancel so the platform mic is released.
  Future<void> release() async {
    try { if (_speech.isListening) await _speech.stop(); } catch (_) {}
    try { await _speech.cancel(); } catch (_) {}
    _initialized = false;
    dev.log('[SpeechService] released');
  }

  /// Start listening.
  Future<bool> start({
    required SpeechResultListener onResult,
    Duration listenFor = const Duration(seconds: 15),
    Duration pauseFor = const Duration(seconds: 8),
    ListenMode listenMode = ListenMode.dictation,
    bool partialResults = true,
  }) async {
    if (!_initialized) {
      final ok = await init();
      if (!ok) return false;
    }
    try {
      final started = await _speech.listen(
        onResult: onResult,
        listenOptions: SpeechListenOptions(
          listenFor: listenFor,
          pauseFor: pauseFor,
          listenMode: listenMode,
          partialResults: partialResults,
          cancelOnError: false,
        ),
      );
      dev.log('[SpeechService] start returned $started');
      return started == true;
    } catch (e) {
      dev.log('[SpeechService] start error: $e');
      return false;
    }
  }

  Future<void> stop() async {
    try { if (_speech.isListening) await _speech.stop(); } catch (_) {}
  }
}
