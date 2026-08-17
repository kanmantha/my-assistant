import 'package:flutter_secure_storage/flutter_secure_storage.dart';

/// Persists tokens & demo-mode flag in encrypted storage; lightweight prefs elsewhere.
class SecureStore {
  SecureStore._();

  static const _storage = FlutterSecureStorage(
    aOptions: AndroidOptions(),
  );

  static const _tokenKey = 'auth_token';
  static const _refreshKey = 'refresh_token';
  static const _demoKey = 'demo_mode';
  static const _wakeWordKey = 'wake_word';

  static Future<void> saveTokens(String access, String refresh) async {
    await _storage.write(key: _tokenKey, value: access);
    await _storage.write(key: _refreshKey, value: refresh);
  }

  static Future<({String? access, String? refresh})> readTokens() async {
    final access = await _storage.read(key: _tokenKey);
    final refresh = await _storage.read(key: _refreshKey);
    return (access: access, refresh: refresh);
  }

  static Future<void> clearTokens() async {
    await _storage.delete(key: _tokenKey);
    await _storage.delete(key: _refreshKey);
  }

  static Future<void> setDemoMode(bool on) =>
      _storage.write(key: _demoKey, value: on ? '1' : '0');

  static Future<bool> isDemoMode() async =>
      await _storage.read(key: _demoKey) == '1';

  static Future<void> setWakeWord(bool on) =>
      _storage.write(key: _wakeWordKey, value: on ? '1' : '0');

  static Future<bool> isWakeWordEnabled() async =>
      await _storage.read(key: _wakeWordKey) != '0';

  static Future<bool> hasWakeWordSetting() async =>
      await _storage.read(key: _wakeWordKey) != null;

  static Future<void> clearAll() async {
    await _storage.deleteAll();
  }
}