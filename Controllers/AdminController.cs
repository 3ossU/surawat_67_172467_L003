using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using surawat_67_172467_L003.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Filters;
namespace surawat_67_172467_L003.Controllers
{
    public class AdminController : Controller
    {
        private readonly Csi402dbContext _db;

        public AdminController(Csi402dbContext db)
        {
            _db = db;
        }
        // ==========================================
        // ตัวกรองป้องกันการพิมพ์ URL เข้าหน้าข้ามสิทธิ์ของแต่ละตำแหน่ง
        // ==========================================
        public override void OnActionExecuting(ActionExecutingContext context)
        {
            int? userId = HttpContext.Session.GetInt32("UserId");
            int roleId = HttpContext.Session.GetInt32("RoleId") ?? 0;

            // 1. ยังไม่เข้าสู่ระบบ หรือเป็นลูกค้าทั่วไป (RoleId = 1) ห้ามเข้าหลังบ้านเด็ดขาด
            if (userId == null || roleId <= 1)
            {
                context.Result = RedirectToAction("Login", "Account");
                return;
            }

            string actionName = (context.RouteData.Values["action"]?.ToString() ?? "").ToLower();

            // 2. กำหนดสิทธิ์รายชื่อหน้าเว็บและคำสั่งที่แต่ละ RoleId เข้าถึงได้
            // 2 = ADMIN, 3 = MANAGER, 4 = CHEF, 5 = DELIVERY, 6 = WAITER, 7 = FINANCE
            bool isAllowed = actionName switch
            {
                // หน้าภาพรวมร้าน: Manager, Admin, Finance
                "dashboard" => roleId is 2 or 3 or 7,

                // จัดการผู้ใช้งาน: Admin เท่านั้น
                "userlist" or "adduser" or "getuserdetailsjson" or "togglesuspenduser" or "deleteuser" => roleId == 2,

                // จัดการโปรโมชั่น: Admin เท่านั้น
                "promotionlist" or "createpromotion" or "getpromotiondetailsjson" or "deletepromotion" => roleId == 2,

                // จัดการเมนูอาหาร: Admin เท่านั้น
                "foodlist" or "createfood" or "editfood" or "deletefood" => roleId == 2,

                // จัดการคำสั่งซื้อ: Admin, Chef, Delivery
                "orderlist" or "getorderdetailsjson" or "updateorderstatus" => roleId is 2 or 4 or 5,

                // สต็อกวัตถุดิบ (ดูและเบิกใช้): Manager, Chef
                "stocklist" or "adjuststock" => roleId is 3 or 4,

                // สต็อกวัตถุดิบ (เพิ่ม/แก้ไข/ลบรายการ): Manager
                "createstock" or "editstock" or "deletestock" => roleId == 3,

                // คิวจองโต๊ะ: Waiter เท่านั้น
                "bookinglist" or "updatebookingstatus" => roleId == 6,

                // บิลและการเงิน: Finance เท่านั้น
                "billlist" or "getbilldetailsjson" => roleId == 7,

                _ => false
            };

            // 3. หากไม่มีสิทธิ์ ให้ส่งกลับไปหน้าหลักของตำแหน่งตัวเองทันที
            if (!isAllowed)
            {
                if (actionName.Contains("json") || HttpContext.Request.Method == "POST")
                {
                    context.Result = Json(new { success = false, message = "คุณไม่มีสิทธิ์เข้าถึงคำสั่งนี้" });
                    return;
                }

                TempData["Error"] = "คุณไม่มีสิทธิ์เข้าถึงหน้าดังกล่าว";
                context.Result = roleId switch
                {
                    2 or 3 or 7 => RedirectToAction("Dashboard", "Admin"),
                    4 or 5 => RedirectToAction("Orderlist", "Admin"),
                    6 => RedirectToAction("Bookinglist", "Admin"),
                    _ => RedirectToAction("Login", "Account")
                };
                return;
            }

            base.OnActionExecuting(context);
        }

