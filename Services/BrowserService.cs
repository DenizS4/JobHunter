using JobHunter.Interfaces;
using JobHunter.Models;
using Microsoft.Playwright;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Spectre.Console;

namespace JobHunter.Services;
public class BrowserService : IBrowserService
{
    private readonly ILogger<BrowserService> _logger;
    private readonly AppConfig _config;
    private IPlaywright? _playwright;
    private IBrowser? _browser;
    private IPage? _page;
    private bool _isInitialized = false;

    public BrowserService(ILogger<BrowserService> logger, IOptions<AppConfig> config)
    {
        _logger = logger;
        _config = config.Value;
    }

    public async Task InitializeAsync(bool headless = true)
    {
        try
        {
            _playwright = await Playwright.CreateAsync();
            
            var browserOptions = new BrowserTypeLaunchOptions
            {
                Headless = headless,
                SlowMo = _config.DelayBetweenActions / 2, // Slow down actions for stability
                Args = new[] { 
                    "--disable-blink-features=AutomationControlled",
                    "--disable-web-security",
                    "--disable-features=VizDisplayCompositor"
                }
            };

            _browser = await _playwright.Chromium.LaunchAsync(browserOptions);
            
            var context = await _browser.NewContextAsync(new BrowserNewContextOptions
            {
                UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36",
                ViewportSize = new ViewportSize { Width = 1920, Height = 1080 }
            });

            _page = await context.NewPageAsync();
            _isInitialized = true;
            
            _logger.LogInformation("Browser initialized successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize browser");
            throw;
        }
    }

    public async Task<IPage> GetPageAsync()
    {
        if (!_isInitialized || _page == null)
            throw new InvalidOperationException("Browser not initialized. Call InitializeAsync first.");
        
        return _page;
    }

    public async Task NavigateToAsync(string url)
    {
        if (_page == null) throw new InvalidOperationException("Browser not initialized");
        
        try
        {
            await _page.GotoAsync(url, new PageGotoOptions
            {
                WaitUntil = WaitUntilState.Load,
                Timeout = 30000 
            });
            await Task.Delay(_config.DelayBetweenActions);
            
            // Check for captcha after navigation
            //await HandleCaptchaIfPresentAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to navigate to {Url}", url);
            throw;
        }
    }

    public async Task<bool> IsElementVisibleAsync(string selector, int timeoutMs = 5000)
    {
        if (_page == null) return false;
        
        try
        {
            await _page.WaitForSelectorAsync(selector, new PageWaitForSelectorOptions 
            { 
                Timeout = timeoutMs,
                State = WaitForSelectorState.Visible 
            });
            return true;
        }
        catch (TimeoutException)
        {
            return false;
        }
    }

    public async Task<string> GetTextAsync(string selector)
    {
        if (_page == null) throw new InvalidOperationException("Browser not initialized");
        
        try
        {
            var element = await _page.WaitForSelectorAsync(selector);
            return await element.TextContentAsync() ?? "";
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get text from selector: {Selector}", selector);
            return "";
        }
    }

    public async Task ClickAsync(string selector)
    {
        if (_page == null) throw new InvalidOperationException("Browser not initialized");
        
        try
        {
            await _page.WaitForSelectorAsync(selector, new PageWaitForSelectorOptions { State = WaitForSelectorState.Visible });
            await _page.ClickAsync(selector);
            await Task.Delay(_config.DelayBetweenActions);
            
            // Check for captcha after clicking
            await HandleCaptchaIfPresentAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to click element: {Selector}", selector);
            throw;
        }
    }

    public async Task TypeAsync(string selector, string text)
    {
        if (_page == null) throw new InvalidOperationException("Browser not initialized");
        
        try
        {
            await _page.WaitForSelectorAsync(selector);
            await _page.FillAsync(selector, text);
            await Task.Delay(_config.DelayBetweenActions / 2);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to type in element: {Selector}", selector);
            throw;
        }
    }
    public async Task ClearAsync(string selector)
    {
        if (_page == null) throw new InvalidOperationException("Browser not initialized");
    
        try
        {
            await _page.WaitForSelectorAsync(selector);
            await _page.FillAsync(selector, ""); // Clear by filling with empty string
            await Task.Delay(_config.DelayBetweenActions / 2);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to clear element: {Selector}", selector);
            throw;
        }
    }

