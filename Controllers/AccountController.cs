using System;
using System.Linq;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using surawat_67_172467_L003.Models;

namespace surawat_67_172467_L003.Controllers
{
    public class AccountController : Controller
    {
        private readonly Csi402dbContext _db;

        public AccountController(Csi402dbContext db)
        {
            _db = db;
        }

        // ==========================================
        // 1. หน้าเข้าสู่ระบบ
        // ==========================================
        [HttpGet]
        public IActionResult Login()
        {
            var existingRole = HttpContext.Session.GetInt32("RoleId");
            if (existingRole != null)
            {
                return RedirectByRole(existingRole.Value);
            }

            return View();
        }

        [HttpPost]
        public IActionResult Login(string? Phone, string? Password)
        {
            string inputPhone = (Phone ?? "").Trim();
            string inputPassword = Password ?? "";

            if (string.IsNullOrWhiteSpace(inputPhone) || string.IsNullOrWhiteSpace(inputPassword))
            {
                ViewBag.Status = "กรุณากรอกเบอร์โทรศัพท์และรหัสผ่าน";
                return View();
            }

            var user = _db.Users.FirstOrDefault(u => u.PhoneNumber == inputPhone && u.IsDeleted == false);

            if (user == null)
            {
                ViewBag.Status = "ไม่พบเบอร์โทรศัพท์นี้ในระบบ";
                return View();
            }

            if (user.IsActive == false)
            {
                ViewBag.Status = "บัญชีนี้ถูกระงับการใช้งาน กรุณาติดต่อผู้ดูแลระบบ";
                return View();
            }

            // ตรวจสอบรหัสผ่านที่เข้ารหัสไว้ และรองรับรหัสผ่านทดสอบชุดแรกในฐานข้อมูล
            bool isPasswordValid = false;
            if (!string.IsNullOrEmpty(user.PasswordHash))
            {
                if (user.PasswordHash == inputPassword)
                {
                    isPasswordValid = true;
                }
                else
                {
                    var hasher = new PasswordHasher<User>();
                    var result = hasher.VerifyHashedPassword(user, user.PasswordHash, inputPassword);
                    isPasswordValid = (result == PasswordVerificationResult.Success);
                }
            }

            if (!isPasswordValid)
            {
                ViewBag.Status = "รหัสผ่านไม่ถูกต้อง";
                return View();
            }

           // บันทึกข้อมูลผู้ใช้งานลงหน่วยความจำชั่วคราว
            HttpContext.Session.SetInt32("UserId", user.Id);
            HttpContext.Session.SetInt32("RoleId", user.RoleId);
            HttpContext.Session.SetString("UserName", $"{user.FirstName} {user.LastName}");

            return RedirectByRole(user.RoleId);
        }

        // ==========================================
        // 2. หน้าสมัครสมาชิก
        // ==========================================
        [HttpGet]
        public IActionResult Register()
        {
            return View();
        }

        [HttpPost]
        public IActionResult Register(string? Name, string? Lastname, string? Phone,
                                      DateOnly? Birthdate, string? Gender, string? Password)
        {
            string fName = (Name ?? "").Trim();
            string lName = (Lastname ?? "").Trim();
            string phone = (Phone ?? "").Trim();
            string password = Password ?? "";

            if (string.IsNullOrWhiteSpace(fName) || string.IsNullOrWhiteSpace(lName) ||
                string.IsNullOrWhiteSpace(phone) || string.IsNullOrWhiteSpace(password))
            {
                ViewBag.Status = "กรุณากรอกข้อมูลให้ครบถ้วน";
                return View();
            }

            bool isDuplicate = _db.Users.Any(u => u.PhoneNumber == phone && u.IsDeleted == false);
            if (isDuplicate)
            {
                ViewBag.Status = "เบอร์โทรศัพท์นี้ถูกใช้งานแล้ว กรุณาใช้เบอร์อื่น";
                return View();
            }

            var hasher = new PasswordHasher<User>();
            var newUser = new User
            {
                FirstName = fName,
                LastName = lName,
                PhoneNumber = phone,
                Birthdate = Birthdate,
                Gender = string.IsNullOrWhiteSpace(Gender) ? "Other" : Gender,
                RoleId = 1, // กำหนดสิทธิ์เริ่มต้นเป็นลูกค้าทั่วไป
                IsActive = true,
                IsDeleted = false,
                CreatedAt = DateTime.Now,
                UpdatedAt = DateTime.Now
            };

            // เข้ารหัสรหัสผ่านก่อนบันทึกลงฐานข้อมูล
            newUser.PasswordHash = hasher.HashPassword(newUser, password);

            _db.Users.Add(newUser);
            _db.SaveChanges();

            TempData["Success"] = "สมัครสมาชิกสำเร็จ กรุณาเข้าสู่ระบบ";
            return RedirectToAction("Login", "Account");
        }

