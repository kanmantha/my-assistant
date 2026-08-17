namespace MyAssistant.Infrastructure.Email;

public class EmailOptions
{
    public string From { get; set; } = "no-reply@myassistant.app";
    public string Host { get; set; } = string.Empty;
    public int Port { get; set; } = 587;
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public bool UseSsl { get; set; } = true;
    public string FrontendUrl { get; set; } = "http://localhost:5173";
}
