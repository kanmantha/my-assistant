import type { EnumValue } from "../models";

export type UiLang = "en" | "hi" | "te";

export function whichUiLang(raw: string): UiLang {
  switch (raw.toLowerCase()) {
    case "hi":
      return "hi";
    case "te":
      return "te";
    default:
      return "en";
  }
}

export function fmt(template: string, vars: Record<string, string | number>): string {
  return Object.entries(vars).reduce((acc, [k, v]) => acc.split(`{${k}}`).join(String(v)), template);
}

const DICT: Record<string, Record<UiLang, string>> = {
  home: { en: "Home", hi: "होम", te: "హోమ్" },
  tasks: { en: "Tasks", hi: "कार्य", te: "టాస్క్‌లు" },
  calendar: { en: "Calendar", hi: "कैलेंडर", te: "క్యాలెండర్" },
  notes: { en: "Notes", hi: "नोट्स", te: "నోట్స్" },
  reminders: { en: "Reminders", hi: "रिमाइंडर", te: "రిమైండర్‌లు" },
  settings: { en: "Settings", hi: "सेटिंग्स", te: "సెట్టింగ్స్" },
  search: { en: "Search", hi: "खोजें", te: "వెతకండి" },
  assistant: { en: "Assistant", hi: "असिस्टेंट", te: "అసిస్టెంట్" },
  history: { en: "Conversations", hi: "बातचीत", te: "సంభాషణలు" },
  addNote: { en: "Add Note", hi: "नोट जोड़ें", te: "నోట్ జోడించండి" },
  addTask: { en: "Add Task", hi: "कार्य जोड़ें", te: "టాస్క్ జోడించండి" },
  addReminder: { en: "Reminder", hi: "रिमाइंडर", te: "రిమైండర్" },
  addAppointment: { en: "Appointment", hi: "अपॉइंटमेंट", te: "అపాయింట్‌మెంట్" },
  today: { en: "Today", hi: "आज", te: "ఈరోజు" },
  logout: { en: "Log out", hi: "लॉग आउट", te: "లాగ్ అవుట్" },
  save: { en: "Save", hi: "सहेजें", te: "సేవ్ చేయండి" },
  cancel: { en: "Cancel", hi: "रद्द करें", te: "రద్దు చేయండి" },
  delete: { en: "Delete", hi: "हटाएं", te: "తొలగించండి" },
  say: { en: "Say \"Assistant\" to start", hi: "शुरू करने के लिए \"असिस्टेंट\" बोलें", te: "ప్రారంభించడానికి \"అసిస్టెంట్\" చెప్పండి" },
  listening: { en: "Listening...", hi: "सुन रहा हूँ...", te: "వింటున్నాను..." },
  processing: { en: "Thinking...", hi: "सोच रहा हूँ...", te: "ఆలోచిస్తున్నాను..." },
  speaking: { en: "Speaking...", hi: "बोल रहा हूँ...", te: "మాట్లాడుతున్నాను..." },
  idle: { en: "How can I help you?", hi: "मैं आपकी कैसे मदद कर सकता हूँ?", te: "నేను మీకు ఎలా సహాయం చేయగలను?" },
  ready: { en: "Ready", hi: "तैयार", te: "సిద్ధంగా" },
  wakeActive: { en: "Wake word active", hi: "वेक वर्ड सक्रिय", te: "వేక్ వర్డ్ యాక్టివ్" },
  searching: { en: "Searching...", hi: "खोज रहा हूँ...", te: "వెతుకుతున్నాను..." },

  // ---------- Common ----------
  all: { en: "All", hi: "सभी", te: "అన్నీ" },
  yes: { en: "Yes", hi: "हाँ", te: "అవును" },
  no: { en: "No", hi: "नहीं", te: "కాదు" },
  at: { en: "at", hi: "को", te: "వద్ద" },
  voiceBadge: { en: "Voice", hi: "आवाज़", te: "వాయిస్" },
  voiceMuted: { en: "Assistant voice muted", hi: "सहायक की आवाज़ बंद है", te: "అసిస్టెంట్ వాయిస్ మ్యూట్ చేయబడింది" },
  typePlaceholder: { en: "Type your command...", hi: "यहाँ टाइप करें...", te: "ఇక్కడ టైప్ చేయండి..." },
  loading: { en: "Loading...", hi: "लोड हो रहा है...", te: "లోడ్ అవుతోంది..." },
  appointments: { en: "Appointments", hi: "अपॉइंटमेंट", te: "అపాయింట్‌మెంట్లు" },
  appHome: { en: "My Assistant", hi: "मेरा असिस्टेंट", te: "నా అసిస్టెంట్" },
  mainNav: { en: "Main navigation", hi: "मुख्य नेविगेशन", te: "ప్రధాన నావిగేషన్" },
  mobileNav: { en: "Mobile navigation", hi: "मोबाइल नेविगेशन", te: "మొబైల్ నావిగేషన్" },
  openAssistant: { en: "Open assistant", hi: "असिस्टेंट खोलें", te: "అసిస్టెంట్ తెరవండి" },
  userFallback: { en: "User", hi: "उपयोगकर्ता", te: "వినియోగదారుడు" },

  // ---------- Auth ----------
  authLoginTitle: { en: "Welcome back", hi: "वापसी पर स्वागत है", te: "తిరిగి స్వాగతం" },
  authSignUpTitle: { en: "Create your account", hi: "अपना खाता बनाएं", te: "మీ ఖాతాను సృష్టించండి" },
  authForgotTitle: { en: "Reset your password", hi: "अपना पासवर्ड रीसेट करें", te: "మీ పాస్వర్డ్ రీసెట్ చేయండి" },
  authResetTitle: { en: "Set a new password", hi: "नया पासवर्ड सेट करें", te: "కొత్త పాస్వర్డ్ సెట్ చేయండి" },
  authLoginSub: { en: "Sign in to your personal assistant", hi: "अपने निजी सहायक में साइन इन करें", te: "మీ వ్యక్తిగత అసిస్టెంట్‌లో సైన్ ఇన్ చేయండి" },
  authSignUpSub: { en: "Your voice is all you need", hi: "आपकी आवाज़ ही काफी है", te: "మీ స్వరమే సరిపోతుంది" },
  authForgotSub: { en: "Enter your email and we'll send a reset link", hi: "अपना ईमेल दर्ज करें, हम रीसेट लिंक भेज देंगे", te: "మీ ఇమెయిల్ నమోదు చేయండి, మేము రీసెట్ లింక్ పంపుతాము" },
  authResetSub: { en: "Enter a strong new password", hi: "एक मजबूत नया पासवर्ड दर्ज करें", te: "బలమైన కొత్త పాస్వర్డ్ను నమోదు చేయండి" },
  firstName: { en: "First name", hi: "पहला नाम", te: "మొదటి పేరు" },
  lastName: { en: "Last name", hi: "अंतिम नाम", te: "చివరి పేరు" },
  email: { en: "Email", hi: "ईमेल", te: "ఇమెయిల్" },
  passwordField: { en: "Password", hi: "पासवर्ड", te: "పాస్వర్డ్" },
  newPasswordField: { en: "New password", hi: "नया पासवर्ड", te: "కొత్త పాస్వర్డ్" },
  confirmNewPassword: { en: "Confirm new password", hi: "नया पासवर्ड फिर से दर्ज करें", te: "కొత్త పాస్వర్డ్ను ధృవీకరించండి" },
  confirmPassword: { en: "Confirm password", hi: "पासवर्ड फिर से दर्ज करें", te: "పాస్వర్డ్ను ధృవీకరించండి" },
  passwordMin: { en: "Password (min 8 chars)", hi: "पासवर्ड (कम से कम 8 अक्षर)", te: "పాస్వర్డ్ (కనీసం 8 అక్షరాలు)" },
  resetToken: { en: "Reset token", hi: "रीसेट टोकन", te: "రీసెట్ టోకెన్" },
  pwMinError: { en: "Password must be at least 8 characters.", hi: "पासवर्ड कम से कम 8 अक्षरों का होना चाहिए।", te: "పాస్వర్డ్ కనీసం 8 అక్షరాలు ఉండాలి." },
  pwMatchError: { en: "Passwords do not match.", hi: "पासवर्ड मेल नहीं खाते।", te: "పాస్వర్డ్లు సరిపోలడం లేదు." },
  forgotSent: { en: "If that email exists, a reset link has been sent.", hi: "यदि वह ईमेल मौजूद है, तो रीसेट लिंक भेज दिया गया है।", te: "ఆ ఇమెయిల్ ఉంటే, రీసెట్ లింక్ పంపబడింది." },
  resetSuccess: { en: "Password reset successful. You can now log in.", hi: "पासवर्ड रीसेट सफल रहा। अब आप लॉग इन कर सकते हैं।", te: "పాస్వర్డ్ రీసెట్ విజయవంతం. ఇప్పుడు మీరు లాగిన్ చేయవచ్చు." },
  genericError: { en: "Something went wrong.", hi: "कुछ गलत हो गया।", te: "ఏదో తప్పు జరిగింది." },
  signIn: { en: "Sign in", hi: "साइन इन करें", te: "సైన్ ఇన్" },
  createAccount: { en: "Create account", hi: "खाता बनाएं", te: "ఖాతాను సృష్టించండి" },
  sendResetLink: { en: "Send reset link", hi: "रीसेट लिंक भेजें", te: "రీసెట్ లింక్ పంపండి" },
  updatePassword: { en: "Update password", hi: "पासवर्ड अपडेट करें", te: "పాస్వర్డ్ను అప్డేట్ చేయండి" },
  noAccount: { en: "Don't have an account?", hi: "खाता नहीं है?", te: "ఖాతా లేదా?" },
  signUp: { en: "Sign up", hi: "साइन अप करें", te: "సైన్ అప్ చేయండి" },
  forgotPassword: { en: "Forgot password?", hi: "पासवर्ड भूल गए?", te: "పాస్వర్డ్ మరచిపోయారా?" },
  haveAccount: { en: "Already have an account?", hi: "पहले से खाता है?", te: "ఇప్పటికే ఖాతా ఉందా?" },
  backToLogin: { en: "Back to login", hi: "लॉग इन पर वापस जाएं", te: "లాగిన్‌కు తిరిగి వెళ్ళండి" },

  // ---------- Tasks ----------
  filterAria: { en: "Filter tasks", hi: "कार्य फ़िल्टर करें", te: "టాస్క్‌లను ఫిల్టర్ చేయండి" },
  newTask: { en: "New Task", hi: "नया कार्य", te: "కొత్త టాస్క్" },
  noTasksYet: { en: "No tasks yet", hi: "अभी कोई कार्य नहीं", te: "ఇంకా టాస్క్‌లు లేవు" },
  noTasksInView: { en: "No tasks in this view", hi: "इस दृश्य में कोई कार्य नहीं", te: "ఈ వీకులో టాస్క్‌లు లేవు" },
  taskHint: { en: "Say 'Assistant, add a task to complete the project report'", hi: "कहें 'असिस्टेंट, प्रोजेक्ट रिपोर्ट पूरी करने का कार्य जोड़ें'", te: "'అసిస్టెంట్, ప్రాజెక్టు రిపోర్ట్ పూర్తి చేయడానికి టాస్క్ జోడించండి' అని చెప్పండి" },
  markIncomplete: { en: "Mark incomplete", hi: "अधूरा चिह्नित करें", te: "అసంపూర్తిగా గుర్తించండి" },
  markCompleted: { en: "Mark completed", hi: "पूर्ण चिह्नित करें", te: "పూర్తయినట్లు గుర్తించండి" },
  editTask: { en: "Edit task", hi: "कार्य संपादित करें", te: "టాస్క్ సవరించండి" },
  deleteTask: { en: "Delete task", hi: "कार्य हटाएं", te: "టాస్క్ తొలగించండి" },
  deleteTaskTitle: { en: "Delete task?", hi: "कार्य हटाएं?", te: "టాస్క్ తొలగించాలా?" },

  // ---------- Notes ----------
  newNote: { en: "New Note", hi: "नया नोट", te: "కొత్త నోట్" },
  noNotesYet: { en: "No notes yet", hi: "अभी कोई नोट नहीं", te: "ఇంకా నోట్స్ లేవు" },
  noteHint: { en: "Say 'Assistant, take a note about the quarterly review'", hi: "कहें 'असिस्टेंट, तिमाही समीक्षा के बारे में नोट लें'", te: "'అసిస్టెంట్, త్రైమాసిక సమీక్ష గురించి నోటు రాయండి' అని చెప్పండి" },
  pinNote: { en: "Pin note", hi: "नोट पिन करें", te: "నోట్ పిన్ చేయండి" },
  unpinNote: { en: "Unpin note", hi: "नोट अनपिन करें", te: "నోట్ అన్‌పిన్ చేయండి" },
  editNote: { en: "Edit note", hi: "नोट संपादित करें", te: "నోట్ సవరించండి" },
  deleteNote: { en: "Delete note", hi: "नोट हटाएं", te: "నోట్ తొలగించండి" },
  deleteNoteTitle: { en: "Delete note?", hi: "नोट हटाएं?", te: "నోట్ తొలగించాలా?" },

  // ---------- Reminders ----------
  newReminder: { en: "New Reminder", hi: "नया रिमाइंडर", te: "కొత్త రిమైండర్" },
  noRemindersYet: { en: "No reminders yet", hi: "अभी कोई रिमाइंडर नहीं", te: "ఇంకా రిమైండర్లు లేవు" },
  reminderHint: { en: "Say 'Assistant, remind me to call Ravi at 5 PM'", hi: "कहें 'असिस्टेंट, मुझे रवि को शाम 5 बजे कॉल करने की याद दिलाएं'", te: "'అసిస్టెంట్, నాకు సాయంత్రం 5 గంటలకు రవి కి కాల్ చేయమని గుర్తు చేయి' అని చెప్పండి" },
  editReminder: { en: "Edit reminder", hi: "रिमाइंडर संपादित करें", te: "రిమైండర్ సవరించండి" },
  deleteReminder: { en: "Delete reminder", hi: "रिमाइंडर हटाएं", te: "రిమైండర్ తొలగించండి" },
  deleteReminderTitle: { en: "Delete reminder?", hi: "रिमाइंडर हटाएं?", te: "రిమైండర్ తొలగించాలా?" },

  // ---------- Calendar ----------
  newEvent: { en: "New", hi: "नया", te: "కొత్త" },
  prevMonth: { en: "Previous month", hi: "पिछला महीना", te: "మునుపటి నెల" },
  nextMonth: { en: "Next month", hi: "अगला महीना", te: "తదుపరి నెల" },
  noAppointmentsThisDay: { en: "No appointments this day", hi: "इस दिन कोई अपॉइंटमेंट नहीं", te: "ఈ రోజు అపాయింట్‌మెంట్లు లేవు" },
  moreCount: { en: "+{count} more", hi: "+{count} और", te: "+{count} మరిన్ని" },
  editAppointment: { en: "Edit appointment", hi: "अपॉइंटमेंट संपादित करें", te: "అపాయింట్‌మెంట్ సవరించండి" },
  deleteAppointment: { en: "Delete appointment", hi: "अपॉइंटमेंट हटाएं", te: "అపాయింట్‌మెంట్ తొలగించండి" },
  deleteAppointmentTitle: { en: "Delete appointment?", hi: "अपॉइंटमेंट हटाएं?", te: "అపాయింట్‌మెంట్ తొలగించాలా?" },

  // ---------- Confirmation dialog (shared) ----------
  deleteConfirm: {
    en: 'Are you sure you want to delete "{title}"?',
    hi: 'क्या आप वाकई "{title}" को हटाना चाहते हैं?',
    te: 'మీరు నిజంగా "{title}" ను తొలగించాలనుకుంటున్నారా?'
  },
  deleteConfirmPermanent: {
    en: 'Delete "{title}" permanently?',
    hi: '"{title}" को स्थायी रूप से हटाएं?',
    te: '"{title}" ను శాశ్వతంగా తొలగించాలా?'
  },

  // ---------- Settings ----------
  appearanceSection: { en: "Appearance", hi: "दिखावट", te: "రూపము" },
  appearanceSub: { en: "Language and theme", hi: "भाषा और थीम", te: "భాష మరియు థీమ్" },
  languageField: { en: "Language", hi: "भाषा", te: "భాష" },
  themeField: { en: "Theme", hi: "थीम", te: "థీమ్" },
  themeLight: { en: "Light", hi: "लाइट", te: "లైట్" },
  themeDark: { en: "Dark", hi: "डार्क", te: "డార్క్" },
  themeSystem: { en: "System", hi: "सिस्टम", te: "సిస్టమ్" },
  reducedMotion: { en: "Reduced motion", hi: "कम गति", te: "తగ్గిన మోషన్" },
  highContrast: { en: "High contrast", hi: "उच्च कंट्रास्ट", te: "అధిక కంట్రాస్ట్" },
  fontScale: { en: "Font scale", hi: "फ़ॉन्ट आकार", te: "ఫాంట్ స్కేల్" },
  assistantVoiceSection: { en: "Assistant & Voice", hi: "असिस्टेंट और आवाज़", te: "అసిస్టెంట్ & వాయిస్" },
  assistantVoiceSub: { en: "Wake word, speech and reminders", hi: "वेक वर्ड, बोल और रिमाइंडर", te: "వేక్ వర్డ్, ప్రసంగం మరియు రిమైండర్స్" },
  enableWakeWord: { en: "Enable wake word", hi: "वेक वर्ड सक्षम करें", te: "వేక్ వర్డ్ ప్రారంభించండి" },
  wakeWordHint: {
    en: "Wake word matching runs entirely on your device.",
    hi: "वेक वर्ड मिलान पूरी तरह से आपके डिवाइस पर चलता है।",
    te: "వేక్ వర్డ్ సరిపోలిక పూర్తిగా మీ పరికరంలో నడుస్తుంది."
  },
  wakeWordField: { en: "Wake word", hi: "वेक वर्ड", te: "వేక్ వర్డ్" },
  autoDetect: { en: "Auto-detect language", hi: "भाषा स्वतः पहचानें", te: "భాషను స్వయంగా గుర్తించు" },
  muteVoice: { en: "Mute assistant voice", hi: "असिस्टेंट की आवाज़ म्यूट करें", te: "అసిస్టెంట్ వాయిస్ మ్యూట్ చేయండి" },
  speechSpeed: { en: "Speech speed", hi: "बोलने की गति", te: "ప్రస్తావన వేగం" },
  voiceVolume: { en: "Voice volume", hi: "आवाज़ का स्तर", te: "వాయిస్ వాల్యూమ్" },
  notificationsSection: { en: "Notifications", hi: "सूचनाएं", te: "నోటిఫికేషన్లు" },
  notificationsSub: { en: "Reminders and alerts", hi: "रिमाइंडर और अलर्ट", te: "రిమైండర్లు మరియు అలర్ట్‌లు" },
  enableNotifications: { en: "Enable notifications", hi: "सूचनाएं सक्षम करें", te: "నోటిఫికేషన్లను ప్రారంభించండి" },
  defaultReminderLabel: {
    en: "Default reminder time (minutes before)",
    hi: "डिफ़ॉल्ट रिमाइंडर समय (कितने मिनट पहले)",
    te: "డిఫాల్ట్ రిమైండర్ సమయం (ఎన్ని నిమిషాలు ముందు)"
  },
  timezoneField: { en: "Timezone", hi: "समय क्षेत्र", te: "టైమ్‌జోన్" },
  confirmActions: { en: "Ask before performing actions", hi: "कार्य करने से पहले पूछें", te: "చర్యలు చేసే ముందు అడగండి" },
  confirmActionsHint: {
    en: "Assistant confirms destructive or state-changing actions.",
    hi: "असिस्टेंट विनाशकारी या स्थिति बदलने वाले कार्यों की पुष्टि करता है।",
    te: "అసిస్టెంట్ విధ్వంసకర లేదా స్థితిని మార్చే చర్యలను ధృవీకరిస్తుంది."
  },
  accountSection: { en: "Account", hi: "खाता", te: "ఖాతా" },
  saveProfile: { en: "Save profile", hi: "प्रोफ़ाइल सहेजें", te: "ప్రొఫైల్ సేవ్ చేయండి" },
  saved: { en: "Saved", hi: "सहेजा गया", te: "సేవ్ చేయబడింది" },

  // ---------- History ----------
  conversationHistory: { en: "Conversation History", hi: "बातचीत इतिहास", te: "సంభాషణ చరిత్ర" },
  clearAll: { en: "Clear all", hi: "सभी साफ़ करें", te: "అన్నీ తొలగించండి" },
  sortField: { en: "Sort", hi: "क्रमित करें", te: "క్రమం" },
  sortAria: { en: "Sort conversations", hi: "बातचीत क्रमित करें", te: "సంభాషణలను క్రమం చేయండి" },
  newestFirst: { en: "Newest first", hi: "नए पहले", te: "కొత్తవి ముందు" },
  oldestFirst: { en: "Oldest first", hi: "पुराने पहले", te: "పాతవి ముందు" },
  voiceOnly: { en: "Voice only", hi: "केवल आवाज़", te: "వాయిస్ మాత్రం" },
  noConversationsYet: { en: "No conversations yet", hi: "अभी कोई बातचीत नहीं", te: "ఇంకా సంభాషణలు లేవు" },
  historyHint: {
    en: "Your interactions with the assistant will appear here.",
    hi: "असिस्टेंट के साथ आपकी बातचीत यहाँ दिखाई देगी।",
    te: "అసిస్టెంట్‌తో మీ సంభాషణలు ఇక్కడ కనిపిస్తాయి."
  },
  youLabel: { en: "You", hi: "आप", te: "మీరు" },
  clearHistoryTitle: { en: "Clear conversation history?", hi: "बातचीत इतिहास साफ़ करें?", te: "సంభాషణ చరిత్రను తొలగించాలా?" },
  clearHistoryConfirm: {
    en: "This will permanently remove all stored conversations. This action cannot be undone.",
    hi: "इससे सभी संग्रहीत बातचीत स्थायी रूप से हट जाएंगी। इसे पूर्ववत नहीं किया जा सकता।",
    te: "ఇది నిల్వ ఉన్న అన్ని సంభాషణలను శాశ్వతంగా తొలగిస్తుంది. దీన్ని రద్దు చేయలేము."
  },

  // ---------- Search ----------
  searchPlaceholder: {
    en: "Try 'notes about project', tasks due next week...",
    hi: "'प्रोजेक्ट के नोट्स', अगले हफ़्ते के कार्य आज़माएं...",
    te: "'ప్రాజెక్ట్ గురించి నోట్స్', వచ్చే వారం టాస్క్‌లు ప్రయత్నించండి..."
  },
  searchScopeAria: { en: "Search scope", hi: "खोज क्षेत्र", te: "అన్వేషణ పరిధి" },
  searchFailed: { en: "Search failed", hi: "खोज विफल रही", te: "శోధన విఫలమైంది" },
  searchWorkspaceTitle: { en: "Search across your workspace", hi: "अपने वर्कस्पेस में खोजें", te: "మీ వర్క్‌స్పేస్‌లో వెతకండి" },
  searchWorkspaceHint: {
    en: "Find notes, tasks, appointments and reminders in one place.",
    hi: "नोट्स, कार्य, अपॉइंटमेंट और रिमाइंडर एक ही जगह खोजें।",
    te: "నోట్స్, టాస్క్‌లు, అపాయింట్‌మెంట్లు మరియు రిమైండర్లను ఒకే చోట కనుగొనండి."
  },
  searchResultsLabel: { en: "result(s)", hi: "परिणाम", te: "ఫలితాలు" },
  noResultsTitle: { en: "No results found", hi: "कोई परिणाम नहीं मिला", te: "ఫలితాలు లేవు" },
  noResultsHint: {
    en: "Try different keywords or broaden your search.",
    hi: "अलग कीवर्ड या व्यापक खोज आज़माएं।",
    te: "వేరే కీవర్డ్‌లు లేదా విస్తృత శోధన ప్రయత్నించండి."
  },

  // ---------- Forms ----------
  titleField: { en: "Title", hi: "शीर्षक", te: "శీర్షిక" },
  contentField: { en: "Content", hi: "विषय-सामग्री", te: "విషయం" },
  tagsField: { en: "Tags (comma separated)", hi: "टैग (कॉमा से अलग)", te: "ట్యాగ్లు (కామాతో వేరుచేయబడినవి)" },
  tagsPlaceholder: { en: "work, ideas, ...", hi: "काम, विचार, ...", te: "పని, ఆలోచనలు, ..." },
  description: { en: "Description", hi: "विवरण", te: "వివరణ" },
  dueDate: { en: "Due date", hi: "नियत तिथि", te: "చివరి తేదీ" },
  dueTime: { en: "Due time", hi: "नियत समय", te: "చివరి సమయం" },
  priority: { en: "Priority", hi: "प्राथमिकता", te: "ప్రాధాన్యత" },
  statusField: { en: "Status", hi: "स्थिति", te: "స్థితి" },
  category: { en: "Category", hi: "श्रेणी", te: "వర్గం" },
  messageField: { en: "Message", hi: "संदेश", te: "సందేశం" },
  dateField: { en: "Date", hi: "तिथि", te: "తేదీ" },
  timeField: { en: "Time", hi: "समय", te: "సమయం" },
  repeatField: { en: "Repeat", hi: "दोहराना", te: "పునరాబచేయండి" },
  remindBefore: { en: "Remind before (min)", hi: "पहले याद दिलाएं (मिनट)", te: "ముందు గుర్తు (నిమి)" },
  startTime: { en: "Start time", hi: "प्रारंभ समय", te: "ప్రారంభ సమయం" },
  endTime: { en: "End time", hi: "समाप्ति समय", te: "ముగింపు సమయం" },
  location: { en: "Location", hi: "स्थान", te: "ప్రదేశం" },
  participantsField: { en: "Participants (comma separated)", hi: "प्रतिभागी (कॉमा से अलग)", te: "పాల్గొనేవారు (కామాతో వేరుచేయిన)" },
  participantsPlaceholder: { en: "John, Ravi", hi: "जॉन, रवि", te: "జాన్, రవి" },
  failedSaveNote: { en: "Failed to save note", hi: "नोट सहेजने में विफल", te: "నోట్ సేవ్ చేయడం విఫలమైంది" },
  failedSaveTask: { en: "Failed to save task", hi: "कार्य सहेजने में विफल", te: "టాస్క్ సేవ్ చేయడం విఫలమైంది" },
  failedSaveReminder: { en: "Failed to save reminder", hi: "रिमाइंडर सहेजने में विफल", te: "రిమైండర్ సేవ్ చేయడం విఫలమైంది" },
  failedSaveAppointment: { en: "Failed to save appointment", hi: "अपॉइंटमेंट सहेजने में विफल", te: "అపాయింట్‌మెంట్ సేవ్ చేయడం విఫలమైంది" },

  // ---------- Mobile App ----------
  mobileAppTitle: { en: "Get the Mobile App", hi: "मोबाइल ऐप प्राप्त करें", te: "మొబైల్ యాప్ పొందండి" },
  mobileAppHint: { en: "Scan this QR code to open the app on your phone", hi: "अपने फ़ोन पर ऐप खोलने के लिए इस QR कोड को स्कैन करें", te: "మీ ఫోన్‌లో యాప్ తెరవడానికి ఈ QR కోడ్‌ను స్కాన్ చేయండి" }
};

