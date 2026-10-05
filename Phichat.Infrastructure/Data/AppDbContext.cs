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
    }
}