        // ==========================================
        // 3. หน้าลืมรหัสผ่าน
        // ==========================================
        [HttpGet]
        public IActionResult ForgotPassword()
        {
            return View();
        }

        // ==========================================
        // 4. ออกจากระบบ (ล้างข้อมูลและคุกกี้ทั้งหมด)
        // ==========================================
        [HttpGet]
        [HttpPost]
        public IActionResult Logout()
        {
            HttpContext.Session.Clear();
            Response.Cookies.Delete(".AspNetCore.Session");

            // ป้องกันเบราว์เซอร์จำหน้าเก่าไว้เมื่อกดปุ่มย้อนกลับ
            Response.Headers["Cache-Control"] = "no-cache, no-store, must-revalidate";
            Response.Headers["Pragma"] = "no-cache";
            Response.Headers["Expires"] = "0";

            return RedirectToAction("Login", "Account");
        }

        // ==========================================
        // ตัวช่วยส่งไปยังหน้าแรกตามตำแหน่งงาน
        // ==========================================
        private IActionResult RedirectByRole(int roleId)
        {
            switch (roleId)
            {
                case 2: // ADMIN (ผู้ดูแลระบบสูงสุด)
                case 3: // MANAGER (ผู้จัดการร้าน)
                case 7: // FINANCE (พนักงานการเงิน)
                    return RedirectToAction("Dashboard", "Admin");

                case 4: // CHEF (พ่อครัว)
                case 5: // DELIVERY (พนักงานส่งอาหาร)
                    return RedirectToAction("Orderlist", "Admin");

                case 6: // WAITER (พนักงานบริการ)
                    return RedirectToAction("Bookinglist", "Admin");

                default: // 1 = CUSTOMER (ลูกค้าทั่วไป)
                    return RedirectToAction("Menu", "Menu");
            }
        }
        // ==========================================
        // 5. หน้าแก้ไขข้อมูลส่วนตัว (EditProfile)
        // ==========================================
        [HttpGet]
        public IActionResult EditProfile()
        {
            int? sessionUserId = HttpContext.Session.GetInt32("UserId");
            if (sessionUserId == null)
            {
                return RedirectToAction("Login", "Account");
            }

            var user = _db.Users.FirstOrDefault(u => u.Id == sessionUserId.Value && u.IsDeleted == false);
            if (user == null)
            {
                return RedirectToAction("Login", "Account");
            }

            return View(user);
        }

        [HttpPost]
        public IActionResult EditProfile(User data, string? NewPassword, string? ConfirmPassword)
        {
            int? sessionUserId = HttpContext.Session.GetInt32("UserId");
            if (sessionUserId == null)
            {
                return RedirectToAction("Login", "Account");
            }

            var user = _db.Users.FirstOrDefault(u => u.Id == sessionUserId.Value && u.IsDeleted == false);
            if (user == null)
            {
                ViewBag.Status = "ไม่พบผู้ใช้งานในระบบ";
                return View(data);
            }

            // ตรวจสอบเบอร์โทรศัพท์ซ้ำกับผู้อื่น (ยกเว้นตนเอง)
            var phoneDuplicate = _db.Users.FirstOrDefault(u =>
                u.PhoneNumber == data.PhoneNumber &&
                u.Id != sessionUserId.Value &&
                u.IsDeleted == false);

            if (phoneDuplicate != null)
            {
                ViewBag.Status = "เบอร์โทรศัพท์นี้มีผู้อื่นใช้งานแล้ว กรุณาใช้เบอร์อื่น";
                return View(user);
            }

            // ตรวจสอบและเข้ารหัสรหัสผ่านใหม่หากมีการกรอกเข้ามา
            if (!string.IsNullOrWhiteSpace(NewPassword))
            {
                if (NewPassword != ConfirmPassword)
                {
                    ViewBag.Status = "รหัสผ่านใหม่และยืนยันรหัสผ่านไม่ตรงกัน";
                    return View(user);
                }

                var hasher = new PasswordHasher<User>();
                user.PasswordHash = hasher.HashPassword(user, NewPassword);
            }

            user.FirstName = data.FirstName;
            user.LastName = data.LastName;
            user.Gender = data.Gender;
            user.Birthdate = data.Birthdate;
            user.PhoneNumber = data.PhoneNumber;
            user.UpdatedAt = DateTime.Now;

            _db.SaveChanges();

            // อัปเดตชื่อที่แสดงบนแถบเมนูด้านบนทันที
            HttpContext.Session.SetString("UserName", $"{user.FirstName} {user.LastName}");

            ViewBag.Status = "บันทึกข้อมูลเรียบร้อยแล้ว";
            return View(user);
        }
    }
}