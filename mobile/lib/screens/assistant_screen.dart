import 'dart:async';
import 'dart:developer' as dev;

import 'package:flutter/material.dart';
import 'package:provider/provider.dart';
import 'package:speech_to_text/speech_to_text.dart' as stt;

import '../providers/assistant_provider.dart';
import '../providers/wake_word_provider.dart';
import '../services/api_client.dart';
import '../services/backend_client.dart';
import '../theme.dart';

class AssistantScreen extends StatefulWidget {
  const AssistantScreen({super.key});

  @override
  State<AssistantScreen> createState() => _AssistantScreenState();
}

class _AssistantScreenState extends State<AssistantScreen> {
  final _controller = TextEditingController();
  final _scroll = ScrollController();
  final _speech = stt.SpeechToText();
  bool _speechInitialized = false;
  bool _listeningActive = false;
  String _partialText = '';
  bool _wasWakeRunning = false;

  @override
  void initState() {
    super.initState();
    WidgetsBinding.instance.addPostFrameCallback((_) {
      if (!mounted) return;
      context.read<AssistantProvider>().addListener(_onAssistantChange);
    });
  }

  void _onAssistantChange() {
    if (!mounted) return;
    final assistant = context.read<AssistantProvider>();
    if (assistant.consumeAutoListen()) {
      _showWakeCue();
      _startListening(fromWake: true);
    }
  }

  void _showWakeCue() {
    if (!mounted) return;
    ScaffoldMessenger.of(context).showSnackBar(
      const SnackBar(
        content: Text('Hey Assistant heard! Speak your command…'),
        duration: Duration(seconds: 2),
      ),
    );
  }

  @override
  void dispose() {
    try { context.read<AssistantProvider>().removeListener(_onAssistantChange); } catch (_) {}
    _controller.dispose();
    _scroll.dispose();
    super.dispose();
  }

  Future<void> _send() async {
    final text = _controller.text.trim();
    if (text.isEmpty) return;
    _controller.clear();
    setState(() => _partialText = '');

    final assistant = context.read<AssistantProvider>();
    assistant.addUserMessage(text);

    final demoResponse = assistant.demoRespond(text);
    if (demoResponse != null) {
      assistant.setBusy(false);
      assistant.addAssistantResponse(demoResponse);
      _scrollToBottom();
      return;
    }

    assistant.setBusy(true);
    try {
      final backend = context.read<BackendClient>();
      final result = await backend.sendCommand(
        text: text,
        language: assistant.language,
        timezone: 'Asia/Kolkata',
      );
      assistant.addAssistantResponse(result);
    } on ApiException catch (e) {
      assistant.addAssistantText('Unable to reach the assistant: ${e.message}');
    } catch (e) {
      assistant.addAssistantText('Something went wrong: $e');
    } finally {
      assistant.setBusy(false);
      _scrollToBottom();
    }
  }

  void _scrollToBottom() {
    WidgetsBinding.instance.addPostFrameCallback((_) {
      if (_scroll.hasClients) {
        _scroll.animateTo(_scroll.position.maxScrollExtent,
            duration: const Duration(milliseconds: 250), curve: Curves.easeOut);
      }
    });
  }

