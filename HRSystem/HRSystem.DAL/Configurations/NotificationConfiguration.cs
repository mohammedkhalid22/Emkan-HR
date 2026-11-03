using HRSystem.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HRSystem.DAL.Configurations;

public class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> builder)
    {
        builder.Property(n => n.UserId).HasMaxLength(450).IsRequired();
        builder.Property(n => n.Title).HasMaxLength(200).IsRequired();
        builder.Property(n => n.Body).HasMaxLength(500);
        builder.Property(n => n.Url).HasMaxLength(300);
        builder.Property(n => n.Key).HasMaxLength(100);

        // The bell asks "unread for this user, newest first" on every page
        builder.HasIndex(n => new { n.UserId, n.IsRead, n.CreatedAt });

        // One reminder per key per person
        builder.HasIndex(n => new { n.UserId, n.Key }).IsUnique().HasFilter("[Key] IS NOT NULL");
    }
}