        [HttpGet]
        public IActionResult Dashboard(string period = "all")
        {
            // กรองออเดอร์ที่ไม่ถูกยกเลิก
            var query = _db.Orders
                .Include(o => o.OrderItems)
                .Where(o => o.Status != "CANCELLED");

            DateTime now = DateTime.Now;
            if (period == "today")
            {
                query = query.Where(o => o.CreatedAt.HasValue && o.CreatedAt.Value.Date == now.Date);
            }
            else if (period == "7days")
            {
                var startDate = now.Date.AddDays(-6);
                query = query.Where(o => o.CreatedAt.HasValue && o.CreatedAt.Value.Date >= startDate);
            }
            else if (period == "month")
            {
                query = query.Where(o => o.CreatedAt.HasValue && o.CreatedAt.Value.Month == now.Month && o.CreatedAt.Value.Year == now.Year);
            }

            var orders = query.ToList();

            // 1. คำนวณรายได้
            decimal totalRevenue = orders.Sum(o => o.TotalAmount);
            decimal dineInRevenue = orders.Where(o => o.OrderType == "DINE_IN").Sum(o => o.TotalAmount);
            decimal deliveryRevenue = orders.Where(o => o.OrderType == "DELIVERY").Sum(o => o.TotalAmount);

            // 2. คำนวณจำนวนคน / จำนวนออเดอร์
            int deliveryCustomers = orders.Count(o => o.OrderType == "DELIVERY");
            int dineInOrders = orders.Count(o => o.OrderType == "DINE_IN");
            int bookedGuests = _db.TableBookings
                .Where(b => b.Status != "CANCELLED")
                .Sum(b => (int?)b.TotalMember) ?? 0;
            int dineInCustomers = dineInOrders + bookedGuests;

            int totalOrders = orders.Count;
            int pendingOrders = orders.Count(o => o.Status == "PENDING" || o.Status == "PREPARING");

            // 3. เตรียมข้อมูลกราฟย้อนหลัง 7 วันล่าสุด
            var last7Days = Enumerable.Range(0, 7)
                .Select(i => now.Date.AddDays(-6 + i))
                .ToList();

            var allValidOrders = _db.Orders.Where(o => o.Status != "CANCELLED").ToList();
            var chartLabels = last7Days.Select(d => d.ToString("dd/MM")).ToList();
            var chartDineInData = last7Days.Select(d =>
                allValidOrders.Where(o => o.CreatedAt?.Date == d && o.OrderType == "DINE_IN").Sum(o => o.TotalAmount)
            ).ToList();
            var chartDeliveryData = last7Days.Select(d =>
                allValidOrders.Where(o => o.CreatedAt?.Date == d && o.OrderType == "DELIVERY").Sum(o => o.TotalAmount)
            ).ToList();

            // 4. เมนูขายดี 5 อันดับแรก
            var topFoods = orders
                .SelectMany(o => o.OrderItems)
                .GroupBy(i => i.FoodName)
                .Select(g => new
                {
                    FoodName = g.Key,
                    TotalQty = g.Sum(x => x.Quantity),
                    TotalSales = g.Sum(x => x.Subtotal)
                })
                .OrderByDescending(x => x.TotalQty)
                .Take(5)
                .ToList();

            ViewBag.Period = period;
            ViewBag.TotalRevenue = totalRevenue;
            ViewBag.DineInRevenue = dineInRevenue;
            ViewBag.DeliveryRevenue = deliveryRevenue;
            ViewBag.DineInCustomers = dineInCustomers;
            ViewBag.DeliveryCustomers = deliveryCustomers;
            ViewBag.TotalOrders = totalOrders;
            ViewBag.PendingOrders = pendingOrders;

            ViewBag.ChartLabels = JsonSerializer.Serialize(chartLabels);
            ViewBag.ChartDineIn = JsonSerializer.Serialize(chartDineInData);
            ViewBag.ChartDelivery = JsonSerializer.Serialize(chartDeliveryData);
            ViewBag.TopFoods = topFoods;

            return View();
        }
        // ==========================================
        // 1. หน้าเพิ่มผู้ใช้งาน (Adduser)
        // ==========================================
        [HttpGet]
        public IActionResult Adduser()
        {
            return View();
        }

