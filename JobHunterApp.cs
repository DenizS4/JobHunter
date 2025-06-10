using JobHunter.Interfaces;
using JobHunter.Models;
using Spectre.Console;

namespace JobHunter;

public class JobHunterApp
{
    private readonly IBrowserService _browserService;
    private readonly IEmailService _emailService;
    private readonly IJobScrapingService _jobScrapingService;
    private readonly IDatabaseService _databaseService;

    public JobHunterApp(
        IBrowserService browserService,
        IEmailService emailService,
        IJobScrapingService jobScrapingService,
        IDatabaseService databaseService)
    {
        _browserService = browserService;
        _emailService = emailService;
        _jobScrapingService = jobScrapingService;
        _databaseService = databaseService;
    }

    public async Task RunAsync()
    {
        AnsiConsole.Write(new FigletText("JobHunter").Centered().Color(Color.Blue));
        
        var config = await GetUserConfigurationAsync();
        
        await AnsiConsole.Status()
            .Start("Starting job search...", async ctx =>
            {
                ctx.Status("Initializing browser...");
                await _browserService.InitializeAsync(!config.ShowBrowser);
                
                ctx.Status("Searching for jobs...");
                var jobs = await _jobScrapingService.SearchJobsAsync(config);
                
                ctx.Status("Processing applications...");
                await ProcessJobApplicationsAsync(jobs, config);
                
                await _browserService.DisposeAsync();
            });
        
        AnsiConsole.WriteLine();
        AnsiConsole.Markup("[green]Job hunting session completed![/]");
    }

    private async Task<UserConfiguration> GetUserConfigurationAsync()
    {
        var config = new UserConfiguration();
        
        // Platform selection
        var platforms = AnsiConsole.Prompt(
            new MultiSelectionPrompt<string>()
                .Title("Select platforms to search:")
                .AddChoices("Linkedin", "Kariyer.net")
                .Required());
        config.Platforms = platforms.ToList();
        
        // Job titles
        config.JobTitles = AnsiConsole.Ask<string>("Enter job titles (comma separated):").Split(',').Select(x => x.Trim()).ToList();
        
        // CV file
        config.CvFilePath = AnsiConsole.Ask<string>("Enter CV file path:");
        
        // Email mode
        config.EmailMode = AnsiConsole.Prompt(
            new SelectionPrompt<EmailMode>()
                .Title("Email sending mode:")
                .AddChoices(EmailMode.Draft, EmailMode.AutoSend));
        
        // Browser visibility
        config.ShowBrowser = AnsiConsole.Confirm("Show browser during automation?");
        
        // Template questions will be loaded from JSON
        config.TemplateAnswers = await LoadTemplateAnswersAsync();
        
        return config;
    }

    private async Task<Dictionary<string, string>> LoadTemplateAnswersAsync()
    {
        // This will be loaded from questions.json and user will be prompted for answers
        var answers = new Dictionary<string, string>();
        
        // For now, let's ask basic questions
        answers["salary_expectation"] = AnsiConsole.Ask<string>("Salary expectation:");
        answers["experience_level"] = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title("Experience level:")
                .AddChoices("Entry Level", "Mid Level", "Senior Level", "Expert Level"));
        answers["currently_working"] = AnsiConsole.Confirm("Are you currently working?") ? "Yes" : "No";
        
        if (answers["currently_working"] == "Yes")
        {
            answers["notice_period"] = AnsiConsole.Ask<string>("Notice period (e.g., 2 weeks, 1 month):");
        }
        
        return answers;
    }

    private async Task ProcessJobApplicationsAsync(List<JobPosting> jobs, UserConfiguration config)
    {
        var processedJobs = await _databaseService.FilterAlreadyAppliedJobsAsync(jobs);
        
        AnsiConsole.WriteLine($"Found {jobs.Count} jobs, {processedJobs.Count} new jobs to process.");
        
        await AnsiConsole.Progress()
            .StartAsync(async ctx =>
            {
                var task = ctx.AddTask("[green]Processing applications[/]");
                task.MaxValue = processedJobs.Count;
                
                foreach (var job in processedJobs)
                {
                    task.Description = $"[green]Processing: {job.Title} at {job.Company}[/]";
                    
                    try
                    {
                        await ProcessSingleJobAsync(job, config);
                        await _databaseService.MarkJobAsAppliedAsync(job);
                    }
                    catch (Exception ex)
                    {
                        AnsiConsole.WriteException(ex);
                    }
                    
                    task.Increment(1);
                    await Task.Delay(2000); // Rate limiting
                }
            });
    }

    private async Task ProcessSingleJobAsync(JobPosting job, UserConfiguration config)
    {
        // Try email first, then easy apply
        if (!string.IsNullOrEmpty(job.ContactEmail))
        {
            await _emailService.SendJobApplicationEmailAsync(job, config);
        }
        else if (job.HasEasyApply)
        {
            await _jobScrapingService.ApplyViaEasyApplyAsync(job, config);
        }
        else
        {
            AnsiConsole.WriteLine($"[yellow]Skipping {job.Title} - No email or easy apply available[/]");
        }
    }
}