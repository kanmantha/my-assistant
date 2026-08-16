import 'package:flutter/foundation.dart';

import '../models/models.dart';

class AuthProvider extends ChangeNotifier {
  bool _busy = false;
  String? _error;
  String? _accessToken;
  UserProfile? _profile;
  bool _demoMode = false;

  bool get busy => _busy;
  bool get isAuthenticated => _accessToken != null && _profile != null;
  bool get demoMode => _demoMode;
  String? get error => _error;
  UserProfile? get profile => _profile;

  void setDemoAuth({required UserProfile profile, String? accessToken, String? refreshToken}) {
    _demoMode = true;
    _profile = profile;
    _accessToken = accessToken;
    notifyListeners();
  }

  void restoreSession(String access, String refresh, UserProfile profile) {
    _demoMode = false;
    _accessToken = access;
    _profile = profile;
    notifyListeners();
  }

  void setSessionFromAuth(AuthResult result) {
    _demoMode = false;
    _accessToken = result.accessToken;
    _profile = result.profile;
    notifyListeners();
  }

  void updateProfile(UserProfile p) {
    _profile = p;
    notifyListeners();
  }

  Future<void> signOut() async {
    _demoMode = false;
    _accessToken = null;
    _profile = null;
    notifyListeners();
  }

  void setBusy(bool value) {
    _busy = value;
    notifyListeners();
  }

  void setError(String message) {
    _error = message;
    notifyListeners();
  }

  void clearError() {
    _error = null;
  }
}