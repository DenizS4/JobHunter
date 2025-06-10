using JobHunter.Models;

namespace JobHunter.Interfaces;

public interface IJobScrapingService
{
    Task<List<JobPosting>> SearchJobsAsync(UserConfiguration config);
    Task<List<JobPosting>> SearchLinkedInAsync(List<string> jobTitles);
    Task<List<JobPosting>> SearchKariyerAsync(List<string> jobTitles);
    Task<bool> ApplyViaEasyApplyAsync(JobPosting job, UserConfiguration config);
    Task<string> ExtractContactEmailAsync(string description);
}