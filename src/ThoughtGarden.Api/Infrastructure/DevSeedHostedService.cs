using Microsoft.EntityFrameworkCore;
using ThoughtGarden.Api.Data;
using ThoughtGarden.Models;

namespace ThoughtGarden.Api.Infrastructure;

public sealed class DevSeedHostedService : IHostedService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<DevSeedHostedService> _log;
    private readonly IHostEnvironment _env;

    public DevSeedHostedService(IServiceScopeFactory scopeFactory, ILogger<DevSeedHostedService> log, IHostEnvironment env)
    {
        _scopeFactory = scopeFactory;
        _log = log;
        _env = env;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (!_env.IsDevelopment())
        {
            _log.LogDebug("DevSeedHostedService skipped (not Development).");
            return;
        }

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ThoughtGardenDbContext>();
        var crypto = scope.ServiceProvider.GetRequiredService<EnvelopeCrypto>();

        // Idempotent: bail if anything already exists
        if (await db.JournalEntries.AnyAsync(cancellationToken))
        {
            _log.LogDebug("Dev seed skipped (JournalEntries already present).");
            return;
        }

        var now = DateTime.UtcNow;

        // Ensure referenced rows exist (Users, EmotionTags are seeded via HasData)
        // Create two demo entries per user using current active KEKs
        void AddEntry(int userId, int moodId, string text, IEnumerable<(int emotionId, int intensity)> seconds)
        {
            var enc = crypto.Encrypt(text);
            var entry = new JournalEntry
            {
                UserId = userId,
                MoodId = moodId,
                Text = enc.cipher,
                IV = enc.nonce,           // mirror nonce for compatibility
                DataNonce = enc.nonce,    // 12B
                DataTag = enc.tag,        // 16B
                WrappedKeys = enc.wrappedKeysJson,
                AlgVersion = enc.algVersion,
                CreatedAt = now,
                UpdatedAt = now,
                IsDeleted = false
            };
            db.JournalEntries.Add(entry);
            db.SaveChanges(); // get Entry.Id for join table

            foreach (var (emotionId, intensity) in seconds)
            {
                db.EntryEmotions.Add(new EntryEmotion
                {
                    EntryId = entry.Id,
                    EmotionId = emotionId,
                    Intensity = intensity
                });
            }
        }

        // User 1 (admin) samples
        AddEntry(1, moodId: 1, text: "First sunrise journal — feeling optimistic.",
                 seconds: new[] { (4, 5) }); // Calm
        AddEntry(1, moodId: 3, text: "Busy day; a bit on edge but productive.",
                 seconds: new[] { (3, 2), (1, 3) }); // Angry(2), Happy(3)

        // User 2 (regular) samples
        AddEntry(2, moodId: 2, text: "Missing home today.",
                 seconds: new[] { (2, 4) }); // Sad
        AddEntry(2, moodId: 4, text: "Post-run calm. Breathing feels easy.",
                 seconds: new[] { (4, 5) }); // Calm

        await db.SaveChangesAsync(cancellationToken);
        _log.LogInformation("Dev seed complete: demo journal entries created with envelope encryption.");
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
