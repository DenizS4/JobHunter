using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using JobHunter.Models;
using Spectre.Console;
using System.Text;
using JobHunter.Interfaces;

namespace JobHunter.Services;

public class EmailService : IEmailService
{
    private readonly ILogger<EmailService> _logger;
    private readonly EmailConfig _emailConfig;

    public EmailService(ILogger<EmailService> logger, IOptions<EmailConfig> emailConfig)
    {
        _logger = logger;
        _emailConfig = emailConfig.Value;
    }

    public async Task SendJobApplicationEmailAsync(JobPosting job, UserConfiguration config)
    {
        try
        {
            var emailContent = GenerateJobApplicationEmail(job, config);
            var message = CreateEmailMessage(job, emailContent, config.CvFilePath);

            if (config.EmailMode == EmailMode.Draft)
            {
                await SaveEmailAsDraftAsync(message, job);
                _logger.LogInformation("Email draft created for {Company} - {Title}", job.Company, job.Title);
            }
            else
            {
                await SendEmailAsync(message);
                _logger.LogInformation("Email sent to {Company} - {Title}", job.Company, job.Title);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send email for job: {JobTitle} at {Company}", job.Title, job.Company);
            throw;
        }
    }

    public string GenerateJobApplicationEmail(JobPosting job, UserConfiguration config)
    {
        var emailBuilder = new StringBuilder();
        
        // Subject line
        var subject = $"Application for {job.Title} Position";
        
        // Email body
        emailBuilder.AppendLine($"Dear Hiring Manager,");
        emailBuilder.AppendLine();
        emailBuilder.AppendLine($"I hope this email finds you well. I am writing to express my strong interest in the {job.Title} position at {job.Company} that I found on {job.Platform}.");
        emailBuilder.AppendLine();
        
        // Add experience level if provided
        if (config.TemplateAnswers.TryGetValue("experience_level", out var experienceLevel))
        {
            emailBuilder.AppendLine($"As a {experienceLevel.ToLower()} professional, I believe I would be a great fit for this role.");
        }
        
        // Add tech stack if provided
        if (config.TemplateAnswers.TryGetValue("tech_stack", out var techStack))
        {
            emailBuilder.AppendLine($"My technical expertise includes: {techStack}.");
        }
        
        // Add years of experience if provided
        if (config.TemplateAnswers.TryGetValue("years_of_experience", out var yearsExp))
        {
            emailBuilder.AppendLine($"I have {yearsExp} years of professional experience in software development.");
        }
        
        emailBuilder.AppendLine();
        emailBuilder.AppendLine("I am particularly drawn to this opportunity because of your company's reputation and the interesting challenges this role presents. I would welcome the opportunity to discuss how my skills and enthusiasm can contribute to your team's success.");
        emailBuilder.AppendLine();
        
        // Add availability information
        if (config.TemplateAnswers.TryGetValue("currently_working", out var currentlyWorking) && currentlyWorking == "Yes")
        {
            if (config.TemplateAnswers.TryGetValue("notice_period", out var noticePeriod))
            {
                emailBuilder.AppendLine($"I am currently employed but can start with {noticePeriod} notice period.");
            }
        }
        else
        {
            emailBuilder.AppendLine("I am immediately available to start.");
        }
        
        // Add location preference
        if (config.TemplateAnswers.TryGetValue("location_preference", out var locationPref))
        {
            emailBuilder.AppendLine($"I am open to {locationPref.ToLower()} work arrangements.");
        }
        
        emailBuilder.AppendLine();
        emailBuilder.AppendLine("I have attached my resume for your review. I would be happy to provide any additional information you might need and am available for an interview at your convenience.");
        emailBuilder.AppendLine();
        emailBuilder.AppendLine("Thank you for considering my application. I look forward to hearing from you soon.");
        emailBuilder.AppendLine();
        emailBuilder.AppendLine("Best regards,");
        emailBuilder.AppendLine(_emailConfig.SenderName);
        emailBuilder.AppendLine(_emailConfig.Email);
        
        return emailBuilder.ToString();
    }

    private MimeMessage CreateEmailMessage(JobPosting job, string emailContent, string cvFilePath)
    {
        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(_emailConfig.SenderName, _emailConfig.Email));
        message.To.Add(new MailboxAddress("", job.ContactEmail));
        message.Subject = $"Application for {job.Title} Position - {_emailConfig.SenderName}";

        var bodyBuilder = new BodyBuilder
        {
            TextBody = emailContent
        };

        // Attach CV if provided and file exists
        if (!string.IsNullOrEmpty(cvFilePath) && File.Exists(cvFilePath))
        {
            bodyBuilder.Attachments.Add(cvFilePath);
        }
        else
        {
            _logger.LogWarning("CV file not found: {CvPath}", cvFilePath);
        }

        message.Body = bodyBuilder.ToMessageBody();
        return message;
    }

