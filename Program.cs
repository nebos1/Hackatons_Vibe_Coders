using EventsApp.Configuration;
using EventsApp.Data;
using EventsApp.Infrastructure;
using EventsApp.Models;
using EventsApp.Services;
using EventsApp.Services.AI;
using EventsApp.Services.Email;
using EventsApp.Services.Geocoding;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.AspNetCore.RateLimiting;
using System.Security.Claims;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);
var isDevelopment = builder.Environment.IsDevelopment();

DotEnvLoader.LoadIntoConfiguration(
    Path.Combine(builder.Environment.ContentRootPath, ".env"),
    builder.Configuration);

AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

var port = builder.Configuration["PORT"];
if (!string.IsNullOrWhiteSpace(port))
{
    builder.WebHost.UseUrls($"http://0.0.0.0:{port}");
}

var connectionString = DatabaseConnection.GetPostgresConnectionString(builder.Configuration);

builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options
        .UseNpgsql(connectionString)
        .ConfigureWarnings(warnings => warnings.Ignore(RelationalEventId.BoolWithDefaultWarning)));

builder.Services.AddDatabaseDeveloperPageExceptionFilter();

builder.Services.AddDefaultIdentity<ApplicationUser>(options =>
    {
        options.SignIn.RequireConfirmedAccount = builder.Configuration.GetValue("Identity:RequireConfirmedAccount", false);
        options.User.RequireUniqueEmail = true;
        options.Password.RequiredLength = builder.Configuration.GetValue("Identity:Password:RequiredLength", isDevelopment ? 5 : 10);
        options.Password.RequireDigit = builder.Configuration.GetValue("Identity:Password:RequireDigit", !isDevelopment);
        options.Password.RequireLowercase = builder.Configuration.GetValue("Identity:Password:RequireLowercase", !isDevelopment);
        options.Password.RequireUppercase = builder.Configuration.GetValue("Identity:Password:RequireUppercase", !isDevelopment);
        options.Password.RequireNonAlphanumeric = builder.Configuration.GetValue("Identity:Password:RequireNonAlphanumeric", !isDevelopment);
        options.Lockout.MaxFailedAccessAttempts = builder.Configuration.GetValue("Identity:Lockout:MaxFailedAccessAttempts", 5);
        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(builder.Configuration.GetValue("Identity:Lockout:Minutes", 15));
        options.Lockout.AllowedForNewUsers = true;
    })
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<ApplicationDbContext>();

builder.Services.ConfigureApplicationCookie(options =>
{
    options.Cookie.Name = ".Evento.Auth";
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.SecurePolicy = isDevelopment ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
    options.ExpireTimeSpan = TimeSpan.FromHours(8);
    options.SlidingExpiration = true;
    options.LoginPath = "/Identity/Account/Login";
    options.AccessDeniedPath = "/Identity/Account/AccessDenied";
});

if (UseS3MediaStorage(builder.Configuration, isDevelopment))
{
    builder.Services.AddSingleton<S3MediaUploadService>();
    builder.Services.AddSingleton<IMediaUploadService>(sp => sp.GetRequiredService<S3MediaUploadService>());
    builder.Services.AddSingleton<IRemoteMediaService>(sp => sp.GetRequiredService<S3MediaUploadService>());
}
else
{
    builder.Services.AddSingleton<IMediaUploadService, MediaUploadService>();
    builder.Services.AddSingleton<IRemoteMediaService, NullRemoteMediaService>();
}
builder.Services.AddSingleton<ITicketDocumentService, TicketDocumentService>();
builder.Services.AddTransient<IEmailSender, SmtpEmailSender>();
builder.Services.AddScoped<ISocialFeedService, SocialFeedService>();
builder.Services.AddScoped<IRecurringEventService, RecurringEventService>();
builder.Services.AddScoped<ILayoutService, LayoutService>();
builder.Services.AddScoped<ISeatReservationService, SeatReservationService>();
builder.Services.AddScoped<IPlatformPermissionService, PlatformPermissionService>();
builder.Services.AddScoped<IBusinessContextService, BusinessContextService>();
builder.Services.AddScoped<IActingIdentityService, ActingIdentityService>();

builder.Services.Configure<AiOptions>(builder.Configuration.GetSection(AiOptions.SectionName));
builder.Services.Configure<SirmaAiOptions>(builder.Configuration.GetSection(SirmaAiOptions.SectionName));
builder.Services.Configure<GoogleMapsOptions>(builder.Configuration.GetSection(GoogleMapsOptions.SectionName));
builder.Services.AddHttpClient<IAiSearchService, OpenAiService>();
builder.Services.AddHttpClient<ILayoutAiService, OpenAiLayoutService>();
builder.Services.AddHttpClient<IGeocodingService, NominatimGeocodingService>();
// Image generation removed — no additional HttpClient or image service registered.