export function t(key: string, lang: UiLang | string): string {
  const u = whichUiLang(lang);
  return DICT[key]?.[u] ?? DICT[key]?.en ?? key;
}

// ---------- Enum label maps (works for numeric string/enum values) ----------
const TASK_STATUS_LABELS: Record<string, Record<UiLang, string>> = {
  Pending: { en: "Pending", hi: "लंबित", te: "పెండింగ్" },
  InProgress: { en: "In Progress", hi: "चल रहा है", te: "పురోగతిలో" },
  Completed: { en: "Completed", hi: "पूर्ण", te: "పూర్తయింది" },
  Cancelled: { en: "Cancelled", hi: "रद्द", te: "రద్దు చేయబడింది" }
};

const PRIORITY_LABELS: Record<string, Record<UiLang, string>> = {
  Low: { en: "Low", hi: "कम", te: "తక్కువ" },
  Medium: { en: "Medium", hi: "मध्यम", te: "మధ్యస్థం" },
  High: { en: "High", hi: "उच्च", te: "అధికం" },
  Urgent: { en: "Urgent", hi: "अति आवश्यक", te: "అత్యవసరం" }
};

const RECURRENCE_LABELS: Record<string, Record<UiLang, string>> = {
  Once: { en: "Once", hi: "एक बार", te: "ఒకసారి" },
  Daily: { en: "Daily", hi: "दैनिक", te: "రోజువారీ" },
  Weekly: { en: "Weekly", hi: "साप्ताहिक", te: "వారానికి" },
  Monthly: { en: "Monthly", hi: "मासिक", te: "నెలవారీ" },
  Yearly: { en: "Yearly", hi: "वार्षिक", te: "సంవత్సరానికి" },
  Custom: { en: "Custom", hi: "कस्टम", te: "కస్టమ్" }
};

