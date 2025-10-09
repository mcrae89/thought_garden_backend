using HotChocolate.Authorization;
using Microsoft.EntityFrameworkCore;
using ThoughtGarden.Api.Data;

namespace ThoughtGarden.Api.GraphQL.Mutations
{

    [ExtendObjectType("Mutation")]
    public sealed class MaintenanceMutations
    {
        private static bool IsDevOrTesting(Microsoft.Extensions.Hosting.IHostEnvironment envHost) => envHost.IsDevelopment() || envHost.IsEnvironment("Testing");
        // Re-wrap DEKs under a new primary (no data decrypt)
        [Authorize]
        public async Task<CombinedRewrapResult> RewrapAndPrunePrimary(
    string oldPrimaryId,
    string newPrimaryId,
    [Service] ThoughtGardenDbContext db,
    [Service] EnvelopeCrypto env,
    [Service] Microsoft.Extensions.Hosting.IHostEnvironment envHost,
    CancellationToken ct)
        {
            if (!IsDevOrTesting(envHost)) throw new GraphQLException("not authorized");

            const int batchSize = 500;
            var result = new CombinedRewrapResult();
            var lastId = 0;
            var currentRecovery = env.RecoveryKeyId;

            while (true)
            {
                var batch = await db.JournalEntries
                    .Where(e => e.WrappedKeys != null && e.Id > lastId && !e.IsDeleted)
                    .OrderBy(e => e.Id)
                    .Take(batchSize)
                    .ToListAsync(ct);

                if (batch.Count == 0) break;

                foreach (var e in batch)
                {
                    lastId = e.Id;

                    Dictionary<string, string>? map;
                    try { map = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(e.WrappedKeys!); }
                    catch { result.SkippedInvalidJson++; continue; }
                    if (map is null || map.Count == 0) { result.SkippedInvalidJson++; continue; }

                    var hadOld = map.ContainsKey(oldPrimaryId);
                    var hadNew = map.ContainsKey(newPrimaryId);
                    var hadRecovery = map.ContainsKey(currentRecovery);

                    // Skip rows that already have new primary and don't have the old one anymore
                    if (!hadOld && hadNew) { result.AlreadyUpToDate++; continue; }

                    // Unwrap DEK via any available wrap
                    if (!env.TryUnwrapAny(map, out var dek))
                    {
                        result.SkippedUnwrapFailed++;
                        continue;
                    }

                    // Ensure recovery wrap exists before pruning old
                    if (!hadRecovery)
                    {
                        map[currentRecovery] = env.WrapDekWithId(currentRecovery, dek);
                        result.AddedRecovery++;
                    }

                    // Add new primary wrap (if missing)
                    if (!hadNew)
                    {
                        map[newPrimaryId] = env.WrapDekWithId(newPrimaryId, dek);
                        result.AddedNewPrimary++;
                    }

                    // Remove old primary wrap (if present)
                    if (hadOld && map.Remove(oldPrimaryId)) result.PrunedOldPrimary++;

                    e.WrappedKeys = System.Text.Json.JsonSerializer.Serialize(map);
                    e.UpdatedAt = DateTime.UtcNow;
                    result.UpdatedRows++;
                }

                await db.SaveChangesAsync(ct);
            }

            return result;
        }

        public sealed class CombinedRewrapResult
        {
            public int UpdatedRows { get; set; }
            public int AddedNewPrimary { get; set; }
            public int AddedRecovery { get; set; }
            public int PrunedOldPrimary { get; set; }
            public int SkippedUnwrapFailed { get; set; }
            public int SkippedInvalidJson { get; set; }
            public int AlreadyUpToDate { get; set; }
        }


        // Full hygiene after compromise: re-encrypt data with new DEKs (uses current active keys)
        [Authorize]
        public async Task<bool> ReencryptAfterCompromise(
    string compromisedKeyId,
    [Service] ThoughtGardenDbContext db,
    [Service] EnvelopeCrypto env,
    [Service] IHostEnvironment envHost,
    CancellationToken ct)
        {
            if (!IsDevOrTesting(envHost)) throw new GraphQLException("not authorized");

            const int batchSize = 250;
            var lastId = 0;

            while (true)
            {
                var batch = await db.JournalEntries
                    .Where(e => e.WrappedKeys != null
                                && e.Id > lastId
                                && EF.Functions.ILike(e.WrappedKeys!, $"%\"{compromisedKeyId}\"%"))
                    .OrderBy(e => e.Id)
                    .Take(batchSize)
                    .ToListAsync(ct);

                if (batch.Count == 0) break;

                foreach (var e in batch)
                {
                    // move cursor
                    if (e.Id > lastId) lastId = e.Id;

                    var plaintext = env.Decrypt(e.Text, e.DataNonce!, e.DataTag!, e.WrappedKeys!);

                    var enc = env.Encrypt(plaintext);
                    e.Text = enc.cipher;
                    e.DataNonce = enc.nonce;
                    e.DataTag = enc.tag;
                    e.WrappedKeys = enc.wrappedKeysJson; // only active primary + recovery now
                    e.AlgVersion = enc.algVersion;
                    e.UpdatedAt = DateTime.UtcNow;
                }

                await db.SaveChangesAsync(ct);
            }

            return true;
        }
    }
}