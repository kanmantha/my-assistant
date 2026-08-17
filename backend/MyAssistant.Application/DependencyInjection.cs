using FluentValidation;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MyAssistant.Application.Interfaces;
using MyAssistant.Application.Services;

namespace MyAssistant.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddValidatorsFromAssembly(typeof(DependencyInjection).Assembly);

        services.AddScoped<DateTimeParserService>();
        services.AddScoped<HeuristicAIService>();
        services.AddScoped<OpenAiAIService>();
        services.AddScoped<ConfigurableAIService>();
        services.AddScoped<IAssistantAIService>(sp => sp.GetRequiredService<ConfigurableAIService>());

        services.Configure<OpenAiOptions>(o =>
        {
            o.ApiKey = configuration["AI_API_KEY"] ?? string.Empty;
            o.Model = configuration["AI_MODEL"] ?? "gpt-4o-mini";
            o.BaseUrl = configuration["AI_BASE_URL"] ?? "https://api.openai.com/v1";
        });

        services.AddScoped<IDateTimeParser>(sp => sp.GetRequiredService<DateTimeParserService>());

        services.AddScoped<INoteService, NoteService>();
        services.AddScoped<ITaskService, TaskService>();
        services.AddScoped<IReminderService, ReminderService>();
        services.AddScoped<IAppointmentService, AppointmentService>();
        services.AddScoped<IConversationService, ConversationService>();
        services.AddScoped<ISettingsService, SettingsService>();
        services.AddScoped<ISearchService, SearchService>();
        services.AddScoped<IDashboardService, DashboardService>();
        services.AddScoped<ISubscriptionService, SubscriptionService>();
        services.AddScoped<INotificationService, NotificationService>();
        services.AddScoped<IAssistantIntentService, AssistantService>();
        services.AddSingleton<IAssistantSessionStore, InMemoryAssistantSessionStore>();

        return services;
    }
}
