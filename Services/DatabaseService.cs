using JobHunter.DbContext;
using JobHunter.Interfaces;
using JobHunter.Models;
using Microsoft.EntityFrameworkCore;

namespace JobHunter.Services;

public class DatabaseService : IDatabaseService
{
    private readonly JobHunterDbContext _context;

    public DatabaseService(JobHunterDbContext context)
    {
        _context = context;
    }

    public async Task<List<JobPosting>> FilterAlreadyAppliedJobsAsync(List<JobPosting> jobs)
    {
        var existingJobIds = await _context.JobPostings
            .Where(j => j.IsApplied)
            .Select(j => new { j.JobId, j.Platform })
            .ToListAsync();

        var existingSet = existingJobIds.ToHashSet();

        return jobs.Where(job => !existingSet.Contains(new { job.JobId, job.Platform })).ToList();
    }

    public async Task MarkJobAsAppliedAsync(JobPosting job)
    {
        var existingJob = await GetJobByPlatformIdAsync(job.JobId, job.Platform);
        
        if (existingJob != null)
        {
            existingJob.IsApplied = true;
            existingJob.AppliedDate = DateTime.UtcNow;
        }
        else
        {
            job.IsApplied = true;
            job.AppliedDate = DateTime.UtcNow;
            _context.JobPostings.Add(job);
        }

        await _context.SaveChangesAsync();
    }

    public async Task SaveJobPostingAsync(JobPosting job)
    {
        var existing = await GetJobByPlatformIdAsync(job.JobId, job.Platform);
        
        if (existing == null)
        {
            _context.JobPostings.Add(job);
            await _context.SaveChangesAsync();
        }
    }

    public async Task<JobPosting?> GetJobByPlatformIdAsync(string jobId, string platform)
    {
        return await _context.JobPostings
            .FirstOrDefaultAsync(j => j.JobId == jobId && j.Platform == platform);
    }

    public async Task<List<JobPosting>> GetRecentApplicationsAsync(int days = 7)
    {
        var cutoffDate = DateTime.UtcNow.AddDays(-days);
        
        return await _context.JobPostings
            .Where(j => j.IsApplied && j.AppliedDate >= cutoffDate)
            .OrderByDescending(j => j.AppliedDate)
            .ToListAsync();
    }
}