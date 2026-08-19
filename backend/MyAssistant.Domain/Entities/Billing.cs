namespace MyAssistant.Domain.Entities;

public enum PlanType
{
    Free = 0,
    Pro = 1,
    Premium = 2
}

public class Plan
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty; // FREE | PRO | PREMIUM
    public PlanType Type { get; set; } = PlanType.Free;
    public decimal PriceMonthly { get; set; }
    public decimal PriceYearly { get; set; }
    public string Currency { get; set; } = "INR";
    public int MaxNotes { get; set; } = -1;       // -1 = unlimited
    public int MaxTasks { get; set; } = -1;
    public int MaxRemindersPerMonth { get; set; } = -1;
    public int MaxAppointments { get; set; } = -1;
    public int MaxAiRequestsPerMonth { get; set; } = 20;
    public int MaxVoiceRequestsPerMonth { get; set; } = 20;
    public int MaxSpeechMinutesPerMonth { get; set; } = 0;
    public bool AllowsVoice { get; set; }
    public bool AllowsCalendar { get; set; }
    public bool AllowsCloudBackup { get; set; }
    public bool AllowsCalendarIntegrations { get; set; }
    public bool AllowsAdvancedAi { get; set; }
    public List<string> Features { get; set; } = new();
    public bool IsEnabled { get; set; } = true;
    public int DisplayOrder { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

public enum SubscriptionStatus
{
    Active = 0,
    Pending = 1,
    Cancelled = 2,
    Expired = 3,
    PastDue = 4
}

public enum BillingPeriod
{
    Monthly = 0,
    Yearly = 1
}

public class Subscription
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public Guid PlanId { get; set; }
    public SubscriptionStatus Status { get; set; } = SubscriptionStatus.Active;
    public BillingPeriod BillingPeriod { get; set; } = BillingPeriod.Monthly;
    public DateTime StartDate { get; set; } = DateTime.UtcNow;
    public DateTime? CurrentPeriodEnd { get; set; }
    public DateTime? RenewalDate { get; set; }
    public DateTime? CancelAt { get; set; }
    public DateTime? CancelledAt { get; set; }
    public string? Provider { get; set; }       // googleplay | apple | stripe | mock (development only)
    public string? ProviderSubscriptionId { get; set; }
    public string? ProviderPurchaseToken { get; set; }
    public string? Platform { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public User? User { get; set; }
    public Plan? Plan { get; set; }
}

public enum PaymentStatus
{
    Pending = 0,
    Completed = 1,
    Failed = 2,
    Refunded = 3
}

public class Payment
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public Guid? SubscriptionId { get; set; }
    public Guid PlanId { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "INR";
    public PaymentStatus Status { get; set; } = PaymentStatus.Pending;
    public string Provider { get; set; } = string.Empty;  // googleplay | apple | stripe | mock (development only)
    public string? ProviderReference { get; set; }
    public string? ProviderReceipt { get; set; }
    public string Description { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? PaidAt { get; set; }

    public User? User { get; set; }
    public Subscription? Subscription { get; set; }
}

public class UsageRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public string UsageType { get; set; } = string.Empty; // AI_Request | Voice_Request | Speech_Minutes | Note | Task | Reminder | Appointment
    public int Quantity { get; set; }
    public DateTime OccurredAt { get; set; } = DateTime.UtcNow;
    public string? Meta { get; set; }

    public User? User { get; set; }
}