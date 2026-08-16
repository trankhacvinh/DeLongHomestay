using System.Net;
using System.Net.Mail;

namespace DeLong.Web.Features.Notifications;

public sealed class NotificationEmailSender
{
    public async Task SendAsync(
        SmtpDeliveryProfile profile,
        IEnumerable<string> recipients,
        string subject,
        string bodyText,
        CancellationToken cancellationToken = default)
    {
        using var message = new MailMessage
        {
            From = new MailAddress(profile.FromEmail, profile.FromName),
            Subject = subject,
            Body = bodyText,
            IsBodyHtml = false
        };
        foreach (var recipient in recipients) message.To.Add(new MailAddress(recipient));
        if (message.To.Count == 0) throw new InvalidOperationException("Email notification has no recipients.");

        using var client = new SmtpClient(profile.Host, profile.Port)
        {
            EnableSsl = profile.UseSsl,
            DeliveryMethod = SmtpDeliveryMethod.Network,
            UseDefaultCredentials = false,
            Timeout = 15000
        };
        if (!string.IsNullOrWhiteSpace(profile.Username))
            client.Credentials = new NetworkCredential(profile.Username, profile.Password ?? string.Empty);

        await client.SendMailAsync(message).WaitAsync(cancellationToken);
    }
}