        [HttpPost]
        public IActionResult Adduser(string FirstName, string LastName, string PhoneNumber,
                                     DateTime BirthDate, string Gender, string Role, string Password)
        {
            if (string.IsNullOrEmpty(Password))
            {
                ViewBag.Status = "กรุณากรอกรหัสผ่าน";
                return View();
            }

            // ป้องกันการเพิ่มบทบาท Admin และ Manager
            if (string.Equals(Role, "Admin", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(Role, "Manager", StringComparison.OrdinalIgnoreCase))
            {
                ViewBag.Status = "ไม่อนุญาตให้เพิ่มผู้ใช้งานในบทบาท Admin หรือ Manager";
                return View();
            }

            var userCheck = (from u in _db.Users
                             where u.PhoneNumber == PhoneNumber
                             select u).FirstOrDefault();

            if (userCheck == null)
            {
                // 1 = CUSTOMER, 4 = CHEF, 5 = DELIVERY, 6 = WAITER, 7 = FINANCE
                int selectedRoleId = Role switch
                {
                    "Chef" => 4,
                    "Rider" => 5,
                    "Delivery" => 5,
                    "Waiter" => 6,
                    "Finance" => 7,
                    _ => 1
                };

                var hasher = new PasswordHasher<User>();
                User newUser = new User
                {
                    FirstName = FirstName.Trim(),
                    LastName = LastName.Trim(),
                    PhoneNumber = PhoneNumber.Trim(),
                    Gender = Gender,
                    Birthdate = DateOnly.FromDateTime(BirthDate),
                    RoleId = selectedRoleId,
                    IsActive = true,
                    IsDeleted = false,
                    CreatedAt = DateTime.Now,
                    UpdatedAt = DateTime.Now
                };

                // เข้ารหัสรหัสผ่านก่อนบันทึกลงฐานข้อมูล
                newUser.PasswordHash = hasher.HashPassword(newUser, Password);

                _db.Users.Add(newUser);
                _db.SaveChanges();

                return RedirectToAction("Userlist", "Admin");
            }
            else
            {
                ViewBag.Status = "เบอร์โทรศัพท์นี้ถูกใช้งานไปแล้ว";
                return View();
            }
        }

        // ==========================================
        // 2. หน้ารายชื่อผู้ใช้งาน (Userlist) - ซ่อน Admin (2) และ Manager (3)
        // ==========================================
        [HttpGet]
        public IActionResult Userlist(string? searchString)
        {
            var userList = (from u in _db.Users
                            where u.IsDeleted == false && u.RoleId != 2 && u.RoleId != 3
                            select u).ToList();

            if (!string.IsNullOrEmpty(searchString))
            {
                userList = userList.Where(u => u.FirstName.Contains(searchString)
                                            || u.LastName.Contains(searchString)
                                            || u.PhoneNumber.Contains(searchString)
                                            || u.Id.ToString() == searchString).ToList();
            }

            return View(userList);
        }

        // ==========================================
        // 3. ดึงรายละเอียดผู้ใช้สำหรับหน้าต่างตรวจสอบ (JSON) - บล็อก Admin และ Manager
        // ==========================================
        [HttpGet]
        public IActionResult GetUserDetailsJson(int id)
        {
            var user = (from u in _db.Users
                        where u.Id == id && u.IsDeleted == false && u.RoleId != 2 && u.RoleId != 3
                        select u).FirstOrDefault();

            if (user == null) return NotFound();

            var orderIds = (from o in _db.Orders
                            where o.UserId == id
                            orderby o.CreatedAt descending
                            select o.OrderNumber).Take(10).ToList();

            string ageDisplay = "-";
            if (user.Birthdate.HasValue)
            {
                var today = DateOnly.FromDateTime(DateTime.Today);
                int age = today.Year - user.Birthdate.Value.Year;
                if (user.Birthdate.Value > today.AddYears(-age)) age--;
                ageDisplay = age > 0 ? age.ToString() : "-";
            }

            string roleName = user.RoleId switch
            {
                1 => "Customer",
                4 => "Chef",
                5 => "Delivery",
                6 => "Waiter",
                7 => "Finance",
                _ => "-"
            };

            return Json(new
            {
                id = user.Id,
                firstName = user.FirstName,
                lastName = user.LastName,
                roleName = roleName,
                age = ageDisplay,
                phoneNumber = user.PhoneNumber,
                gender = user.Gender,
                birthDate = user.Birthdate?.ToString("dd-MM-yyyy"),
                createdAt = user.CreatedAt?.ToString("dd-MM-yyyy"),
                isActive = user.IsActive,
                orders = orderIds
            });
        }

        // ==========================================
        // 4. ระงับบัญชี / ยกเลิกระงับบัญชี - บล็อก Admin และ Manager
        // ==========================================
        [HttpPost]
        public IActionResult ToggleSuspendUser(int id)
        {
            var user = (from u in _db.Users
                        where u.Id == id && u.IsDeleted == false && u.RoleId != 2 && u.RoleId != 3
                        select u).FirstOrDefault();

            if (user != null)
            {
                user.IsActive = !user.IsActive;
                user.UpdatedAt = DateTime.Now;
                _db.SaveChanges();
                return Json(new { success = true, isActive = user.IsActive });
            }

            return Json(new { success = false, message = "ไม่พบผู้ใช้ หรือไม่สามารถดำเนินการกับบัญชีระดับผู้ดูแลระบบได้" });
        }

        // ==========================================
        // 5. ลบบัญชีผู้ใช้งาน (Soft Delete) - บล็อก Admin และ Manager
        // ==========================================
        [HttpPost]
        public IActionResult DeleteUser(int id)
        {
            var user = (from u in _db.Users
                        where u.Id == id && u.IsDeleted == false && u.RoleId != 2 && u.RoleId != 3
                        select u).FirstOrDefault();

            if (user != null)
            {
                user.IsDeleted = true;      // ซ่อนออกจากระบบ
                user.IsActive = false;      // ปิดการใช้งานบัญชีควบคู่ไปด้วย
                user.UpdatedAt = DateTime.Now;

                _db.SaveChanges();
            }
            else
            {
                TempData["Error"] = "ไม่สามารถลบบัญชีระดับ Admin หรือ Manager ได้";
            }

            return RedirectToAction("Userlist", "Admin");
        }
        // ==========================================
        // 6. หน้าตารางจัดการคำสั่งซื้อ (Orderlist)
        // ==========================================
        [HttpGet]
        public IActionResult Orderlist()
        {
            var orders = _db.Orders
                .Include(o => o.User)
                .Include(o => o.OrderItems)
                .OrderByDescending(o => o.CreatedAt)
                .ToList();

            ViewBag.RoleId = HttpContext.Session.GetInt32("RoleId") ?? 0;
            return View(orders);
        }

        // ==========================================
        // 7. ดึงรายละเอียดออเดอร์สำหรับ Modal Inspect (JSON)
        // ==========================================
        [HttpGet]
        public IActionResult GetOrderDetailsJson(int id)
        {
            var order = _db.Orders
                .Include(o => o.User)
                .Include(o => o.OrderItems)
                .FirstOrDefault(o => o.Id == id);

            if (order == null) return NotFound();

            string customerName = order.User != null
                ? $"{order.User.FirstName} {order.User.LastName}"
                : "ลูกค้าทั่วไป (Guest)";

            var items = order.OrderItems.Select(i => new
            {
                foodName = i.FoodName,
                quantity = i.Quantity,
                unitPrice = i.UnitPrice,
                subtotal = i.Subtotal
            }).ToList();

            return Json(new
            {
                id = order.Id,
                orderNumber = order.OrderNumber,
                customerName = customerName,
                phoneNumber = order.User?.PhoneNumber ?? "-",
                orderType = order.OrderType,
                tableNumber = order.TableNumber ?? "-",
                deliveryAddress = string.IsNullOrEmpty(order.DeliveryAddress) ? "-" : order.DeliveryAddress,
                notes = string.IsNullOrEmpty(order.Notes) ? "-" : order.Notes,
                needCutlery = order.NeedCutlery,
                needSauce = order.NeedSauce,
                subtotal = order.Subtotal,
                discountAmount = order.DiscountAmount,
                totalAmount = order.TotalAmount,
                status = order.Status,
                createdAt = order.CreatedAt?.ToString("dd/MM/yyyy HH:mm") ?? "-",
                items = items
            });
        }

        // ==========================================
        // 8. อัปเดตสถานะคำสั่งซื้อ (แยกสิทธิ์ตามตำแหน่ง)
        // ==========================================
        [HttpPost]
        public IActionResult UpdateOrderStatus(int id, string status)
        {
            int roleId = HttpContext.Session.GetInt32("RoleId") ?? 0;
            var order = _db.Orders.FirstOrDefault(o => o.Id == id);

            if (order == null)
            {
                return Json(new { success = false, message = "ไม่พบคำสั่งซื้อ" });
            }

            // ตรวจสอบสิทธิ์ตามตำแหน่งงาน
            bool isAllowed = false;

            if (roleId == 2) // ADMIN: ทำได้ทุกขั้นตอนและยกเลิกได้
            {
                isAllowed = true;
            }
            else if (roleId == 4) // CHEF: เริ่มทำอาหาร และ ส่งอาหารให้คนขับ เท่านั้น
            {
                if ((order.Status == "PENDING" && status == "PREPARING") ||
                    (order.Status == "PREPARING" && status == "ON_DELIVERY"))
                {
                    isAllowed = true;
                }
            }
            else if (roleId == 5) // DELIVERY: ยืนยันจัดส่งสำเร็จ เท่านั้น
            {
                if (order.Status == "ON_DELIVERY" && status == "COMPLETED")
                {
                    isAllowed = true;
                }
            }

            if (!isAllowed)
            {
                return Json(new { success = false, message = "ตำแหน่งของคุณไม่มีสิทธิ์เปลี่ยนสถานะในขั้นตอนนี้" });
            }

            order.Status = status;
            if (status == "COMPLETED")
            {
                order.PaymentStatus = "PAID";
            }
            order.UpdatedAt = DateTime.Now;

            _db.SaveChanges();
            return Json(new { success = true });
        }
        // ==========================================
        // 9. หน้าจัดการโปรโมชั่น (Promotionlist)
        // ==========================================
        [HttpGet]
        public IActionResult Promotionlist()
        {
            var promotions = _db.Promotions
                .Include(p => p.PromotionItems)
                    .ThenInclude(pi => pi.Food)
                .Where(p => p.IsDeleted == false)
                .OrderByDescending(p => p.Id)
                .ToList();

            // ดึงเมนูอาหารทั้งหมดไปใส่ใน Dropdown ของ Modal สร้างโปรโมชั่น
            var foods = _db.Foods
                .Where(f => f.IsDeleted == false && f.IsAvailable == true)
                .OrderBy(f => f.Name)
                .ToList();

            ViewBag.Foods = foods;
            return View(promotions);
        }

        // ==========================================
        // 10. บันทึกสร้างโปรโมชั่นใหม่ (CreatePromotion)
        // ==========================================
        [HttpPost]
        public IActionResult CreatePromotion(string name, int? foodId1, int? foodId2, int? foodId3,
                                             DateOnly startDate, DateOnly endDate, decimal specialPrice, string? description)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                TempData["Error"] = "กรุณากรอกชื่อโปรโมชั่น";
                return RedirectToAction("Promotionlist");
            }

