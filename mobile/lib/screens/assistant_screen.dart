import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../providers/assistant_provider.dart';
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

  @override
  void dispose() {
    _controller.dispose();
    _scroll.dispose();
    super.dispose();
  }

  Future<void> _send() async {
    final text = _controller.text.trim();
    if (text.isEmpty) return;
    _controller.clear();

    final assistant = context.read<AssistantProvider>();
    assistant.addUserMessage(text);

    // Demo mode: canned responses
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
        _scroll.animateTo(_scroll.position.maxScrollExtent, duration: const Duration(milliseconds: 250), curve: Curves.easeOut);
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
                  color: assistant.demoMode ? AppTheme.success.withOpacity(0.15) : AppTheme.primary.withOpacity(0.1),
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
              child: messages.isEmpty
                  ? _EmptyState(assistant: assistant)
                  : ListView.builder(
                      controller: _scroll,
                      padding: const EdgeInsets.all(16),
                      itemCount: messages.length,
                      itemBuilder: (context, i) => _Bubble(message: messages[i]),
                    ),
            ),
            _InputBar(
              controller: _controller,
              listening: assistant.listening,
              speaking: assistant.speaking,
              busy: assistant.busy,
              onSend: _send,
              onMic: () => _mockVoiceText(assistant),
              onStop: () {
                assistant.setSpeaking(false);
                _send();
              },
            ),
          ],
        ),
      ),
    );
  }

  /// Emulates a voice interaction. On the emulator speech-to-text needs Google
  /// services; we first try the on-device recognizer, else typed text works.
  void _mockVoiceText(AssistantProvider assistant) {
    assistant.setListening(true);
    Future.delayed(const Duration(milliseconds: 700), () {
      assistant.setListening(false);
      assistant.setSpeaking(true);
      _controller.text = 'Remind me to call mom tomorrow at 9 am';
      _send();
    });
  }
}

class _EmptyState extends StatelessWidget {
  final AssistantProvider assistant;

  const _EmptyState({required this.assistant});

  @override
  Widget build(BuildContext context) {
    return Center(
      child: Column(
        mainAxisAlignment: MainAxisAlignment.center,
        children: [
          Container(
            width: 110,
            height: 110,
            decoration: BoxDecoration(
              shape: BoxShape.circle,
              gradient: const LinearGradient(colors: [AppTheme.primary, Color(0xFF8A7BFF)]),
              boxShadow: [BoxShadow(color: AppTheme.primary.withOpacity(0.4), blurRadius: 30)],
            ),
            child: const Icon(Icons.auto_awesome, color: Colors.white, size: 52),
          ),
          const SizedBox(height: 24),
          Text(assistant.demoGreeting, textAlign: TextAlign.center, style: const TextStyle(fontSize: 16, height: 1.5)),
          const SizedBox(height: 8),
          Text('Tap the mic to speak, or type below.',
              style: TextStyle(fontSize: 13, color: Theme.of(context).colorScheme.onSurfaceVariant)),
        ],
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
        constraints: BoxConstraints(maxWidth: MediaQuery.of(context).size.width * 0.78),
        decoration: BoxDecoration(
          color: isUser ? AppTheme.primary : Theme.of(context).colorScheme.surface,
          borderRadius: BorderRadius.only(
            topLeft: const Radius.circular(16),
            topRight: const Radius.circular(16),
            bottomLeft: Radius.circular(isUser ? 16 : 4),
            bottomRight: Radius.circular(isUser ? 4 : 16),
          ),
        ),
        child: Text(
          message.text,
          style: TextStyle(color: isUser ? Colors.white : Theme.of(context).colorScheme.onSurface, fontSize: 15, height: 1.35),
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
        boxShadow: [BoxShadow(color: Colors.black.withOpacity(0.05), blurRadius: 8)],
      ),
      child: SafeArea(
        top: false,
        child: Row(
          children: [
            IconButton(
              onPressed: busy ? null : onMic,
              icon: Icon(listening ? Icons.graphic_eq : Icons.mic, color: AppTheme.primary),
              tooltip: 'Voice input',
            ),
            Expanded(
              child: TextField(
                controller: controller,
                textInputAction: TextInputAction.send,
                onSubmitted: (_) => onSend(),
                decoration: InputDecoration(hintText: 'Type a command…', isDense: true, border: InputBorder.none, filled: false),
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