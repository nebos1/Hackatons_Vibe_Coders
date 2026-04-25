using EventsApp.Configuration;
using EventsApp.Data;
using EventsApp.Infrastructure;
using EventsApp.Models;
using EventsApp.Services;
using EventsApp.Services.AI;
using EventsApp.Services.Geocoding;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

DotEnvLoader.LoadIntoConfiguration(
    Path.Combine(builder.Environment.ContentRootPath, ".env"),
    builder.Configuration);

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(connectionString));

builder.Services.AddDatabaseDeveloperPageExceptionFilter();

builder.Services.AddDefaultIdentity<ApplicationUser>(options =>
    {
        options.SignIn.RequireConfirmedAccount = false;
        options.Password.RequireDigit = false;
        options.Password.RequireLowercase = false;
        options.Password.RequireUppercase = false;
        options.Password.RequireNonAlphanumeric = false;
        options.Password.RequiredLength = 5;
    })
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<ApplicationDbContext>();

builder.Services.AddSingleton<IMediaUploadService, MediaUploadService>();
builder.Services.AddSingleton<ITicketDocumentService, TicketDocumentService>();

builder.Services.Configure<AiOptions>(builder.Configuration.GetSection(AiOptions.SectionName));
builder.Services.Configure<SirmaAiOptions>(builder.Configuration.GetSection(SirmaAiOptions.SectionName));
builder.Services.Configure<GoogleMapsOptions>(builder.Configuration.GetSection(GoogleMapsOptions.SectionName));
builder.Services.AddHttpClient<IAiSearchService, SirmaAiSearchService>();
builder.Services.AddHttpClient<IGeocodingService, NominatimGeocodingService>();
// Image generation removed — no additional HttpClient or image service registered.

builder.Services.Configure<FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = 100L * 1024 * 1024;
});

builder.Services.AddControllersWithViews();
builder.Services.AddAntiforgery(opts => opts.HeaderName = "RequestVerificationToken");

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;

    var dbContext = services.GetRequiredService<ApplicationDbContext>();
    dbContext.Database.Migrate();

    await RoleSeeder.SeedRolesAsync(services);
    await AdminSeeder.SeedAdminAsync(services);
    await DemoDataSeeder.SeedAsync(services);
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

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.MapRazorPages();

app.Run();
