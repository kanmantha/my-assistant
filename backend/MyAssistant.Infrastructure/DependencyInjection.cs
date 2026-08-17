using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using MyAssistant.Application.Interfaces;
using MyAssistant.Application.Services;
using MyAssistant.Infrastructure.Auth;
using MyAssistant.Infrastructure.Data;
using MyAssistant.Infrastructure.Email;
using MyAssistant.Infrastructure.Services;

namespace MyAssistant.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
                               ?? configuration["DATABASE_CONNECTION_STRING"]
                               ?? "Data Source=myassistant.db";

        // Render (and most cloud providers) supply a DATABASE_URL env var for
        // PostgreSQL.  When it is present we use Npgsql; otherwise the local
        // SQLite file is used so that local development keeps working without
        // any extra setup.
        var databaseUrl = configuration["DATABASE_URL"];
        if (!string.IsNullOrWhiteSpace(databaseUrl))
        {
            connectionString = databaseUrl;
            services.AddDbContext<AppDbContext>(options =>
                options.UseNpgsql(connectionString));
        }
        else
        {
            services.AddDbContext<AppDbContext>(options =>
                options.UseSqlite(connectionString));
        }

        services.AddScoped<IApplicationDbContext>(sp => sp.GetRequiredService<AppDbContext>());

        services.AddScoped(typeof(IRepository<>), typeof(Repository<>));
        services.AddScoped<INoteRepository, NoteRepository>();
        services.AddScoped<ITaskRepository, TaskRepository>();
        services.AddScoped<IReminderRepository, ReminderRepository>();
        services.AddScoped<IAppointmentRepository, AppointmentRepository>();
        services.AddScoped<IConversationRepository, ConversationRepository>();
        services.AddScoped<INotificationRepository, NotificationRepository>();
        services.AddScoped<IUserSettingsRepository, UserSettingsRepository>();
        services.AddScoped<ISubscriptionRepository, SubscriptionRepository>();
        services.AddScoped<IUsageRepository, UsageRepository>();

        services.AddScoped<ITimeZoneService, TimeZoneService>();
        services.AddScoped<ICurrentUserService, CurrentUserService>();
        services.AddScoped<ITokenService, TokenService>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IAdminService, AdminService>();
        services.AddScoped<ISpeechRecognitionService, SpeechRecognitionService>();
        services.AddScoped<ITextToSpeechService, TextToSpeechService>();
        services.AddScoped<IWakeWordService, WakeWordService>();

        services.AddOptions<EmailOptions>().Bind(configuration.GetSection("Email"));
        services.AddScoped<LogEmailSender>();
        services.AddScoped<SmtpEmailSender>();
        services.AddScoped<IEmailSender>(sp =>
            string.IsNullOrWhiteSpace(sp.GetRequiredService<IOptions<EmailOptions>>().Value.Host)
                ? sp.GetRequiredService<LogEmailSender>()
                : sp.GetRequiredService<SmtpEmailSender>());

        services.AddHostedService<NotificationBackgroundService>();
        services.AddScoped<DbSeeder>();

        return services;
    }
}
