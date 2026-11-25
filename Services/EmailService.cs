using System.Net;
using System.Net.Mail;
using BeanSceneReservationSystem.Interfaces;

namespace BeanSceneReservationSystem.Services;

public class EmailService : IEmailService
{
    private readonly IConfiguration _configuration;

    public EmailService(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public async Task SendRegistrationConfirmationAsync(string toEmail, string firstName)
    {
        var host = _configuration["Email:Host"];
        var username = _configuration["Email:Username"];
        var password = _configuration["Email:Password"];
        var fromAddress = _configuration["Email:FromAddress"];

        if (string.IsNullOrWhiteSpace(fromAddress))
        {
            fromAddress = username;
        }

        if (string.IsNullOrWhiteSpace(host) ||
            !int.TryParse(_configuration["Email:Port"], out var port) ||
            string.IsNullOrWhiteSpace(username) ||
            string.IsNullOrWhiteSpace(password) ||
            string.IsNullOrWhiteSpace(fromAddress))
        {
            throw new InvalidOperationException("Registration confirmation email settings are incomplete.");
        }

        var safeFirstName = firstName.Replace("\r", string.Empty).Replace("\n", string.Empty).Trim();

        using var message = new MailMessage
        {
            From = new MailAddress(fromAddress, "Bean Scene Restaurant"),
            Subject = "Bean Scene Registration Successful",
            Body =
                $"Hello {safeFirstName},{Environment.NewLine}{Environment.NewLine}" +
                $"Your Bean Scene account has been created successfully.{Environment.NewLine}{Environment.NewLine}" +
                $"You can now log in and manage your reservations.{Environment.NewLine}{Environment.NewLine}" +
                $"Thank you for registering with Bean Scene.{Environment.NewLine}{Environment.NewLine}" +
                "Bean Scene Restaurant",
            IsBodyHtml = false
        };
        message.To.Add(toEmail);

        using var client = new SmtpClient(host, port)
        {
            EnableSsl = true,
            UseDefaultCredentials = false,
            Credentials = new NetworkCredential(username, password),
            Timeout = 20000
        };

        await client.SendMailAsync(message);
    }
}
