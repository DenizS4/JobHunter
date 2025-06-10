namespace JobHunter.Models;

public class AppConfig
{
    public string LinkedInEmail { get; set; } = "";
    public string LinkedInPassword { get; set; } = "";
    public string KariyerEmail { get; set; } = "";
    public string KariyerPassword { get; set; } = "";
    public int DelayBetweenActions { get; set; } = 2000;
    public int MaxJobsPerSession { get; set; } = 50;
}