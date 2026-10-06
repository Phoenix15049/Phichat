using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.IdentityModel.Tokens;
using Phichat.API.Security;
using Phichat.Application.Common.Exceptions;
using Phichat.Application.DTOs.Settings;
using Phichat.Application.Interfaces;
using Phichat.Domain.Entities;
using Phichat.Infrastructure.Data;
using Phichat.Infrastructure.Push;
using Phichat.Infrastructure.Security;
using Phichat.Infrastructure.Services;

namespace Phichat.Tests;

public class WebPushTests
{
    private static byte[] B64(string value) => Base64UrlEncoder.DecodeBytes(value);

    /// <summary>The worked example of RFC 8291, appendix A.</summary>
    [Fact]
    public void Encrypts_rfc8291_example()
    {
        var senderPublic = B64("BP4z9KsN6nGRTbVYI_c7VJSPQTBtkgcy27mlmlMoZIIgDll6e3vCYLocInmYWAmS6TlzAC8wEqKK6PBru3jl7A8");
        var senderKey = new ECParameters
        {
            Curve = ECCurve.NamedCurves.nistP256,
            D = B64("yfWPiYE-n46HLnH0KqZOF1fJJU3MYrct3AELtAQ-oRw"),
            Q = new ECPoint { X = senderPublic[1..33], Y = senderPublic[33..65] }
        };

        var body = WebPushCrypto.Encrypt(
            Encoding.UTF8.GetBytes("When I grow up, I want to be a watermelon"),
            B64("BCVxsr7N_eNgVRqvHtD0zTZsEc6-VV-JvLexhqUzORcxaOzi6-AYWXvTBHm4bjyPjs7Vd8pZGH6SRpkNtoIAiw4"),
            B64("BTBZMqHH6r4Tts7J_aSIgg"),
            senderKey,
            B64("DGv6ra1nlYgDCS1FRnbzlw"));

        Assert.Equal(
            "DGv6ra1nlYgDCS1FRnbzlwAAEABBBP4z9KsN6nGRTbVYI_c7VJSPQTBtkgcy27mlmlMoZIIgDll6e3vCYLocInmYWAmS6TlzAC8wEqKK6PBru3jl7A_yl95bQpu6cVPTpK4Mqgkf1CXztLVBSt2Ks3oZwbuwXPXLWyouBWLVWGNWQexSgSxsj_Qulcy4a-fN",
            Base64UrlEncoder.Encode(body));
    }

    [Fact]
    public void Rejects_invalid_subscription_keys()
    {
        Assert.False(WebPushCrypto.IsValidPublicKey(new byte[65]));      // no 0x04 prefix
        var offCurve = new byte[65];
        offCurve[0] = 0x04;
        offCurve[64] = 1;
        Assert.False(WebPushCrypto.IsValidPublicKey(offCurve));
        Assert.True(WebPushCrypto.IsValidPublicKey(B64("BCVxsr7N_eNgVRqvHtD0zTZsEc6-VV-JvLexhqUzORcxaOzi6-AYWXvTBHm4bjyPjs7Vd8pZGH6SRpkNtoIAiw4")));
    }

    [Theory]
    [InlineData("https://fcm.googleapis.com/fcm/send/abc", true)]
    [InlineData("https://updates.push.services.mozilla.com/wpush/v2/abc", true)]
    [InlineData("https://wns2-par02p.notify.windows.com/w/?token=abc", true)]
    [InlineData("https://web.push.apple.com/abc", true)]
    [InlineData("http://fcm.googleapis.com/fcm/send/abc", false)]         // not https
    [InlineData("https://fcm.googleapis.com:8443/fcm/send/abc", false)]   // non-default port
    [InlineData("https://evilfcm.googleapis.com.attacker.net/x", false)]
    [InlineData("https://notify.windows.com.evil.com/x", false)]
    [InlineData("https://127.0.0.1/x", false)]
    [InlineData("https://localhost/x", false)]
    [InlineData("not a url", false)]
    public void Only_known_push_services_are_allowed(string endpoint, bool allowed)
    {
        Assert.Equal(allowed, PushEndpoints.Parse(endpoint) != null);
    }

