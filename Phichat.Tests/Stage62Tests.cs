using System.Net;
using System.Security.Cryptography;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Phichat.Application.Common.Exceptions;
using Phichat.Application.DTOs.Keys;
using Phichat.Application.DTOs.Message;
using Phichat.Application.Validators.Keys;
using Phichat.Domain.Entities;
using Phichat.Infrastructure.Data;
using Phichat.Infrastructure.LinkPreview;
using Phichat.Infrastructure.Services;

namespace Phichat.Tests;

public class PublicAddressTests
{
    [Theory]
    [InlineData("127.0.0.1")]
    [InlineData("10.1.2.3")]
    [InlineData("172.16.0.1")]
    [InlineData("172.31.255.255")]
    [InlineData("192.168.1.1")]
    [InlineData("169.254.169.254")]   // cloud metadata
    [InlineData("100.64.0.1")]
    [InlineData("0.0.0.0")]
    [InlineData("224.0.0.1")]
    [InlineData("255.255.255.255")]
    [InlineData("::1")]
    [InlineData("fe80::1")]
    [InlineData("fd00::1")]
    [InlineData("::ffff:127.0.0.1")]  // IPv4-mapped loopback
    [InlineData("::ffff:10.0.0.1")]
    [InlineData("64:ff9b::a00:1")]    // NAT64 to 10.0.0.1
    [InlineData("2002:a00:1::1")]     // 6to4 embedding 10.0.0.1
    public void Blocks_internal_addresses(string ip)
    {
        Assert.False(PublicAddress.IsAllowed(IPAddress.Parse(ip)));
    }

    [Theory]
    [InlineData("8.8.8.8")]
    [InlineData("172.32.0.1")]
    [InlineData("93.184.216.34")]
    [InlineData("2606:4700:4700::1111")]
    public void Allows_public_addresses(string ip)
    {
        Assert.True(PublicAddress.IsAllowed(IPAddress.Parse(ip)));
    }

    [Theory]
    [InlineData("https://example.com/page", true)]
    [InlineData("http://example.com", true)]
    [InlineData("https://example.com:443/x", true)]
    [InlineData("https://example.com:8443/x", false)]   // non-default port
    [InlineData("ftp://example.com", false)]
    [InlineData("file:///etc/passwd", false)]
    [InlineData("javascript:alert(1)", false)]
    [InlineData("https://user:pass@example.com", false)]
    [InlineData("/relative", false)]
    [InlineData("", false)]
    public void Normalizes_only_safe_urls(string url, bool ok)
    {
        Assert.Equal(ok, LinkPreviewService.NormalizeUrl(url) != null);
    }

    [Fact]
    public void Parses_open_graph_and_meta_tags()
    {
        const string html = """
            <html><head>
              <meta property="og:title" content="Hello &amp; welcome">
              <meta content='A   description' name='description' />
              <meta name="og:title" content="ignored duplicate">
              <meta property="og:image" content="/img.png">
            </head></html>
            """;

        var meta = LinkPreviewService.ParseMeta(html);

        Assert.Equal("Hello & welcome", meta["og:title"]);
        Assert.Equal("A description", meta["description"]);
        Assert.Equal("/img.png", meta["og:image"]);
    }
}

