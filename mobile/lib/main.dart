import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import 'models/models.dart';
import 'providers/assistant_provider.dart';
import 'providers/auth_provider.dart';
import 'providers/productivity_providers.dart';
import 'providers/wake_word_provider.dart';
import 'screens/home_shell.dart';
import 'screens/onboarding_screen.dart';
import 'screens/splash_screen.dart';
import 'services/api_client.dart';
import 'services/backend_client.dart';
import 'services/secure_store.dart';
import 'theme.dart';

void main() {
  WidgetsFlutterBinding.ensureInitialized();
  runApp(const MyAssistantApp());
}

/// Fire-and-forget with error swallowing so restore() never blocks boot.
void unawaitedWithLog(Future<void> future) {
  future.catchError((_) {});
}

class MyAssistantApp extends StatelessWidget {
  const MyAssistantApp({super.key});

  @override
  Widget build(BuildContext context) {
    return MultiProvider(
      providers: [
        ChangeNotifierProvider(create: (_) => AuthProvider()),
        ChangeNotifierProvider(create: (_) => AssistantProvider()),
        Provider<ApiClient>(create: (_) => ApiClient()),
        Provider<BackendClient>(
          create: (context) => BackendClient(context.read<ApiClient>()),
        ),
        ChangeNotifierProvider(
          create: (context) => TasksState(context.read<BackendClient>()),
        ),
        ChangeNotifierProvider(
          create: (context) => NotesState(context.read<BackendClient>()),
        ),
        ChangeNotifierProvider(
          create: (context) => WakeWordProvider(context.read<BackendClient>()),
        ),
      ],
      child: MaterialApp(
        title: 'My Assistant',
        debugShowCheckedModeBanner: false,
        theme: AppTheme.light(),
        darkTheme: AppTheme.dark(),
        home: const SplashScreen(),
      ),
    );
  }
}

/// Chooses the first screen after splash based on stored session/demo state.
class Gate extends StatefulWidget {
  const Gate({super.key});

  @override
  State<Gate> createState() => _GateState();
}

class _GateState extends State<Gate> {
  @override
  void initState() {
    super.initState();
    _bootstrap();
  }

  Future<void> _bootstrap() async {
    final auth = context.read<AuthProvider>();
    final assistant = context.read<AssistantProvider>();
    final api = context.read<ApiClient>();
    final wake = context.read<WakeWordProvider>();

    try {
      final demo = await SecureStore.isDemoMode();
      final tokens = await SecureStore.readTokens();
      if (demo) {
        auth.setDemoAuth(
          profile: demoProfile,
          accessToken: '',
          refreshToken: '',
        );
        await assistant.setDemoMode();
      } else if (tokens.access != null && tokens.refresh != null) {
        final backend = context.read<BackendClient>();
        try {
          final result = await backend.refreshToken(tokens.refresh!);
          if (!mounted) return;
          await SecureStore.saveTokens(result.accessToken, result.refreshToken);
          api.setTokens(result.accessToken, result.refreshToken);
          auth.restoreSession(result.accessToken, result.refreshToken, result.profile);
        } catch (_) {
          await SecureStore.clearTokens();
        }
      }
    } catch (_) {
      // In test environments secure storage has no native channel; ignore.
    }
    // Resume wake-word listening from persisted preference (no-ops if off).
    unawaitedWithLog(wake.restore());
  }

  @override
  Widget build(BuildContext context) {
    final auth = context.watch<AuthProvider>();
    if (auth.isAuthenticated || auth.demoMode) {
      return const HomeShell();
    }
    return const OnboardingScreen();
  }
}