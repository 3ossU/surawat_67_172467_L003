using System;
using System.Collections.Generic;

namespace surawat_67_172467_L003.Models;

public partial class PromotionItem
{
    public int Id { get; set; }

    public int PromotionId { get; set; }

    public int FoodId { get; set; }

    public int Quantity { get; set; }

    public virtual Food Food { get; set; } = null!;

    public virtual Promotion Promotion { get; set; } = null!;
}
