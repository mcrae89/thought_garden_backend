using System.Security.Cryptography;
using System.Text.Json;

namespace ThoughtGarden.Api.Data;

public sealed class EnvelopeCrypto
{
    private readonly IReadOnlyDictionary<string, byte[]> _keks;
    private readonly string _primaryId;
    private readonly string _recoveryId;

    private const int NonceSize = 12; // GCM nonce
    private const int TagSize = 16; // GCM auth tag

    public EnvelopeCrypto(IConfiguration cfg)
    {
        var enc = cfg.GetSection("Encryption");
        _primaryId = enc["ActivePrimaryKeyId"] ?? throw new("Encryption:ActivePrimaryKeyId missing");
        _recoveryId = enc["ActiveRecoveryKeyId"] ?? throw new("Encryption:ActiveRecoveryKeyId missing");

        _keks = enc.GetSection("Keys").GetChildren()
                   .ToDictionary(s => s.Key, s => Convert.FromBase64String(s.Value!));

        if (!_keks.ContainsKey(_primaryId) || !_keks.ContainsKey(_recoveryId))
            throw new InvalidOperationException("Active key ids must exist in Encryption:Keys");
    }

    public string PrimaryKeyId => _primaryId;
    public string RecoveryKeyId => _recoveryId;

    public (string cipher, string nonce, string tag, string wrappedKeysJson, string algVersion)
        Encrypt(string plaintext)
    {
        // per-entry DEK
        Span<byte> dek = stackalloc byte[32];
        RandomNumberGenerator.Fill(dek);

        // data encrypt (AES-GCM, 12B nonce, 16B tag)
        Span<byte> nonce = stackalloc byte[NonceSize];
        RandomNumberGenerator.Fill(nonce);

        var pt = System.Text.Encoding.UTF8.GetBytes(plaintext);
        var ct = new byte[pt.Length];
        Span<byte> tag = stackalloc byte[TagSize];

        using (var gcm = new AesGcm(dek, tagSizeInBytes: TagSize))
            gcm.Encrypt(nonce, pt, ct, tag);

        // wrap DEK under primary + recovery
        var wrapped = new Dictionary<string, string>(2)
        {
            [_primaryId] = WrapDek(_keks[_primaryId], dek),
            [_recoveryId] = WrapDek(_keks[_recoveryId], dek)
        };

        return (Convert.ToBase64String(ct),
                Convert.ToBase64String(nonce),
                Convert.ToBase64String(tag),
                JsonSerializer.Serialize(wrapped),
                "gcm.v1");
    }

    public string Decrypt(string cipher, string nonce, string tag, string wrappedKeysJson)
    {
        var map = JsonSerializer.Deserialize<Dictionary<string, string>>(wrappedKeysJson)
                  ?? throw new DecryptionFailedException("WrappedKeys missing.");

        if (!TryUnwrap(map, _primaryId, out var dek) && !TryAny(map, out dek))
            throw new DecryptionFailedException("No available KEK to unwrap DEK.");

        var ct = Convert.FromBase64String(cipher);
        var n = Convert.FromBase64String(nonce);
        var t = Convert.FromBase64String(tag);
        var pt = new byte[ct.Length];

        using (var gcm = new AesGcm(dek, tagSizeInBytes: TagSize))
            gcm.Decrypt(n, ct, t, pt);

        return System.Text.Encoding.UTF8.GetString(pt);
    }

    // ---------- Public helpers for rotation jobs ----------

    /// <summary>Wrap an existing DEK under a specific keyId and return base64(nonce||tag||wrappedDEK).</summary>
    public string WrapDekWithId(string keyId, ReadOnlySpan<byte> dek)
    {
        if (!_keks.TryGetValue(keyId, out var kek))
            throw new InvalidOperationException($"KEK not found for keyId '{keyId}'.");

        Span<byte> n = stackalloc byte[NonceSize];
        RandomNumberGenerator.Fill(n);

        var c = new byte[dek.Length];
        Span<byte> t = stackalloc byte[TagSize];

        using (var g = new AesGcm(kek, tagSizeInBytes: TagSize))
            g.Encrypt(n, dek, c, t);

        return Convert.ToBase64String(n.ToArray().Concat(t.ToArray()).Concat(c).ToArray());
    }

    /// <summary>Try to unwrap a DEK from a WrappedKeys map using a specific keyId.</summary>
    public bool TryUnwrapDek(IReadOnlyDictionary<string, string> wrappedMap, string keyId, out byte[] dek)
        => TryUnwrap(wrappedMap, keyId, out dek);

    /// <summary>Try to unwrap a DEK using any available key present in the map.</summary>
    public bool TryUnwrapAny(IReadOnlyDictionary<string, string> wrappedMap, out byte[] dek)
        => TryAny(wrappedMap, out dek);

    // ---------- Internals ----------

    private static string WrapDek(byte[] kek, ReadOnlySpan<byte> dek)
    {
        Span<byte> n = stackalloc byte[NonceSize];
        RandomNumberGenerator.Fill(n);

        var c = new byte[dek.Length];
        Span<byte> t = stackalloc byte[TagSize];

        using (var g = new AesGcm(kek, tagSizeInBytes: TagSize))
            g.Encrypt(n, dek, c, t);

        return Convert.ToBase64String(n.ToArray().Concat(t.ToArray()).Concat(c).ToArray());
    }

    private bool TryUnwrap(IReadOnlyDictionary<string, string> map, string keyId, out byte[] dek)
    {
        dek = Array.Empty<byte>();
        if (!map.TryGetValue(keyId, out var b64) || !_keks.TryGetValue(keyId, out var kek))
            return false;

        var raw = Convert.FromBase64String(b64);
        var n = raw.AsSpan(0, NonceSize);
        var t = raw.AsSpan(NonceSize, TagSize);
        var c = raw.AsSpan(NonceSize + TagSize);

        dek = new byte[c.Length];
        try
        {
            using var g = new AesGcm(kek, tagSizeInBytes: TagSize);
            g.Decrypt(n, c, t, dek);
            return true;
        }
        catch
        {
            dek = Array.Empty<byte>();
            return false;
        }
    }

    private bool TryAny(IReadOnlyDictionary<string, string> map, out byte[] dek)
    {
        foreach (var (keyId, _) in map)
            if (TryUnwrap(map, keyId, out dek)) return true;

        dek = Array.Empty<byte>();
        return false;
    }
}
