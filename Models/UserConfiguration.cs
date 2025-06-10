namespace JobHunter.Models;

public class UserConfiguration
{
    
    public List<string> Platforms { get; set; } = new();
    public List<string> JobTitles { get; set; } = new();
    public string CvFilePath { get; set; } = "";
    public EmailMode EmailMode { get; set; }
    public bool ShowBrowser { get; set; }
    public Dictionary<string, string> TemplateAnswers { get; set; } = new();
}