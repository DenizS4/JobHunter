# JobHunter Automation App

An automated job application system that helps you search for jobs and apply to them automatically on multiple platforms including LinkedIn and Kariyer.net.

## ✨ Features

- 🔍 **Multi-Platform Job Search**: Search jobs on LinkedIn and Kariyer.net
- 🤖 **Automated Applications**: Apply using Easy Apply (LinkedIn) or email applications
- 📝 **Smart Cover Letters**: Generate personalized cover letters automatically
- 📊 **Application Tracking**: Track all applications in a local database
- 🛡️ **Duplicate Prevention**: Avoid applying to the same job twice
- 🎯 **Question Templates**: Handle common application questions with pre-configured answers
- ⏱️ **Rate Limiting**: Built-in delays to avoid being blocked by platforms

## 🖥️ System Requirements

- **Operating System**: Windows 10/11 (SQL Server LocalDB dependency)
- **.NET Runtime**: .NET 6.0 or higher
- **Database**: Microsoft SQL Server LocalDB
- **Browser**: Chrome (for Playwright automation)
- **Internet**: Active internet connection required

## 🚀 Installation

### 1. Prerequisites

Install the required dependencies:

```bash
# Install .NET 6.0 Runtime from Microsoft's website
# Ensure SQL Server LocalDB is installed (usually included with Visual Studio or SQL Server Express)
```

### 2. Download and Setup

1. Extract all files to a folder (e.g., `C:\JobHunter\`)
2. Open Command Prompt as Administrator in the JobHunter folder
3. Install Playwright browsers:

```bash
pwsh bin/playwright.ps1 install chromium
```

### 3. Prepare Your Files

- **CV/Resume**: Save as PDF and note the full path
- **Cover Letter Template**: Generated automatically (customizable)

## ⚙️ Configuration

### 1. Configure `appsettings.json`

Update your platform credentials and email settings:

```json
{
  "AppConfig": {
    "LinkedInEmail": "your-actual-linkedin-email@gmail.com",
    "LinkedInPassword": "your-linkedin-password",
    "KariyerEmail": "your-kariyer-email@gmail.com",
    "KariyerPassword": "your-kariyer-password",
    "DelayBetweenActions": 3000,
    "MaxJobsPerSession": 50
  },
  "EmailConfig": {
    "SmtpServer": "smtp.gmail.com",
    "SmtpPort": 587,
    "Email": "your-email@gmail.com",
    "Password": "your-gmail-app-password",
    "SenderName": "Your Full Name",
    "EnableSsl": true,
    "Provider": "Gmail"
  }
}
```

> **📧 Gmail Setup**: Enable 2FA and generate an "App Password" (not your regular password)

### 2. Configure `questions.json`

Template answers for common application questions (auto-configured during setup):

```json
{
  "TemplateQuestions": [
    {
      "Key": "salary_expectation",
      "Question": "What is your salary expectation?",
      "Type": "text",
      "Keywords": ["salary", "expected salary", "compensation"]
    }
  ]
}
```

## 🎯 Usage

### 1. Start the Application

```bash
# Double-click JobHunter.exe or run from command line:
dotnet JobHunter.dll
```

### 2. Interactive Setup

The app guides you through:

1. **Job Titles**: Enter positions you're seeking (e.g., "Software Developer", "C# Developer")
2. **Platforms**: Choose LinkedIn, Kariyer.net, or both
3. **CV Path**: Provide full path to your CV file
4. **Application Mode**:
   - Easy Apply Only
   - Email Applications
   - Both methods
5. **Template Questions**: Answer common questions for automatic responses

### 3. Automated Process

Once configured, the app will:
- Search for jobs on selected platforms
- Filter out previously applied positions
- Apply automatically or generate email applications
- Track all activities in the database
- Handle CAPTCHAs (pauses for manual solving)

## 🔧 Features in Detail

### Easy Apply (LinkedIn)
- Automatically fills LinkedIn Easy Apply forms
- Handles multi-step applications
- Uploads CV when required
- Uses template responses for questions

### Email Applications
- Extracts contact emails from job postings
- Generates personalized cover letters
- Sends emails directly or saves as drafts
- Automatically attaches CV

### Question Types Supported
- Salary expectations
- Experience level (Entry/Mid/Senior/Expert)
- Years of experience
- Current employment status
- Notice period/availability
- Location preferences (Remote/Hybrid/On-site)
- Education level
- English proficiency
- Technology stack
- Willingness to relocate

### CAPTCHA Handling
1. App pauses automatically when CAPTCHA appears
2. Solve manually in the browser window
3. Press ENTER in console to continue

## 🛠️ Troubleshooting

### Common Issues

**"Browser not initialized" Error**
```bash
# Ensure Playwright browsers are installed
pwsh bin/playwright.ps1 install chromium
# Run as Administrator if needed
```

**Login Issues**
- Verify credentials in `appsettings.json`
- Check 2FA settings
- Clear browser cache

**Email Sending Fails**
- Verify email credentials and App Password
- Check SMTP settings
- Test email configuration

**Database Connection Issues**
- Ensure SQL Server LocalDB is installed
- Run as Administrator
- Check Windows SQL Server features

## 📋 Best Practices

### Before Running
- Test configuration with small job batches
- Ensure CV is up-to-date
- Prepare thoughtful template answers
- Test email deliverability

### During Operation
- Monitor the process actively
- Solve CAPTCHAs promptly
- Check console for errors
- Pause/restart as needed

### After Running
- Review email drafts before sending
- Check application status on platforms
- Update tracking records
- Backup database periodically

## ⚖️ Ethical Considerations

- Use reasonable delays to avoid server overload
- Apply only to relevant positions
- Personalize applications when possible
- Respect platform terms of service
- Monitor for policy changes

## 📁 Project Structure

```
JobHunter/
├── JobHunter.exe           # Main application
├── appsettings.json        # Configuration file
├── questions.json          # Question templates
├── bin/                    # Application binaries
├── Documents/JobHunter/    # Output folder
│   └── Drafts/            # Email drafts
└── logs/                   # Application logs
```

## 🔒 Legal Disclaimer

- Use responsibly and comply with platform terms of service
- Automated applications may violate some platform policies
- Authors not responsible for account suspensions or consequences
- Always review applications when possible

## 🐛 Support

For issues and debugging:
1. Check console output for detailed information
2. Review error messages carefully
3. Verify file paths and accessibility
4. Ensure all prerequisites are installed

## 📝 License

[Add your license information here]

## 🤝 Contributing

[Add contribution guidelines if open source]

---

**⚠️ Remember**: This tool assists your job search, but personal review and customization of applications will always yield better results than fully automated processes.
