import 'package:flutter/material.dart';
import 'package:intl/intl.dart';

import '../models/models.dart';

class ConfirmationDialog extends StatefulWidget {
  final String initialType;
  final String initialTitle;
  final String initialContent;
  final String? initialPriority;
  final DateTime? initialDateTime;
  final String? initialLocation;

  const ConfirmationDialog({
    super.key,
    required this.initialType,
    required this.initialTitle,
    this.initialContent = '',
    this.initialPriority,
    this.initialDateTime,
    this.initialLocation,
  });

  @override
  State<ConfirmationDialog> createState() => _ConfirmationDialogState();
}

class _ConfirmationDialogState extends State<ConfirmationDialog> {
  late String _type;
  late TextEditingController _titleCtrl;
  late TextEditingController _contentCtrl;
  late TextEditingController _locationCtrl;
  late String _priority;
  late DateTime _selectedDate;
  late TimeOfDay _selectedTime;
  bool _hasTime = false;

  @override
  void initState() {
    super.initState();
    _type = widget.initialType;
    _titleCtrl = TextEditingController(text: widget.initialTitle);
    _contentCtrl = TextEditingController(text: widget.initialContent);
    _locationCtrl = TextEditingController(text: widget.initialLocation ?? '');
    _priority = widget.initialPriority ?? 'Medium';
    final dt = widget.initialDateTime ?? DateTime.now().add(const Duration(hours: 1));
    _selectedDate = DateTime(dt.year, dt.month, dt.day);
    _selectedTime = TimeOfDay(hour: dt.hour, minute: dt.minute);
    _hasTime = widget.initialDateTime != null;
  }

  @override
  void dispose() {
    _titleCtrl.dispose();
    _contentCtrl.dispose();
    _locationCtrl.dispose();
    super.dispose();
  }

  void _save() {
    final title = _titleCtrl.text.trim();
    if (title.isEmpty) return;

    final dt = DateTime(
      _selectedDate.year, _selectedDate.month, _selectedDate.day,
      _hasTime ? _selectedTime.hour : 0,
      _hasTime ? _selectedTime.minute : 0,
    );

    Navigator.of(context).pop(ConfirmationResult(
      type: _type,
      title: title,
      content: _contentCtrl.text.trim(),
      priority: _type == 'task' ? _priority : null,
      dateTime: _hasTime || _type == 'appointment' ? dt : null,
      location: _type == 'appointment' ? _locationCtrl.text.trim() : null,
    ));
  }

  @override
  Widget build(BuildContext context) {
    final bottomInset = MediaQuery.of(context).viewInsets.bottom;

    return Padding(
      padding: EdgeInsets.fromLTRB(20, 20, 20, bottomInset + 20),
      child: SingleChildScrollView(
        child: Column(
          mainAxisSize: MainAxisSize.min,
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            Text(
              'Confirm & Save',
              style: Theme.of(context).textTheme.titleLarge?.copyWith(fontWeight: FontWeight.bold),
            ),
            const SizedBox(height: 16),

            // ── Entity type selector ──────────────────────────
            SegmentedButton<String>(
              segments: const [
                ButtonSegment(value: 'note', label: Text('Note'), icon: Icon(Icons.note_alt_outlined, size: 18)),
                ButtonSegment(value: 'task', label: Text('Task'), icon: Icon(Icons.task_alt, size: 18)),
                ButtonSegment(value: 'appointment', label: Text('Event'), icon: Icon(Icons.event_outlined, size: 18)),
              ],
              selected: {_type},
              onSelectionChanged: (sel) => setState(() => _type = sel.first),
            ),
            const SizedBox(height: 16),

            // ── Title ─────────────────────────────────────────
            TextField(
              controller: _titleCtrl,
              autofocus: true,
              decoration: const InputDecoration(
                labelText: 'Title',
                border: OutlineInputBorder(),
                isDense: true,
              ),
            ),
            const SizedBox(height: 12),

            // ── Content / Description ─────────────────────────
            TextField(
              controller: _contentCtrl,
              maxLines: 3,
              decoration: InputDecoration(
                labelText: _type == 'note' ? 'Content' : 'Description',
                border: const OutlineInputBorder(),
                isDense: true,
              ),
            ),

            // ── Task-only: priority ────────────────────────────
            if (_type == 'task') ...[
              const SizedBox(height: 12),
              DropdownButtonFormField<String>(
                initialValue: _priority,
                decoration: const InputDecoration(
                  labelText: 'Priority',
                  border: OutlineInputBorder(),
                  isDense: true,
                ),
                items: const [
                  DropdownMenuItem(value: 'Low', child: Text('Low')),
                  DropdownMenuItem(value: 'Medium', child: Text('Medium')),
                  DropdownMenuItem(value: 'High', child: Text('High')),
                  DropdownMenuItem(value: 'Urgent', child: Text('Urgent')),
                ],
                onChanged: (v) => setState(() => _priority = v ?? 'Medium'),
              ),
            ],

            // ── Appointment-only: location ────────────────────
            if (_type == 'appointment') ...[
              const SizedBox(height: 12),
              TextField(
                controller: _locationCtrl,
                decoration: const InputDecoration(
                  labelText: 'Location (optional)',
                  border: OutlineInputBorder(),
                  isDense: true,
                ),
              ),
            ],

            const SizedBox(height: 16),

            // ── Date / Time row ───────────────────────────────
            Row(
              children: [
                Expanded(
                  child: OutlinedButton.icon(
                    onPressed: () async {
                      final picked = await showDatePicker(
                        context: context,
                        initialDate: _selectedDate,
                        firstDate: DateTime(2020),
                        lastDate: DateTime(2030),
                      );
                      if (picked != null) setState(() => _selectedDate = picked);
                    },
                    icon: const Icon(Icons.calendar_today, size: 16),
                    label: Text(DateFormat('dd MMM yyyy').format(_selectedDate)),
                  ),
                ),
                const SizedBox(width: 8),
                Expanded(
                  child: OutlinedButton.icon(
                    onPressed: () async {
                      final picked = await showTimePicker(
                        context: context,
                        initialTime: _selectedTime,
                      );
                      if (picked != null) {
                        setState(() {
                          _selectedTime = picked;
                          _hasTime = true;
                        });
                      }
                    },
                    icon: const Icon(Icons.access_time, size: 16),
                    label: Text(_hasTime ? _selectedTime.format(context) : 'Set time'),
                  ),
                ),
              ],
            ),

            const SizedBox(height: 20),

            // ── Action buttons ────────────────────────────────
            Row(
              children: [
                Expanded(
                  child: OutlinedButton(
                    onPressed: () => Navigator.of(context).pop(),
                    child: const Text('Cancel'),
                  ),
                ),
                const SizedBox(width: 12),
                Expanded(
                  child: FilledButton.icon(
                    onPressed: _save,
                    icon: const Icon(Icons.check, size: 18),
                    label: const Text('Save'),
                  ),
                ),
              ],
            ),
          ],
        ),
      ),
    );
  }
}