            if (endDate < startDate)
            {
                TempData["Error"] = "วันที่สิ้นสุดโปรโมชั่นต้องไม่น้อยกว่าวันที่เริ่มต้น";
                return RedirectToAction("Promotionlist");
            }

            // รวมรายการอาหารที่เลือก (รองรับการเลือกเมนูซ้ำกันได้ เช่น ไก่ทอด 2 ชิ้น)
            var selectedFoodIds = new List<int>();
            if (foodId1.HasValue && foodId1.Value > 0) selectedFoodIds.Add(foodId1.Value);
            if (foodId2.HasValue && foodId2.Value > 0) selectedFoodIds.Add(foodId2.Value);
            if (foodId3.HasValue && foodId3.Value > 0) selectedFoodIds.Add(foodId3.Value);

            if (!selectedFoodIds.Any())
            {
                TempData["Error"] = "กรุณาเลือกอาหารที่ต้องการจัดโปรโมชั่นอย่างน้อย 1 รายการ";
                return RedirectToAction("Promotionlist");
            }

            // คำนวณราคารวมปกติจากฐานข้อมูลจริง
            decimal originalPrice = 0;
            var groupedItems = selectedFoodIds
                .GroupBy(id => id)
                .Select(g => new { FoodId = g.Key, Quantity = g.Count() })
                .ToList();

            string? coverImageUrl = null;
            foreach (var item in groupedItems)
            {
                var food = _db.Foods.FirstOrDefault(f => f.Id == item.FoodId);
                if (food != null)
                {
                    originalPrice += food.Price * item.Quantity;
                    if (string.IsNullOrEmpty(coverImageUrl))
                    {
                        coverImageUrl = food.ImageUrl;
                    }
                }
            }

            decimal discountAmount = originalPrice - specialPrice;
            if (discountAmount < 0) discountAmount = 0;

            var promo = new Promotion
            {
                Name = name.Trim(),
                Description = description,
                ImageUrl = coverImageUrl,
                OriginalPrice = originalPrice,
                SpecialPrice = specialPrice,
                DiscountAmount = discountAmount,
                StartDate = startDate,
                EndDate = endDate,
                IsActive = true,
                IsDeleted = false,
                CreatedAt = DateTime.Now,
                UpdatedAt = DateTime.Now
            };

            _db.Promotions.Add(promo);
            _db.SaveChanges(); // บันทึกเพื่อรับ promo.Id

