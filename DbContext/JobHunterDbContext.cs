using JobHunter.Models;
using Microsoft.EntityFrameworkCore;

namespace JobHunter.DbContext;

public class JobHunterDbContext : Microsoft.EntityFrameworkCore.DbContext
{
    public JobHunterDbContext(DbContextOptions<JobHunterDbContext> options) : base(options)
    {
    }

    public DbSet<JobPosting> JobPostings { get; set; }
    public DbSet<JobApplication> JobApplications { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<JobPosting>(entity =>
        {
            entity.HasIndex(e => new { e.JobId, e.Platform }).IsUnique();
            entity.Property(e => e.JobId).HasMaxLength(100);
            entity.Property(e => e.Platform).HasMaxLength(50);
            entity.Property(e => e.Title).HasMaxLength(200);
            entity.Property(e => e.Company).HasMaxLength(100);
            entity.Property(e => e.Location).HasMaxLength(100);
            entity.Property(e => e.JobUrl).HasMaxLength(500);
            entity.Property(e => e.ContactEmail).HasMaxLength(100);
        });

        modelBuilder.Entity<JobApplication>(entity =>
        {
            entity.HasOne(e => e.JobPosting)
                .WithMany()
                .HasForeignKey(e => e.JobPostingId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}