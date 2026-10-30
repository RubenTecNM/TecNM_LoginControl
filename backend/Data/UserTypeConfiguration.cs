using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace TecNM.Api.Data;

public sealed class UserTypeConfiguration : IEntityTypeConfiguration<UserType>
{
    public void Configure(EntityTypeBuilder<UserType> builder)
    {
        builder.ToTable("user_types");
        builder.HasKey(type => type.Id);
        builder.Property(type => type.Id).HasColumnName("id");
        builder.Property(type => type.Code).HasColumnName("code").HasMaxLength(32).IsRequired();
        builder.Property(type => type.Name).HasColumnName("name").HasMaxLength(100).IsRequired();
    }
}
