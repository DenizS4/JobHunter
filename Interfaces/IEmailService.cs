using JobHunter.Models;

namespace JobHunter.Interfaces;

public interface IEmailService
{
    Task SendJobApplicationEmailAsync(JobPosting job, UserConfiguration config);
    Task<bool> TestConnectionAsync();
    string GenerateJobApplicationEmail(JobPosting job, UserConfiguration config);
}