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

  /// Fired when the wake phrase is heard so the UI can navigate to the
  /// assistant.
  void Function()? onWake;

  bool get enabled => _enabled;
  bool get available => _available;
  bool get running => _running;
  bool get listening => _listening;
  bool get isWeb => WakeWordService.instance.isWeb;

  /// Loads the persisted setting and resumes listening when it is enabled.
  /// When signed in, the server is the source of truth (so toggles survive
  /// reinstall/re-login); otherwise the local value is used.
  Future<void> restore() async {
    try {
      _enabled = await SecureStore.isWakeWordEnabled();
    } catch (_) {
      _enabled = false;
    }
    final backend = _backend;
    if (backend != null) {
      try {
        final settings = await backend.userSettings();
        if (settings.wakeWordEnabled != null) {
          _enabled = settings.wakeWordEnabled!;
          await SecureStore.setWakeWord(_enabled);
        }
      } catch (_) {
        // Not signed in or backend unreachable: keep local value.
      }
    }
    if (_enabled) {
      final ok = await start();
      if (!ok) _enabled = false;
    }
    notifyListeners();
  }

  /// Starts continuous listening. Returns true when listening began.
  Future<bool> start() async {
    _available = await WakeWordService.instance.initialize();
    if (!_available) return false;
    await WakeWordService.instance.start(onWake: _wake);
    _running = WakeWordService.instance.isRunning;
    _listening = WakeWordService.instance.isListening;
    notifyListeners();
    return _running;
  }

  void _wake() {
    unawaited(_syncBackend(true));
    onWake?.call();
    notifyListeners();
  }

  Future<void> _syncBackend(bool value) async {
    final backend = _backend;
    if (backend == null) return;
    try {
      await backend.updateUserSettings(wakeWordEnabled: value);
    } catch (_) {
      // Non-fatal: local state wins until next restore.
    }
  }

  Future<void> stop() async {
    await WakeWordService.instance.stop();
    _running = false;
    _listening = false;
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