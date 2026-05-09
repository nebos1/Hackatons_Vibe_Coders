using EventsApp.Configuration;
using EventsApp.Data;
using EventsApp.Infrastructure;
using EventsApp.Models;
using EventsApp.Services;
using EventsApp.Services.AI;
using EventsApp.Services.Email;
using EventsApp.Services.Geocoding;
using Microsoft.AspNetCore.DataProtection;
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

builder.Services.AddDataProtection()
    .SetApplicationName("Evento")
    .PersistKeysToDbContext<ApplicationDbContext>();

builder.Services.AddDatabaseDeveloperPageExceptionFilter();

builder.Services.AddDefaultIdentity<ApplicationUser>(options =>
    {
        options.SignIn.RequireConfirmedAccount = builder.Configuration.GetValue("Identity:RequireConfirmedAccount", !isDevelopment);
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
builder.Services.AddSingleton<IAppLinkService, AppLinkService>();
builder.Services.AddTransient<IEmailSender, SmtpEmailSender>();
builder.Services.AddTransient<IEmailConfirmationSender, EmailConfirmationSender>();
builder.Services.AddScoped<IPushNotificationService, WebPushNotificationService>();
builder.Services.AddScoped<IMentionService, MentionService>();
builder.Services.AddScoped<ISocialFeedService, SocialFeedService>();
builder.Services.AddScoped<IEventDeletionService, EventDeletionService>();
builder.Services.AddScoped<IRecurringEventService, RecurringEventService>();
builder.Services.AddScoped<ILayoutService, LayoutService>();
builder.Services.AddScoped<ISeatReservationService, SeatReservationService>();
builder.Services.AddScoped<IPlatformPermissionService, PlatformPermissionService>();
builder.Services.AddScoped<IBusinessContextService, BusinessContextService>();
builder.Services.AddScoped<IActingIdentityService, ActingIdentityService>();
builder.Services.AddHostedService<ExpiredEventCleanupHostedService>();

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
builder.Services.AddSignalR();
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

    options.OnRejected = async (context, cancellationToken) =>
    {
        var logger = context.HttpContext.RequestServices.GetRequiredService<ILoggerFactory>()
            .CreateLogger("RateLimit");
        var ip = context.HttpContext.Connection.RemoteIpAddress?.ToString() ?? "?";
        var path = context.HttpContext.Request.Path.Value ?? "?";
        logger.LogWarning("Rate limit hit. IP={Ip} Path={Path} Method={Method}",
            ip, path, context.HttpContext.Request.Method);

        if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
        {
            context.HttpContext.Response.Headers["Retry-After"] = ((int)retryAfter.TotalSeconds).ToString();
        }

        var accept = context.HttpContext.Request.Headers["Accept"].ToString();
        var requestedWith = context.HttpContext.Request.Headers["X-Requested-With"].ToString();
        var wantsJson = accept.Contains("application/json", StringComparison.OrdinalIgnoreCase)
            || string.Equals(requestedWith, "XMLHttpRequest", StringComparison.OrdinalIgnoreCase);

        if (wantsJson)
        {
            context.HttpContext.Response.ContentType = "application/json";
            await context.HttpContext.Response.WriteAsync(
                "{\"error\":\"too_many_requests\",\"message\":\"Прекалено много заявки. Опитай след малко.\"}",
                cancellationToken);
            return;
        }

        context.HttpContext.Response.ContentType = "text/html; charset=utf-8";
        await context.HttpContext.Response.WriteAsync(
            "<!doctype html><html lang='bg'><head><meta charset='utf-8'><title>Прекалено много заявки</title>" +
            "<style>body{margin:0;font-family:-apple-system,Segoe UI,Roboto,sans-serif;background:#131826;color:#f5f6fa;" +
            "display:flex;align-items:center;justify-content:center;min-height:100vh}" +
            ".card{max-width:420px;background:#1b2030;border:1px solid #2a3142;border-radius:18px;padding:32px;text-align:center}" +
            "h1{margin:0 0 8px;font-size:24px;font-weight:800}" +
            "p{margin:0 0 18px;color:#c5cad8;line-height:1.55}" +
            "a{color:#6b85ff;text-decoration:none;font-weight:700}</style></head>" +
            "<body><div class='card'><h1>Прекалено много заявки</h1>" +
            "<p>Опитай отново след минута. Ако смяташ, че това е грешка, презареди страницата по-късно.</p>" +
            "<a href='/'>← Към началото</a></div></body></html>",
            cancellationToken);
    };

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

    // Brute-force protection for login / register / password reset
    options.AddPolicy("auth", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            "auth:" + (context.Connection.RemoteIpAddress?.ToString() ?? "anonymous"),
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 10,
                Window = TimeSpan.FromMinutes(5),
                QueueLimit = 0,
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                AutoReplenishment = true,
            }));

    // Like / save / attend / follow / buy — bot protection on social actions
    options.AddPolicy("interactions", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            "interactions:" + GetRateLimitKey(context),
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 120,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                AutoReplenishment = true,
            }));

    // Create / edit content (events, posts, comments, tickets) — anti-spam
    options.AddPolicy("content-write", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            "content-write:" + GetRateLimitKey(context),
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 30,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                AutoReplenishment = true,
            }));

    // Public read endpoints (search, etc) — basic DoS protection
    options.AddPolicy("public-read", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            "public-read:" + GetRateLimitKey(context),
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 200,
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
    headers.TryAdd("Permissions-Policy", "camera=(self), microphone=(), geolocation=(self), payment=(self), usb=()");
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

// Public aliases keep MVC controller names out of visible URLs while old routes still work.
app.MapControllerRoute(
    name: "email-confirm",
    pattern: "email/confirm",
    defaults: new { controller = "EmailConfirmation", action = "Index" });

app.MapControllerRoute(
    name: "confirm-email-html",
    pattern: "confirm-email.html",
    defaults: new { controller = "EmailConfirmation", action = "RedirectToCanonical" });

app.MapControllerRoute(
    name: "confirm-email",
    pattern: "confirm-email",
    defaults: new { controller = "EmailConfirmation", action = "RedirectToCanonical" });

app.MapControllerRoute(
    name: "confirm-email-legacy",
    pattern: "account/confirm-email",
    defaults: new { controller = "EmailConfirmation", action = "RedirectToCanonical" });

app.MapControllerRoute(
    name: "confirm-email-identity-legacy",
    pattern: "Identity/Account/ConfirmEmail",
    defaults: new { controller = "EmailConfirmation", action = "RedirectToCanonical" });

app.MapControllerRoute(
    name: "confirm-email-account-legacy",
    pattern: "Account/ConfirmEmail",
    defaults: new { controller = "EmailConfirmation", action = "RedirectToCanonical" });

app.MapControllerRoute(
    name: "flow-create",
    pattern: "flow/new",
    defaults: new { controller = "Posts", action = "Create" });

app.MapControllerRoute(
    name: "flow-details",
    pattern: "flow/p/{id:int}",
    defaults: new { controller = "Posts", action = "Details" });

app.MapControllerRoute(
    name: "flow-edit",
    pattern: "flow/p/{id:int}/edit",
    defaults: new { controller = "Posts", action = "Edit" });

app.MapControllerRoute(
    name: "flow-delete",
    pattern: "flow/p/{id:int}/delete",
    defaults: new { controller = "Posts", action = "Delete" });

app.MapControllerRoute(
    name: "flow-delete-confirmed",
    pattern: "flow/p/{id:int}/delete-confirmed",
    defaults: new { controller = "Posts", action = "DeleteConfirmed" });

app.MapControllerRoute(
    name: "flow-like",
    pattern: "flow/p/{id:int}/like",
    defaults: new { controller = "Posts", action = "Like" });

app.MapControllerRoute(
    name: "flow-unlike",
    pattern: "flow/p/{id:int}/unlike",
    defaults: new { controller = "Posts", action = "Unlike" });

app.MapControllerRoute(
    name: "flow-save",
    pattern: "flow/p/{id:int}/save",
    defaults: new { controller = "Posts", action = "Save" });

app.MapControllerRoute(
    name: "flow-unsave",
    pattern: "flow/p/{id:int}/unsave",
    defaults: new { controller = "Posts", action = "Unsave" });

app.MapControllerRoute(
    name: "flow-comments",
    pattern: "flow/p/{id:int}/comments",
    defaults: new { controller = "Posts", action = "AddComment" });

app.MapControllerRoute(
    name: "flow",
    pattern: "flow/{action=Index}/{id?}",
    defaults: new { controller = "Posts" });

app.MapControllerRoute(
    name: "inbox-details",
    pattern: "inbox/{token:guid}",
    defaults: new { controller = "Messages", action = "Details" });

app.MapControllerRoute(
    name: "inbox-poll",
    pattern: "inbox/poll/{token:guid}",
    defaults: new { controller = "Messages", action = "Poll" });

app.MapControllerRoute(
    name: "inbox-send",
    pattern: "inbox/send/{token:guid}",
    defaults: new { controller = "Messages", action = "Send" });

app.MapControllerRoute(
    name: "inbox-approve",
    pattern: "inbox/{token:guid}/approve",
    defaults: new { controller = "Messages", action = "Approve" });

app.MapControllerRoute(
    name: "inbox-decline",
    pattern: "inbox/{token:guid}/decline",
    defaults: new { controller = "Messages", action = "Decline" });

app.MapControllerRoute(
    name: "inbox-share-event",
    pattern: "inbox/share/event/{id:int}",
    defaults: new { controller = "Messages", action = "ShareEvent" });

app.MapControllerRoute(
    name: "inbox-share-post",
    pattern: "inbox/share/post/{id:int}",
    defaults: new { controller = "Messages", action = "SharePost" });

app.MapControllerRoute(
    name: "inbox",
    pattern: "inbox/{action=Index}/{id?}",
    defaults: new { controller = "Messages" });

app.MapControllerRoute(
    name: "event-details",
    pattern: "e/{id:int}",
    defaults: new { controller = "Events", action = "Details" });

app.MapControllerRoute(
    name: "event-create",
    pattern: "events/new",
    defaults: new { controller = "Events", action = "Create" });

app.MapControllerRoute(
    name: "event-edit",
    pattern: "events/{id:int}/edit",
    defaults: new { controller = "Events", action = "Edit" });

app.MapControllerRoute(
    name: "event-delete",
    pattern: "events/{id:int}/delete",
    defaults: new { controller = "Events", action = "Delete" });

app.MapControllerRoute(
    name: "events",
    pattern: "events/{action=Index}/{id?}",
    defaults: new { controller = "Events" });

app.MapControllerRoute(
    name: "ticket-details",
    pattern: "ticket/{id:guid}",
    defaults: new { controller = "Tickets", action = "Details" });

app.MapControllerRoute(
    name: "ticket-pdf",
    pattern: "ticket/{id:guid}/pdf",
    defaults: new { controller = "Tickets", action = "DownloadPdf" });

app.MapControllerRoute(
    name: "ticket-qr",
    pattern: "ticket/{id:guid}/qr",
    defaults: new { controller = "Tickets", action = "Qr" });

app.MapControllerRoute(
    name: "tickets-mine",
    pattern: "tickets/mine",
    defaults: new { controller = "Tickets", action = "MyTickets" });

app.MapControllerRoute(
    name: "tickets-scan",
    pattern: "tickets/scan",
    defaults: new { controller = "Tickets", action = "Validate" });

app.MapControllerRoute(
    name: "tickets",
    pattern: "tickets/{action=Manage}/{id?}",
    defaults: new { controller = "Tickets" });

app.MapControllerRoute(
    name: "profile-page",
    pattern: "profile/page/{id:int}",
    defaults: new { controller = "Profiles", action = "Page" });

app.MapControllerRoute(
    name: "profile-details",
    pattern: "profile/{id}",
    defaults: new { controller = "Profiles", action = "Details" });

app.MapControllerRoute(
    name: "profiles",
    pattern: "profile/{action}/{id?}",
    defaults: new { controller = "Profiles" });

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.MapRazorPages()
    .Add(builder =>
    {
        if (builder is RouteEndpointBuilder route
            && route.RoutePattern.RawText is string pattern
            && (pattern.Contains("Account/ForgotPassword", StringComparison.OrdinalIgnoreCase)
                || pattern.Contains("Account/ResetPassword", StringComparison.OrdinalIgnoreCase)
                || pattern.Contains("Account/ConfirmEmail", StringComparison.OrdinalIgnoreCase)
                || pattern.Contains("forgot-password", StringComparison.OrdinalIgnoreCase)
                || pattern.Contains("reset-password", StringComparison.OrdinalIgnoreCase)
                || pattern.Contains("confirm-email", StringComparison.OrdinalIgnoreCase)
                || pattern.Contains("Account/ResendEmailConfirmation", StringComparison.OrdinalIgnoreCase)))
        {
            builder.Metadata.Add(new EnableRateLimitingAttribute("auth"));
        }
    });

// SignalR hubs
app.MapHub<EventsApp.Hubs.ChatHub>("/hubs/chat");

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
    var configuredMode = configuration["MEDIA_STORAGE"] ?? configuration["Media:Storage"];
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