    public async Task UploadFileAsync(string selector, string filePath)
    {
        if (_page == null) throw new InvalidOperationException("Browser not initialized");
    
        try
        {
            if (!File.Exists(filePath))
            {
                _logger.LogWarning("File not found: {FilePath}", filePath);
                return;
            }
        
            await _page.WaitForSelectorAsync(selector);
            await _page.SetInputFilesAsync(selector, filePath);
            await Task.Delay(_config.DelayBetweenActions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to upload file to element: {Selector}", selector);
            throw;
        }
    }
    public async Task<bool> HandleCaptchaIfPresentAsync()
    {
        if (_page == null) return false;

        // Common captcha selectors
        var captchaSelectors = new[]
        {
            "iframe[src*='recaptcha']",
            ".g-recaptcha",
            "#captcha",
            "[data-testid*='captcha']",
            ".captcha",
            "iframe[title*='reCAPTCHA']",
            ".challenge-form"
        };

        foreach (var selector in captchaSelectors)
        {
            if (await IsElementVisibleAsync(selector, 1000))
            {
                AnsiConsole.WriteLine();
                AnsiConsole.Markup("[red]🤖 CAPTCHA detected![/]");
                AnsiConsole.WriteLine();
                AnsiConsole.Markup("[yellow]Please solve the CAPTCHA manually in the browser window.[/]");
                AnsiConsole.WriteLine();
                
                // Wait for user to solve captcha
                var continueWaiting = true;
                while (continueWaiting)
                {
                    AnsiConsole.Markup("[blue]Press ENTER when you've solved the CAPTCHA...[/]");
                    Console.ReadLine();
                    
                    // Check if captcha is still present
                    var stillPresent = false;
                    foreach (var captchaSelector in captchaSelectors)
                    {
                        if (await IsElementVisibleAsync(captchaSelector, 1000))
                        {
                            stillPresent = true;
                            break;
                        }
                    }
                    
                    if (!stillPresent)
                    {
                        continueWaiting = false;
                        AnsiConsole.Markup("[green]✅ CAPTCHA solved! Continuing...[/]");
                        AnsiConsole.WriteLine();
                    }
                    else
                    {
                        AnsiConsole.Markup("[yellow]CAPTCHA still present. Please try again.[/]");
                        AnsiConsole.WriteLine();
                    }
                }
                
                return true;
            }
        }

        return false;
    }

    public async Task DisposeAsync()
    {
        try
        {
            if (_page != null)
            {
                await _page.CloseAsync();
                _page = null;
            }
            
            if (_browser != null)
            {
                await _browser.CloseAsync();
                _browser = null;
            }
            
            _playwright?.Dispose();
            _playwright = null;
            
            _isInitialized = false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error disposing browser");
        }
    }
    public async Task ScrollWithinNextSiblingAsync(string headerSelector)
    {
        if (_page == null) return;
    
        await _page.EvaluateAsync($@"
        const header = document.querySelector('{headerSelector}');
        if (header && header.nextElementSibling) {{
            const jobsContainer = header.nextElementSibling;
            
            // Get all currently visible job cards
            const jobCards = 
            jobsContainer.querySelectorAll('[data-job-id]');
            if (jobCards.length > 0) {{
                // Calculate total height of visible job cards
                let totalJobsHeight = 0;
                jobCards.forEach(card => {{
                    totalJobsHeight += card.offsetHeight;
                }});
                
                // Scroll by the height of current visible jobs
                jobsContainer.scrollTop += totalJobsHeight;
            }} else {{
                // Fallback: scroll by container height
                jobsContainer.scrollTop += jobsContainer.clientHeight;
            }}
        }}
    ");
        await Task.Delay(1000);
    }
    // Helper methods for common browser operations
    public async Task ScrollToBottomAsync()
    {
        if (_page == null) return;
        
        await _page.EvaluateAsync("window.scrollTo(0, document.body.scrollHeight)");
        await Task.Delay(1000);
    }

    public async Task WaitForPageLoadAsync()
    {
        if (_page == null) return;
        
        await _page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await Task.Delay(_config.DelayBetweenActions);
    }

    public async Task<List<string>> GetAllLinksAsync(string containerSelector = "body")
    {
        if (_page == null) return new List<string>();
        
        try
        {
            var links = await _page.EvalOnSelectorAllAsync<string[]>($"{containerSelector} a[href]", 
                "elements => elements.map(el => el.href)");
            
            return links?.ToList() ?? new List<string>();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get links from container: {Container}", containerSelector);
            return new List<string>();
        }
    }
}