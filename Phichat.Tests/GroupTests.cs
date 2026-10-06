using System.Security.Cryptography;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Phichat.Application.Common.Exceptions;
using Phichat.Application.DTOs.Group;
using Phichat.Application.DTOs.Keys;
using Phichat.Application.DTOs.Message;
using Phichat.Application.Security;
using Phichat.Application.Validators.Keys;
using Phichat.Domain.Entities;
using Phichat.Infrastructure.Data;
using Phichat.Infrastructure.Services;

namespace Phichat.Tests;

public class GroupMessageFormatTests
{
    private static string Key(char c) => new(c, 22);
    private static readonly string Wrap = Convert.ToBase64String(new byte[60]);
    private static readonly string Payload = Convert.ToBase64String(new byte[40]);

    [Fact]
    public void Parses_a_group_body()
    {
        var body = $"g1:{Key('a')}:{Key('a')}.{Wrap},{Key('b')}.{Wrap}:{Payload}";
        Assert.True(EncryptedMessageFormat.TryParseGroup(body, out var sender, out var recipients));
        Assert.Equal(Key('a'), sender);
        Assert.Equal(new[] { Key('a'), Key('b') }, recipients);
    }

    [Theory]
    [InlineData("v2:aaaaaaaaaaaaaaaaaaaaaa:bbbbbbbbbbbbbbbbbbbbbb:AAAA")]   // private format
    [InlineData("g1:short:x:y")]
    public void Rejects_malformed_bodies(string body)
    {
        Assert.False(EncryptedMessageFormat.TryParseGroup(body, out _, out _));
    }

    [Fact]
    public void Rejects_duplicate_recipients_bad_wraps_and_short_payloads()
    {
        Assert.False(EncryptedMessageFormat.TryParseGroup($"g1:{Key('a')}:{Key('b')}.{Wrap},{Key('b')}.{Wrap}:{Payload}", out _, out _));
        Assert.False(EncryptedMessageFormat.TryParseGroup($"g1:{Key('a')}:{Key('b')}.{Wrap[..79]}:{Payload}", out _, out _));
        Assert.False(EncryptedMessageFormat.TryParseGroup($"g1:{Key('a')}:{Key('b')}.{Wrap}:{Convert.ToBase64String(new byte[10])}", out _, out _));
        Assert.False(EncryptedMessageFormat.TryParseGroup($"g1:{Key('a')}:{Key('b')}.{Wrap}:{Payload}:extra", out _, out _));
    }
}