            // บันทึกรายการอาหารในเซตโปรโมชั่นลงตาราง promotion_items
            foreach (var item in groupedItems)
            {
                _db.PromotionItems.Add(new PromotionItem
                {
                    PromotionId = promo.Id,
                    FoodId = item.FoodId,
                    Quantity = item.Quantity
                });
            }
            _db.SaveChanges();

            TempData["Success"] = $"สร้างโปรโมชั่น \"{name}\" เรียบร้อยแล้ว!";
            return RedirectToAction("Promotionlist");
        }

        // ==========================================
        // 11. ดึงรายละเอียดโปรโมชั่นสำหรับ Modal Inspect (JSON)
        // ==========================================
        [HttpGet]
        public IActionResult GetPromotionDetailsJson(int id)
        {
            var promo = _db.Promotions
                .Include(p => p.PromotionItems)
                    .ThenInclude(pi => pi.Food)
                .FirstOrDefault(p => p.Id == id && p.IsDeleted == false);

            if (promo == null) return NotFound();

            var items = promo.PromotionItems.Select(pi => new
            {
                foodName = pi.Food?.Name ?? "-",
                quantity = pi.Quantity,
                price = pi.Food?.Price ?? 0
            }).ToList();

            return Json(new
            {
                id = promo.Id,
                name = promo.Name,
                originalPrice = promo.OriginalPrice,
                specialPrice = promo.SpecialPrice,
                discountAmount = promo.DiscountAmount,
                startDate = promo.StartDate.ToString("dd/MM/yyyy"),
                endDate = promo.EndDate.ToString("dd/MM/yyyy"),
                isActive = promo.IsActive,
                items = items
            });
        }

        // ==========================================
        // 12. ลบโปรโมชั่น (Soft Delete)
        // ==========================================
        [HttpPost]
        public IActionResult DeletePromotion(int id)
        {
            var promo = _db.Promotions.FirstOrDefault(p => p.Id == id);
            if (promo != null)
            {
                promo.IsDeleted = true;
                promo.IsActive = false;
                promo.UpdatedAt = DateTime.Now;
                _db.SaveChanges();
                TempData["Success"] = "ลบโปรโมชั่นเรียบร้อยแล้ว";
            }
            return RedirectToAction("Promotionlist");
        }
        // ==========================================
        // 13. หน้าจัดการเมนูอาหาร (Foodlist)
        // ==========================================
        [HttpGet]
        public IActionResult Foodlist()
        {
            var foods = _db.Foods
                .Include(f => f.Category)
                .Where(f => f.IsDeleted == false)
                .OrderByDescending(f => f.Id)
                .ToList();

            var categories = _db.Categories
                .Where(c => c.IsDeleted == false && c.IsActive == true)
                .OrderBy(c => c.DisplayOrder)
                .ToList();

            ViewBag.Categories = categories;
            return View(foods);
        }

        // ==========================================
        // 14. เพิ่มเมนูอาหารใหม่ (CreateFood)
        // ==========================================
        [HttpPost]
        public async Task<IActionResult> CreateFood(string name, int categoryId, decimal price,
                                                    string? description, string? imageUrl, IFormFile? imageFile, bool isAvailable = true)
        {
            if (string.IsNullOrWhiteSpace(name) || categoryId <= 0)
            {
                TempData["Error"] = "กรุณากรอกชื่อเมนูอาหารและเลือกหมวดหมู่ให้ครบถ้วน";
                return RedirectToAction("Foodlist");
            }

            string? finalImageUrl = imageUrl?.Trim();

            // หากมีการอัปโหลดไฟล์รูปภาพจากเครื่อง ให้บันทึกลง wwwroot/images/foods
            if (imageFile != null && imageFile.Length > 0)
            {
                var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "images", "foods");
                if (!Directory.Exists(uploadsFolder))
                {
                    Directory.CreateDirectory(uploadsFolder);
                }

                var ext = Path.GetExtension(imageFile.FileName);
                var fileName = $"food_{DateTime.Now:yyyyMMddHHmmssfff}{ext}";
                var filePath = Path.Combine(uploadsFolder, fileName);

                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await imageFile.CopyToAsync(stream);
                }

