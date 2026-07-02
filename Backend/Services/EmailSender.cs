namespace Backend.Services;

public interface IEmailSender
{
    Task SendAsync(string to, string subject, string body);
}

/// <summary>
/// Development mail transport: writes each message as a .txt file into a pickup
/// directory (Email:PickupDirectory) and logs it, so activation/reset flows are
/// fully testable without SMTP. Swap for a real SMTP/API implementation in
/// production by registering a different IEmailSender.
/// </summary>
public sealed class FilePickupEmailSender(IConfiguration config, ILogger<FilePickupEmailSender> logger)
    : IEmailSender
{
    public async Task SendAsync(string to, string subject, string body)
    {
        var directory = config["Email:PickupDirectory"] ?? "App_Data/emails";
        Directory.CreateDirectory(directory);

        var fileName = $"{DateTime.UtcNow:yyyyMMdd_HHmmss_fff}_{SanitizeFileName(to)}.txt";
        var content = $"To: {to}\r\nSubject: {subject}\r\nDate: {DateTime.UtcNow:O}\r\n\r\n{body}";
        await File.WriteAllTextAsync(Path.Combine(directory, fileName), content);

        logger.LogInformation("Email to {To} ({Subject}) written to {Directory}", to, subject, directory);
    }

    private static string SanitizeFileName(string value) =>
        string.Concat(value.Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c));
}

/// <summary>
/// Real SMTP transport (MailKit). Selected automatically when Email:Smtp:Host
/// is configured; supports any provider (Gmail with an app password,
/// Outlook/Office365, school mail server, Mailtrap, ...).
/// </summary>
public sealed class SmtpEmailSender(IConfiguration config, ILogger<SmtpEmailSender> logger)
    : IEmailSender
{
    public async Task SendAsync(string to, string subject, string body)
    {
        var smtp = config.GetRequiredSection("Email:Smtp");
        var from = smtp["From"]
            ?? throw new InvalidOperationException("Email:Smtp:From is not configured.");

        var message = new MimeKit.MimeMessage();
        message.From.Add(new MimeKit.MailboxAddress(smtp["FromName"] ?? "VerBuddy", from));
        message.To.Add(MimeKit.MailboxAddress.Parse(to));
        message.Subject = subject;
        message.Body = new MimeKit.TextPart("plain") { Text = body };

        using var client = new MailKit.Net.Smtp.SmtpClient();
        await client.ConnectAsync(
            smtp["Host"],
            smtp.GetValue("Port", 587),
            MailKit.Security.SecureSocketOptions.StartTlsWhenAvailable);
        if (!string.IsNullOrEmpty(smtp["Username"]))
            await client.AuthenticateAsync(smtp["Username"], smtp["Password"]);
        await client.SendAsync(message);
        await client.DisconnectAsync(quit: true);

        logger.LogInformation("Email to {To} ({Subject}) sent via SMTP {Host}", to, subject, smtp["Host"]);
    }
}
