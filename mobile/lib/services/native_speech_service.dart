import 'dart:async';
import 'dart:developer' as dev;
import 'dart:io' show Platform;

import 'package:flutter/foundation.dart' show kIsWeb;
import 'package:flutter/services.dart';

/// Direct Android SpeechRecognizer via MethodChannel + EventChannel.
/// Completely bypasses the speech_to_text Flutter plugin.
class NativeSpeechService {
  NativeSpeechService._();
  static final NativeSpeechService instance = NativeSpeechService._();

  static const _method = MethodChannel('myassistant/native_speech');
  static const _events = EventChannel('myassistant/native_speech_events');

  StreamSubscription<dynamic>? _eventSub;
  final _controller = StreamController<NativeSpeechEvent>.broadcast();
  bool _listening = false;

  bool get isListening => _listening;
  bool get isWeb => kIsWeb;
  bool get isSupported => !kIsWeb && (Platform.isAndroid || Platform.isIOS);

  Stream<NativeSpeechEvent> get onEvent => _controller.stream;

  /// Ensure the native EventChannel subscription is active.
  void _ensureSubscription() {
    if (_eventSub != null) return;
    _eventSub = _events.receiveBroadcastStream().listen((dynamic data) {
      final map = Map<String, dynamic>.from(data as Map);
      final type = map['type'] as String? ?? '';

      switch (type) {
        case 'ready':
          dev.log('[NativeSpeech] ready for speech');
          break;
        case 'speech_started':
          _listening = true;
          dev.log('[NativeSpeech] speech started');
          break;
        case 'speech_ended':
          _listening = false;
          dev.log('[NativeSpeech] speech ended');
          break;
        case 'result':
          final text = map['text'] as String? ?? '';
          final isFinal = map['final'] as bool? ?? false;
          dev.log('[NativeSpeech] result: "$text" final=$isFinal');
          _controller.add(NativeSpeechResultEvent(text, isFinal));
          if (isFinal) _listening = false;
          break;
        case 'error':
          final error = map['error'] as String? ?? 'UNKNOWN';
          dev.log('[NativeSpeech] error: $error');
          _listening = false;
          _controller.add(NativeSpeechErrorEvent(error));
          break;
        default:
          dev.log('[NativeSpeech] unknown event: $type');
      }
    }, onError: (e) {
      dev.log('[NativeSpeech] stream error: $e');
      _listening = false;
    });
  }

  Future<bool> isAvailable() async {
    if (!isSupported) return false;
    try {
      final result = await _method.invokeMethod<bool>('isAvailable');
      return result ?? false;
    } catch (e) {
      dev.log('[NativeSpeech] isAvailable error: $e');
      return false;
    }
  }

  Future<bool> startListening({String language = 'en-IN'}) async {
    if (_listening) {
      await stopListening();
      await Future<void>.delayed(const Duration(milliseconds: 500));
    }

    _ensureSubscription();

    try {
      await _method.invokeMethod('startListening', {'language': language});
      _listening = true;
      return true;
    } catch (e) {
      dev.log('[NativeSpeech] startListening error: $e');
      _listening = false;
      return false;
    }
  }

  Future<void> stopListening() async {
    _listening = false;
    try {
      await _method.invokeMethod('stopListening');
    } catch (e) {
      dev.log('[NativeSpeech] stopListening error: $e');
    }
  }

  /// Listen once and return the result text.
  Future<String?> listenOnce({
    String language = 'en-IN',
    Duration timeout = const Duration(seconds: 15),
  }) async {
    final completer = Completer<String?>();

    // Listen to the ALREADY-EXISTING stream (no new EventChannel subscription).
    late StreamSubscription<dynamic> sub;
    sub = _controller.stream.listen((event) {
      if (completer.isCompleted) return;
      if (event is NativeSpeechResultEvent) {
        if (event.isFinal) {
          completer.complete(event.text.isEmpty ? null : event.text);
        }
      } else if (event is NativeSpeechErrorEvent) {
        completer.complete(null);
      }
    });

    final started = await startListening(language: language);
    if (!started) {
      sub.cancel();
      return null;
    }

    final result = await completer.future.timeout(timeout, onTimeout: () {
      dev.log('[NativeSpeech] listenOnce timed out');
      return null;
    });

    sub.cancel();
    await stopListening();
    return result;
  }

  void dispose() {
    _eventSub?.cancel();
    _eventSub = null;
    _controller.close();
  }
}

sealed class NativeSpeechEvent {}

class NativeSpeechResultEvent extends NativeSpeechEvent {
  final String text;
  final bool isFinal;
  NativeSpeechResultEvent(this.text, this.isFinal);
}

class NativeSpeechErrorEvent extends NativeSpeechEvent {
  final String error;
  NativeSpeechErrorEvent(this.error);
}
