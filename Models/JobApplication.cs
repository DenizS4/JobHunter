using System.ComponentModel.DataAnnotations;

namespace JobHunter.Models;

public class JobApplication
{
    [Key]
    public int Id { get; set; }
    public int JobPostingId { get; set; }
    public JobPosting JobPosting { get; set; } = null!;
    public DateTime AppliedDate { get; set; } = DateTime.UtcNow;
    public ApplicationMethod Method { get; set; }
    public bool IsSuccessful { get; set; }
    public string? ErrorMessage { get; set; }
    public string? EmailSubject { get; set; }
    public string? EmailBody { get; set; }
}