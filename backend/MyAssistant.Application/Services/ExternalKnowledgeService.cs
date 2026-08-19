using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace MyAssistant.Application.Services;

public class WeatherInfo
{
    public string Location { get; set; } = string.Empty;
    public double TemperatureC { get; set; }
    public double FeelsLikeC { get; set; }
    public int HumidityPercent { get; set; }
    public double WindKmh { get; set; }
    public int WeatherCode { get; set; }
}

public interface IExternalKnowledgeService
{
    Task<WeatherInfo?> GetWeatherAsync(string location, CancellationToken cancellationToken = default);
    Task<string?> SearchWebAsync(string query, CancellationToken cancellationToken = default);
}

/// <summary>
/// Fetches live weather (Open-Meteo, no API key) and web search summaries
/// (Wikipedia, no API key) so general questions return real data.
/// </summary>
public class ExternalKnowledgeService : IExternalKnowledgeService
{
    private readonly HttpClient _http;
    private readonly string _defaultCity;
    private readonly ILogger<ExternalKnowledgeService> _logger;

    public ExternalKnowledgeService(IConfiguration configuration, ILogger<ExternalKnowledgeService> logger)
    {
        _http = new HttpClient { Timeout = TimeSpan.FromSeconds(12) };
        _defaultCity = configuration["Weather:DefaultCity"] ?? string.Empty;
        _logger = logger;
    }

    public async Task<WeatherInfo?> GetWeatherAsync(string location, CancellationToken cancellationToken = default)
    {
        try
        {
            var city = string.IsNullOrWhiteSpace(location) ? _defaultCity : location.Trim();
            if (string.IsNullOrWhiteSpace(city))
            {
                return null;
            }

            var geo = await _http.GetFromJsonAsync<JsonElement>(
                $"https://geocoding-api.open-meteo.com/v1/search?name={Uri.EscapeDataString(city)}&count=1&language=en&format=json",
                cancellationToken);
            if (!geo.TryGetProperty("results", out var results) || results.GetArrayLength() == 0)
            {
                return null;
            }

            var first = results[0];
            var lat = first.GetProperty("latitude").GetDouble();
            var lon = first.GetProperty("longitude").GetDouble();
            var displayName = first.TryGetProperty("name", out var nameProp) ? nameProp.GetString() ?? city : city;

            var weather = await _http.GetFromJsonAsync<JsonElement>(
                $"https://api.open-meteo.com/v1/forecast?latitude={lat}&longitude={lon}&current_weather=true&timezone=auto",
                cancellationToken);
            if (!weather.TryGetProperty("current_weather", out var current))
            {
                return null;
            }

            return new WeatherInfo
            {
                Location = displayName,
                TemperatureC = current.TryGetProperty("temperature", out var t) ? t.GetDouble() : double.NaN,
                FeelsLikeC = current.TryGetProperty("apparent_temperature", out var at) ? at.GetDouble() : double.NaN,
                WindKmh = current.TryGetProperty("windspeed", out var w) ? w.GetDouble() : double.NaN,
                WeatherCode = current.TryGetProperty("weathercode", out var wc) ? wc.GetInt32() : 0,
                HumidityPercent = -1
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Weather lookup failed for: {Location}", location);
            return null;
        }
    }

    public async Task<string?> SearchWebAsync(string query, CancellationToken cancellationToken = default)
    {
        try
        {
            var url = "https://en.wikipedia.org/w/api.php" +
                      $"?action=query&format=json&generator=search&gsrsearch={Uri.EscapeDataString(query)}&gsrlimit=1" +
                      "&prop=extracts&exintro=1&explaintext=1";
            var json = await _http.GetFromJsonAsync<JsonElement>(url, cancellationToken);
            if (!json.TryGetProperty("query", out var queryObj) || !queryObj.TryGetProperty("pages", out var pages))
            {
                return null;
            }

            foreach (var page in pages.EnumerateObject())
            {
                if (page.Value.TryGetProperty("extract", out var extract))
                {
                    var text = extract.GetString();
                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        return text.Length > 600 ? text[..600] : text;
                    }
                }
            }
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Wikipedia search failed for: {Query}", query);
            return null;
        }
    }
}