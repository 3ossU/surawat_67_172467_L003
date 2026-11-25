using System;
using System.Collections.Generic;

namespace surawat_67_172467_L003.Models;

public partial class Promotion
{
    public int Id { get; set; }

    public string Name { get; set; } = null!;

    public string? Description { get; set; }

    public string? ImageUrl { get; set; }

    public DateOnly StartDate { get; set; }

    public DateOnly EndDate { get; set; }

    public decimal OriginalPrice { get; set; }

    public decimal SpecialPrice { get; set; }

    public decimal DiscountAmount { get; set; }

    public bool? IsActive { get; set; }

    public bool IsDeleted { get; set; }

    public DateTime? CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public virtual ICollection<PromotionItem> PromotionItems { get; set; } = new List<PromotionItem>();
}
