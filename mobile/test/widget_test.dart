import 'package:flutter_test/flutter_test.dart';

import 'package:my_assistant/main.dart';

void main() {
  testWidgets('App boots and shows splash', (WidgetTester tester) async {
    await tester.pumpWidget(const MyAssistantApp());
    await tester.pump(const Duration(milliseconds: 100));
    expect(find.text('My Assistant'), findsOneWidget);
    await tester.pump(const Duration(seconds: 3));
    await tester.pump(const Duration(milliseconds: 500));
  });

  testWidgets('Onboarding renders after splash', (WidgetTester tester) async {
    await tester.pumpWidget(const MyAssistantApp());
    await tester.pump(const Duration(seconds: 3));
    await tester.pump(const Duration(milliseconds: 500));
    expect(find.text('Try Demo Mode'), findsOneWidget);
    expect(find.text('Sign in / Create Account'), findsOneWidget);
  });
}