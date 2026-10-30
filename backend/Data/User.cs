namespace TecNM.Api.Data;

public sealed class User
{
    public Guid Id { get; set; }
    public Guid UserTypeId { get; set; }
    public UserType UserType { get; set; } = null!;
    public string FirstNames { get; set; } = string.Empty;
    public string PaternalLastName { get; set; } = string.Empty;
    public string? MaternalLastName { get; set; }
    public string? GoogleEmail { get; set; }
    public string? GoogleSubject { get; set; }
    public string? MicrosoftEmail { get; set; }
    public Guid? MicrosoftTenantId { get; set; }
    public Guid? MicrosoftObjectId { get; set; }
}
