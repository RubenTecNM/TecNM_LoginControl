using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace TecNM.Api.Data;

public sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("users");
        builder.HasKey(user => user.Id);
        builder.Property(user => user.Id).HasColumnName("id");
        builder.Property(user => user.UserTypeId).HasColumnName("user_type_id");
        builder.Property(user => user.FirstNames).HasColumnName("first_names").HasMaxLength(150).IsRequired();
        builder.Property(user => user.PaternalLastName).HasColumnName("paternal_last_name").HasMaxLength(100).IsRequired();
        builder.Property(user => user.MaternalLastName).HasColumnName("maternal_last_name").HasMaxLength(100);
        builder.Property(user => user.GoogleEmail).HasColumnName("google_email").HasMaxLength(254);
        builder.Property(user => user.GoogleSubject).HasColumnName("google_subject").HasMaxLength(255).UseCollation("C");
        builder.Property(user => user.MicrosoftEmail).HasColumnName("microsoft_email").HasMaxLength(254);
        builder.Property(user => user.MicrosoftTenantId).HasColumnName("microsoft_tenant_id");
        builder.Property(user => user.MicrosoftObjectId).HasColumnName("microsoft_object_id");
        builder.HasOne(user => user.UserType).WithMany().HasForeignKey(user => user.UserTypeId);
    }
}
