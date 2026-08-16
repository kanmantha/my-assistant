using FluentValidation;
using MyAssistant.Application.Interfaces;

namespace MyAssistant.API.Validation;

public class RegisterRequestValidator : AbstractValidator<RegisterRequest>
{
    public RegisterRequestValidator()
    {
        RuleFor(x => x.FullName).NotEmpty().WithMessage("Full name is required").MaximumLength(100);
        RuleFor(x => x.Email).NotEmpty().EmailAddress().WithMessage("A valid email is required").MaximumLength(256);
        RuleFor(x => x.Password).NotEmpty().MinimumLength(8).WithMessage("Password must be at least 8 characters");
        RuleFor(x => x.PreferredLanguage).Must(l => l is null or "en-IN" or "hi-IN" or "te-IN").WithMessage("Unsupported language");
    }
}

public class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    public LoginRequestValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
        RuleFor(x => x.Password).NotEmpty();
    }
}

public class CreateNoteValidator : AbstractValidator<CreateNoteRequest>
{
    public CreateNoteValidator()
    {
        RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Content).NotEmpty();
    }
}

public class CreateTaskValidator : AbstractValidator<CreateTaskRequest>
{
    public CreateTaskValidator()
    {
        RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Priority).Must(p => p is null or "Low" or "Medium" or "High" or "Urgent").When(x => x.Priority != null);
    }
}

public class CreateReminderValidator : AbstractValidator<CreateReminderRequest>
{
    public CreateReminderValidator()
    {
        RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Recurrence).Must(r => r is null or "Once" or "Daily" or "Weekly" or "Monthly" or "Yearly" or "Custom").When(x => x.Recurrence != null);
    }
}

public class CreateAppointmentValidator : AbstractValidator<CreateAppointmentRequest>
{
    public CreateAppointmentValidator()
    {
        RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
        RuleFor(x => x.DurationMinutes).GreaterThan(0).When(x => x.DurationMinutes.HasValue);
    }
}

public static class ValidatorExtensions
{
    public static IRuleBuilder<T, TProperty> Optional<T, TProperty>(this IRuleBuilder<T, TProperty> builder)
        => builder;
}