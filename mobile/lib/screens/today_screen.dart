import 'package:flutter/material.dart';
import 'package:intl/intl.dart';
import 'package:provider/provider.dart';

import '../models/models.dart';
import '../providers/auth_provider.dart';
import '../providers/productivity_providers.dart';
import '../services/tts_service.dart';

class TodayScreen extends StatefulWidget {
  const TodayScreen({super.key});

  @override
  State<TodayScreen> createState() => _TodayScreenState();
}

class _TodayScreenState extends State<TodayScreen> {
  bool _loading = true;

  @override
  void initState() {
    super.initState();
    WidgetsBinding.instance.addPostFrameCallback((_) => _loadData());
  }

  Future<void> _loadData() async {
    final auth = context.read<AuthProvider>();
    final state = context.read<AppointmentsState>();
    if (auth.demoMode) {
      state.setDemo();
    } else {
      await state.load();
    }
    if (mounted) setState(() => _loading = false);
  }

  List<Appointment> _getTodayEvents(AppointmentsState state) {
    final now = DateTime.now();
    return state.appointments.where((a) {
      return a.startDateTime.year == now.year &&
          a.startDateTime.month == now.month &&
          a.startDateTime.day == now.day;
    }).toList()
      ..sort((a, b) => a.startDateTime.compareTo(b.startDateTime));
  }

  Future<void> _readAloud(List<Appointment> events) async {
    if (events.isEmpty) {
      await TtsService.instance.speak('You have no events scheduled for today.');
      return;
    }
    final timeFmt = DateFormat('h:mm a');
    var msg = 'Today you have ${events.length} event${events.length > 1 ? 's' : ''}. ';
    for (var i = 0; i < events.length; i++) {
      final e = events[i];
      final timeStr = timeFmt.format(e.startDateTime);
      final loc = e.location.isNotEmpty ? ' at ${e.location}' : '';
      msg += 'Number ${i + 1}: ${e.title}, scheduled for $timeStr$loc. ';
    }
    await TtsService.instance.speak(msg);
  }

  @override
  Widget build(BuildContext context) {
    final state = context.watch<AppointmentsState>();
    final today = _getTodayEvents(state);
    final timeFmt = DateFormat('h:mm a');
    final dateFmt = DateFormat('EEEE, d MMMM');

    return Scaffold(
      appBar: AppBar(
        title: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            const Text("Today's Events", style: TextStyle(fontSize: 18)),
            Text(
              dateFmt.format(DateTime.now()),
              style: const TextStyle(fontSize: 13, fontWeight: FontWeight.w400),
            ),
          ],
        ),
        actions: [
          if (today.isNotEmpty)
            IconButton(
              icon: const Icon(Icons.volume_up),
              tooltip: 'Read aloud',
              onPressed: () => _readAloud(today),
            ),
          IconButton(
            icon: const Icon(Icons.refresh),
            onPressed: () => _loadData(),
          ),
        ],
      ),
      body: _loading
          ? const Center(child: CircularProgressIndicator())
          : today.isEmpty
              ? Center(
                  child: Column(
                    mainAxisSize: MainAxisSize.min,
                    children: [
                      Icon(Icons.event_available, size: 64, color: Colors.grey.shade300),
                      const SizedBox(height: 16),
                      Text('No events today', style: TextStyle(fontSize: 18, color: Colors.grey.shade600)),
                      const SizedBox(height: 8),
                      Text(
                        'Say "add event" or "schedule meeting" to create one',
                        style: TextStyle(fontSize: 13, color: Colors.grey.shade400),
                        textAlign: TextAlign.center,
                      ),
                    ],
                  ),
                )
              : RefreshIndicator(
                  onRefresh: _loadData,
                  child: ListView.builder(
                    padding: const EdgeInsets.symmetric(vertical: 8),
                    itemCount: today.length,
                    itemBuilder: (_, i) {
                      final a = today[i];
                      final timeStr = timeFmt.format(a.startDateTime);
                      final now = DateTime.now();
                      final diff = a.startDateTime.difference(now);
                      final isPast = diff.isNegative;
                      final isSoon = !isPast && diff.inMinutes < 60 && diff.inMinutes >= 0;

                      return Card(
                        margin: const EdgeInsets.symmetric(horizontal: 16, vertical: 4),
                        child: ListTile(
                          leading: Container(
                            width: 4,
                            height: 48,
                            decoration: BoxDecoration(
                              color: isPast
                                  ? Colors.grey
                                  : isSoon
                                      ? Colors.orange
                                      : Colors.deepPurple,
                              borderRadius: BorderRadius.circular(2),
                            ),
                          ),
                          title: Text(
                            a.title,
                            style: TextStyle(
                              fontSize: 16,
                              fontWeight: FontWeight.w600,
                              color: isPast ? Colors.grey : null,
                            ),
                          ),
                          subtitle: Row(
                            children: [
                              Icon(Icons.access_time, size: 14, color: Colors.grey.shade500),
                              const SizedBox(width: 4),
                              Text(timeStr, style: TextStyle(fontSize: 13, color: Colors.grey.shade600)),
                              if (a.location.isNotEmpty) ...[
                                const SizedBox(width: 12),
                                Icon(Icons.location_on, size: 14, color: Colors.grey.shade500),
                                const SizedBox(width: 4),
                                Expanded(
                                  child: Text(
                                    a.location,
                                    style: TextStyle(fontSize: 13, color: Colors.grey.shade600),
                                    overflow: TextOverflow.ellipsis,
                                  ),
                                ),
                              ] else
                                const Spacer(),
                              if (isPast)
                                Text('Past', style: TextStyle(fontSize: 11, color: Colors.grey.shade400))
                              else if (isSoon)
                                Container(
                                  padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 2),
                                  decoration: BoxDecoration(
                                    color: Colors.orange.shade50,
                                    borderRadius: BorderRadius.circular(8),
                                  ),
                                  child: Text('Soon', style: TextStyle(fontSize: 11, color: Colors.orange.shade700)),
                                ),
                            ],
                          ),
                        ),
                      );
                    },
                  ),
                ),
      floatingActionButton: today.isNotEmpty
          ? FloatingActionButton.extended(
              onPressed: () => _readAloud(today),
              icon: const Icon(Icons.volume_up),
              label: const Text('Read aloud'),
            )
          : null,
    );
  }
}
