using HRSystem.BLL;
using HRSystem.BLL.Common;
using HRSystem.DAL;
using HRSystem.DAL.Data;
using HRSystem.DAL.Interfaces;
using HRSystem.Domain.Identity;
using HRSystem.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Authorization;

var builder = WebApplication.CreateBuilder(args);

// Secrets for this machine only (AI key...), kept out of git
builder.Configuration.AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: true);

// QuestPDF (payslips) is free under its Community license for companies under $1M revenue
QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;

// Payslips use Tahoma (installed on Windows) for Arabic. To run on a server without it,
// put an Arabic .ttf (e.g. Cairo from Google Fonts) next to the app and change PayslipPdf.Font.
QuestPDF.Settings.UseSystemFonts = true;

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

builder.Services.AddDataAccess(connectionString);
builder.Services.AddBusinessLogic();

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();
builder.Services.AddSingleton<IFileStorage, LocalFileStorage>();
builder.Services.AddHostedService<AttendanceCloseJob>();
builder.Services.AddHostedService<ReminderJob>();
builder.Services.AddHostedService<DeviceSyncJob>();
builder.Services.AddScoped<HRSystem.Web.Services.DataImport.DataImporter>();

// Daily database backup (settings under "Backup"); needs a server that allows BACKUP DATABASE
builder.Services.Configure<BackupOptions>(builder.Configuration.GetSection(BackupOptions.Section));
builder.Services.AddSingleton<BackupState>();
builder.Services.AddSingleton<DatabaseBackup>();
builder.Services.AddHostedService<BackupJob>();

// Notification emails: off until "Email:Host" and "Email:From" are set (appsettings.Local.json on each server)
builder.Services.Configure<EmailOptions>(builder.Configuration.GetSection(EmailOptions.Section));
builder.Services.AddSingleton<IEmailSender, SmtpEmailSender>();

// AI (CV analysis). The key lives in appsettings "Ai:ApiKey" on each machine, never in git.
builder.Services.Configure<AiOptions>(builder.Configuration.GetSection(AiOptions.Section));
builder.Services.AddHttpClient<IAiClient, GeminiClient>(client => client.Timeout = TimeSpan.FromSeconds(90));

// Keep the login cookie keys in a file: shared hosts restart the app often and
// keys held in memory would log everyone out on each restart
builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(Path.Combine(builder.Environment.ContentRootPath, "App_Data", "keys")));

builder.Services
    .AddIdentity<ApplicationUser, IdentityRole>(options =>
    {
        options.Password.RequiredLength = 8;
        options.Password.RequireNonAlphanumeric = false;
        options.Lockout.MaxFailedAccessAttempts = 5;
        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
        options.User.RequireUniqueEmail = true;
    })
    .AddEntityFrameworkStores<HRDbContext>()
    .AddDefaultTokenProviders();

// Password reset links work for two hours
builder.Services.Configure<DataProtectionTokenProviderOptions>(options => options.TokenLifespan = TimeSpan.FromHours(2));

builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.LogoutPath = "/Account/Logout";
    options.AccessDeniedPath = "/Account/AccessDenied";
    options.ExpireTimeSpan = TimeSpan.FromHours(8);
    options.SlidingExpiration = true;
});

// Every page needs login unless marked [AllowAnonymous]
builder.Services.AddControllersWithViews(options =>
{
    var policy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build();
    options.Filters.Add(new AuthorizeFilter(policy));
    options.Filters.Add<TwoFactorRequiredFilter>();
});

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    await DbInitializer.SeedAsync(
        services.GetRequiredService<HRDbContext>(),
        services.GetRequiredService<UserManager<ApplicationUser>>(),
        services.GetRequiredService<RoleManager<IdentityRole>>(),
        app.Configuration.GetSection("SeedAdmin").Get<SeedAdminOptions>());
}

// "--seed-demo [cv folder]": fill an empty database with the demo company, then stop
if (args.Contains("--seed-demo"))
{
    using var scope = app.Services.CreateScope();
    var cvFolder = args.SkipWhile(a => a != "--seed-demo").Skip(1).FirstOrDefault();
    await DemoDataSeeder.RunAsync(scope.ServiceProvider, cvFolder);
    return;
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

app.Run();