    [Fact]
    public void Vapid_header_is_a_valid_es256_jwt()
    {
        var path = Path.Combine(Path.GetTempPath(), "vapid-" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            var keys = VapidKeys.Load(new PushOptions { Subject = "mailto:test@example.com" }, path);
            // Loading again reuses the stored pair.
            Assert.Equal(keys.PublicKeyBase64Url, VapidKeys.Load(new PushOptions(), path).PublicKeyBase64Url);

            var header = keys.AuthorizationFor(new Uri("https://fcm.googleapis.com/fcm/send/abc"));
            var match = System.Text.RegularExpressions.Regex.Match(header, "^vapid t=([^.]+)\\.([^.]+)\\.([^,]+), k=(.+)$");
            Assert.True(match.Success);
            Assert.Equal(keys.PublicKeyBase64Url, match.Groups[4].Value);

            var claims = JsonDocument.Parse(Base64UrlEncoder.Decode(match.Groups[2].Value)).RootElement;
            Assert.Equal("https://fcm.googleapis.com", claims.GetProperty("aud").GetString());
            Assert.Equal("mailto:test@example.com", claims.GetProperty("sub").GetString());

            using var verifier = ECDsa.Create(new ECParameters
            {
                Curve = ECCurve.NamedCurves.nistP256,
                Q = new ECPoint { X = keys.PublicKey[1..33], Y = keys.PublicKey[33..65] }
            });
            Assert.True(verifier.VerifyData(
                Encoding.ASCII.GetBytes(match.Groups[1].Value + "." + match.Groups[2].Value),
                Base64UrlEncoder.DecodeBytes(match.Groups[3].Value),
                HashAlgorithmName.SHA256));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Theory]
    [InlineData("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/126.0.0.0 Safari/537.36 Edg/126.0.0.0", "Edge · Windows")]
    [InlineData("Mozilla/5.0 (Linux; Android 14; Pixel 8) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/126.0.0.0 Mobile Safari/537.36", "Chrome · Android")]
    [InlineData("Mozilla/5.0 (iPhone; CPU iPhone OS 17_5 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/17.5 Mobile/15E148 Safari/604.1", "Safari · iPhone")]
    [InlineData("Mozilla/5.0 (Macintosh; Intel Mac OS X 14.5; rv:127.0) Gecko/20100101 Firefox/127.0", "Firefox · macOS")]
    [InlineData("curl/8.0", "Unknown device")]
    [InlineData("", null)]
    public void Describes_devices(string userAgent, string? expected)
    {
        Assert.Equal(expected, DeviceNames.Describe(userAgent));
    }
}

public class PrivacyAndSessionTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly AppDbContext _db;
    private readonly PrivacyService _privacy;

    private readonly Guid _alice = Guid.NewGuid();
    private readonly Guid _bob = Guid.NewGuid();
    private readonly Guid _carol = Guid.NewGuid();

