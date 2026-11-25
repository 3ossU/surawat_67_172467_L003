using System;
using System.Collections.Generic;

namespace surawat_67_172467_L003.Models;

public partial class User
{
    public int Id { get; set; }

    public int RoleId { get; set; }

    public string PhoneNumber { get; set; } = null!;

    public string? PasswordHash { get; set; }

    public string FirstName { get; set; } = null!;

    public string LastName { get; set; } = null!;

    public string? Gender { get; set; }

    public DateOnly? Birthdate { get; set; }

    public bool? IsActive { get; set; }

    public bool IsDeleted { get; set; }

    public DateTime? CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public virtual ICollection<Order> Orders { get; set; } = new List<Order>();

    public virtual Role Role { get; set; } = null!;
}
