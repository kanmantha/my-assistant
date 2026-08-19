import 'package:flutter/material.dart';
import 'package:intl/intl.dart';
import 'package:provider/provider.dart';

import '../models/models.dart';
import '../providers/auth_provider.dart';
import '../providers/productivity_providers.dart';
import '../theme.dart';

class AppointmentsScreen extends StatefulWidget {
  const AppointmentsScreen({super.key});

  @override
  State<AppointmentsScreen> createState() => _AppointmentsScreenState();
}

class _AppointmentsScreenState extends State<AppointmentsScreen> {
  @override
  void initState() {
    super.initState();
    WidgetsBinding.instance.addPostFrameCallback((_) {
      final auth = context.read<AuthProvider>();
      if (auth.demoMode) {
        context.read<AppointmentsState>().setDemo();
      } else {
        context.read<AppointmentsState>().load();
      }
    });
  }

  void _showAddSheet() {
    final titleCtrl = TextEditingController();
    final descCtrl = TextEditingController();
    final locationCtrl = TextEditingController();
    DateTime selectedDate = DateTime.now();
    TimeOfDay selectedTime = TimeOfDay.now();

    showModalBottomSheet(
      context: context,
      isScrollControlled: true,
      shape: const RoundedRectangleBorder(
        borderRadius: BorderRadius.vertical(top: Radius.circular(20)),
      ),
      builder: (ctx) => StatefulBuilder(
        builder: (ctx, setSheetState) => Padding(
          padding: EdgeInsets.fromLTRB(20, 20, 20, MediaQuery.of(ctx).viewInsets.bottom + 20),
          child: Column(
            mainAxisSize: MainAxisSize.min,
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              const Text('New Appointment', style: TextStyle(fontSize: 18, fontWeight: FontWeight.bold)),
              const SizedBox(height: 12),
              TextField(
                controller: titleCtrl,
                decoration: const InputDecoration(hintText: 'Title', isDense: true),
                autofocus: true,
              ),
              const SizedBox(height: 8),
              TextField(
                controller: descCtrl,
                decoration: const InputDecoration(hintText: 'Description (optional)', isDense: true),
              ),
              const SizedBox(height: 8),
              TextField(
                controller: locationCtrl,
                decoration: const InputDecoration(hintText: 'Location (optional)', isDense: true),
              ),
              const SizedBox(height: 12),
              Row(
                children: [
                  Expanded(
                    child: OutlinedButton.icon(
                      onPressed: () async {
                        final picked = await showDatePicker(
                          context: ctx, initialDate: selectedDate,
                          firstDate: DateTime(2020), lastDate: DateTime(2030),
                        );
                        if (picked != null) setSheetState(() => selectedDate = picked);
                      },
                      icon: const Icon(Icons.calendar_today, size: 16),
                      label: Text(DateFormat('dd MMM yyyy').format(selectedDate)),
                    ),
                  ),
                  const SizedBox(width: 8),
                  Expanded(
                    child: OutlinedButton.icon(
                      onPressed: () async {
                        final picked = await showTimePicker(
                          context: ctx, initialTime: selectedTime,
                        );
                        if (picked != null) setSheetState(() => selectedTime = picked);
                      },
                      icon: const Icon(Icons.access_time, size: 16),
                      label: Text(selectedTime.format(ctx)),
                    ),
                  ),
                ],
              ),
              const SizedBox(height: 16),
              SizedBox(
                width: double.infinity,
                child: FilledButton(
                  onPressed: () async {
                    if (titleCtrl.text.trim().isEmpty) return;
                    final dt = DateTime(
                      selectedDate.year, selectedDate.month, selectedDate.day,
                      selectedTime.hour, selectedTime.minute,
                    );
                    final apptState = context.read<AppointmentsState>();
                    try {
                      await apptState.add(
                        title: titleCtrl.text.trim(),
                        description: descCtrl.text.trim(),
                        startDateTime: dt.toIso8601String(),
                        location: locationCtrl.text.trim().isEmpty ? null : locationCtrl.text.trim(),
                      );
                      if (ctx.mounted) Navigator.pop(ctx);
                    } catch (e) {
                      if (ctx.mounted) {
                        ScaffoldMessenger.of(ctx).showSnackBar(
                          SnackBar(content: Text('Failed to save appointment: $e'), backgroundColor: Colors.red),
                        );
                      }
                    }
                  },
                  child: const Text('Save Appointment'),
                ),
              ),
            ],
          ),
        ),
      ),
    );
  }

  @override
  Widget build(BuildContext context) {
    final state = context.watch<AppointmentsState>();
    final appointments = state.appointments;

    return Scaffold(
      appBar: AppBar(title: const Text('Appointments')),
      body: state.busy && appointments.isEmpty
          ? const Center(child: CircularProgressIndicator())
          : appointments.isEmpty
              ? Center(
                  child: Column(
                    mainAxisAlignment: MainAxisAlignment.center,
                    children: [
                      Icon(Icons.event_available, size: 64, color: AppTheme.primary.withValues(alpha: 0.3)),
                      const SizedBox(height: 16),
                      const Text('No appointments yet', style: TextStyle(fontSize: 16)),
                      const SizedBox(height: 8),
                      Text(
                        'Tap + to add or say "Schedule meeting at 3 PM"',
                        style: TextStyle(color: Theme.of(context).colorScheme.onSurfaceVariant),
                      ),
                    ],
                  ),
                )
              : RefreshIndicator(
                  onRefresh: () => state.load(),
                  child: ListView.builder(
                    padding: const EdgeInsets.all(12),
                    itemCount: appointments.length,
                    itemBuilder: (context, i) => _AppointmentTile(
                      appointment: appointments[i],
                      onDelete: () => state.remove(appointments[i].id),
                    ),
                  ),
                ),
      floatingActionButton: FloatingActionButton(
        onPressed: _showAddSheet,
        child: const Icon(Icons.add),
      ),
    );
  }
}

