import 'package:flutter/services.dart';

/// Opens URLs strictly in Google Chrome on Android via an explicit intent
/// targeting the `com.android.chrome` package, so the system never shows a
/// multi-browser chooser. Fallback: opens the system default browser.
class ChromeOnlyLauncher {
  ChromeOnlyLauncher._();

  static const MethodChannel _channel = MethodChannel('myassistant/chrome');

  /// Opens [url] in Chrome. Returns false (without launching anything) when
  /// the URL is invalid. Throws [PlatformException] if no browser is found.
  static Future<bool> open(String url) async {
    if (!Uri.parse(url).hasScheme) return false;
    final ok = await _channel.invokeMethod<bool>('openUrl', {'url': url});
    return ok ?? false;
  }
}