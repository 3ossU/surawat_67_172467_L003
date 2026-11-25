using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using surawat_67_172467_L003.Models;

namespace surawat_67_172467_L003.Controllers
{
    public class OrderController : Controller
    {
        private readonly Csi402dbContext _db;

        public OrderController(Csi402dbContext db)
        {
            _db = db;
        }

        public class CartItemDto
        {
            public string Id { get; set; } = string.Empty;
            public string Name { get; set; } = string.Empty;
            public decimal Price { get; set; }
            public int Quantity { get; set; }
        }

        // ==========================================
        // 1. หน้าสรุปคำสั่งซื้อ
        // ==========================================
        [HttpGet]
        public IActionResult Checkout()
        {
            int? sessionUserId = HttpContext.Session.GetInt32("UserId");
            if (sessionUserId == null)
            {
                TempData["Status"] = "กรุณาเข้าสู่ระบบก่อนทำการสั่งซื้ออาหาร";
                return RedirectToAction("Login", "Account");
            }

            // 1. เช็กสิทธิ์สมาชิกใหม่ (ยังไม่เคยมีคำสั่งซื้อสำเร็จมาก่อน)
            bool isFirstOrder = !_db.Orders.Any(o => o.UserId == sessionUserId.Value && o.Status == "COMPLETED");

            // 2. เช็กโปรโมชั่นประจำวันตาม DayOfWeek
            var (dailyPromoName, dailyDiscountPercent, dailyMaxDiscount) = GetDailyPromotionInfo(DateTime.Now.DayOfWeek);

            ViewBag.IsFirstOrder = isFirstOrder;
            ViewBag.DailyPromoName = dailyPromoName;
            ViewBag.DailyDiscountPercent = dailyDiscountPercent;
            ViewBag.DailyMaxDiscount = dailyMaxDiscount;

            return View();
        }

