import 'package:flutter/foundation.dart';

import '../models/models.dart';
import '../services/secure_store.dart';

/// Data-first assistant chat state. In demo mode it returns canned localized
/// responses so the full UX runs even when the backend is offline.
class AssistantProvider extends ChangeNotifier {
  bool _listening = false;
  bool _speaking = false;
  bool _busy = false;
  bool _demoMode = false;
  bool _autoListen = false;
  final List<ChatMessage> _messages = [];
  String _languageCode = 'en-IN';

  List<ChatMessage> get messages => List.unmodifiable(_messages);
  bool get listening => _listening;
  bool get speaking => _speaking;
  bool get busy => _busy;
  bool get demoMode => _demoMode;
  bool get autoListen => _autoListen;
  String get language => _languageCode;

  /// Syncs demo mode with persisted storage and clears chat history, so a
  /// switch between demo and live (login/sign out) never leaves stale DEMO
  /// badges or canned messages behind.
  Future<void> setDemoMode() async {
    _demoMode = await SecureStore.isDemoMode();
    _messages.clear();
    if (_demoMode) {
      _messages.add(ChatMessage(role: 'assistant', text: demoGreeting, language: _languageCode));
    }
    notifyListeners();
  }

  String get demoGreeting {
    if (_languageCode == 'hi-IN') {
      return 'नमस्ते! मैं आपका सहायक हूँ। आज मैं आपके लिए क्या कर सकता हूँ?';
    }
    if (_languageCode == 'te-IN') {
      return 'నమస్తే! నేను మీ అసిస్టెంట్ను. ఈరోజు నేను మీ కోసం ఏమి చేయగలను?';
    }
    return 'Hello! I am your AI assistant. What can I do for you today?';
  }

  /// Canned localized demo response. Returns null when online mode is active
  /// (the caller must then call the backend).
  AssistantCommandResult? demoRespond(String text) {
    if (!_demoMode) return null;
    final lang = _languageCode;
    final t = text.toLowerCase();

    String pick(String en, String hi, String te) {
      if (lang == 'hi-IN') return hi;
      if (lang == 'te-IN') return te;
      return en;
    }

    if (t.contains('note') || t.contains('नोट') || t.contains('గమనిక')) {
      return AssistantCommandResult(
        success: true,
        intent: 'CreateNote',
        responseText: pick('Note created!', 'नोट बन गया है!', 'గమనిక సృష్టించబడింది!'),
        responseLanguage: lang,
        needsClarification: false,
      );
    }
    if (t.contains('remind') || t.contains('याद') || t.contains('గుర్తు')) {
      return AssistantCommandResult(
        success: true,
        intent: 'CreateReminder',
        responseText: pick('Reminder set!', 'रिमाइंडर सेट कर दिया है!', 'రిమైండర్ సెట్ చేయబడింది!'),
        responseLanguage: lang,
        needsClarification: false,
      );
    }
    if (t.contains('task') || t.contains('काम') || t.contains('పని')) {
      return AssistantCommandResult(
        success: true,
        intent: 'CreateTask',
        responseText: pick('Task added!', 'नया काम जोड़ दिया है!', 'కొత్త పని జోడించబడింది!'),
        responseLanguage: lang,
        needsClarification: false,
      );
    }
    if (t.contains('help') || t.contains('मदद') || t.contains('సహాయం')) {
      return AssistantCommandResult(
        success: true,
        intent: 'Help',
        responseText: pick(
          'I can create notes, tasks, reminders, and schedule appointments. What would you like?',
          'मैं नोट, काम, रिमाइंडर बना सकता हूँ और अपॉइंटमेंट शेड्यूल कर सकता हूँ।',
          'నేను గమనికలు, పనులు, రిమైండర్లు సృష్టించగలను మరియు అపాయింట్మెంట్ షెడ్యూల్ చేయగలను.',
        ),
        responseLanguage: lang,
        needsClarification: false,
      );
    }
    return AssistantCommandResult(
      success: true,
      intent: 'Help',
      responseText: pick(
        'Got it. Please tell me a little more.',
        'समझ गया। मुझे थोड़ा और बताइए।',
        'అర్థమైంది. మరికాస్త చెప్పండి.',
      ),
      responseLanguage: lang,
      needsClarification: false,
    );
  }

  void addUserMessage(String text) {
    _messages.add(ChatMessage(role: 'user', text: text, language: _languageCode));
    notifyListeners();
  }

  void addAssistantResponse(AssistantCommandResult result) {
    _messages.add(ChatMessage(
      role: 'assistant',
      text: result.responseText ?? '…',
      language: result.responseLanguage ?? _languageCode,
    ));
    notifyListeners();
  }

  void addAssistantText(String text) {
    _messages.add(ChatMessage(role: 'assistant', text: text, language: _languageCode));
    notifyListeners();
  }

  void setListening(bool value) {
    _listening = value;
    notifyListeners();
  }

  /// Called by the wake-word handler to signal that the assistant screen
  /// should immediately start listening for the user's instruction.
  void requestAutoListen() {
    _autoListen = true;
    notifyListeners();
  }

  /// Consumes the auto-listen flag (returns true once, then resets).
  bool consumeAutoListen() {
    if (!_autoListen) return false;
    _autoListen = false;
    return true;
  }

  void setSpeaking(bool value) {
    _speaking = value;
    notifyListeners();
  }

  void setBusy(bool value) {
    _busy = value;
    notifyListeners();
  }

  void setLanguage(String code) {
    _languageCode = code;
    notifyListeners();
  }

  void clear() {
    _messages.clear();
    notifyListeners();
  }
}

class ChatMessage {
  final String role;
  final String text;
  final String language;

  const ChatMessage({required this.role, required this.text, required this.language});
}