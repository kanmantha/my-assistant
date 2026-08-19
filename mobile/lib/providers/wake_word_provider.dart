import 'dart:async';

import 'package:flutter/foundation.dart';

import '../services/backend_client.dart';
import '../services/secure_store.dart';
import '../services/wake_word_service.dart';

/// App-level wake-word state: setting persistence, backend sync, and the
/// continuous listening lifecycle. Fires [onWake] when the wake phrase is
/// heard so callers can navigate the user to the assistant.
class WakeWordProvider extends ChangeNotifier {
  WakeWordProvider(this._backend);

  final BackendClient? _backend;

  bool _enabled = false;
  bool _available = false;
  bool _running = false;
  bool _listening = false;
  String _status = 'Idle';

  /// Fired when the wake phrase is heard so the UI can navigate to the
  /// assistant.
  void Function()? onWake;

  bool get enabled => _enabled;
  bool get available => _available;
  bool get running => _running;
  bool get listening => _listening;
  String get status => _status;
  bool get isWeb => WakeWordService.instance.isWeb;

  /// Loads the persisted setting and resumes listening when it is enabled.
  /// Wake word is enabled by default. The backend value is only used to
  /// seed the local preference on first sync (e.g. after a fresh login
  /// with no local value yet).
  Future<void> restore() async {
    _status = 'Initializing…';
    notifyListeners();
    try {
      final local = await SecureStore.isWakeWordEnabled();
      final explicit = await SecureStore.hasWakeWordSetting();
      if (explicit) {
        _enabled = local;
      } else {
        final backend = _backend;
        if (backend != null) {
          try {
            final settings = await backend.userSettings();
            _enabled = settings.wakeWordEnabled ?? true;
          } catch (_) {
            _enabled = true;
          }
        } else {
          _enabled = true;
        }
        await SecureStore.setWakeWord(_enabled);
      }
    } catch (_) {
      _enabled = true;
    }
    if (_enabled) {
      // Delay to let Android fully initialize SpeechRecognizer.
      await Future<void>.delayed(const Duration(seconds: 3));
      final ok = await start();
      if (!ok) {
        _enabled = false;
        _status = 'Speech unavailable — tap to retry';
      }
    } else {
      _status = 'Disabled in settings';
    }
    notifyListeners();
  }

  /// Starts continuous listening. Returns true when listening began.
  Future<bool> start() async {
    _status = 'Requesting microphone permission…';
    notifyListeners();
    _available = await WakeWordService.instance.initialize();
    if (!_available) {
      _status = 'Speech engine unavailable — tap to retry';
      notifyListeners();
      return false;
    }
    _status = 'Starting wake word loop…';
    notifyListeners();
    await WakeWordService.instance.start(onWake: _wake);
    _running = WakeWordService.instance.isRunning;
    _listening = WakeWordService.instance.isListening;
    if (_running) {
      _status = 'Listening for "Hey Assistant"';
    } else {
      _status = 'Failed to start';
    }
    notifyListeners();
    return _running;
  }

  void _wake() {
    _status = 'Wake detected!';
    notifyListeners();
    unawaited(_syncBackend(true));
    onWake?.call();
  }

  Future<void> _syncBackend(bool value) async {
    final backend = _backend;
    if (backend == null) return;
    try {
      await backend.updateUserSettings(wakeWordEnabled: value);
    } catch (_) {}
  }

  Future<void> stop() async {
    await WakeWordService.instance.stop();
    _running = false;
    _listening = false;
    _status = 'Stopped';
    notifyListeners();
  }

  /// Retry initialization after failure.
  Future<void> retry() async {
    _status = 'Retrying…';
    notifyListeners();
    await WakeWordService.instance.stop();
    _running = false;
    _listening = false;
    _available = false;
    // Force re-initialization.
    await start();
    notifyListeners();
  }

  /// Toggles the feature (persists locally and pushes to backend when signed
  /// in). Microphone permission is requested by the speech service.
  Future<void> setEnabled(bool value) async {
    if (_enabled == value) return;
    _enabled = value;
    await SecureStore.setWakeWord(value);
    unawaited(_syncBackend(value));
    if (value) {
      final ok = await start();
      if (!ok) {
        _enabled = false;
        await SecureStore.setWakeWord(false);
        notifyListeners();
      }
    } else {
      await stop();
    }
  }
}
