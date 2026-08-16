import 'dart:async';
import 'dart:convert';
import 'dart:io';

import 'package:http/http.dart' as http;

import '../config.dart';

class ApiException implements Exception {
  final int statusCode;
  final String message;
  final String? errorCode;

  ApiException(this.statusCode, this.message, [this.errorCode]);

  @override
  String toString() => 'ApiException($statusCode): $message';
}

class ApiClient {
  ApiClient({http.Client? client}) : _client = client ?? http.Client();

  final http.Client _client;
  String? _accessToken;
  String? _refreshToken;

  void setTokens(String access, String refresh) {
    _accessToken = access;
    _refreshToken = refresh;
  }

  void clearTokens() {
    _accessToken = null;
    _refreshToken = null;
  }

  /// Resolves the full API URI. An empty [AppConfig.apiBaseUrl] means
  /// "same origin" (used by the hosted web build served from the API itself).
  Uri _uri(String path, [Map<String, String>? query]) {
    final base = AppConfig.apiBaseUrl;
    final url =
        base.isEmpty ? Uri.base.resolve(path) : Uri.parse('$base$path');
    return url.replace(queryParameters: query);
  }

  Map<String, String> _headers({bool json = true}) => {
        if (json) 'Content-Type': 'application/json',
        if (_accessToken != null) 'Authorization': 'Bearer $_accessToken',
      };

  Future<Map<String, dynamic>> get(String path, {Map<String, String>? query}) =>
      _send('GET', path, query: query);

  Future<Map<String, dynamic>> post(String path, [Object? body]) =>
      _send('POST', path, body: body);

  Future<Map<String, dynamic>> put(String path, [Object? body]) =>
      _send('PUT', path, body: body);

  Future<Map<String, dynamic>> delete(String path) => _send('DELETE', path);

  Future<Map<String, dynamic>> _send(
    String method,
    String path, {
    Object? body,
    Map<String, String>? query,
    bool retried = false,
  }) async {
    final uri = _uri(path, query);
    late http.Response resp;
    try {
      final encoded = body == null ? null : jsonEncode(body);
      switch (method) {
        case 'GET':
          resp = await _client.get(uri, headers: _headers()).timeout(const Duration(seconds: 20));
          break;
        case 'POST':
          resp = await _client.post(uri, headers: _headers(), body: encoded).timeout(const Duration(seconds: 20));
          break;
        case 'PUT':
          resp = await _client.put(uri, headers: _headers(), body: encoded).timeout(const Duration(seconds: 20));
          break;
        default:
          resp = await _client.delete(uri, headers: _headers()).timeout(const Duration(seconds: 20));
      }
} on SocketException {
      throw ApiException(0, 'No network connection');
    } on TimeoutException {
      throw ApiException(0, 'Request timed out');
    }

    Map<String, dynamic>? decoded;
    try {
      if (resp.body.isNotEmpty) decoded = jsonDecode(resp.body) as Map<String, dynamic>;
    } catch (_) {}

    if (resp.statusCode == 401 && !retried && _refreshToken != null) {
      await refresh();
      return _send(method, path, body: body, query: query, retried: true);
    }
    if (resp.statusCode >= 400) {
      final msg = decoded?['message']?.toString() ?? 'Request failed (${resp.statusCode})';
      throw ApiException(resp.statusCode, msg, decoded?['errorCode']?.toString());
    }
    return decoded ?? {};
  }

  /// Refreshes the access token via /api/auth/refresh and updates stored tokens.
  Future<void> refresh() async {
    final refresh = _refreshToken;
    if (refresh == null) return;
    final base = AppConfig.apiBaseUrl;
    final uri = base.isEmpty
        ? Uri.base.resolve('/api/auth/refresh')
        : Uri.parse('$base/api/auth/refresh');
    final encoded = jsonEncode({'refreshToken': refresh});
    final resp = await _client
        .post(uri, headers: {'Content-Type': 'application/json'}, body: encoded)
        .timeout(const Duration(seconds: 20));
    if (resp.statusCode >= 400) {
      _accessToken = null;
      _refreshToken = null;
      throw ApiException(resp.statusCode, 'Session expired');
    }
    try {
      final body = jsonDecode(resp.body) as Map<String, dynamic>;
      final data = body['data'] as Map<String, dynamic>? ?? body;
      final access = data['accessToken']?.toString();
      final newRefresh = data['refreshToken']?.toString() ?? refresh;
      if (access != null) {
        setTokens(access, newRefresh);
      }
    } catch (_) {}
  }
}