/// <summary>Blocking and pins against SQLite (real queries, ExecuteDelete, unique keys).</summary>
public sealed class BlockAndPinTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly AppDbContext _db;
    private readonly BlockService _blocks;
    private readonly IdentityKeyService _keys;
    private readonly MessageService _messages;
    private readonly Guid _alice = Guid.NewGuid();
    private readonly Guid _bob = Guid.NewGuid();
    private readonly Guid _carol = Guid.NewGuid();

    public BlockAndPinTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        _db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options);
        _db.Database.EnsureCreated();

        _db.Users.AddRange(
            new User { Id = _alice, Username = "alice", PasswordHash = "x" },
            new User { Id = _bob, Username = "bob", PasswordHash = "x" },
            new User { Id = _carol, Username = "carol", PasswordHash = "x" });
        _db.SaveChanges();

        _blocks = new BlockService(_db);
        _keys = new IdentityKeyService(_db);
        _messages = new MessageService(_db, _keys, _blocks);
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }

    private async Task<string> PublishKeyAsync(Guid user)
    {
        using var ecdh = ECDiffieHellman.Create(ECCurve.NamedCurves.nistP256);
        var key = await _keys.CreateAsync(user, new PublishIdentityKeyRequest
        {
            PublicKey = Convert.ToBase64String(ecdh.ExportSubjectPublicKeyInfo()),
            Backup = new KeyBackupDto
            {
                Ciphertext = Convert.ToBase64String(new byte[150]),
                Salt = Convert.ToBase64String(new byte[16]),
                Iv = Convert.ToBase64String(new byte[12]),
                Kdf = KeyRules.BackupKdf,
                Iterations = 600_000
            }
        });
        return key.KeyId;
    }

    private async Task<Message> SendAsync(Guid from, Guid to, string fromKey, string toKey) =>
        await _messages.SendMessageAsync(from, new SendMessageRequest
        {
            ReceiverId = to,
            EncryptedText = $"v2:{fromKey}:{toKey}:{Convert.ToBase64String(new byte[40])}"
        });

    [Fact]
    public async Task Blocking_stops_messages_both_ways_with_different_codes()
    {
        var a = await PublishKeyAsync(_alice);
        var b = await PublishKeyAsync(_bob);
        await SendAsync(_alice, _bob, a, b);

        await _blocks.BlockAsync(_alice, _bob);
        await _blocks.BlockAsync(_alice, _bob); // idempotent

        var mine = await Assert.ThrowsAsync<ForbiddenException>(() => SendAsync(_alice, _bob, a, b));
        Assert.Equal("user_blocked", mine.Code);

        // The blocked side is not told it was blocked.
        var theirs = await Assert.ThrowsAsync<ForbiddenException>(() => SendAsync(_bob, _alice, b, a));
        Assert.Equal("cannot_message_user", theirs.Code);

        Assert.True(await _blocks.IsBlockedEitherWayAsync(_bob, _alice));
        Assert.Single(await _blocks.GetBlockedAsync(_alice));
        Assert.Empty(await _blocks.GetBlockedAsync(_bob));

        await _blocks.UnblockAsync(_alice, _bob);
        await SendAsync(_bob, _alice, b, a);
    }

    [Fact]
    public async Task Cannot_block_self_or_unknown_user()
    {
        Assert.Equal("cannot_block_self", (await Assert.ThrowsAsync<BadRequestException>(() => _blocks.BlockAsync(_alice, _alice))).Code);
        await Assert.ThrowsAsync<NotFoundException>(() => _blocks.BlockAsync(_alice, Guid.NewGuid()));
    }

    [Fact]
    public async Task Blocked_users_are_not_related_for_presence()
    {
        var a = await PublishKeyAsync(_alice);
        var b = await PublishKeyAsync(_bob);
        await SendAsync(_alice, _bob, a, b);

        var users = new UserService(_db);
        Assert.Contains(_bob, await users.GetRelatedUserIdsAsync(_alice));

        await _blocks.BlockAsync(_bob, _alice);
        Assert.DoesNotContain(_bob, await users.GetRelatedUserIdsAsync(_alice));
        Assert.DoesNotContain(_alice, await users.GetRelatedUserIdsAsync(_bob));
    }

    [Fact]
    public async Task Pins_are_shared_per_conversation_and_respect_visibility()
    {
        var a = await PublishKeyAsync(_alice);
        var b = await PublishKeyAsync(_bob);
        var c = await PublishKeyAsync(_carol);
        var m1 = await SendAsync(_alice, _bob, a, b);
        var m2 = await SendAsync(_bob, _alice, b, a);
        var other = await SendAsync(_alice, _carol, a, c);

        await _messages.PinAsync(_alice, m1.Id);
        await _messages.PinAsync(_bob, m2.Id);
        await _messages.PinAsync(_bob, m2.Id); // already pinned: no-op

        var forBob = await _messages.GetPinnedAsync(_bob, _alice);
        Assert.Equal(new[] { m2.Id, m1.Id }, forBob.Select(p => p.MessageId));

        // Carol cannot pin a message of someone else's conversation.
        await Assert.ThrowsAsync<NotFoundException>(() => _messages.PinAsync(_carol, m1.Id));
        Assert.Empty(await _messages.GetPinnedAsync(_carol, _alice).ContinueWith(t => t.Result.Where(p => p.MessageId != other.Id).ToList()));

        // Deleted for everyone -> no longer listed.
        await _messages.DeleteMessageAsync(_alice, m1.Id, "all");
        Assert.Equal(new[] { m2.Id }, (await _messages.GetPinnedAsync(_alice, _bob)).Select(p => p.MessageId));

        await _messages.UnpinAsync(_alice, m2.Id);
        Assert.Empty(await _messages.GetPinnedAsync(_bob, _alice));
    }
}