        // ==========================================
        // 2. บันทึกคำสั่งซื้อ (รวมคูปองส่วนลดและวิธีชำระเงิน)
        // ==========================================
        [HttpPost]
        public IActionResult CreateOrder(string address, double distanceKm, string? couponCode,
                                         string paymentMethod, string? note, bool needCutlery, bool needSauce, string cartData)
        {
            try
            {
                int? sessionUserId = HttpContext.Session.GetInt32("UserId");
                if (sessionUserId == null)
                {
                    return Unauthorized(new { success = false, message = "กรุณาเข้าสู่ระบบก่อนทำการสั่งซื้อ" });
                }

                if (string.IsNullOrWhiteSpace(address) || string.IsNullOrWhiteSpace(cartData))
                {
                    return BadRequest(new { success = false, message = "ข้อมูลไม่ครบถ้วน กรุณากรอกที่อยู่จัดส่งหรือเลือกอาหาร" });
                }

                var jsonOptions = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    NumberHandling = JsonNumberHandling.AllowReadingFromString
                };

                var items = JsonSerializer.Deserialize<List<CartItemDto>>(cartData, jsonOptions);

                if (items == null || !items.Any())
                {
                    return BadRequest(new { success = false, message = "ไม่มีรายการสินค้าในตะกร้า" });
                }

                // 1. คำนวณยอดค่าอาหารรวม
                decimal subtotal = items.Sum(i => i.Price * i.Quantity);

                // 2. คำนวณค่าจัดส่งตามระยะทาง (กม.) และเงื่อนไขสั่งครบ 350 บาทส่งฟรี
                double validKm = distanceKm <= 0 ? 1 : distanceKm;
                decimal billedKm = (decimal)Math.Ceiling(validKm);
                decimal deliveryFee = subtotal >= 350 ? 0 : (15 + (billedKm * 10));

                // 3. คำนวณส่วนลดอัตโนมัติ + คูปองส่วนลดที่เลือก
                decimal discountAmount = 0;
                var appliedPromos = new List<string>();

                // 3.1 ส่วนลดสมาชิกใหม่สั่งซื้อครั้งแรก
                bool isFirstOrder = !_db.Orders.Any(o => o.UserId == sessionUserId.Value && o.Status == "COMPLETED");
                if (isFirstOrder && subtotal >= 100)
                {
                    discountAmount += 50;
                    appliedPromos.Add("สมาชิกใหม่ -50 บาท");
                }

                // 3.2 ส่วนลดโปรโมชั่นประจำวันตาม DayOfWeek
                var (dailyPromoName, dailyDiscountPercent, dailyMaxDiscount) = GetDailyPromotionInfo(DateTime.Now.DayOfWeek);
                if (dailyDiscountPercent > 0 && subtotal > 0)
                {
                    decimal dailyDiscount = Math.Floor(subtotal * (dailyDiscountPercent / 100m));
                    if (dailyDiscount > dailyMaxDiscount)
                    {
                        dailyDiscount = dailyMaxDiscount;
                    }
                    if (dailyDiscount > 0)
                    {
                        discountAmount += dailyDiscount;
                        appliedPromos.Add($"{dailyPromoName} -{dailyDiscount:N0} บาท");
                    }
                }

                // 3.3 ส่วนลดจากคูปองที่ลูกค้ากดเลือก
                string? validCouponCode = null;
                if (!string.IsNullOrWhiteSpace(couponCode))
                {
                    string code = couponCode.Trim().ToUpper();
                    decimal couponDiscount = CalculateCouponDiscount(code, subtotal);
                    if (couponDiscount > 0)
                    {
                        validCouponCode = code;
                        discountAmount += couponDiscount;
                        appliedPromos.Add($"คูปอง {code} -{couponDiscount:N0} บาท");
                    }
                }

                if (discountAmount > subtotal)
                {
                    discountAmount = subtotal;
                }

                // 4. ยอดชำระสุทธิทั้งหมด
                decimal totalAmount = Math.Max(0, subtotal + deliveryFee - discountAmount);

                // 5. จัดการประเภทการชำระเงิน (COD หรือ PROMPTPAY)
                string normalizedPaymentMethod = (paymentMethod?.Trim().ToUpper() == "PROMPTPAY") ? "PROMPTPAY" : "COD";
                string paymentStatus = (normalizedPaymentMethod == "PROMPTPAY") ? "PAID" : "PENDING";

                var noteParts = new List<string>
                {
                    subtotal >= 350
                        ? $"[ระยะทาง {validKm:0.#} กม. | ส่งฟรี]"
                        : $"[ระยะทาง {validKm:0.#} กม. (คิด {billedKm} กม.) | ค่าส่ง {deliveryFee:N0} บาท]"
                };

                if (appliedPromos.Any())
                {
                    noteParts.Add($"[{string.Join(", ", appliedPromos)}]");
                }

                if (!string.IsNullOrWhiteSpace(note))
                {
                    noteParts.Add(note.Trim());
                }

                var order = new Order
                {
                    UserId = sessionUserId.Value,
                    OrderNumber = "ORD-" + DateTime.Now.ToString("yyyyMMddHHmmss"),
                    OrderType = "DELIVERY",
                    DeliveryAddress = address.Trim(),
                    NeedCutlery = needCutlery,
                    NeedSauce = needSauce,
                    Notes = string.Join(" ", noteParts),
                    Subtotal = subtotal,
                    DiscountAmount = discountAmount,
                    TotalAmount = totalAmount,
                    CouponCode = validCouponCode,
                    PaymentMethod = normalizedPaymentMethod,
                    PaymentStatus = paymentStatus,
                    Status = "PENDING",
                    CreatedAt = DateTime.Now,
                    UpdatedAt = DateTime.Now
                };

                _db.Orders.Add(order);
                _db.SaveChanges();

                foreach (var item in items)
                {
                    int.TryParse(item.Id, out int foodId);

                    var orderItem = new OrderItem
                    {
                        OrderId = order.Id,
                        FoodId = foodId,
                        FoodName = item.Name,
                        UnitPrice = item.Price,
                        Quantity = item.Quantity,
                        Subtotal = item.Price * item.Quantity,
                        CreatedAt = DateTime.Now
                    };
                    _db.OrderItems.Add(orderItem);
                }

                _db.SaveChanges();

                return Json(new { success = true, orderId = order.Id });
            }
            catch (Exception ex)
            {
                var errorMsg = ex.InnerException != null ? ex.InnerException.Message : ex.Message;
                return StatusCode(500, new { success = false, message = errorMsg });
            }
        }

