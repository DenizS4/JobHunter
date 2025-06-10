using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using JobHunter.Models;
using Spectre.Console;
using System.Text.RegularExpressions;
using System.Web;
using JobHunter.Interfaces;

namespace JobHunter.Services;

public class JobScrapingService : IJobScrapingService
{
    private readonly IBrowserService _browserService;
    private readonly ILogger<JobScrapingService> _logger;
    private readonly AppConfig _config;
    private readonly List<TemplateQuestion> _templateQuestions;

    public JobScrapingService(
        IBrowserService browserService, 
        ILogger<JobScrapingService> logger,
        IOptions<AppConfig> config,
        IOptions<List<TemplateQuestion>> templateQuestions)
    {
        _browserService = browserService;
        _logger = logger;
        _config = config.Value;
        _templateQuestions = templateQuestions.Value;
    }

    public async Task<List<JobPosting>> SearchJobsAsync(UserConfiguration config)
    {
        var allJobs = new List<JobPosting>();

        foreach (var platform in config.Platforms)
        {
            try
            {
                AnsiConsole.Markup($"[blue]🔍 Searching on {platform}...[/]");
                AnsiConsole.WriteLine();

                var jobs = platform.ToLower() switch
                {
                    "linkedin" => await SearchLinkedInAsync(config.JobTitles),
                    "kariyer.net" => await SearchKariyerAsync(config.JobTitles),
                    _ => new List<JobPosting>()
                };

                allJobs.AddRange(jobs);
                AnsiConsole.Markup($"[green]✅ Found {jobs.Count} jobs on {platform}[/]");
                AnsiConsole.WriteLine();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching on platform: {Platform}", platform);
                AnsiConsole.Markup($"[red]❌ Error searching {platform}: {ex.Message}[/]");
                AnsiConsole.WriteLine();
            }
        }

        return allJobs;
    }

