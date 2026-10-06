using Microsoft.EntityFrameworkCore;
using Phichat.Domain.Entities;
using System.Collections.Generic;
using System.Reflection.Emit;

namespace Phichat.Infrastructure.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();
    public DbSet<Message> Messages => Set<Message>();
    public DbSet<Contact> Contacts => Set<Contact>();
    public DbSet<PhoneVerification> PhoneVerifications => Set<PhoneVerification>();
    public DbSet<MessageHide> MessageHides => Set<MessageHide>();
    public DbSet<MessageReaction> MessageReactions => Set<MessageReaction>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<UserIdentityKey> UserIdentityKeys => Set<UserIdentityKey>();
    public DbSet<UserBlock> UserBlocks => Set<UserBlock>();
    public DbSet<PinnedMessage> PinnedMessages => Set<PinnedMessage>();
    public DbSet<PushSubscription> PushSubscriptions => Set<PushSubscription>();
    public DbSet<ChatMute> ChatMutes => Set<ChatMute>();
    public DbSet<Group> Groups => Set<Group>();
    public DbSet<GroupMember> GroupMembers => Set<GroupMember>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // User config
        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.Username).IsUnique();
            entity.Property(x => x.Username).IsRequired().HasMaxLength(50);
            entity.Property(x => x.PasswordHash).IsRequired();

        });

        // Message config
        modelBuilder.Entity<Message>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.EncryptedContent).IsRequired();

            entity.HasOne(x => x.Sender)
                  .WithMany()
                  .HasForeignKey(x => x.SenderId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.Receiver)
                  .WithMany()
                  .HasForeignKey(x => x.ReceiverId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<User>()
            .HasIndex(u => u.Username)
            .IsUnique();


        modelBuilder.Entity<Contact>()
            .HasIndex(c => new { c.OwnerId, c.ContactId })
            .IsUnique();

        modelBuilder.Entity<Contact>()
            .HasOne(c => c.Owner)
            .WithMany()
            .HasForeignKey(c => c.OwnerId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Contact>()
            .HasOne(c => c.ContactUser)
            .WithMany()
            .HasForeignKey(c => c.ContactId)
            .OnDelete(DeleteBehavior.Restrict);


        modelBuilder.Entity<User>()
            .HasIndex(u => u.PhoneNumber)
            .IsUnique()
            .HasFilter("[PhoneNumber] IS NOT NULL");

        modelBuilder.Entity<PhoneVerification>()
            .HasIndex(p => p.PhoneNumber);

        modelBuilder.Entity<Message>()
            .HasIndex(m => m.SentAt);

        modelBuilder.Entity<Message>()
            .HasIndex(m => new { m.SenderId, m.ReceiverId, m.SentAt });

        modelBuilder.Entity<Message>()
            .HasOne(m => m.ReplyToMessage)
            .WithMany()
            .HasForeignKey(m => m.ReplyToMessageId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<MessageReaction>()
        .HasKey(x => new { x.MessageId, x.UserId, x.Emoji });


        modelBuilder.Entity<MessageHide>().HasKey(x => new { x.UserId, x.MessageId });

        modelBuilder.Entity<RefreshToken>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.TokenHash).IsRequired().HasMaxLength(64);
            entity.Property(x => x.ReplacedByTokenHash).HasMaxLength(64);
            entity.Property(x => x.DeviceName).HasMaxLength(128);
            entity.Property(x => x.IpAddress).HasMaxLength(45);
            entity.HasIndex(x => x.TokenHash).IsUnique();
            entity.HasIndex(x => x.FamilyId);
            entity.HasIndex(x => new { x.UserId, x.ExpiresAtUtc });

            entity.HasOne(x => x.User)
                  .WithMany()
                  .HasForeignKey(x => x.UserId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<UserIdentityKey>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.KeyId).IsRequired().HasMaxLength(32);
            entity.Property(x => x.PublicKey).IsRequired().HasMaxLength(256);
            entity.Property(x => x.BackupCiphertext).HasMaxLength(1024);
            entity.Property(x => x.BackupSalt).HasMaxLength(128);
            entity.Property(x => x.BackupIv).HasMaxLength(32);
            entity.Property(x => x.BackupKdf).HasMaxLength(32);

            entity.HasIndex(x => x.KeyId).IsUnique();

            // At most one active key per user; also guards concurrent publishes.
            entity.HasIndex(x => x.UserId)
                  .IsUnique()
                  .HasFilter("[RevokedAtUtc] IS NULL")
                  .HasDatabaseName("IX_UserIdentityKeys_UserId_Active");

            entity.HasOne(x => x.User)
                  .WithMany()
                  .HasForeignKey(x => x.UserId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<UserBlock>(entity =>
        {
            entity.HasKey(x => new { x.BlockerId, x.BlockedId });
            entity.HasIndex(x => x.BlockedId);

            entity.HasOne(x => x.Blocker)
                  .WithMany()
                  .HasForeignKey(x => x.BlockerId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(x => x.Blocked)
                  .WithMany()
                  .HasForeignKey(x => x.BlockedId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<PinnedMessage>(entity =>
        {
            entity.HasKey(x => x.MessageId);

            entity.HasOne(x => x.Message)
                  .WithOne()
                  .HasForeignKey<PinnedMessage>(x => x.MessageId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<PushSubscription>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Endpoint).IsRequired().HasMaxLength(2048);
            entity.Property(x => x.EndpointHash).IsRequired().HasMaxLength(64);
            entity.Property(x => x.P256dh).IsRequired().HasMaxLength(128);
            entity.Property(x => x.Auth).IsRequired().HasMaxLength(64);
            entity.Property(x => x.Lang).IsRequired().HasMaxLength(8);

            entity.HasIndex(x => x.EndpointHash).IsUnique();
            entity.HasIndex(x => x.UserId);
            entity.HasIndex(x => x.SessionId);

            entity.HasOne(x => x.User)
                  .WithMany()
                  .HasForeignKey(x => x.UserId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Group>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Title).IsRequired().HasMaxLength(64);
            entity.Property(x => x.Description).HasMaxLength(255);
            entity.Property(x => x.AvatarUrl).HasMaxLength(256);
        });

        modelBuilder.Entity<GroupMember>(entity =>
        {
            entity.HasKey(x => new { x.GroupId, x.UserId });
            entity.HasIndex(x => x.UserId);

            entity.HasOne(x => x.Group)
                  .WithMany()
                  .HasForeignKey(x => x.GroupId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(x => x.User)
                  .WithMany()
                  .HasForeignKey(x => x.UserId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Message>(entity =>
        {
            entity.Property(x => x.SystemEvent).HasMaxLength(2048);
            entity.HasIndex(x => new { x.GroupId, x.SentAt });

            // Group messages are deleted with the group (by the group service, together with their reactions and pins).
            entity.HasOne<Group>()
                  .WithMany()
                  .HasForeignKey(x => x.GroupId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<ChatMute>(entity =>
        {
            entity.HasKey(x => new { x.UserId, x.ChatId });

            entity.HasOne<User>()
                  .WithMany()
                  .HasForeignKey(x => x.UserId)
                  .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
