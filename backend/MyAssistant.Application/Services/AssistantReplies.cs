namespace MyAssistant.Application.Services;

public static class AssistantReplies
{
    public static string NormalizeLanguage(string? language)
    {
        if (string.IsNullOrWhiteSpace(language)) return "en-IN";
        var l = language.Trim().ToLowerInvariant();
        if (l.StartsWith("hi")) return "hi-IN";
        if (l.StartsWith("te")) return "te-IN";
        return "en-IN";
    }

    public static string Greeting => "Yes, how can I help you?";
    public static string GreetingHi => "जी, मैं आपकी कैसे सहायता कर सकता हूँ?";
    public static string GreetingTe => "అవును, నేను మీకు ఎలా సహాయం చేయగలను?";

    public static string WelcomeBack(string lang) => lang switch
    {
        "hi-IN" => "नमस्ते! आपके पास आज 3 कार्य, 2 मीटिंग और 1 रिमाइंडर हैं। मैं आपकी कैसे सहायता करूँ?",
        "te-IN" => "నమస్కారం! మీకు ఈరోజు 3 పనులు, 2 అపాయింట్‌మెంట్‌లు మరియు 1 రిమైండర్ ఉన్నాయి. నేను ఎలా సహాయం చేయాలి?",
        _ => "Hello! You have tasks, appointments and reminders scheduled today. How can I help you?"
    };

    public static string Help(string lang) => lang switch
    {
        "hi-IN" => "आप मुझसे बात करके नोट्स, कार्य, रिमाइंडर और मीटिंग्स बना सकते हैं। उदाहरण: \"एक नोट लिखो\", \"कल 9 बजे रिमाइंडर सेट करो\"।",
        "te-IN" => "మీరు నాతో మాట్లాడి నోట్స్, పనులు, రిమైండర్‌లు మరియు మీటింగ్‌లు సృష్టించవచ్చు. ఉదాహరణ: \"ఒక నోట్ తీసుకో\", \"రేపు 9 గంటలకు రిమైండర్ సెట్ చేయి\".",
        _ => "You can create notes, tasks, reminders and appointments by speaking to me. For example: \"Take a note\", \"Remind me tomorrow at 9 AM\"."
    };

    public static string NotUnderstood(string lang) => lang switch
    {
        "hi-IN" => "क्षमा करें, मैं समझ नहीं पाया। कृपया दोबारा कहें।",
        "te-IN" => "క్షమించండి, నేను అర్థం చేసుకోలేకపోయాను. దయచేసి మళ్లీ చెప్పండి.",
        _ => "Sorry, I didn't understand that. Could you please say it again?"
    };

    public static string AskNoteContent(string lang) => lang switch
    {
        "hi-IN" => "ठीक है, नोट की सामग्री क्या होगी?",
        "te-IN" => "సరే, నోట్ విషయం ఏమిటి?",
        _ => "Sure, what should I note down?"
    };

    public static string AskTaskTitle(string lang) => lang switch
    {
        "hi-IN" => "ठीक है, कार्य का नाम क्या होगा?",
        "te-IN" => "సరే, పని పేరు ఏమిటి?",
        _ => "Sure, what task should I add?"
    };

    public static string Cancelled(string lang) => lang switch
    {
        "hi-IN" => "ठीक है, मैंने वह क्रिया रद्द कर दी है।",
        "te-IN" => "సరే, నేను ఆ చర్యను రద్దు చేశాను.",
        _ => "Okay, I have cancelled that action."
    };

    public static string Confirmed(string lang) => lang switch
    {
        "hi-IN" => "हो गया।",
        "te-IN" => "అయిపోయింది.",
        _ => "Done."
    };

    public static string ReminderCreated(string title, string when, string lang) => lang switch
    {
        "hi-IN" => $"ज़रूर। मैंने {when} के लिए '{title}' का रिमाइंडर सेट कर दिया है।",
        "te-IN" => $"తప్పకుండా. {when}కి '{title}' రిమైండర్ సెట్ చేశాను.",
        _ => $"Sure. I've set a reminder for {when} to {title}."
    };

    public static string ReminderDeleted(string title, string lang) => lang switch
    {
        "hi-IN" => $"रिमाइंडर '{title}' हटा दिया गया है।",
        "te-IN" => $"రిమైండర్ '{title}' తొలగించబడింది.",
        _ => $"Reminder '{title}' has been deleted."
    };

    public static string NoteCreated(string lang) => lang switch
    {
        "hi-IN" => "नोट सेव कर लिया गया है।",
        "te-IN" => "నోట్ సేవ్ చేయబడింది.",
        _ => "Your note has been saved."
    };

    public static string NoteDeleted(string lang) => lang switch
    {
        "hi-IN" => "नोट हटा दिया गया है।",
        "te-IN" => "నోట్ తొలగించబడింది.",
        _ => "The note has been deleted."
    };

    public static string TaskCreated(string title, string lang) => lang switch
    {
        "hi-IN" => $"कार्य '{title}' जोड़ दिया गया है।",
        "te-IN" => $"పని '{title}' జోడించబడింది.",
        _ => $"Task '{title}' has been added."
    };