  @override
  Widget build(BuildContext context) {
    final assistant = context.watch<AssistantProvider>();
    final messages = assistant.messages;

    return Scaffold(
      appBar: AppBar(
        title: const Text('Assistant'),
        actions: [
          Padding(
            padding: const EdgeInsets.only(right: 16),
            child: Center(
              child: Container(
                padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 4),
                decoration: BoxDecoration(
                  color: assistant.demoMode
                      ? AppTheme.success.withValues(alpha: 0.15)
                      : AppTheme.primary.withValues(alpha: 0.1),
                  borderRadius: BorderRadius.circular(8),
                ),
                child: Text(
                  assistant.demoMode ? 'DEMO' : 'LIVE',
                  style: TextStyle(
                    fontSize: 11,
                    fontWeight: FontWeight.bold,
                    color: assistant.demoMode ? AppTheme.success : AppTheme.primary,
                  ),
                ),
              ),
            ),
          ),
        ],
      ),
      body: SafeArea(
        child: Column(
          children: [
            Expanded(
              child: messages.isEmpty && _partialText.isEmpty
                  ? _EmptyState(assistant: assistant)
                  : ListView.builder(
                      controller: _scroll,
                      padding: const EdgeInsets.all(16),
                      itemCount: messages.length + (_partialText.isNotEmpty ? 1 : 0),
                      itemBuilder: (context, i) {
                        if (i < messages.length) {
                          return _Bubble(message: messages[i]);
                        }
                        return _PartialBubble(text: _partialText);
                      },
                    ),
            ),
            _InputBar(
              controller: _controller,
              listening: assistant.listening,
              speaking: assistant.speaking,
              busy: assistant.busy,
              onSend: _send,
              onMic: () => _startListening(fromWake: false),
              onStop: () {
                _stopListening();
                _send();
              },
            ),
          ],
        ),
      ),
    );
  }

  Future<void> _startListening({required bool fromWake}) async {
    if (_listeningActive) return;
    final assistant = context.read<AssistantProvider>();

    // Stop the wake word service so it releases the mic.
    final wake = context.read<WakeWordProvider>();
    _wasWakeRunning = wake.running;
    if (_wasWakeRunning) {
      dev.log('[Assistant] stopping wake word for mic handoff');
      await wake.stop();
      await Future<void>.delayed(const Duration(milliseconds: 500));
    }

    // Stop any lingering session on our own instance.
    try {
      if (_speech.isListening) await _speech.stop();
    } catch (_) {}
    await Future<void>.delayed(const Duration(milliseconds: 200));

    if (!_speechInitialized) {
      _speechInitialized = await _speech.initialize(
        onError: (e) {
          dev.log('[Assistant] speech error: ${e.errorMsg}');
          _listeningActive = false;
          assistant.setListening(false);
          _speechInitialized = false;
          setState(() => _partialText = '');
          if (mounted) {
            ScaffoldMessenger.of(context).showSnackBar(
              SnackBar(content: Text('Speech error: ${e.errorMsg}')),
            );
          }
          _restartWakeWord();
        },
      );
      if (!_speechInitialized) {
        dev.log('[Assistant] speech init failed');
        if (mounted) {
          ScaffoldMessenger.of(context).showSnackBar(
            const SnackBar(
              content: Text('Speech recognition unavailable. Check microphone permission.'),
            ),
          );
        }
        _restartWakeWord();
        return;
      }
    }

    assistant.setListening(true);
    _listeningActive = true;
    setState(() => _partialText = '');
    dev.log('[Assistant] starting to listen (fromWake=$fromWake)');

    final started = await _speech.listen(
      onResult: (result) {
        if (result.recognizedWords.isNotEmpty) {
          setState(() => _partialText = result.recognizedWords);
        }
        if (result.finalResult) {
          _listeningActive = false;
          assistant.setListening(false);
          if (result.recognizedWords.isNotEmpty) {
            dev.log('[Assistant] final: "${result.recognizedWords}"');
            _controller.text = result.recognizedWords;
            setState(() => _partialText = '');
            _send();
          } else {
            setState(() => _partialText = '');
            if (mounted) {
              ScaffoldMessenger.of(context).showSnackBar(
                const SnackBar(content: Text('No speech detected. Try again.')),
              );
            }
          }
          _restartWakeWord();
        }
      },
      listenOptions: stt.SpeechListenOptions(
        listenFor: const Duration(seconds: 30),
        pauseFor: const Duration(seconds: 10),
        listenMode: stt.ListenMode.dictation,
        cancelOnError: false,
      ),
    );

    if (started != true) {
      dev.log('[Assistant] listen() returned false');
      _listeningActive = false;
      assistant.setListening(false);
      _speechInitialized = false;
      setState(() => _partialText = '');
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          const SnackBar(
            content: Text('Could not start listening. Tap mic to retry.'),
          ),
        );
      }
      _restartWakeWord();
      return;
    }

    // Safety timeout.
    Future<void>.delayed(const Duration(seconds: 30), () {
      if (_listeningActive && mounted) {
        _listeningActive = false;
        assistant.setListening(false);
        setState(() => _partialText = '');
        try { _speech.stop(); } catch (_) {}
        _restartWakeWord();
      }
    });
  }

  void _stopListening() {
    _listeningActive = false;
    setState(() => _partialText = '');
    try { _speech.stop(); } catch (_) {}
    context.read<AssistantProvider>().setListening(false);
    _restartWakeWord();
  }

  void _restartWakeWord() {
    if (!_wasWakeRunning) return;
    Future<void>.delayed(const Duration(milliseconds: 1500), () {
      if (mounted) {
        dev.log('[Assistant] restarting wake word');
        context.read<WakeWordProvider>().start();
      }
    });
  }
}

class _EmptyState extends StatelessWidget {
  final AssistantProvider assistant;

  const _EmptyState({required this.assistant});

