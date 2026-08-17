/// Central configuration. Override API_BASE_URL for real deployments.
class AppConfig {
  AppConfig._();

  /// Points at the deployed backend by default. Override with
  /// `--dart-define=API_BASE_URL=http://10.0.2.2:5088` for local emulator testing.
  static const String apiBaseUrl = String.fromEnvironment(
    'API_BASE_URL',
    defaultValue: 'https://my-assistant-v35w.onrender.com',
  );

  static String get authTokenKey => 'auth_token';
  static String get refreshTokenKey => 'refresh_token';
  static String get userProfileKey => 'user_profile';
  static String get demoModeKey => 'demo_mode';
  static String get themeModeKey => 'theme_mode';
  static String get languageKey => 'language';
}
enum AsiaLanguages {
  english('en-IN', 'English', 'English'),
  hindi('hi-IN', 'Hindi', 'हिन्दी'),
  telugu('te-IN', 'Telugu', 'తెలుగు');

  const AsiaLanguages(this.code, this.name, this.nativeName);
  final String code;
  final String name;
  final String nativeName;
}