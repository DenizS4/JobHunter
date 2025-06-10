using JobHunter.Models;

namespace JobHunter.Interfaces;

public interface IDatabaseService
{
    Task<List<JobPosting>> FilterAlreadyAppliedJobsAsync(List<JobPosting> jobs);
    Task MarkJobAsAppliedAsync(JobPosting job);
    Task SaveJobPostingAsync(JobPosting job);
    Task<JobPosting?> GetJobByPlatformIdAsync(string jobId, string platform);
    Task<List<JobPosting>> GetRecentApplicationsAsync(int days = 7);
}