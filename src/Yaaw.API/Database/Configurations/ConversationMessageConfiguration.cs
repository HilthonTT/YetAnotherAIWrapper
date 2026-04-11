using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Yaaw.API.Entities;

namespace Yaaw.API.Database.Configurations;

public sealed class ConversationMessageConfiguration : IEntityTypeConfiguration<ConversationMessage>
{
    public void Configure(EntityTypeBuilder<ConversationMessage> builder)
    {
        builder.HasKey(m => m.Id);

        builder.Property(m => m.Id).HasMaxLength(500);
        builder.Property(m => m.ConversationId).HasMaxLength(500);
        builder.Property(m => m.Role).HasMaxLength(50);
        builder.Property(m => m.Text).HasMaxLength(10_000); // adjust to your needs

        builder.HasIndex(m => m.ConversationId);
    }
}