const APPT_STATUS_LABELS: Record<string, Record<UiLang, string>> = {
  Scheduled: { en: "Scheduled", hi: "निर्धारित", te: "షెడ్యూల్ చేయబడింది" },
  Completed: { en: "Completed", hi: "पूर्ण", te: "పూర్తయింది" },
  Cancelled: { en: "Cancelled", hi: "रद्द", te: "రద్దు" },
  Rescheduled: { en: "Rescheduled", hi: "पुनर्निर्धारित", te: "మార్చ బడింది" }
};

const LANG_NAMES: Record<string, string> = {
  en: "English",
  hi: "हिंदी",
  te: "తెలుగు",
  Auto: "Auto Detect"
};

const ENUM_NAMES: Record<string, string[]> = {
  Pending: ["Pending", "InProgress", "Completed", "Cancelled"],
  Priority: ["Low", "Medium", "High", "Urgent"],
  Recurrence: ["Once", "Daily", "Weekly", "Monthly", "Yearly", "Custom"],
  Appointment: ["Scheduled", "Completed", "Cancelled", "Rescheduled"]
};

function enumName(value: EnumValue): string | undefined {
  if (typeof value === "string") return value;
  const names = Object.values(ENUM_NAMES).flat();
  return names[value];
}

export function taskStatusLabel(value: EnumValue, lang: UiLang | string): string {
  return TASK_STATUS_LABELS[enumName(value) ?? ""]?.[whichUiLang(lang)] ?? String(value);
}

export function priorityLabel(value: EnumValue, lang: UiLang | string): string {
  return PRIORITY_LABELS[enumName(value) ?? ""]?.[whichUiLang(lang)] ?? String(value);
}

export function recurrenceLabel(value: EnumValue, lang: UiLang | string): string {
  return RECURRENCE_LABELS[enumName(value) ?? ""]?.[whichUiLang(lang)] ?? String(value);
}

export function appointmentStatusLabel(value: EnumValue, lang: UiLang | string): string {
  return APPT_STATUS_LABELS[enumName(value) ?? ""]?.[whichUiLang(lang)] ?? String(value);
}

export function languageName(lang: string): string {
  return LANG_NAMES[lang] ?? lang;
}

// ---------- Date/locale helpers ----------
export function dateLocale(lang: UiLang | string): string {
  switch (whichUiLang(lang)) {
    case "hi":
      return "hi";
    case "te":
      return "te";
    default:
      return "en-IN";
  }
}

export const ENUM_OPTIONS: Record<string, string[]> = {
  Status: ENUM_NAMES.Pending,
  Priority: ENUM_NAMES.Priority,
  Recurrence: ENUM_NAMES.Recurrence,
  AppointmentStatus: ENUM_NAMES.Appointment
};