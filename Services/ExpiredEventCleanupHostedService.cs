using EventsApp.Common;
using EventsApp.Data;
using Microsoft.EntityFrameworkCore;

namespace EventsApp.Services
{
    public sealed class ExpiredEventCleanupHostedService : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IConfiguration _configuration;
        private readonly ILogger<ExpiredEventCleanupHostedService> _logger;

        public ExpiredEventCleanupHostedService(
            IServiceScopeFactory scopeFactory,
            IConfiguration configuration,
            ILogger<ExpiredEventCleanupHostedService> logger)
        {
            _scopeFactory = scopeFactory;
            _configuration = configuration;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            if (!IsEnabled())
            {
                _logger.LogInformation("Expired event cleanup is disabled.");
                return;
            }

            var interval = TimeSpan.FromHours(ReadInt("Cleanup:ExpiredEvents:IntervalHours", "EXPIRED_EVENT_CLEANUP_INTERVAL_HOURS", 24, 1, 168));

            try
            {
                await Task.Delay(TimeSpan.FromMinutes(2), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await RunOnceAsync(stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Expired event cleanup failed.");
                }

                try
                {
                    await Task.Delay(interval, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }

        private async Task RunOnceAsync(CancellationToken cancellationToken)
        {
            var retentionDays = ReadInt("Cleanup:ExpiredEvents:DaysAfterEnd", "EXPIRED_EVENT_CLEANUP_DAYS_AFTER_END", 7, 0, 365);
            var batchSize = ReadInt("Cleanup:ExpiredEvents:BatchSize", "EXPIRED_EVENT_CLEANUP_BATCH_SIZE", 20, 1, 200);
            var cutoff = DateTime.UtcNow.AddDays(-retentionDays);

            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var deletion = scope.ServiceProvider.GetRequiredService<IEventDeletionService>();

            // Pick candidates AND remember which organizer profile each belongs to,
            // so we can bump PastEventsCount after a successful deletion and still
            // show the historical count on the public organizer page.
            var candidates = await db.Events
                .AsNoTracking()
                .Where(e => e.EndTime < cutoff)
                .Where(e => e.EventSeries == null || !e.EventSeries.Occurrences.Any(o => o.EndDateTime >= cutoff))
                .Where(e => !e.Tickets
                    .SelectMany(t => t.UserTickets)
                    .Any(ut => ut.Transaction.Status == GlobalConstants.TransactionStatuses.Paid))
                .OrderBy(e => e.EndTime)
                .Take(batchSize)
                .Select(e => new { e.Id, e.OrganizerProfileId })
                .ToListAsync(cancellationToken);

            if (candidates.Count == 0)
            {
                _logger.LogDebug("Expired event cleanup found no candidates before {Cutoff}.", cutoff);
                return;
            }

            var deleted = 0;
            var counterBumps = new Dictionary<int, int>();
            foreach (var candidate in candidates)
            {
                var result = await deletion.DeleteEventAsync(candidate.Id, preservePaidTickets: true, cancellationToken);
                if (result.Deleted)
                {
                    deleted++;
                    if (candidate.OrganizerProfileId.HasValue)
                    {
                        var key = candidate.OrganizerProfileId.Value;
                        counterBumps[key] = counterBumps.TryGetValue(key, out var current) ? current + 1 : 1;
                    }
                }
                else
                {
                    _logger.LogInformation("Skipped expired event {EventId}: {Reason}", candidate.Id, result.SkippedReason);
                }
            }

            if (counterBumps.Count > 0)
            {
                var profileIds = counterBumps.Keys.ToList();
                var profiles = await db.OrganizerProfiles
                    .Where(p => profileIds.Contains(p.Id))
                    .ToListAsync(cancellationToken);
                foreach (var profile in profiles)
                {
                    if (counterBumps.TryGetValue(profile.Id, out var bump))
                    {
                        profile.PastEventsCount += bump;
                    }
                }
                await db.SaveChangesAsync(cancellationToken);
            }

            _logger.LogInformation("Expired event cleanup deleted {DeletedCount}/{CandidateCount} event(s).", deleted, candidates.Count);
        }

        private bool IsEnabled()
        {
            var value = _configuration["Cleanup:ExpiredEvents:Enabled"] ?? _configuration["EXPIRED_EVENT_CLEANUP_ENABLED"];
            return bool.TryParse(value, out var enabled) && enabled;
        }

        private int ReadInt(string configKey, string envKey, int fallback, int min, int max)
        {
            var raw = _configuration[configKey] ?? _configuration[envKey];
            return int.TryParse(raw, out var value)
                ? Math.Clamp(value, min, max)
                : fallback;
        }
    }
}