        // ตัวช่วยคำนวณส่วนลดจากรหัสคูปอง
        private decimal CalculateCouponDiscount(string code, decimal subtotal)
        {
            return code switch
            {
                "SAVE20" when subtotal >= 100 => 20m,
                "HUNGRY50" when subtotal >= 250 => 50m,
                "VIP15" when subtotal >= 300 => Math.Min(80m, Math.Floor(subtotal * 0.15m)),
                _ => 0m
            };
        }

        // ตัวช่วยกำหนดโปรโมชั่นประจำวันตาม DayOfWeek
        private (string PromoName, decimal Percent, decimal MaxDiscount) GetDailyPromotionInfo(DayOfWeek day)
        {
            return day switch
            {
                DayOfWeek.Tuesday => ("โปรวันอังคารสุดคุ้ม ลด 15%", 15m, 60m),
                DayOfWeek.Friday  => ("โปรศุกร์หรรษา ลด 10%", 10m, 50m),
                _                 => ("", 0m, 0m)
            };
        }

        // ==========================================
        // 3. หน้าสถานะคำสั่งซื้อล่าสุด
        // ==========================================
        [HttpGet]
        public IActionResult CurrentOrder()
        {
            int? sessionUserId = HttpContext.Session.GetInt32("UserId");
            if (sessionUserId == null)
            {
                return RedirectToAction("Login", "Account");
            }

            var currentOrder = _db.Orders
                .Include(o => o.OrderItems)
                .Where(o => o.UserId == sessionUserId.Value)
                .OrderByDescending(o => o.CreatedAt)
                .FirstOrDefault();

            return View(currentOrder);
        }

        // ==========================================
        // 4. หน้าประวัติการสั่งซื้อ
        // ==========================================
        [HttpGet]
        public IActionResult History()
        {
            int? sessionUserId = HttpContext.Session.GetInt32("UserId");
            if (sessionUserId == null)
            {
                return RedirectToAction("Login", "Account");
            }

            var orders = _db.Orders
                .Include(o => o.OrderItems)
                .Where(o => o.UserId == sessionUserId.Value)
                .OrderByDescending(o => o.CreatedAt)
                .ToList();

            var reviewedKeys = _db.Reviews
                .Where(r => r.UserId == sessionUserId.Value)
                .Select(r => r.OrderId + "_" + r.FoodId)
                .ToHashSet();

            ViewBag.ReviewedKeys = reviewedKeys;

            return View(orders);
        }

        // ==========================================
        // 5. เปิดหน้ารีวิวอาหาร
        // ==========================================
        [HttpGet]
        public IActionResult Review(int orderId, int foodId)
        {
            int? sessionUserId = HttpContext.Session.GetInt32("UserId");
            if (sessionUserId == null)
            {
                return RedirectToAction("Login", "Account");
            }

            var food = _db.Foods.FirstOrDefault(f => f.Id == foodId);
            if (food == null)
            {
                return NotFound();
            }

            ViewBag.OrderId = orderId;
            return View(food);
        }

        // ==========================================
        // 6. บันทึกคะแนนรีวิวอาหาร
        // ==========================================
        [HttpPost]
        public IActionResult SubmitReview(int orderId, int foodId, int rating, string? comment)
        {
            int? sessionUserId = HttpContext.Session.GetInt32("UserId");
            if (sessionUserId == null)
            {
                return RedirectToAction("Login", "Account");
            }

            bool alreadyReviewed = _db.Reviews.Any(r => r.OrderId == orderId && r.FoodId == foodId && r.UserId == sessionUserId.Value);
            if (alreadyReviewed)
            {
                TempData["Error"] = "คุณได้รีวิวเมนูนี้ในคำสั่งซื้อนี้ไปแล้ว";
                return RedirectToAction("History");
            }

            var review = new Review
            {
                OrderId = orderId,
                FoodId = foodId,
                UserId = sessionUserId.Value,
                Rating = rating < 1 ? 5 : rating,
                Comment = comment,
                CreatedAt = DateTime.Now
            };

            _db.Reviews.Add(review);
            _db.SaveChanges();

            TempData["Success"] = "ขอบคุณสำหรับความคิดเห็นและคะแนนรีวิว";
            return RedirectToAction("History");
        }

        public IActionResult Cart()
        {
            return View();
        }
    }
}