    public async Task<List<JobPosting>> SearchLinkedInAsync(List<string> jobTitles)
    {
        var jobs = new List<JobPosting>();
        
        try
        {
            // Login to LinkedIn
            await LoginToLinkedInAsync();
            
            foreach (var jobTitle in jobTitles)
            {
                AnsiConsole.Markup($"[yellow]Searching LinkedIn for: {jobTitle}[/]");
                AnsiConsole.WriteLine();
                
                var jobsForTitle = await SearchLinkedInJobTitleAsync(jobTitle);
                jobs.AddRange(jobsForTitle);
                
                // Rate limiting
                await Task.Delay(_config.DelayBetweenActions);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching LinkedIn");
            throw;
        }

        return jobs;
    }

    public async Task<List<JobPosting>> SearchKariyerAsync(List<string> jobTitles)
    {
        var jobs = new List<JobPosting>();
        
        try
        {
            foreach (var jobTitle in jobTitles)
            {
                AnsiConsole.Markup($"[yellow]Searching Kariyer.net for: {jobTitle}[/]");
                AnsiConsole.WriteLine();
                
                var jobsForTitle = await SearchKariyerJobTitleAsync(jobTitle);
                jobs.AddRange(jobsForTitle);
                
                // Rate limiting
                await Task.Delay(_config.DelayBetweenActions);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching Kariyer.net");
            throw;
        }

        return jobs;
    }

    public async Task<bool> ApplyViaEasyApplyAsync(JobPosting job, UserConfiguration config)
    {
        try
        {
            if (!job.HasEasyApply)
            {
                _logger.LogWarning("Job does not support Easy Apply: {JobTitle} at {Company}", job.Title, job.Company);
                return false;
            }

            // Navigate to job page
            await _browserService.NavigateToAsync(job.JobUrl);
            await Task.Delay(2000);

            // Click Easy Apply button
            var easyApplySelectors = new[]
            {
                ".jobs-apply-button--top-card",
                ".jobs-apply-button",
                "[data-control-name='jobdetails_topcard_inapply']"
            };

            bool clicked = false;
            foreach (var selector in easyApplySelectors)
            {
                if (await _browserService.IsElementVisibleAsync(selector, 2000))
                {
                    await _browserService.ClickAsync(selector);
                    clicked = true;
                    break;
                }
            }

            if (!clicked)
            {
                _logger.LogWarning("Could not find Easy Apply button for job: {JobTitle}", job.Title);
                return false;
            }

            await Task.Delay(3000);

            // Handle multi-step application process
            var maxSteps = 10;
            var currentStep = 0;

            while (currentStep < maxSteps)
            {
                // Check if application is complete
                if (await _browserService.IsElementVisibleAsync(".artdeco-modal__header h2", 2000))
                {
                    var headerText = await _browserService.GetTextAsync(".artdeco-modal__header h2");
                    if (headerText.Contains("Application sent") || headerText.Contains("Your application was sent"))
                    {
                        AnsiConsole.Markup($"[green]✅ Successfully applied to {job.Title} at {job.Company}[/]");
                        AnsiConsole.WriteLine();
                        return true;
                    }
                }

                // Fill form fields
                await FillApplicationFormAsync(config);

                // Try to proceed to next step
                var nextButtonSelectors = new[]
                {
                    "button[aria-label='Continue to next step']",
                    "button[aria-label='Review your application']",
                    "button[aria-label='Submit application']",
                    ".jobs-easy-apply-modal footer button[data-control-name='continue_unify']"
                };

                bool proceeded = false;
                foreach (var selector in nextButtonSelectors)
                {
                    if (await _browserService.IsElementVisibleAsync(selector, 2000))
                    {
                        await _browserService.ClickAsync(selector);
                        proceeded = true;
                        await Task.Delay(2000);
                        break;
                    }
                }

                if (!proceeded)
                {
                    // Try generic submit/continue buttons
                    if (await _browserService.IsElementVisibleAsync("button[type='submit']", 2000))
                    {
                        await _browserService.ClickAsync("button[type='submit']");
                        await Task.Delay(2000);
                    }
                    else
                    {
                        _logger.LogWarning("Could not find next step button for job application: {JobTitle}", job.Title);
                        break;
                    }
                }

                currentStep++;
            }

            _logger.LogWarning("Application process incomplete for job: {JobTitle}", job.Title);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error applying to job: {JobTitle} at {Company}", job.Title, job.Company);
            return false;
        }
    }

    public async Task<string> ExtractContactEmailAsync(string description)
    {
        try
        {
            
            //var companySection = await _browserService.GetTextAsync(".job-details-jobs-unified-top-card__company-name, .jobs-unified-top-card__company-name");

            // Extract email using regex
            var emailPattern = @"\b[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Z|a-z]{2,}\b";
            var emailMatches = Regex.Matches($"{description}", emailPattern);

            if (emailMatches.Count > 0)
            {
                return emailMatches[0].Value;
            }

            // // Try to find contact information section
            // var contactSelectors = new[]
            // {
            //     "[data-test-id='contact-info']",
            //     ".contact-info",
            //     ".job-details-contact-info"
            // };
            //
            // foreach (var selector in contactSelectors)
            // {
            //     if (await _browserService.IsElementVisibleAsync(selector, 2000))
            //     {
            //         var contactText = await _browserService.GetTextAsync(selector);
            //         var contactMatches = Regex.Matches(contactText, emailPattern);
            //         if (contactMatches.Count > 0)
            //         {
            //             return contactMatches[0].Value;
            //         }
            //     }
            // }

            return string.Empty;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error extracting contact email");
            return string.Empty;
        }
    }

    #region Private LinkedIn Methods

    private async Task LoginToLinkedInAsync()
    {
        await _browserService.NavigateToAsync("https://www.linkedin.com/login");
        
        // Check if already logged in
        if (await _browserService.IsElementVisibleAsync("[data-test-id='nav-top-logo']", 3000))
        {
            AnsiConsole.Markup("[green]Already logged in to LinkedIn[/]");
            AnsiConsole.WriteLine();
            return;
        }

        // Login process
        await _browserService.TypeAsync("#username", _config.LinkedInEmail);
        await _browserService.TypeAsync("#password", _config.LinkedInPassword);
        await _browserService.ClickAsync("button[type='submit']");
        
        // Wait for login to complete
        await Task.Delay(3000);
        
        // Handle potential security check
        if (await _browserService.IsElementVisibleAsync("input[name='pin']", 5000))
        {
            AnsiConsole.Markup("[yellow]LinkedIn security check detected. Please complete verification manually.[/]");
            AnsiConsole.WriteLine();
            AnsiConsole.Ask<string>("Press ENTER when verification is complete...");
        }
        
        AnsiConsole.Markup("[green]✅ LinkedIn login successful[/]");
        AnsiConsole.WriteLine();
    }
    private async Task<List<JobPosting>> SearchLinkedInJobTitleAsync(string jobTitle)
    {
        var jobs = new List<JobPosting>();
    
        // Navigate to jobs search
        var searchUrl = $"https://www.linkedin.com/jobs/search/?keywords={HttpUtility.UrlEncode(jobTitle)}&location=Turkey&f_TPR=r604800"; // Last week
        await _browserService.NavigateToAsync(searchUrl);
    
        // Wait for results to load
        await Task.Delay(3000);
    
        var maxJobs = Math.Min(_config.MaxJobsPerSession, 25); // LinkedIn shows 25 per page
    
        // First, collect all available job IDs by scrolling
        var allJobIds = await CollectAllJobIdsAsync(maxJobs);
    
        // Then process each job ID
        foreach (var jobId in allJobIds.Take(maxJobs))
        {
            try
            {
                var jobUrl = $"https://www.linkedin.com/jobs/search/?currentJobId={jobId}&keywords={HttpUtility.UrlEncode(jobTitle)}&location=Turkey&f_TPR=r604800";
                await _browserService.NavigateToAsync(jobUrl);
                var job = await ExtractLinkedInJobDetailsAsync(jobId);
                if (job != null)
                {
                    jobs.Add(job);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error extracting LinkedIn job details for ID {JobId}", jobId);
            }
        }
    
        return jobs;
    }

    private async Task<List<string>> CollectAllJobIdsAsync(int maxJobs)
    {
        var allJobIds = new HashSet<string>(); // Use HashSet to avoid duplicates
        var previousCount = 0;
        var noNewJobsCount = 0;
        const int maxRetries = 5; // Prevent infinite loops
    
        while (allJobIds.Count < maxJobs && noNewJobsCount < maxRetries)
        {
            // Extract current job IDs
            var currentJobIds = await ExtractJobIdsFromSearchPageAsync();
        
            // Add new IDs to our collection
            foreach (var jobId in currentJobIds)
            {
                allJobIds.Add(jobId);
            }
        
            // Check if we got new jobs
            if (allJobIds.Count == previousCount)
            {
                noNewJobsCount++;
            }
            else
            {
                noNewJobsCount = 0; // Reset counter if we found new jobs
            }
        
            previousCount = allJobIds.Count;
        
            _logger.LogInformation("Collected {Count} job IDs so far", allJobIds.Count);
        
            // Try to load more jobs if we haven't reached our limit
            if (allJobIds.Count < maxJobs)
            {
                await ScrollAndLoadMoreLinkedInJobsAsync();
                await Task.Delay(2000); // Give time for new jobs to load
            }
        }
    
        return allJobIds.ToList();
    }
    private async Task<List<string>> GetLinkedInJobCardsAsync()
    {
        var page = await _browserService.GetPageAsync();
        
        try
        {
            var jobCardSelectors = await page.QuerySelectorAllAsync(".jobs-search__results-list .result-card, .jobs-search-results__list-item");
            
            var jobCards = new List<string>();
            for (int i = 0; i < jobCardSelectors.Count; i++)
            {
                jobCards.Add($".jobs-search__results-list .result-card:nth-child({i + 1}), .jobs-search-results__list-item:nth-child({i + 1})");
            }
            
            return jobCards;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error getting LinkedIn job cards");
            return new List<string>();
        }
    }
    private async Task<List<string>> GetLinkedInJobCardsByDataAttributeAsync()
    {
        var page = await _browserService.GetPageAsync();

        try
        {
            // Using data-job-id attribute which is more reliable
            var jobCardElements = await page.QuerySelectorAllAsync("[data-job-id]");
    
            var jobCards = new List<string>();
        
            // Get the actual data-job-id values to create more specific selectors
            for (int i = 0; i < jobCardElements.Count; i++)
            {
                var jobId = await jobCardElements[i].GetAttributeAsync("data-job-id");
                if (!string.IsNullOrEmpty(jobId))
                {
                    jobCards.Add($"[data-job-id='{jobId}']");
                }
            }
    
            return jobCards;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error getting LinkedIn job cards by data attribute");
            return new List<string>();
        }
    }
    private async Task<List<string>> ExtractJobIdsFromSearchPageAsync()
    {
        var jobIds = new List<string>();
    
        try
        {
            var page = await _browserService.GetPageAsync();
        
            // Get job IDs from data-job-id attributes
            var jobElements = await page.QuerySelectorAllAsync("[data-job-id]");
        
            foreach (var element in jobElements)
            {
                var jobId = await element.GetAttributeAsync("data-job-id");
                if (!string.IsNullOrEmpty(jobId) && !jobIds.Contains(jobId))
                {
                    jobIds.Add(jobId);
                }
            }
        
            // Alternative: Extract from URLs in href attributes
            if (!jobIds.Any())
            {
                var linkElements = await page.QuerySelectorAllAsync("a[href*='/jobs/view/']");
                foreach (var link in linkElements)
                {
                    var href = await link.GetAttributeAsync("href");
                    if (!string.IsNullOrEmpty(href))
                    {
                        var match = Regex.Match(href, @"jobs/view/(\d+)");
                        if (match.Success && !jobIds.Contains(match.Groups[1].Value))
                        {
                            jobIds.Add(match.Groups[1].Value);
                        }
                    }
                }
            }
        
            _logger.LogInformation("Found {Count} job IDs on search page", jobIds.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error extracting job IDs from search page");
        }
    
        return jobIds;
    }
    private async Task<JobPosting?> ExtractLinkedInJobDetailsAsync(string jobId)
{
    try
    {
        
        var job = new JobPosting
        {
            Platform = "LinkedIn",
            ScrapedDate = DateTime.UtcNow,
            JobId = jobId
        };
        
        // Extract job details from the job details panel (not the card)
        job.Title = await _browserService.GetTextAsync(".job-details-jobs-unified-top-card__job-title h1, .jobs-unified-top-card__job-title a");
        job.Company = await _browserService.GetTextAsync(".job-details-jobs-unified-top-card__company-name a, .jobs-unified-top-card__company-name a");
        job.Location = await _browserService.GetTextAsync(".job-details-jobs-unified-top-card__tertiary-description-container .tvm__text--low-emphasis:first-child"); 
        
        // Get job URL from current page or construct it
        job.JobUrl = $"https://www.linkedin.com/jobs/view/{jobId}";
        
        // Extract description
        job.Description = await _browserService.GetTextAsync(".job-details-jobs-unified-top-card__job-description, .jobs-description__content .jobs-box__html-content");
        
        // Check for Easy Apply
        job.HasEasyApply = await _browserService.IsElementVisibleAsync(".jobs-apply-button--top-card, .jobs-apply-button[data-control-name*='apply']", 2000);
        
        // Try to extract contact email
        if(job?.Description != null)
            job.ContactEmail = await ExtractContactEmailAsync(job.Description);
        
        return job;
    }
    catch (Exception ex)
    {
        _logger.LogWarning("Failed to extract essential job data for ID: {JobId}", jobId);
        return null;
    }
}
    private async Task ScrollAndLoadMoreLinkedInJobsAsync()
    {
        // Find the header element and then get its next sibling (the job list container)
        var headerSelector = ".scaffold-layout__list-header.jobs-search-results-list__header--blue";
    
        if (await _browserService.IsElementVisibleAsync(headerSelector, 1000))
        {
            await _browserService.ScrollWithinNextSiblingAsync(headerSelector);
            await Task.Delay(2000);
        } 
    }
    

    private string ExtractLinkedInJobId(string jobUrl)
    {
        var match = Regex.Match(jobUrl, @"jobs/view/(\d+)");
        return match.Success ? match.Groups[1].Value : string.Empty;
    }

    #endregion

    #region Private Kariyer.net Methods

    private async Task<List<JobPosting>> SearchKariyerJobTitleAsync(string jobTitle)
    {
        var jobs = new List<JobPosting>();
        
        try
        {
            // Navigate to Kariyer.net search
            var searchUrl = $"https://www.kariyer.net/is-ilanlari?q={HttpUtility.UrlEncode(jobTitle)}";
            await _browserService.NavigateToAsync(searchUrl);
            await Task.Delay(3000);
            
            var processedCount = 0;
            var maxJobs = Math.Min(_config.MaxJobsPerSession, 20);
            
            while (processedCount < maxJobs)
            {
                var jobCards = await GetKariyerJobCardsAsync();
                
                if (!jobCards.Any())
                    break;
                    
                foreach (var jobCard in jobCards.Take(maxJobs - processedCount))
                {
                    try
                    {
                        var job = await ExtractKariyerJobDetailsAsync(jobCard);
                        if (job != null)
                        {
                            jobs.Add(job);
                            processedCount++;
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Error extracting Kariyer.net job details");
                    }
                }
                
                // Try to load next page or more jobs
                if (processedCount < maxJobs && !await LoadMoreKariyerJobsAsync())
                {
                    break;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching Kariyer.net for job title: {JobTitle}", jobTitle);
        }
        
        return jobs;
    }

    private async Task<List<string>> GetKariyerJobCardsAsync()
    {
        try
        {
            var page = await _browserService.GetPageAsync();
            var jobCardElements = await page.QuerySelectorAllAsync(".list-items .list-item, .job-list-item");
            
            var jobCards = new List<string>();
            for (int i = 0; i < jobCardElements.Count; i++)
            {
                jobCards.Add($".list-items .list-item:nth-child({i + 1}), .job-list-item:nth-child({i + 1})");
            }
            
            return jobCards;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error getting Kariyer.net job cards");
            return new List<string>();
        }
    }

    private async Task<JobPosting?> ExtractKariyerJobDetailsAsync(string jobCardSelector)
    {
        try
        {
            // Click on job card to get details
            await _browserService.ClickAsync($"{jobCardSelector} a, {jobCardSelector} .job-title");
            await Task.Delay(2000);
            
            var job = new JobPosting
            {
                Platform = "Kariyer.net",
                ScrapedDate = DateTime.UtcNow
            };
            
            // Extract job details
            job.Title = await _browserService.GetTextAsync("h1.job-title, .job-detail-title");
            job.Company = await _browserService.GetTextAsync(".company-name, .job-company-name");
            job.Location = await _browserService.GetTextAsync(".job-location, .location");
            
            // Get job URL
            var page = await _browserService.GetPageAsync();
            job.JobUrl = page.Url;
            job.JobId = ExtractKariyerJobId(job.JobUrl);
            
            // Extract description
            job.Description = await _browserService.GetTextAsync(".job-description, .job-detail-content");
            
            // Kariyer.net doesn't have easy apply, so set to false
            job.HasEasyApply = false;
            
            // Try to extract contact email
            if(job?.Description != null)
                job.ContactEmail = await ExtractContactEmailAsync(job.Description);
            
            return job;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error extracting Kariyer.net job details from selector: {Selector}", jobCardSelector);
            return null;
        }
    }

    private async Task<bool> LoadMoreKariyerJobsAsync()
    {
        try
        {
            // Look for pagination or load more button
            if (await _browserService.IsElementVisibleAsync(".pagination .next, .load-more", 2000))
            {
                await _browserService.ClickAsync(".pagination .next, .load-more");
                await Task.Delay(3000);
                return true;
            }
            
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error loading more Kariyer.net jobs");
            return false;
        }
    }

    private string ExtractKariyerJobId(string jobUrl)
    {
        var match = Regex.Match(jobUrl, @"ilan/(\d+)");
        return match.Success ? match.Groups[1].Value : string.Empty;
    }

    #endregion

    #region Private Application Methods

    private async Task FillApplicationFormAsync(UserConfiguration config)
    {
        try
        {
            // Handle resume upload (if required and path is provided)
            if (!string.IsNullOrEmpty(config.CvFilePath) && 
                await _browserService.IsElementVisibleAsync("input[type='file']", 2000))
            {
                await _browserService.UploadFileAsync("input[type='file']", config.CvFilePath);
                await Task.Delay(2000);
            }
            
            // Handle template questions using TemplateAnswers
            await AnswerTemplateQuestionsAsync(config);
            
            // Handle checkboxes and agreements
            await HandleCheckboxesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error filling application form");
        }
    }

    private async Task FillTextFieldIfExists(string selector, string value)
    {
        if (string.IsNullOrEmpty(value))
            return;
            
        try
        {
            if (await _browserService.IsElementVisibleAsync(selector, 1000))
            {
                await _browserService.ClearAsync(selector);
                await _browserService.TypeAsync(selector, value);
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Could not fill field with selector: {Selector}", selector);
        }
    }

     private async Task AnswerTemplateQuestionsAsync(UserConfiguration config)
    {
        try
        {
            // Look for common question patterns and answer them using template questions
            var questionSelectors = new[]
            {
                ".jobs-easy-apply-form-section__grouping",
                ".fb-single-line-text",
                ".fb-dropdown",
                ".application-question"
            };
            
            foreach (var selector in questionSelectors)
            {
                var elements = await _browserService.GetPageAsync();
                var questionElements = await elements.QuerySelectorAllAsync(selector);
                
                foreach (var element in questionElements)
                {
                    try
                    {
                        var questionText = await element.TextContentAsync();
                        if (string.IsNullOrEmpty(questionText))
                            continue;
                            
                        // First try to match with user's template answers
                        var userAnswer = config.TemplateAnswers.FirstOrDefault(ta => 
                            questionText.ToLower().Contains(ta.Key.ToLower()));
                            
                        string answerToUse = null;
                        
                        if (!userAnswer.Equals(default(KeyValuePair<string, string>)))
                        {
                            answerToUse = userAnswer.Value;
                        }
                        else
                        {
                            // Fallback to predefined template questions
                            var matchingTemplate = _templateQuestions.FirstOrDefault(tq => 
                                tq.Keywords.Any(keyword => questionText.ToLower().Contains(keyword.ToLower())));
                            
                            if (matchingTemplate != null)
                            {
                                // Check if user has provided an answer for this template question
                                if (config.TemplateAnswers.ContainsKey(matchingTemplate.Key))
                                {
                                    answerToUse = config.TemplateAnswers[matchingTemplate.Key];
                                }
                                else if (matchingTemplate.Options.Any())
                                {
                                    // For select type, use first option as default
                                    answerToUse = matchingTemplate.Options.First();
                                }
                            }
                        }
                        
                        if (!string.IsNullOrEmpty(answerToUse))
                        {
                            var inputElement = await element.QuerySelectorAsync("input, select, textarea");
                            if (inputElement != null)
                            {
                                var tagName = await inputElement.EvaluateAsync<string>("el => el.tagName.toLowerCase()");
                                
                                if (tagName == "select")
                                {
                                    await inputElement.SelectOptionAsync(answerToUse);
                                }
                                else
                                {
                                    await inputElement.FillAsync(answerToUse);
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogDebug(ex, "Error answering template question");
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error answering template questions");
        }
    }

    private async Task HandleCheckboxesAsync()
    {
        try
        {
            // Handle common agreement checkboxes
            var checkboxSelectors = new[]
            {
                "input[type='checkbox'][required]",
                "input[type='checkbox'][name*='agree']",
                "input[type='checkbox'][name*='terms']",
                "input[type='checkbox'][name*='privacy']"
            };
            
            foreach (var selector in checkboxSelectors)
            {
                if (await _browserService.IsElementVisibleAsync(selector, 1000))
                {
                    var isChecked = await _browserService.GetPageAsync();
                    var checkbox = await isChecked.QuerySelectorAsync(selector);
                    if (checkbox != null && !await checkbox.IsCheckedAsync())
                    {
                        await _browserService.ClickAsync(selector);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error handling checkboxes");
        }
    }

    #endregion
}