using System;
using System.Collections.Generic;
namespace surawat_67_172467_L003.Models;

public partial class Review
{
    public int Id { get; set; }
    public int OrderId { get; set; }
    public int FoodId { get; set; }
    public int UserId { get; set; }
    public int Rating { get; set; }
    public string? Comment { get; set; }
    public DateTime? CreatedAt { get; set; }

    public virtual Order Order { get; set; } = null!;
    public virtual Food Food { get; set; } = null!;
    public virtual User User { get; set; } = null!;
}