public class GroupTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly AppDbContext _db;
    private readonly GroupService _groups;
    private readonly IdentityKeyService _keys;
    private readonly MessageService _messages;
    private readonly Dictionary<Guid, string> _keyIds = new();

    private readonly Guid _alice = Guid.NewGuid();   // owner
    private readonly Guid _bob = Guid.NewGuid();
    private readonly Guid _carol = Guid.NewGuid();
    private readonly Guid _dave = Guid.NewGuid();

    public GroupTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        _db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options);
        _db.Database.EnsureCreated();

        foreach (var (id, name) in new[] { (_alice, "alice"), (_bob, "bob"), (_carol, "carol"), (_dave, "dave") })
            _db.Users.Add(new User { Id = id, Username = name, PasswordHash = "x" });
        _db.SaveChanges();

        var blocks = new BlockService(_db);
        _groups = new GroupService(_db, blocks);
        _keys = new IdentityKeyService(_db);
        _messages = new MessageService(_db, _keys, blocks, new PrivacyService(_db, new UserService(_db)));

        foreach (var id in new[] { _alice, _bob, _carol, _dave })
            _keyIds[id] = PublishKeyAsync(id).GetAwaiter().GetResult();
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

    /// <summary>A well-formed group body wrapped for <paramref name="recipients"/> (the server cannot check more).</summary>
    private string Body(Guid sender, params Guid[] recipients)
    {
        var wrap = Convert.ToBase64String(new byte[60]);
        var entries = string.Join(",", recipients.Select(r => $"{_keyIds[r]}.{wrap}"));
        return $"g1:{_keyIds[sender]}:{entries}:{Convert.ToBase64String(RandomNumberGenerator.GetBytes(40))}";
    }

    private async Task<Guid> CreateAsync(params Guid[] members) =>
        (await _groups.CreateAsync(_alice, new CreateGroupRequest { Title = "  Team  ", MemberIds = members.ToList() })).GroupId;

    private Task<Message> SendAsync(Guid groupId, Guid sender, params Guid[] recipients) =>
        _messages.SendMessageAsync(sender, new SendMessageRequest { GroupId = groupId, EncryptedText = Body(sender, recipients) });

    [Fact]
    public async Task Creating_a_group_records_members_owner_and_a_service_message()
    {
        var result = await _groups.CreateAsync(_alice, new CreateGroupRequest { Title = " Team ", MemberIds = new() { _bob, _bob, _alice } });
        Assert.Equal(new[] { _alice, _bob }.OrderBy(x => x), result.MemberIds.OrderBy(x => x));
        Assert.Contains("\"type\":\"created\"", result.SystemMessage!.SystemEvent);

        var details = await _groups.GetAsync(_bob, result.GroupId);
        Assert.Equal("Team", details.Title);
        Assert.Equal("member", details.MyRole);
        Assert.Equal("owner", details.Members.Single(m => m.UserId == _alice).Role);

        await Assert.ThrowsAsync<NotFoundException>(() => _groups.GetAsync(_carol, result.GroupId));
    }

    [Fact]
    public async Task Messages_must_be_wrapped_for_exactly_the_current_members()
    {
        var group = await CreateAsync(_bob, _carol);

        var sent = await SendAsync(group, _alice, _alice, _bob, _carol);
        Assert.Equal(group, sent.GroupId);
        Assert.Null(sent.ReceiverId);

        var missing = await Assert.ThrowsAsync<ConflictException>(() => SendAsync(group, _alice, _alice, _bob));
        Assert.Equal("group_keys_changed", missing.Code);
        var extra = await Assert.ThrowsAsync<ConflictException>(() => SendAsync(group, _alice, _alice, _bob, _carol, _dave));
        Assert.Equal("group_keys_changed", extra.Code);

        // Claiming another member's key as the sender.
        var forged = await Assert.ThrowsAsync<ConflictException>(() =>
            _messages.SendMessageAsync(_alice, new SendMessageRequest { GroupId = group, EncryptedText = Body(_bob, _alice, _bob, _carol) }));
        Assert.Equal("sender_key_outdated", forged.Code);

        // Private-chat bodies are not accepted in groups.
        await Assert.ThrowsAsync<BadRequestException>(() =>
            _messages.SendMessageAsync(_alice, new SendMessageRequest { GroupId = group, EncryptedText = "v2:" + _keyIds[_alice] + ":" + _keyIds[_bob] + ":" + Convert.ToBase64String(new byte[40]) }));

        // Outsiders cannot write.
        await Assert.ThrowsAsync<NotFoundException>(() => SendAsync(group, _dave, _alice, _bob, _carol, _dave));
    }

    [Fact]
    public async Task New_members_only_see_messages_after_they_joined()
    {
        var group = await CreateAsync(_bob);
        await SendAsync(group, _alice, _alice, _bob);

        await Task.Delay(20);
        await _groups.AddMembersAsync(_alice, group, new[] { _carol });
        await SendAsync(group, _bob, _alice, _bob, _carol);

        var forCarol = await _messages.GetGroupPageAsync(_carol, group, null, 50);
        Assert.Equal(2, forCarol.Items.Count);   // "added" service message + the new message
        Assert.Contains("\"type\":\"added\"", forCarol.Items[0].SystemEvent);
        Assert.Equal(_bob, forCarol.Items[1].SenderId);

        Assert.Equal(4, (await _messages.GetGroupPageAsync(_alice, group, null, 50)).Items.Count);
        await Assert.ThrowsAsync<NotFoundException>(() => _messages.GetGroupPageAsync(_dave, group, null, 50));
    }

    [Fact]
    public async Task Only_admins_manage_members_and_the_owner_manages_admins()
    {
        var group = await CreateAsync(_bob, _carol);

        await Assert.ThrowsAsync<ForbiddenException>(() => _groups.AddMembersAsync(_bob, group, new[] { _dave }));
        await Assert.ThrowsAsync<ForbiddenException>(() => _groups.RemoveMemberAsync(_bob, group, _carol));
        await Assert.ThrowsAsync<ForbiddenException>(() => _groups.SetRoleAsync(_bob, group, _carol, "admin"));

        await _groups.SetRoleAsync(_alice, group, _bob, "admin");
        await _groups.AddMembersAsync(_bob, group, new[] { _dave });
        await _groups.RemoveMemberAsync(_bob, group, _dave);
        // An admin cannot remove the owner.
        await Assert.ThrowsAsync<ForbiddenException>(() => _groups.RemoveMemberAsync(_bob, group, _alice));

        var removed = await _groups.RemoveMemberAsync(_alice, group, _carol);
        Assert.Equal(new[] { _carol }, removed.RemovedUserIds);
        await Assert.ThrowsAsync<NotFoundException>(() => SendAsync(group, _carol, _alice, _bob, _carol));
    }

    [Fact]
    public async Task Blocked_users_cannot_be_added()
    {
        _db.UserBlocks.Add(new UserBlock { BlockerId = _dave, BlockedId = _alice, CreatedAtUtc = DateTime.UtcNow });
        await _db.SaveChangesAsync();

        var error = await Assert.ThrowsAsync<ForbiddenException>(() => CreateAsync(_bob, _dave));
        Assert.Equal("cannot_add_user", error.Code);
    }

    [Fact]
    public async Task Owner_leaving_hands_over_and_the_last_member_deletes_the_group()
    {
        var group = await CreateAsync(_bob, _carol);
        await _groups.SetRoleAsync(_alice, group, _carol, "admin");

        await _groups.LeaveAsync(_alice, group);
        Assert.Equal("owner", (await _groups.GetAsync(_carol, group)).MyRole);

        await _groups.LeaveAsync(_bob, group);
        var last = await _groups.LeaveAsync(_carol, group);
        Assert.Equal(new[] { _carol }, last.RemovedUserIds);
        Assert.False(await _db.Groups.AnyAsync(g => g.Id == group));
        Assert.False(await _db.Messages.AnyAsync(m => m.GroupId == group));
    }

    [Fact]
    public async Task Deleting_a_group_removes_its_messages_pins_and_reactions()
    {
        var group = await CreateAsync(_bob);
        var message = await SendAsync(group, _alice, _alice, _bob);
        var reply = await _messages.SendMessageAsync(_bob, new SendMessageRequest { GroupId = group, EncryptedText = Body(_bob, _alice, _bob), ReplyToMessageId = message.Id });
        await _messages.PinAsync(_bob, message.Id);
        await _messages.AddReactionAsync(_bob, message.Id, "👍");

        await Assert.ThrowsAsync<ForbiddenException>(() => _groups.DeleteAsync(_bob, group));
        var result = await _groups.DeleteAsync(_alice, group);

        Assert.Equal(new[] { _alice, _bob }.OrderBy(x => x), result.RemovedUserIds.OrderBy(x => x));
        Assert.False(await _db.Messages.AnyAsync(m => m.Id == reply.Id));
        Assert.Empty(_db.PinnedMessages);
        Assert.Empty(_db.MessageReactions);
    }

    [Fact]
    public async Task Read_positions_drive_unread_counts_and_ticks()
    {
        var group = await CreateAsync(_bob, _carol);
        var first = await SendAsync(group, _alice, _alice, _bob, _carol);
        await Task.Delay(20);
        var second = await SendAsync(group, _alice, _alice, _bob, _carol);

        var bobs = (await _messages.GetConversationsAsync(_bob)).Single(c => c.IsGroup);
        Assert.Equal(2, bobs.UnreadCount);   // service messages do not count
        Assert.Equal(3, bobs.MemberCount);

        Assert.NotNull(await _messages.MarkGroupReadAsync(group, _bob, first.Id));
        Assert.Equal(1, (await _messages.GetConversationsAsync(_bob)).Single(c => c.IsGroup).UnreadCount);

        // Alice sees a tick on what someone has read.
        var page = await _messages.GetGroupPageAsync(_alice, group, null, 50);
        Assert.True(page.Items.Single(i => i.MessageId == first.Id).IsRead);
        Assert.False(page.Items.Single(i => i.MessageId == second.Id).IsRead);

        await _messages.MarkGroupReadAsync(group, _bob, second.Id);
        Assert.Null(await _messages.MarkGroupReadAsync(group, _bob, first.Id));   // never moves back
        Assert.Equal(0, (await _messages.GetConversationsAsync(_bob)).Single(c => c.IsGroup).UnreadCount);
    }

    [Fact]
    public async Task Admins_delete_any_message_members_only_their_own()
    {
        var group = await CreateAsync(_bob, _carol);
        var bobs = await SendAsync(group, _bob, _alice, _bob, _carol);

        await Assert.ThrowsAsync<ForbiddenException>(() => _messages.DeleteMessageAsync(_carol, bobs.Id, "all"));
        await _messages.DeleteMessageAsync(_alice, bobs.Id, "all");
        Assert.True((await _db.Messages.FindAsync(bobs.Id))!.IsDeleted);

        var audience = await _messages.GetAudienceAsync(bobs.Id);
        Assert.Equal(group, audience!.GroupId);
        Assert.Equal(3, audience.UserIds.Count);
    }

    [Fact]
    public async Task Service_messages_cannot_be_edited_or_reacted_to()
    {
        var group = await CreateAsync(_bob);
        var created = await _db.Messages.FirstAsync(m => m.GroupId == group && m.SystemEvent != null);

        await Assert.ThrowsAsync<ForbiddenException>(() => _messages.EditMessageAsync(_alice, created.Id, Body(_alice, _alice, _bob)));
        await Assert.ThrowsAsync<NotFoundException>(() => _messages.AddReactionAsync(_bob, created.Id, "👍"));
        await Assert.ThrowsAsync<BadRequestException>(() => _messages.PinAsync(_alice, created.Id));
    }

    [Fact]
    public async Task Group_messages_stay_out_of_private_chats()
    {
        var group = await CreateAsync(_bob);
        await SendAsync(group, _alice, _alice, _bob);

        var conversations = await _messages.GetConversationsAsync(_alice);
        Assert.Single(conversations);
        Assert.True(conversations[0].IsGroup);
        Assert.Empty((await _messages.GetConversationPageAsync(_alice, _bob, null, 50)).Items);

        // Group co-members share presence.
        Assert.Contains(_bob, await new UserService(_db).GetRelatedUserIdsAsync(_alice));
    }

    [Fact]
    public async Task Notes_to_self_are_never_unread()
    {
        var key = _keyIds[_alice];
        await _messages.SendMessageAsync(_alice, new SendMessageRequest
        {
            ReceiverId = _alice,
            EncryptedText = $"v2:{key}:{key}:{Convert.ToBase64String(new byte[40])}"
        });

        var saved = (await _messages.GetConversationsAsync(_alice)).Single(c => c.PeerId == _alice);
        Assert.Equal(0, saved.UnreadCount);
    }

    [Fact]
    public async Task Member_keys_are_only_given_to_members()
    {
        var group = await CreateAsync(_bob);
        var keys = await _groups.GetMemberKeysAsync(_bob, group);
        Assert.Equal(new[] { _keyIds[_alice], _keyIds[_bob] }.OrderBy(x => x), keys.Select(k => k.KeyId).OrderBy(x => x));
        await Assert.ThrowsAsync<NotFoundException>(() => _groups.GetMemberKeysAsync(_carol, group));
    }
}
