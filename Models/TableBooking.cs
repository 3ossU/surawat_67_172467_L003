using System;
using System.Collections.Generic;

namespace surawat_67_172467_L003.Models;

public partial class TableBooking
{
    public int Id { get; set; }

    public int TableId { get; set; }

    public int? UserId { get; set; }

    public string CustomerName { get; set; } = null!;

    public string PhoneNumber { get; set; } = null!;

    public int TotalMember { get; set; }

    public DateOnly BookingDate { get; set; }

    public TimeOnly BookingTime { get; set; }

    public string Status { get; set; } = null!;

    public DateTime? CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public virtual DiningTable Table { get; set; } = null!;
}
