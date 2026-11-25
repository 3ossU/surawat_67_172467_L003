using System;
using System.Collections.Generic;

namespace surawat_67_172467_L003.Models;

public partial class DiningTable
{
    public int Id { get; set; }

    public string TableNumber { get; set; } = null!;

    public int Capacity { get; set; }

    public bool? IsActive { get; set; }

    public DateTime? CreatedAt { get; set; }

    public virtual ICollection<TableBooking> TableBookings { get; set; } = new List<TableBooking>();
}