    public static string TaskCompleted(string title, string lang) => lang switch
    {
        "hi-IN" => $"बहुत बढ़िया! '{title}' पूरा हो गया है।",
        "te-IN" => $"అద్భుతం! '{title}' పూర్తయింది.",
        _ => $"Great! '{title}' is now completed."
    };

    public static string TaskDeleted(string lang) => lang switch
    {
        "hi-IN" => "कार्य हटा दिया गया है।",
        "te-IN" => "పని తొలగించబడింది.",
        _ => "The task has been deleted."
    };

    public static string AppointmentScheduled(string title, string when, string lang) => lang switch
    {
        "hi-IN" => $"मीटिंग '{title}' {when} के लिए निर्धारित कर दी गई है।",
        "te-IN" => $"మీటింగ్ '{title}' {when}కి షెడ్యూల్ చేయబడింది.",
        _ => $"Your appointment '{title}' is scheduled for {when}."
    };

    public static string AppointmentDeleted(string title, string lang) => lang switch
    {
        "hi-IN" => $"मीटिंग '{title}' हटा दी गई है।",
        "te-IN" => $"మీటింగ్ '{title}' తొలగించబడింది.",
        _ => $"Appointment '{title}' has been deleted."
    };

    public static string NoTasks(string lang) => lang switch
    {
        "hi-IN" => "आपके पास कोई कार्य नहीं है।",
        "te-IN" => "మీకు పనులు లేవు.",
        _ => "You have no tasks."
    };

    public static string NoReminders(string lang) => lang switch
    {
        "hi-IN" => "आपके पास कोई रिमाइंडर नहीं है।",
        "te-IN" => "మీకు రిమైండర్‌లు లేవు.",
        _ => "You have no reminders."
    };

    public static string NoAppointments(string lang) => lang switch
    {
        "hi-IN" => "आपके पास कोई मीटिंग नहीं है।",
        "te-IN" => "మీకు మీటింగ్‌లు లేవు.",
        _ => "You have no appointments."
    };

    public static string NoNotes(string lang) => lang switch
    {
        "hi-IN" => "कोई नोट नहीं मिला।",
        "te-IN" => "నోట్స్ ఏవీ కనుగొనబడలేదు.",
        _ => "No notes found."
    };

    public static string LanguageChanged(string lang) => lang switch
    {
        "hi-IN" => "भाषा हिंदी में बदल दी गई है।",
        "te-IN" => "భాష తెలుగుకి మార్చబడింది.",
        _ => "Language changed to English."
    };

    public static string TodaySchedule(string summary, string lang) => lang switch
    {
        "hi-IN" => $"आज का कार्यक्रम: {summary}",
        "te-IN" => $"ఈరోజు షెడ్యూల్: {summary}",
        _ => $"Today's schedule: {summary}"
    };

    public static string TomorrowSchedule(string summary, string lang) => lang switch
    {
        "hi-IN" => $"कल का कार्यक्रम: {summary}",
        "te-IN" => $"రేపు షెడ్యూల్: {summary}",
        _ => $"Tomorrow's schedule: {summary}"
    };

    public static string SearchResults(int count, string query, string lang) => lang switch
    {
        "hi-IN" => $"मुझे '{query}' के लिए {count} परिणाम मिले।",
        "te-IN" => $"'{query}' కోసం {count} ఫలితాలు కనుగొన్నాను.",
        _ => $"I found {count} results for '{query}'."
    };

    public static string EmptySchedule(string lang) => lang switch
    {
        "hi-IN" => "आज कुछ भी निर्धारित नहीं है।",
        "te-IN" => "ఈరోజు ఏమీ షెడ్యూల్ చేయబడలేదు.",
        _ => "Nothing is scheduled today."
    };

    public static string AppointmentsList(List<string> items, string lang)
    {
        var joined = string.Join(", ", items);
        return lang switch
        {
            "hi-IN" => $"आपकी मीटिंग्स: {joined}",
            "te-IN" => $"మీ మీటింగ్‌లు: {joined}",
            _ => $"Your appointments: {joined}"
        };
    }

    public static string TasksList(List<string> items, string lang)
    {
        var joined = string.Join(", ", items);
        return lang switch
        {
            "hi-IN" => $"आपके कार्य: {joined}",
            "te-IN" => $"మీ పనులు: {joined}",
            _ => $"Your tasks: {joined}"
        };
    }

    public static string RemindersList(List<string> items, string lang)
    {
        var joined = string.Join(", ", items);
        return lang switch
        {
            "hi-IN" => $"आपके रिमाइंडर: {joined}",
            "te-IN" => $"మీ రిమైండర్‌లు: {joined}",
            _ => $"Your reminders: {joined}"
        };
    }

    public static string NotesList(List<string> items, string lang)
    {
        var joined = string.Join(", ", items);
        return lang switch
        {
            "hi-IN" => $"आपके नोट्स: {joined}",
            "te-IN" => $"మీ నోట్స్: {joined}",
            _ => $"Your notes: {joined}"
        };
    }
}