                finalImageUrl = $"/images/foods/{fileName}";
            }

            var newFood = new Food
            {
                Name = name.Trim(),
                CategoryId = categoryId,
                Price = price,
                Description = description?.Trim(),
                ImageUrl = finalImageUrl,
                IsAvailable = isAvailable,
                IsDeleted = false,
                CreatedAt = DateTime.Now,
                UpdatedAt = DateTime.Now
            };

            _db.Foods.Add(newFood);
            await _db.SaveChangesAsync();

            TempData["Success"] = $"เพิ่มเมนู \"{name}\" เรียบร้อยแล้ว!";
            return RedirectToAction("Foodlist");
        }

        // ==========================================
        // 15. แก้ไขเมนูอาหาร (EditFood)
        // ==========================================
        [HttpPost]
        public async Task<IActionResult> EditFood(int id, string name, int categoryId, decimal price,
                                                  string? description, string? imageUrl, IFormFile? imageFile, bool isAvailable)
        {
            var food = _db.Foods.FirstOrDefault(f => f.Id == id && f.IsDeleted == false);
            if (food == null)
            {
                TempData["Error"] = "ไม่พบเมนูอาหารที่ต้องการแก้ไข";
                return RedirectToAction("Foodlist");
            }

            if (imageFile != null && imageFile.Length > 0)
            {
                var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "images", "foods");
                if (!Directory.Exists(uploadsFolder))
                {
                    Directory.CreateDirectory(uploadsFolder);
                }

                var ext = Path.GetExtension(imageFile.FileName);
                var fileName = $"food_{DateTime.Now:yyyyMMddHHmmssfff}{ext}";
                var filePath = Path.Combine(uploadsFolder, fileName);

                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await imageFile.CopyToAsync(stream);
                }

                food.ImageUrl = $"/images/foods/{fileName}";
            }
            else if (!string.IsNullOrWhiteSpace(imageUrl))
            {
                food.ImageUrl = imageUrl.Trim();
            }

            food.Name = name.Trim();
            food.CategoryId = categoryId;
            food.Price = price;
            food.Description = description?.Trim();
            food.IsAvailable = isAvailable;
            food.UpdatedAt = DateTime.Now;

            await _db.SaveChangesAsync();

            TempData["Success"] = $"อัปเดตเมนู \"{name}\" เรียบร้อยแล้ว!";
            return RedirectToAction("Foodlist");
        }

        // ==========================================
        // 16. ลบเมนูอาหาร (Soft Delete)
        // ==========================================
        [HttpPost]
        public IActionResult DeleteFood(int id)
        {
            var food = _db.Foods.FirstOrDefault(f => f.Id == id);
            if (food != null)
            {
                food.IsDeleted = true;
                food.IsAvailable = false;
                food.UpdatedAt = DateTime.Now;
                _db.SaveChanges();
                TempData["Success"] = "ลบเมนูอาหารเรียบร้อยแล้ว";
            }
            return RedirectToAction("Foodlist");
        }
        // ==========================================
        // 17. หน้าจัดการสต็อกวัตถุดิบ (Stocklist) - Chef & Manager & Admin
        // ==========================================
        [HttpGet]
        public IActionResult Stocklist()
        {
            var stocks = _db.Ingredients
                .Where(s => s.IsDeleted == false)
                .OrderBy(s => s.Quantity <= s.MinStockAlert ? 0 : 1) // เอาของใกล้หมด/หมด ขึ้นบนสุดก่อน
                .ThenBy(s => s.Name)
                .ToList();

            ViewBag.RoleId = HttpContext.Session.GetInt32("RoleId") ?? 2;
            return View(stocks);
        }

        // ==========================================
        // 18. เพิ่มวัตถุดิบใหม่ (CreateStock) - Manager & Admin
        // ==========================================
        [HttpPost]
        public async Task<IActionResult> CreateStock(string name, string category, decimal quantity,
                                                     string unit, decimal minStockAlert,
                                                     string? imageUrl, IFormFile? imageFile)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                TempData["Error"] = "กรุณากรอกชื่อวัตถุดิบ";
                return RedirectToAction("Stocklist");
            }

            string? finalImageUrl = imageUrl?.Trim();

            if (imageFile != null && imageFile.Length > 0)
            {
                var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "images", "stocks");
                if (!Directory.Exists(uploadsFolder))
                {
                    Directory.CreateDirectory(uploadsFolder);
                }

                var ext = Path.GetExtension(imageFile.FileName);
                var fileName = $"stock_{DateTime.Now:yyyyMMddHHmmssfff}{ext}";
                var filePath = Path.Combine(uploadsFolder, fileName);

                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await imageFile.CopyToAsync(stream);
                }

                finalImageUrl = $"/images/stocks/{fileName}";
            }

            var item = new Ingredient
            {
                Name = name.Trim(),
                Category = category,
                Quantity = quantity,
                Unit = string.IsNullOrWhiteSpace(unit) ? "Kg" : unit.Trim(),
                MinStockAlert = minStockAlert,
                ImageUrl = finalImageUrl,
                IsDeleted = false,
                CreatedAt = DateTime.Now,
                UpdatedAt = DateTime.Now
            };

            _db.Ingredients.Add(item);
            await _db.SaveChangesAsync();

            TempData["Success"] = $"เพิ่มวัตถุดิบ \"{name}\" เรียบร้อยแล้ว!";
            return RedirectToAction("Stocklist");
        }

        // ==========================================
        // 19. เบิกใช้ / เติมสต็อกวัตถุดิบ (AdjustStock)
        // - Chef (4): เบิกใช้ได้เท่านั้น
        // - Manager (3)  เบิกใช้ และ เติมสต็อกได้
        // ==========================================
        [HttpPost]
        public IActionResult AdjustStock(int id, string adjustType, decimal amount)
        {
            int roleId = HttpContext.Session.GetInt32("RoleId") ?? 0;

            var item = _db.Ingredients.FirstOrDefault(s => s.Id == id && s.IsDeleted == false);
            if (item == null || amount <= 0)
            {
                TempData["Error"] = "ข้อมูลไม่ถูกต้อง กรุณาระบุจำนวนมากกว่า 0";
                return RedirectToAction("Stocklist");
            }

            if (adjustType == "USE") // เบิกใช้วัตถุดิบ (ทำได้ทั้ง Chef และ Manager)
            {
                if (amount > item.Quantity)
                {
                    TempData["Error"] = $"ไม่สามารถเบิก \"{item.Name}\" เกินจำนวนคงเหลือได้ (คงเหลือ {item.Quantity:0.##} {item.Unit})";
                    return RedirectToAction("Stocklist");
                }

                item.Quantity -= amount;
                TempData["Success"] = $"บันทึกการเบิกใช้ \"{item.Name}\" จำนวน {amount:0.##} {item.Unit} แล้ว";
            }
            else if (adjustType == "RESTOCK") // เติมของเข้าสต็อก (เฉพาะ Manager เท่านั้น)
            {
                if (roleId != 3)
                {
                    TempData["Error"] = "ตำแหน่งพ่อครัวสามารถเบิกใช้วัตถุดิบได้เท่านั้น ไม่สามารถเติมสต็อกได้";
                    return RedirectToAction("Stocklist");
                }

                item.Quantity += amount;
                TempData["Success"] = $"เติมสต็อก \"{item.Name}\" เพิ่ม {amount:0.##} {item.Unit} เรียบร้อยแล้ว";
            }
            else
            {
                TempData["Error"] = "ประเภทรายการไม่ถูกต้อง";
                return RedirectToAction("Stocklist");
            }

            item.UpdatedAt = DateTime.Now;
            _db.SaveChanges();
            return RedirectToAction("Stocklist");
        }
        // ==========================================
        // 20. แก้ไขข้อมูลวัตถุดิบ (EditStock) - Manager & Admin
        // ==========================================
        [HttpPost]
        public async Task<IActionResult> EditStock(int id, string name, string category, decimal quantity,
                                                   string unit, decimal minStockAlert,
                                                   string? imageUrl, IFormFile? imageFile)
        {
            var item = _db.Ingredients.FirstOrDefault(s => s.Id == id && s.IsDeleted == false);
            if (item == null)
            {
                TempData["Error"] = "ไม่พบข้อมูลวัตถุดิบ";
                return RedirectToAction("Stocklist");
            }

            if (imageFile != null && imageFile.Length > 0)
            {
                var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "images", "stocks");
                if (!Directory.Exists(uploadsFolder))
                {
                    Directory.CreateDirectory(uploadsFolder);
                }

                var ext = Path.GetExtension(imageFile.FileName);
                var fileName = $"stock_{DateTime.Now:yyyyMMddHHmmssfff}{ext}";
                var filePath = Path.Combine(uploadsFolder, fileName);

                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await imageFile.CopyToAsync(stream);
                }

                item.ImageUrl = $"/images/stocks/{fileName}";
            }
            else if (!string.IsNullOrWhiteSpace(imageUrl))
            {
                item.ImageUrl = imageUrl.Trim();
            }

            item.Name = name.Trim();
            item.Category = category;
            item.Quantity = quantity;
            item.Unit = unit.Trim();
            item.MinStockAlert = minStockAlert;
            item.UpdatedAt = DateTime.Now;

            await _db.SaveChangesAsync();
            TempData["Success"] = $"อัปเดตข้อมูล \"{name}\" เรียบร้อยแล้ว!";
            return RedirectToAction("Stocklist");
        }

        // ==========================================
        // 21. ลบวัตถุดิบ (Soft Delete)
        // ==========================================
        [HttpPost]
        public IActionResult DeleteStock(int id)
        {
            var item = _db.Ingredients.FirstOrDefault(s => s.Id == id);
            if (item != null)
            {
                item.IsDeleted = true;
                item.UpdatedAt = DateTime.Now;
                _db.SaveChanges();
                TempData["Success"] = "ลบรายการวัตถุดิบเรียบร้อยแล้ว";
            }
            return RedirectToAction("Stocklist");
        }
        // ==========================================
        // 22. หน้าดูคิวจองโต๊ะ (Bookinglist) - พนักงานต้อนรับ / Manager / Admin
        // ==========================================
        [HttpGet]
        public IActionResult Bookinglist(string filter = "today", DateOnly? date = null)
        {
            var today = DateOnly.FromDateTime(DateTime.Now);

            var query = _db.TableBookings
                .Include(b => b.Table)
                .AsQueryable();

            if (date.HasValue)
            {
                query = query.Where(b => b.BookingDate == date.Value);
                filter = "custom";
            }
            else if (filter == "today")
            {
                query = query.Where(b => b.BookingDate == today);
            }
            else if (filter == "upcoming")
            {
                query = query.Where(b => b.BookingDate >= today);
            }

            var bookings = query
                .OrderBy(b => b.Status == "CONFIRMED" ? 0 : (b.Status == "COMPLETED" ? 1 : 2)) // เอาคิวที่รอรับลูกค้าขึ้นก่อน
                .ThenBy(b => b.BookingDate)
                .ThenBy(b => b.BookingTime)
                .ToList();

            // สรุปตัวเลขเฉพาะของวันนี้สำหรับแสดงบนการ์ดด้านบน
            var todayBookings = _db.TableBookings.Where(b => b.BookingDate == today).ToList();
            ViewBag.TodayTotal = todayBookings.Count(b => b.Status != "CANCELLED");
            ViewBag.TodayWaiting = todayBookings.Count(b => b.Status == "CONFIRMED");
            ViewBag.TodayArrived = todayBookings.Count(b => b.Status == "COMPLETED");

            ViewBag.Filter = filter;
            ViewBag.SelectedDate = date?.ToString("yyyy-MM-dd") ?? today.ToString("yyyy-MM-dd");

            return View(bookings);
        }

        // ==========================================
        // 23. ยืนยันลูกค้ามาถึงร้าน (Check-in) หรือ ยกเลิกการจอง
        // ==========================================
        [HttpPost]
        public IActionResult UpdateBookingStatus(int id, string status)
        {
            var booking = _db.TableBookings
                .Include(b => b.Table)
                .FirstOrDefault(b => b.Id == id);

            if (booking == null)
            {
                TempData["Error"] = "ไม่พบข้อมูลการจองโต๊ะ";
                return RedirectToAction("Bookinglist");
            }

            booking.Status = status;
            booking.UpdatedAt = DateTime.Now;
            _db.SaveChanges();

            if (status == "COMPLETED")
            {
                TempData["Success"] = $"เช็กอินสำเร็จ! คุณ {booking.CustomerName} เข้าโต๊ะ {booking.Table?.TableNumber ?? booking.TableId.ToString()} แล้ว";
            }
            else if (status == "CANCELLED")
            {
                TempData["Success"] = $"ยกเลิกคิวจองของคุณ {booking.CustomerName} เรียบร้อยแล้ว";
            }

            return RedirectToAction("Bookinglist");
        }
        // ==========================================
        // 24. หน้าดูบิลและกระแสเงินเข้า (Billlist) - Finance / Manager / Admin
        // ==========================================
        [HttpGet]
        public IActionResult Billlist(string period = "today", DateOnly? date = null)
        {
            var today = DateTime.Today;

            // ซิงค์ออเดอร์ที่สถานะการจ่ายเงินเป็น PAID แต่ยังไม่มีบิลในตาราง bills ให้อัตโนมัติ
            var paidOrdersWithoutBill = _db.Orders
                .Include(o => o.User)
                .Where(o => o.PaymentStatus == "PAID" && !_db.Bills.Any(b => b.OrderId == o.Id))
                .ToList();

            if (paidOrdersWithoutBill.Any())
            {
                foreach (var ord in paidOrdersWithoutBill)
                {
                    _db.Bills.Add(new Bill
                    {
                        ReferenceNumber = $"REF-{ord.OrderNumber}",
                        OrderId = ord.Id,
                        PayerName = ord.User != null ? $"{ord.User.FirstName} {ord.User.LastName}" : "ลูกค้าหน้าร้าน (Guest)",
                        Amount = ord.TotalAmount,
                        PaymentMethod = string.IsNullOrEmpty(ord.PaymentMethod) ? "PROMPTPAY" : ord.PaymentMethod,
                        Status = "PAID",
                        PaidAt = ord.UpdatedAt ?? ord.CreatedAt ?? DateTime.Now,
                        CreatedAt = DateTime.Now
                    });
                }
                _db.SaveChanges();
            }

            // ดึงรายการบิลเชื่อมกับข้อมูลออเดอร์
            var query = _db.Bills
                .Include(b => b.Order)
                .AsQueryable();

            if (date.HasValue)
            {
                var targetDate = date.Value.ToDateTime(TimeOnly.MinValue).Date;
                query = query.Where(b => b.PaidAt.HasValue && b.PaidAt.Value.Date == targetDate);
                period = "custom";
            }
            else if (period == "today")
            {
                query = query.Where(b => b.PaidAt.HasValue && b.PaidAt.Value.Date == today);
            }
            else if (period == "7days")
            {
                var startDate = today.AddDays(-6);
                query = query.Where(b => b.PaidAt.HasValue && b.PaidAt.Value.Date >= startDate);
            }
            else if (period == "month")
            {
                query = query.Where(b => b.PaidAt.HasValue && b.PaidAt.Value.Month == today.Month && b.PaidAt.Value.Year == today.Year);
            }

            var bills = query
                .OrderByDescending(b => b.PaidAt)
                .ThenByDescending(b => b.Id)
                .ToList();

            // คำนวณสรุปยอดกระแสเงินเข้า (เฉพาะบิลที่สถานะ PAID)
            var validBills = bills.Where(b => b.Status == "PAID").ToList();
            ViewBag.TotalInflow = validBills.Sum(b => b.Amount);
            ViewBag.TotalBillCount = validBills.Count;
            ViewBag.PromptPayTotal = validBills.Where(b => b.PaymentMethod.Contains("PROMPTPAY")).Sum(b => b.Amount);
            ViewBag.OtherPayTotal = validBills.Where(b => !b.PaymentMethod.Contains("PROMPTPAY")).Sum(b => b.Amount);

            ViewBag.Period = period;
            ViewBag.SelectedDate = date?.ToString("yyyy-MM-dd") ?? today.ToString("yyyy-MM-dd");

            return View(bills);
        }

        // ==========================================
        // 25. ดึงรายละเอียดบิลและรายการอาหารในออเดอร์ (JSON สำหรับ Modal)
        // ==========================================
        [HttpGet]
        public IActionResult GetBillDetailsJson(int id)
        {
            var bill = _db.Bills
                .Include(b => b.Order)
                    .ThenInclude(o => o.OrderItems)
                .FirstOrDefault(b => b.Id == id);

            if (bill == null) return NotFound();

            var items = bill.Order?.OrderItems.Select(i => new
            {
                foodName = i.FoodName,
                quantity = i.Quantity,
                unitPrice = i.UnitPrice,
                subtotal = i.Subtotal
            }).ToList();

            return Json(new
            {
                billId = bill.Id,
                referenceNumber = bill.ReferenceNumber,
                orderId = bill.OrderId,
                orderNumber = bill.Order?.OrderNumber ?? "-",
                orderType = bill.Order?.OrderType ?? "-",
                payerName = bill.PayerName,
                paymentMethod = bill.PaymentMethod,
                status = bill.Status,
                paidAt = bill.PaidAt?.ToString("dd/MM/yyyy HH:mm:ss") ?? "-",
                subtotal = bill.Order?.Subtotal ?? bill.Amount,
                discountAmount = bill.Order?.DiscountAmount ?? 0,
                amount = bill.Amount,
                items = items
            });
        }
    }
}