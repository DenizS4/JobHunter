namespace JobHunter.Models;

public class TemplateQuestion
{
    public string Key { get; set; } = "";
    public string Question { get; set; } = "";
    public string Type { get; set; } = "text"; // text, select, boolean
    public List<string> Options { get; set; } = new();
    public List<string> Keywords { get; set; } = new(); // Keywords to match on job sites
}