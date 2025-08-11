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
    public DbSet<ChatKey> ChatKeys { get; set; }
    public DbSet<Contact> Contacts => Set<Contact>();




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

        //ChatKey config
        modelBuilder.Entity<ChatKey>(b =>
        {
            b.HasKey(x => x.Id);
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

    }
}
