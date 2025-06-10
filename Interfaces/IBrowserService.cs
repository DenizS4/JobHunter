using Microsoft.Playwright;

namespace JobHunter.Interfaces;

public interface IBrowserService
{
    Task InitializeAsync(bool headless = true);
    Task<IPage> GetPageAsync();
    Task ScrollToBottomAsync();
    Task NavigateToAsync(string url);
    Task<bool> IsElementVisibleAsync(string selector, int timeoutMs = 5000);
    Task<string> GetTextAsync(string selector);
    Task ClickAsync(string selector);
    Task TypeAsync(string selector, string text);
    Task ClearAsync(string selector); 
    Task UploadFileAsync(string selector, string filePath);  
    Task<bool> HandleCaptchaIfPresentAsync();
    Task DisposeAsync();
    Task ScrollWithinNextSiblingAsync(string headerSelector);
}