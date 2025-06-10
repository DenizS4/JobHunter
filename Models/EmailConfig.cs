namespace JobHunter.Models;

public class EmailConfig
{
    public string SmtpServer { get; set; } = "";
    public int SmtpPort { get; set; } = 587;
    public string Email { get; set; } = "";
    public string Password { get; set; } = "";
    public string SenderName { get; set; } = "";
    public bool EnableSsl { get; set; } = true;
    public EmailProvider Provider { get; set; } = EmailProvider.Gmail;
}