builder.Services.Configure<FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = 100L * 1024 * 1024;
});

builder.Services.AddLocalization();
builder.Services.AddControllersWithViews();
builder.Services.AddAntiforgery(opts =>
{
    opts.HeaderName = "RequestVerificationToken";
    opts.Cookie.Name = ".Evento.Antiforgery";
    opts.Cookie.HttpOnly = true;
    opts.Cookie.SameSite = SameSiteMode.Strict;
    opts.Cookie.SecurePolicy = isDevelopment ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
});
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    options.AddPolicy("ai-light", context =>
        RateLimitPartition.GetFixedWindowLimiter("ai-light:" + GetRateLimitKey(context), _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 20,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0,
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            AutoReplenishment = true,
        }));

    options.AddPolicy("ai-heavy", context =>
        RateLimitPartition.GetFixedWindowLimiter("ai-heavy:" + GetRateLimitKey(context), _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 4,
            Window = TimeSpan.FromMinutes(10),
            QueueLimit = 0,
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            AutoReplenishment = true,
        }));

    options.AddPolicy("messages", context =>
        RateLimitPartition.GetFixedWindowLimiter("messages:" + GetRateLimitKey(context), _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 60,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0,
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            AutoReplenishment = true,
        }));
});

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;

    var dbContext = services.GetRequiredService<ApplicationDbContext>();
    dbContext.Database.Migrate();

    await RoleSeeder.SeedRolesAsync(services);
    await AdminSeeder.SeedAdminAsync(services, app.Configuration, app.Environment);
    await EventsSeeder.SeedAsync(services);

    if (app.Environment.IsDevelopment() && app.Configuration.GetValue("SeedDemoData", false))
    {
        await DemoDataSeeder.SeedAsync(services);
    }
}

if (app.Environment.IsDevelopment())
{
    app.UseMigrationsEndPoint();
}
else
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseForwardedHeaders();
app.Use(async (context, next) =>
{
    var headers = context.Response.Headers;
    headers.TryAdd("X-Content-Type-Options", "nosniff");
    headers.TryAdd("X-Frame-Options", "SAMEORIGIN");
    headers.TryAdd("Referrer-Policy", "strict-origin-when-cross-origin");
    headers.TryAdd("X-Permitted-Cross-Domain-Policies", "none");
    headers.TryAdd("Permissions-Policy", "camera=(), microphone=(), geolocation=(self), payment=(self), usb=()");
    var csp = app.Configuration["Security:ContentSecurityPolicy"];
    if (!string.IsNullOrWhiteSpace(csp))
    {
        headers.TryAdd("Content-Security-Policy", csp);
    }

    await next();
});

if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}
app.UseStaticFiles();

var supportedCultures = new[] { "bg", "en" };
app.UseRequestLocalization(new RequestLocalizationOptions()
    .SetDefaultCulture("bg")
    .AddSupportedCultures(supportedCultures)
    .AddSupportedUICultures(supportedCultures));

app.UseRouting();

app.UseAuthentication();
app.UseRateLimiter();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.MapRazorPages();

app.Run();

static string GetRateLimitKey(HttpContext context)
{
    if (context.User.Identity?.IsAuthenticated == true)
    {
        return context.User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? context.User.Identity.Name
            ?? "authenticated";
    }

    return context.Connection.RemoteIpAddress?.ToString() ?? "anonymous";
}

static bool UseS3MediaStorage(IConfiguration configuration, bool isDevelopment)
{
    var configuredMode = configuration["Media:Storage"] ?? configuration["MEDIA_STORAGE"];
    if (string.Equals(configuredMode, "S3", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(configuredMode, "RailwayBucket", StringComparison.OrdinalIgnoreCase))
    {
        return true;
    }

    if (!string.IsNullOrWhiteSpace(configuredMode))
    {
        return false;
    }

    if (isDevelopment)
    {
        return false;
    }

    var hasS3Bucket = !string.IsNullOrWhiteSpace(configuration["Media:S3:Bucket"]) ||
                      !string.IsNullOrWhiteSpace(configuration["S3_BUCKET"]) ||
                      !string.IsNullOrWhiteSpace(configuration["BUCKET"]);

    var hasS3Endpoint = !string.IsNullOrWhiteSpace(configuration["Media:S3:Endpoint"]) ||
                        !string.IsNullOrWhiteSpace(configuration["S3_ENDPOINT"]) ||
                        !string.IsNullOrWhiteSpace(configuration["ENDPOINT"]) ||
                        !string.IsNullOrWhiteSpace(configuration["AWS_ENDPOINT_URL"]);

    return hasS3Bucket && hasS3Endpoint;
}
