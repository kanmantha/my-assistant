using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MyAssistant.Application.Interfaces;
using MyAssistant.Infrastructure.Data;
using MyAssistant.Infrastructure.Repositories;
using MyAssistant.Infrastructure.Services;
using MyAssistant.Infrastructure.Services.AI;
using MyAssistant.Infrastructure.Services.Speech;
using Npgsql;

namespace MyAssistant.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration config)
    {
        // Env var takes priority (Render/Heroku) so hosted deployments don't
        // fall back to the localhost connection in appsettings.json.
        var connection = config["DATABASE_CONNECTION_STRING"] ?? config.GetConnectionString("Default");
        if (string.IsNullOrWhiteSpace(connection))
            connection = "Host=localhost;Port=5432;Database=myassistant;Username=postgres;Password=postgres";

        var dataSourceBuilder = new NpgsqlDataSourceBuilder(connection);
        dataSourceBuilder.EnableDynamicJson();
        var dataSource = dataSourceBuilder.Build();

        services.AddDbContext<AppDbContext>(options => options.UseNpgsql(dataSource));

        services.AddScoped(typeof(IRepository<>), typeof(Repository<>));
        services.AddScoped<IUnitOfWork, UnitOfWork>();

        services.AddSingleton<IPasswordHasher, PasswordHasher>();
        services.AddScoped<ITokenService, TokenService>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IProductivityService, ProductivityService>();
        services.AddScoped<ISubscriptionService, SubscriptionService>();
        services.AddScoped<AssistantOrchestrator>();

        services.AddHttpClient();

        // AI provider selection via AI_PROVIDER env var (mock | openai | azureopenai | gemini)
        var aiProvider = config["AI_PROVIDER"]?.ToLowerInvariant() ?? "mock";
        var apiKey = config["AI_API_KEY"] ?? string.Empty;
        var model = config["AI_MODEL"] ?? (aiProvider == "gemini" ? "gemini-2.0-flash" : "gpt-4o-mini");

        switch (aiProvider)
        {
            case "openai":
                services.AddScoped<IAssistantAiService>(sp =>
                {
                    var client = sp.GetRequiredService<IHttpClientFactory>().CreateClient();
                    var logger = sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<OpenAiService>>();
                    return new OpenAiService(client, apiKey, model, "https://api.openai.com/v1/chat/completions", logger);
                });
                break;
            case "azureopenai":
                services.AddScoped<IAssistantAiService>(sp =>
                {
                    var client = sp.GetRequiredService<IHttpClientFactory>().CreateClient();
                    var logger = sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<OpenAiService>>();
                    var endpoint = config["AZURE_OPENAI_ENDPOINT"] ?? "https://example.openai.azure.com/v1/chat/completions";
                    return new AzureOpenAiService(new OpenAiService(client, apiKey, model, endpoint, logger));
                });
                break;
            case "gemini":
                services.AddScoped<IAssistantAiService>(sp =>
                {
                    var client = sp.GetRequiredService<IHttpClientFactory>().CreateClient();
                    var logger = sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<GeminiService>>();
                    return new GeminiService(client, apiKey, model, logger);
                });
                break;
            default:
                services.AddScoped<IAssistantAiService, MockAiService>();
                break;
        }

        // Speech providers
        var speechProvider = config["SPEECH_PROVIDER"]?.ToLowerInvariant() ?? "mock";
        switch (speechProvider)
        {
            case "google":
                services.AddScoped<ISpeechRecognitionService>(sp =>
                {
                    var logger = sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<GoogleSpeechService>>();
                    return new GoogleSpeechService(config["SPEECH_API_KEY"] ?? string.Empty, logger);
                });
                services.AddScoped<ITextToSpeechService>(sp =>
                {
                    var logger = sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<GoogleTtsService>>();
                    return new GoogleTtsService(config["TTS_API_KEY"] ?? string.Empty, logger);
                });
                break;
            default:
                services.AddScoped<ISpeechRecognitionService, MockSpeechService>();
                services.AddScoped<ITextToSpeechService, MockTtsService>();
                break;
        }

        services.AddScoped<IWakeWordService, NoopWakeWordService>();

        // Payments
        var paymentProvider = config["PAYMENT_PROVIDER"]?.ToLowerInvariant() ?? "mock";
        switch (paymentProvider)
        {
            case "googleplay":
                services.AddScoped<IPaymentService, GooglePlayPaymentService>();
                break;
            case "apple":
                services.AddScoped<IPaymentService, AppleStoreKitPaymentService>();
                break;
            default:
                services.AddScoped<IPaymentService, MockPaymentService>();
                break;
        }

        return services;
    }
}