class _AppointmentTile extends StatelessWidget {
  final Appointment appointment;
  final VoidCallback onDelete;

  const _AppointmentTile({required this.appointment, required this.onDelete});

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final dt = appointment.startDateTime;
    final isToday = DateTime.now().year == dt.year &&
        DateTime.now().month == dt.month &&
        DateTime.now().day == dt.day;

    return Card(
      margin: const EdgeInsets.only(bottom: 8),
      shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(12)),
      child: ListTile(
        leading: Container(
          width: 50,
          height: 50,
          decoration: BoxDecoration(
            color: isToday ? AppTheme.primary.withValues(alpha: 0.1) : theme.colorScheme.surface,
            borderRadius: BorderRadius.circular(10),
          ),
          child: Column(
            mainAxisAlignment: MainAxisAlignment.center,
            children: [
              Text(
                DateFormat('dd').format(dt),
                style: TextStyle(
                  fontSize: 16, fontWeight: FontWeight.bold,
                  color: isToday ? AppTheme.primary : theme.colorScheme.onSurface,
                ),
              ),
              Text(
                DateFormat('MMM').format(dt),
                style: TextStyle(
                  fontSize: 10,
                  color: isToday ? AppTheme.primary : theme.colorScheme.onSurfaceVariant,
                ),
              ),
            ],
          ),
        ),
        title: Text(
          appointment.title,
          style: const TextStyle(fontWeight: FontWeight.w600),
        ),
        subtitle: Text(
          '${DateFormat('hh:mm a').format(dt)}${appointment.location.isNotEmpty ? " • ${appointment.location}" : ""}',
          style: TextStyle(
            fontSize: 12,
            color: theme.colorScheme.onSurfaceVariant,
          ),
        ),
        trailing: IconButton(
          icon: const Icon(Icons.delete_outline, size: 20),
          onPressed: onDelete,
        ),
      ),
    );
  }
}
