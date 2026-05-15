namespace Cargo.BuildingBlocks.Notifications.Email;


public sealed class EmailSettings
{
    public const string SectionName = "Email";

    public string SmtpHost { get; set; } = "smtp.gmail.com";
    public int SmtpPort { get; set; } = 587;

    /// <summary>
    /// <c>false</c> means use STARTTLS upgrade on port 587.
    /// <c>true</c>  means implicit TLS on port 465.
    /// </summary>
    public bool UseSsl { get; set; } = false;
    public bool UseStartTls { get; set; } = true;

    public string From { get; set; } = string.Empty;
    public string DisplayName { get; set; } = "Customer Service";

    /// <summary>
    /// Gmail App Password (16 chars, no spaces).
    /// Inject via environment variable <c>Email__Password</c> or User Secrets.
    /// </summary>
    public string Password { get; set; } = string.Empty;
}