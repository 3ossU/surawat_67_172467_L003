using System;
using System.Collections.Generic;

namespace surawat_67_172467_L003.Models;

public partial class Bill
{
    public int Id { get; set; }

    public string ReferenceNumber { get; set; } = null!;

    public int OrderId { get; set; }

    public string PayerName { get; set; } = null!;

    public decimal Amount { get; set; }

    public string PaymentMethod { get; set; } = null!;

    public string Status { get; set; } = null!;

    public DateTime? PaidAt { get; set; }

    public DateTime? CreatedAt { get; set; }

    public virtual Order Order { get; set; } = null!;
}
