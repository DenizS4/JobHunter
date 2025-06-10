using System.ComponentModel.DataAnnotations;

namespace JobHunter.Models;

public class JobPosting
{
    [Key]
    public int Id { get; set; }
    public string JobId { get; set; } = ""; // Unique ID from the platform
    public string Platform { get; set; } = ""; // LinkedIn, Kariyer, etc.
    public string Title { get; set; } = "";
    public string Company { get; set; } = "";
    public string Location { get; set; } = "";
    public string Description { get; set; } = "";
    public string JobUrl { get; set; } = "";
    public string ContactEmail { get; set; } = "";
    public bool HasEasyApply { get; set; }
    public DateTime PostedDate { get; set; }
    public DateTime ScrapedDate { get; set; } = DateTime.UtcNow;
    public bool IsApplied { get; set; }
    public DateTime? AppliedDate { get; set; }
    public ApplicationMethod ApplicationMethod { get; set; }
    public string? Notes { get; set; }
}