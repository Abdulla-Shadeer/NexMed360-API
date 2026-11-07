using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations.Schema;

namespace NexMed360Api.Entities;

public class ApplicationUser : IdentityUser
{
    public Guid PersonId { get; set; }
    public Person Person { get; set; } = new();
    public bool IsPlatformAdmin { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedDate { get; set; } = DateTime.UtcNow;
    public string? PasswordResetOtpHash { get; set; }
    public DateTime? PasswordResetOtpExpiresAt { get; set; }
    public int PasswordResetOtpAttempts { get; set; }

    // Account forms use these names. Personal details are stored once on Person.
    [NotMapped] public string FirstName { get => Person.FirstName; set => Person.FirstName = value; }
    [NotMapped] public string LastName { get => Person.LastName; set => Person.LastName = value; }
}