    private async Task SendEmailAsync(MimeMessage message)
    {
        using var client = new SmtpClient();
        
        try
        {
            // Connect to SMTP server
            await client.ConnectAsync(_emailConfig.SmtpServer, _emailConfig.SmtpPort, 
                _emailConfig.EnableSsl ? SecureSocketOptions.StartTls : SecureSocketOptions.None);

            // Authenticate
            await client.AuthenticateAsync(_emailConfig.Email, _emailConfig.Password);

            // Send email
            await client.SendAsync(message);
            
            AnsiConsole.Markup($"[green]✅ Email sent to {message.To.First()}[/]");
            AnsiConsole.WriteLine();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send email");
            throw;
        }
        finally
        {
            await client.DisconnectAsync(true);
        }
    }

    private async Task SaveEmailAsDraftAsync(MimeMessage message, JobPosting job)
    {
        // Save email as .eml file in Drafts folder
        var draftsFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "JobHunter", "Drafts");
        Directory.CreateDirectory(draftsFolder);
        
        var fileName = $"{job.Company}_{job.Title}_{DateTime.Now:yyyyMMdd_HHmmss}.eml".Replace(" ", "_");
        var filePath = Path.Combine(draftsFolder, fileName);
        
        await using var stream = File.Create(filePath);
        await message.WriteToAsync(stream);
        
        AnsiConsole.Markup($"[blue]📧 Email draft saved: {fileName}[/]");
        AnsiConsole.WriteLine();
        
        _logger.LogInformation("Email draft saved to: {FilePath}", filePath);
    }

    public async Task<bool> TestConnectionAsync()
    {
        try
        {
            using var client = new SmtpClient();
            
            await client.ConnectAsync(_emailConfig.SmtpServer, _emailConfig.SmtpPort, 
                _emailConfig.EnableSsl ? SecureSocketOptions.StartTls : SecureSocketOptions.None);
            
            await client.AuthenticateAsync(_emailConfig.Email, _emailConfig.Password);
            await client.DisconnectAsync(true);
            
            AnsiConsole.Markup("[green]✅ Email connection test successful![/]");
            AnsiConsole.WriteLine();
            return true;
        }
        catch (Exception ex)
        {
            AnsiConsole.Markup($"[red]❌ Email connection failed: {ex.Message}[/]");
            AnsiConsole.WriteLine();
            _logger.LogError(ex, "Email connection test failed");
            return false;
        }
    }

    // Helper method to validate email configuration
    public bool ValidateEmailConfiguration()
    {
        var isValid = true;
        var errors = new List<string>();

        if (string.IsNullOrEmpty(_emailConfig.Email))
            errors.Add("Email address is required");

        if (string.IsNullOrEmpty(_emailConfig.Password))
            errors.Add("Email password is required");

        if (string.IsNullOrEmpty(_emailConfig.SmtpServer))
            errors.Add("SMTP server is required");

        if (_emailConfig.SmtpPort <= 0)
            errors.Add("Valid SMTP port is required");

        if (string.IsNullOrEmpty(_emailConfig.SenderName))
            errors.Add("Sender name is required");

        if (errors.Any())
        {
            isValid = false;
            AnsiConsole.Markup("[red]Email configuration errors:[/]");
            AnsiConsole.WriteLine();
            foreach (var error in errors)
            {
                AnsiConsole.Markup($"[red]• {error}[/]");
                AnsiConsole.WriteLine();
            }
        }

        return isValid;
    }

    // Method to get email provider settings
    public static EmailConfig GetProviderDefaults(EmailProvider provider)
    {
        return provider switch
        {
            EmailProvider.Gmail => new EmailConfig
            {
                SmtpServer = "smtp.gmail.com",
                SmtpPort = 587,
                EnableSsl = true,
                Provider = EmailProvider.Gmail
            },
            EmailProvider.Outlook => new EmailConfig
            {
                SmtpServer = "smtp-mail.outlook.com",
                SmtpPort = 587,
                EnableSsl = true,
                Provider = EmailProvider.Outlook
            },
            _ => new EmailConfig
            {
                SmtpPort = 587,
                EnableSsl = true,
                Provider = EmailProvider.Other
            }
        };
    }
}