    public PrivacyAndSessionTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        _db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options);
        _db.Database.EnsureCreated();

        _db.Users.AddRange(
            new User { Id = _alice, Username = "alice", PasswordHash = "x" },
            new User { Id = _bob, Username = "bob", PasswordHash = "x" },
            new User { Id = _carol, Username = "carol", PasswordHash = "x" });

        // Alice talked with Bob and Carol, so both are related to her.
        _db.Messages.AddRange(
            new Message { Id = Guid.NewGuid(), SenderId = _alice, ReceiverId = _bob, EncryptedContent = "x" },
            new Message { Id = Guid.NewGuid(), SenderId = _carol, ReceiverId = _alice, EncryptedContent = "x" });
        _db.SaveChanges();

        _privacy = new PrivacyService(_db, new UserService(_db));
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }

    private Task SetLastSeen(Guid user, string level, bool receipts = true) =>
        _privacy.UpdateAsync(user, new PrivacySettingsDto { LastSeen = level, ReadReceipts = receipts });

    [Fact]
    public async Task Everyone_by_default()
    {
        Assert.True(await _privacy.CanSeePresenceAsync(_bob, _alice));
        Assert.Equal(new[] { _bob, _carol }.OrderBy(x => x), (await _privacy.GetPresenceAudienceAsync(_alice)).OrderBy(x => x));
    }

    [Fact]
    public async Task Nobody_hides_presence_both_ways()
    {
        await SetLastSeen(_alice, "nobody");

        Assert.False(await _privacy.CanSeePresenceAsync(_bob, _alice));
        // Mutual: Alice no longer sees others either.
        Assert.False(await _privacy.CanSeePresenceAsync(_alice, _bob));
        Assert.Empty(await _privacy.GetPresenceAudienceAsync(_alice));
        Assert.DoesNotContain(_alice, await _privacy.GetPresenceAudienceAsync(_bob));
        Assert.True(await _privacy.CanSeePresenceAsync(_alice, _alice));
    }

    [Fact]
    public async Task Contacts_only_shows_presence_to_saved_contacts()
    {
        await SetLastSeen(_alice, "contacts");
        _db.Contacts.Add(new Contact { Id = Guid.NewGuid(), OwnerId = _alice, ContactId = _bob });
        await _db.SaveChangesAsync();

        Assert.True(await _privacy.CanSeePresenceAsync(_bob, _alice));
        Assert.False(await _privacy.CanSeePresenceAsync(_carol, _alice));
        Assert.Equal(new[] { _bob }, await _privacy.GetPresenceAudienceAsync(_alice));
    }

    [Fact]
    public async Task Blocking_hides_presence_regardless_of_settings()
    {
        _db.UserBlocks.Add(new UserBlock { BlockerId = _bob, BlockedId = _alice, CreatedAtUtc = DateTime.UtcNow });
        await _db.SaveChangesAsync();

        Assert.False(await _privacy.CanSeePresenceAsync(_alice, _bob));
        Assert.DoesNotContain(_bob, await _privacy.GetPresenceAudienceAsync(_alice));
    }

    [Fact]
    public async Task Read_receipts_need_both_sides()
    {
        Assert.True(await _privacy.ReadReceiptsSharedAsync(_alice, _bob));
        await SetLastSeen(_bob, "everyone", receipts: false);
        Assert.False(await _privacy.ReadReceiptsSharedAsync(_alice, _bob));
        Assert.False(await _privacy.ReadReceiptsSharedAsync(_bob, _alice));

        var settings = await _privacy.GetAsync(_bob);
        Assert.False(settings.ReadReceipts);
        Assert.Equal("everyone", settings.LastSeen);
    }

    [Fact]
    public async Task Hides_read_state_of_my_messages_when_receipts_are_off()
    {
        var messages = new MessageService(_db, new IdentityKeyService(_db), new BlockService(_db), _privacy);
        var sent = await _db.Messages.FirstAsync(m => m.SenderId == _alice);
        sent.IsRead = true;
        sent.ReadAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        Assert.True((await messages.GetConversationPageAsync(_alice, _bob, null, 50)).Items.Single().IsRead);

        await SetLastSeen(_bob, "everyone", receipts: false);
        var item = (await messages.GetConversationPageAsync(_alice, _bob, null, 50)).Items.Single();
        Assert.False(item.IsRead);
        Assert.Null(item.ReadAtUtc);
    }

    [Fact]
    public async Task Ending_a_session_revokes_tokens_access_and_push()
    {
        var revocations = new SessionRevocationList(new MemoryCache(new MemoryCacheOptions()), new FakeTokens());
        var sessions = new SessionService(_db, revocations);

        var phone = Guid.NewGuid();
        var laptop = Guid.NewGuid();
        var started = DateTime.UtcNow.AddDays(-2);
        _db.RefreshTokens.AddRange(
            Token(_alice, phone, started, "Chrome · Android"),
            Token(_alice, laptop, started, "Edge · Windows"));
        _db.PushSubscriptions.Add(new PushSubscription
        {
            Id = Guid.NewGuid(), UserId = _alice, SessionId = phone, Endpoint = "https://fcm.googleapis.com/x",
            EndpointHash = "h", P256dh = "p", Auth = "a", CreatedAtUtc = DateTime.UtcNow
        });
        await _db.SaveChangesAsync();

        var list = await sessions.GetSessionsAsync(_alice, laptop);
        Assert.Equal(2, list.Count);
        Assert.True(list[0].IsCurrent);
        Assert.Equal("Edge · Windows", list[0].DeviceName);

        // Someone else cannot end Alice's session.
        await Assert.ThrowsAsync<NotFoundException>(() => sessions.EndAsync(_bob, phone));

        await sessions.EndAsync(_alice, phone);
        Assert.True(revocations.IsRevoked(phone));
        Assert.False(revocations.IsRevoked(laptop));
        Assert.Empty(_db.PushSubscriptions);
        Assert.Single(await sessions.GetSessionsAsync(_alice, laptop));

        await Assert.ThrowsAsync<NotFoundException>(() => sessions.EndAsync(_alice, phone));
    }

    [Fact]
    public async Task Mutes_are_per_user()
    {
        var mutes = new MuteService(_db);
        await mutes.MuteAsync(_alice, _bob);
        await mutes.MuteAsync(_alice, _bob);   // idempotent

        Assert.Equal(new[] { _bob }, await mutes.GetMutedAsync(_alice));
        Assert.False(await mutes.IsMutedAsync(_bob, _alice));
        await Assert.ThrowsAsync<NotFoundException>(() => mutes.MuteAsync(_alice, Guid.NewGuid()));

        await mutes.UnmuteAsync(_alice, _bob);
        Assert.Empty(await mutes.GetMutedAsync(_alice));
    }

    private static RefreshToken Token(Guid user, Guid family, DateTime started, string device) => new()
    {
        Id = Guid.NewGuid(),
        UserId = user,
        FamilyId = family,
        TokenHash = Guid.NewGuid().ToString("N"),
        CreatedAtUtc = DateTime.UtcNow,
        ExpiresAtUtc = DateTime.UtcNow.AddDays(10),
        SessionStartedAtUtc = started,
        DeviceName = device
    };

    private sealed class FakeTokens : ITokenService
    {
        public TimeSpan RefreshTokenLifetime => TimeSpan.FromDays(30);
        public TimeSpan AccessTokenLifetime => TimeSpan.FromMinutes(15);
        public (string Token, DateTime ExpiresAtUtc) CreateAccessToken(User user, Guid sessionId) => throw new NotSupportedException();
        public string CreateRegistrationToken(string phoneNumber) => throw new NotSupportedException();
        public Task<string?> ValidateRegistrationTokenAsync(string token) => throw new NotSupportedException();
        public string GenerateRefreshToken() => throw new NotSupportedException();
        public string HashRefreshToken(string refreshToken) => throw new NotSupportedException();
    }
}
