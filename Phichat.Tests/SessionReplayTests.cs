using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Phichat.Application.Common.Exceptions;
using Phichat.Application.DTOs.Auth;
using Phichat.Application.Interfaces;
using Phichat.Infrastructure.Data;
using Phichat.Infrastructure.Security;
using Phichat.Infrastructure.Services;

namespace Phichat.Tests;

public sealed class SessionReplayTests : IDisposable
{
    private sealed class Client : IClientContext
    {
        public string? DeviceName => "test";
        public string? IpAddress => "::1";
    }

    private sealed class NoSms : ISmsCodeService
    {
        public Task SendCodeAsync(string phoneNumber) => Task.CompletedTask;
        public Task VerifyCodeAsync(string phoneNumber, string code) => Task.CompletedTask;
    }

    private readonly SqliteConnection _connection;
    private readonly AppDbContext _db;
    private readonly AuthService _auth;

    public SessionReplayTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        _connection.CreateCollation("Latin1_General_100_BIN2", string.CompareOrdinal);
        _db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options);
        _db.Database.EnsureCreated();

        var tokens = new TokenService(
            Options.Create(new JwtOptions { Key = new string('k', 48), Issuer = "i", Audience = "a" }),
            NullLogger<TokenService>.Instance);
        var sessions = new SessionService(_db, new SessionRevocationList(new MemoryCache(new MemoryCacheOptions()), tokens));
        _auth = new AuthService(_db, new Pbkdf2PasswordHasher(), tokens, new NoSms(), sessions, new Client(), NullLogger<AuthService>.Instance);
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }

    private Task<AuthResult> RegisterAsync() =>
        _auth.RegisterAsync(new RegisterRequest { Username = "replay_user", Password = "Passw0rd!x" });

    [Fact]
    public async Task Two_tabs_refreshing_at_once_keep_the_session()
    {
        var first = await RegisterAsync();
        await _auth.RefreshAsync(first.RefreshToken);

        // The other tab still holds the token that was rotated a moment ago.
        Assert.NotEmpty((await _auth.RefreshAsync(first.RefreshToken)).RefreshToken);
    }

    [Fact]
    public async Task A_rotated_token_cannot_bring_back_a_session_that_ended()
    {
        var first = await RegisterAsync();
        var second = await _auth.RefreshAsync(first.RefreshToken);
        await _auth.LogoutAsync(second.RefreshToken);

        await Assert.ThrowsAsync<UnauthorizedException>(() => _auth.RefreshAsync(first.RefreshToken));
        Assert.Equal(0, await _db.RefreshTokens.CountAsync(t => t.RevokedAtUtc == null));
    }
}
