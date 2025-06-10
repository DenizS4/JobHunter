using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using JobHunter.DbContext;
using JobHunter.Interfaces;
using JobHunter.Services;
using JobHunter.Models;

namespace JobHunter;

class Program
{
    static async Task Main(string[] args)
    {
        var host = CreateHostBuilder(args).Build();
        
        var dbContext = host.Services.GetRequiredService<JobHunterDbContext>();
        await dbContext.Database.EnsureCreatedAsync();
        
        var app = host.Services.GetRequiredService<JobHunterApp>();
        await app.RunAsync();
    }

    static IHostBuilder CreateHostBuilder(string[] args) =>
        Host.CreateDefaultBuilder(args)
            .ConfigureAppConfiguration((context, config) =>
            {
                config.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);
                config.AddJsonFile("questions.json", optional: false, reloadOnChange: true);
            })
            .ConfigureServices((context, services) =>
            {
                services.Configure<AppConfig>(context.Configuration.GetSection("AppConfig"));
                services.Configure<EmailConfig>(context.Configuration.GetSection("EmailConfig"));
                services.Configure<List<TemplateQuestion>>(context.Configuration.GetSection("TemplateQuestions"));
                
                services.AddDbContext<JobHunterDbContext>(options =>
                    options.UseSqlServer(context.Configuration.GetConnectionString("DefaultConnection")));
                
                services.AddScoped<IBrowserService, BrowserService>();
                services.AddScoped<IEmailService, EmailService>();
                services.AddScoped<IJobScrapingService, JobScrapingService>();
                services.AddScoped<IDatabaseService, DatabaseService>();
                services.AddScoped<JobHunterApp>();
            })
            .ConfigureLogging(logging =>
            {
                logging.ClearProviders();
                logging.AddConsole();
            });
}