  @override
  Widget build(BuildContext context) {
    return Center(
      child: Padding(
        padding: const EdgeInsets.all(32),
        child: Column(
          mainAxisAlignment: MainAxisAlignment.center,
          children: [
            Container(
              width: 100,
              height: 100,
              decoration: BoxDecoration(
                shape: BoxShape.circle,
                gradient: const LinearGradient(
                    colors: [AppTheme.primary, Color(0xFF8A7BFF)]),
                boxShadow: [
                  BoxShadow(
                      color: AppTheme.primary.withValues(alpha: 0.4),
                      blurRadius: 30)
                ],
              ),
              child: const Icon(Icons.mic, color: Colors.white, size: 48),
            ),
            const SizedBox(height: 24),
            Text(assistant.demoGreeting,
                textAlign: TextAlign.center,
                style: const TextStyle(fontSize: 16, height: 1.5)),
            const SizedBox(height: 16),
            Text(
              'Say "Hey Assistant" to get started.\n\n'
              'Then try:\n'
              '• "Schedule a meeting with Ram at 3 PM"\n'
              '• "Create a task to buy groceries"\n'
              '• "Remind me to call mom tomorrow"\n'
              '• "Take a note about project deadline"',
              textAlign: TextAlign.center,
              style: TextStyle(
                  fontSize: 13,
                  color: Theme.of(context).colorScheme.onSurfaceVariant,
                  height: 1.5),
            ),
          ],
        ),
      ),
    );
  }
}

class _PartialBubble extends StatelessWidget {
  final String text;

  const _PartialBubble({required this.text});

  @override
  Widget build(BuildContext context) {
    return Align(
      alignment: Alignment.centerRight,
      child: Container(
        margin: const EdgeInsets.symmetric(vertical: 5),
        padding: const EdgeInsets.symmetric(horizontal: 14, vertical: 10),
        constraints:
            BoxConstraints(maxWidth: MediaQuery.of(context).size.width * 0.78),
        decoration: BoxDecoration(
          color: AppTheme.primary.withValues(alpha: 0.5),
          borderRadius: BorderRadius.circular(16),
        ),
        child: Row(
          mainAxisSize: MainAxisSize.min,
          children: [
            SizedBox(
              width: 14,
              height: 14,
              child: CircularProgressIndicator(
                  strokeWidth: 2, color: Colors.white.withValues(alpha: 0.8)),
            ),
            const SizedBox(width: 8),
            Flexible(
              child: Text(
                '$text…',
                style: const TextStyle(
                    color: Colors.white, fontSize: 15, fontStyle: FontStyle.italic),
              ),
            ),
          ],
        ),
      ),
    );
  }
}

class _Bubble extends StatelessWidget {
  final ChatMessage message;

  const _Bubble({required this.message});

  @override
  Widget build(BuildContext context) {
    final isUser = message.role == 'user';
    return Align(
      alignment: isUser ? Alignment.centerRight : Alignment.centerLeft,
      child: Container(
        margin: const EdgeInsets.symmetric(vertical: 5),
        padding: const EdgeInsets.symmetric(horizontal: 14, vertical: 10),
        constraints:
            BoxConstraints(maxWidth: MediaQuery.of(context).size.width * 0.78),
        decoration: BoxDecoration(
          color: isUser
              ? AppTheme.primary
              : Theme.of(context).colorScheme.surface,
          borderRadius: BorderRadius.only(
            topLeft: const Radius.circular(16),
            topRight: const Radius.circular(16),
            bottomLeft: Radius.circular(isUser ? 16 : 4),
            bottomRight: Radius.circular(isUser ? 4 : 16),
          ),
        ),
        child: Text(
          message.text,
          style: TextStyle(
              color: isUser
                  ? Colors.white
                  : Theme.of(context).colorScheme.onSurface,
              fontSize: 15,
              height: 1.35),
        ),
      ),
    );
  }
}

class _InputBar extends StatelessWidget {
  final TextEditingController controller;
  final bool listening;
  final bool speaking;
  final bool busy;
  final VoidCallback onSend;
  final VoidCallback onMic;
  final VoidCallback onStop;

  const _InputBar({
    required this.controller,
    required this.listening,
    required this.speaking,
    required this.busy,
    required this.onSend,
    required this.onMic,
    required this.onStop,
  });

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.fromLTRB(12, 8, 12, 10),
      decoration: BoxDecoration(
        color: Theme.of(context).colorScheme.surface,
        boxShadow: [
          BoxShadow(
              color: Colors.black.withValues(alpha: 0.05), blurRadius: 8)
        ],
      ),
      child: SafeArea(
        top: false,
        child: Row(
          children: [
            IconButton(
              onPressed: busy ? null : onMic,
              icon: Icon(listening ? Icons.graphic_eq : Icons.mic,
                  color: listening ? Colors.red : AppTheme.primary),
              tooltip: 'Tap to speak (or say "Hey Assistant")',
            ),
            Expanded(
              child: TextField(
                controller: controller,
                textInputAction: TextInputAction.send,
                onSubmitted: (_) => onSend(),
                decoration: const InputDecoration(
                    hintText: 'Type a command…',
                    isDense: true,
                    border: InputBorder.none,
                    filled: false),
              ),
            ),
            IconButton.filled(
              onPressed: busy ? null : onSend,
              icon: Icon(busy ? Icons.hourglass_top : Icons.send),
            ),
          ],
        ),
      ),
    );
  }
}
