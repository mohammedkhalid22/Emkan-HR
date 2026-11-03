namespace HRSystem.BLL.Common;

// Implemented in the Web layer over SMTP; does nothing until "Email" is set in appsettings
public interface IEmailSender
{
    bool IsConfigured { get; }
    Task SendAsync(string to, string subject, string htmlBody, CancellationToken cancellationToken = default);
}

public class EmailOptions
{
    public const string Section = "Email";

    public string? Host { get; set; }
    public int Port { get; set; } = 587;
    public bool EnableSsl { get; set; } = true;
    public string? User { get; set; }
    public string? Password { get; set; }
    public string? From { get; set; }
    public string FromName { get; set; } = "إمكان HR";

    // The site's address, so links in emails open the app ("https://hrsystem1.runasp.net")
    public string? BaseUrl { get; set; }
}
