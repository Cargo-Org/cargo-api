using Cargo.BuildingBlocks.Notifications.Email;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;

namespace Cargo.NotificationService.Notifications.Email;

/// <summary>
/// Sends transactional emails through Gmail SMTP via MailKit.
///
/// MailKit is used instead of System.Net.Mail because it:
///   • Has proper STARTTLS / OAuth2 / SASL support
///   • Handles Gmail's quirks (XOAUTH2, app-password auth)
///   • Is actively maintained and .NET 10 compatible
///
/// Connection lifetime:
///   A new SmtpClient is created per-send. For high-volume scenarios,
///   replace this with a pooled/singleton client or a job queue.
/// </summary>
public class EmailService(
    IOptions<EmailSettings> options,
    ILogger<EmailService> logger) : IEmailService
{
    private readonly EmailSettings _settings = options.Value;

    // ──────────────────────────────────────────────────────────────
    //  SendAsync — low-level, accepts a fully-built EmailMessage
    // ──────────────────────────────────────────────────────────────

    public async Task SendAsync(EmailMessage message, CancellationToken ct = default)
    {
        var mime = BuildMimeMessage(message);

        using var client = new SmtpClient();

        try
        {
            // Connect with STARTTLS (port 587) or implicit TLS (port 465)
            SecureSocketOptions socketOptions = _settings.UseSsl
                ? SecureSocketOptions.SslOnConnect
                : SecureSocketOptions.StartTls;

            await client.ConnectAsync(_settings.SmtpHost, _settings.SmtpPort,
                                      socketOptions, ct);

            // Authenticate with Gmail App Password
            await client.AuthenticateAsync(_settings.From, _settings.Password, ct);

            await client.SendAsync(mime, ct);

            logger.LogInformation(
                "Email sent → {Recipient} | Subject: {Subject}",
                message.ToEmail, message.Subject);
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "Failed to send email → {Recipient} | Subject: {Subject}",
                message.ToEmail, message.Subject);
            throw;
        }
        finally
        {
            if (client.IsConnected)
                await client.DisconnectAsync(quit: true, ct);
        }
    }

    // ──────────────────────────────────────────────────────────────
    //  SendOtpAsync — renders the right template and delegates
    // ──────────────────────────────────────────────────────────────

    public Task SendOtpAsync(
        string toEmail, string toName, string otp,
        OtpEmailType emailType, CancellationToken ct = default)
    {
        var (subject, html, plain) = emailType switch
        {
            OtpEmailType.EmailVerification => BuildVerificationEmail(toName, otp),
            OtpEmailType.PasswordReset     => BuildPasswordResetEmail(toName, otp),
            _                              => throw new ArgumentOutOfRangeException(nameof(emailType))
        };

        return SendAsync(new EmailMessage(toEmail, toName, subject, html, plain), ct);
    }

    // ──────────────────────────────────────────────────────────────
    //  MimeMessage builder
    // ──────────────────────────────────────────────────────────────

    private MimeMessage BuildMimeMessage(EmailMessage msg)
    {
        var mime = new MimeMessage();

        mime.From.Add(new MailboxAddress(_settings.DisplayName, _settings.From));
        mime.To.Add(new MailboxAddress(msg.ToName, msg.ToEmail));
        mime.Subject = msg.Subject;

        var bodyBuilder = new BodyBuilder
        {
            HtmlBody = msg.HtmlBody,
            TextBody = msg.PlainTextBody ?? StripHtml(msg.HtmlBody)
        };

        mime.Body = bodyBuilder.ToMessageBody();
        return mime;
    }

    // ──────────────────────────────────────────────────────────────
    //  Email templates
    // ──────────────────────────────────────────────────────────────

    private static (string subject, string html, string plain)
        BuildVerificationEmail(string name, string otp) => (
        subject: "Verify your email address",
        html: OtpEmailTemplate(
            title: "Verify Your Email",
            greeting: $"Hi {name},",
            body: "Use the code below to verify your email address. It expires in <strong>10 minutes</strong>.",
            otp: otp,
            footerNote: "If you didn't create an account, you can safely ignore this email."),
        plain: $"Hi {name},\n\nYour email verification code is: {otp}\n\nIt expires in 10 minutes.\n\nIf you didn't create an account, ignore this email."
    );

    private static (string subject, string html, string plain)
        BuildPasswordResetEmail(string name, string otp) => (
        subject: "Reset your password",
        html: OtpEmailTemplate(
            title: "Reset Your Password",
            greeting: $"Hi {name},",
            body: "Use the code below to reset your password. It expires in <strong>10 minutes</strong>.",
            otp: otp,
            footerNote: "If you didn't request a password reset, please secure your account immediately."),
        plain: $"Hi {name},\n\nYour password reset code is: {otp}\n\nIt expires in 10 minutes.\n\nIf you didn't request this, please secure your account."
    );

    /// <summary>
    /// Single HTML template shared by all OTP email types.
    /// Minimal, dark-branded, mobile-friendly.
    /// </summary>
    private static string OtpEmailTemplate(
        string title, string greeting, string body,
        string otp, string footerNote) => $"""
        <!DOCTYPE html>
        <html lang="en">
        <head>
          <meta charset="UTF-8"/>
          <meta name="viewport" content="width=device-width,initial-scale=1"/>
          <title>{title}</title>
        </head>
        <body style="margin:0;padding:0;background:#0f0f0f;font-family:'Segoe UI',Arial,sans-serif;">
          <table width="100%" cellpadding="0" cellspacing="0"
                 style="background:#0f0f0f;padding:40px 0;">
            <tr>
              <td align="center">
                <table width="480" cellpadding="0" cellspacing="0"
                       style="background:#1a1a1a;border-radius:12px;
                              border:1px solid #2a2a2a;overflow:hidden;">

                  <!-- Header -->
                  <tr>
                    <td style="background:#111;padding:28px 40px;
                               border-bottom:1px solid #2a2a2a;">
                      <p style="margin:0;font-size:18px;font-weight:700;
                                color:#ffffff;letter-spacing:0.5px;">
                        Cargo
                      </p>
                    </td>
                  </tr>

                  <!-- Body -->
                  <tr>
                    <td style="padding:40px;">
                      <h1 style="margin:0 0 16px;font-size:22px;
                                 font-weight:700;color:#ffffff;">
                        {title}
                      </h1>
                      <p style="margin:0 0 12px;font-size:15px;
                                color:#a0a0a0;line-height:1.6;">
                        {greeting}
                      </p>
                      <p style="margin:0 0 32px;font-size:15px;
                                color:#a0a0a0;line-height:1.6;">
                        {body}
                      </p>

                      <!-- OTP box -->
                      <div style="background:#0f0f0f;border:1px solid #333;
                                  border-radius:10px;padding:28px;
                                  text-align:center;margin-bottom:32px;">
                        <p style="margin:0 0 8px;font-size:12px;
                                  color:#666;letter-spacing:2px;
                                  text-transform:uppercase;">
                          Your code
                        </p>
                        <p style="margin:0;font-size:42px;font-weight:800;
                                  color:#ffffff;letter-spacing:14px;
                                  font-variant-numeric:tabular-nums;">
                          {otp}
                        </p>
                      </div>

                      <p style="margin:0;font-size:13px;color:#555;
                                line-height:1.5;">
                        {footerNote}
                      </p>
                    </td>
                  </tr>

                  <!-- Footer -->
                  <tr>
                    <td style="padding:20px 40px;border-top:1px solid #2a2a2a;">
                      <p style="margin:0;font-size:12px;color:#444;
                                text-align:center;">
                        © 2025 Cargo · All rights reserved
                      </p>
                    </td>
                  </tr>

                </table>
              </td>
            </tr>
          </table>
        </body>
        </html>
        """;

    /// <summary>Very simple HTML → plain-text stripper as a fallback.</summary>
    private static string StripHtml(string html) =>
        System.Text.RegularExpressions.Regex.Replace(html, "<[^>]+>", string.Empty)
              .Replace("&nbsp;", " ")
              .Trim();
}
