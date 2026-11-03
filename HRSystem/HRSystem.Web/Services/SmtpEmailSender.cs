using System.Net;
using System.Net.Mail;
using HRSystem.BLL.Common;
using Microsoft.Extensions.Options;

namespace HRSystem.Web.Services;

// Sends notification emails through the SMTP server set in appsettings "Email" (e.g. Gmail, Outlook, the host's mail)
public class SmtpEmailSender : IEmailSender
{
    private readonly IOptionsMonitor<EmailOptions> _options;

    public SmtpEmailSender(IOptionsMonitor<EmailOptions> options)
    {
        _options = options;
    }

    public bool IsConfigured
    {
        get
        {
            var o = _options.CurrentValue;
            return !string.IsNullOrWhiteSpace(o.Host) && !string.IsNullOrWhiteSpace(o.From);
        }
    }

    public async Task SendAsync(string to, string subject, string htmlBody, CancellationToken cancellationToken = default)
    {
        var o = _options.CurrentValue;
        using var message = new MailMessage
        {
            From = new MailAddress(o.From!, o.FromName),
            Subject = subject,
            Body = htmlBody,
            IsBodyHtml = true,
            BodyEncoding = System.Text.Encoding.UTF8,
            SubjectEncoding = System.Text.Encoding.UTF8
        };
        message.To.Add(to);

        using var client = new SmtpClient(o.Host, o.Port)
        {
            EnableSsl = o.EnableSsl,
            Timeout = 15000,
            Credentials = string.IsNullOrWhiteSpace(o.User) ? null : new NetworkCredential(o.User, o.Password)
        };
        await client.SendMailAsync(message, cancellationToken);
    }
}
