using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using surawat_67_172467_L003.Models;

namespace surawat_67_172467_L003.Controllers
{
    public class BookingController : Controller
    {
        private readonly Csi402dbContext _db;

        public BookingController(Csi402dbContext db)
        {
            _db = db;
        }

        // 1. หน้าจองโต๊ะ
        [HttpGet]
        public IActionResult Index()
        {
            int? sessionUserId = HttpContext.Session.GetInt32("UserId");
            if (sessionUserId == null)
            {
                return RedirectToAction("Login", "Account");
            }

            var tables = _db.DiningTables
                .Where(t => t.IsActive == true)
                .OrderBy(t => t.TableNumber)
                .ToList();

            var myBookings = _db.TableBookings
                .Include(b => b.Table)
                .Where(b => b.UserId == sessionUserId.Value)
                .OrderByDescending(b => b.CreatedAt)
                .Take(5)
                .ToList();

            ViewBag.Tables = tables;
            ViewBag.MyBookings = myBookings;

            return View();
        }

        // 2. บันทึกข้อมูลการจองโต๊ะตามรหัสผู้ใช้งานที่เข้าสู่ระบบ
        [HttpPost]
        public IActionResult Create(int totalMember, int? tableId, string customerName, string phoneNumber, DateOnly bookingDate, TimeOnly bookingTime)
        {
            int? sessionUserId = HttpContext.Session.GetInt32("UserId");
            if (sessionUserId == null)
            {
                return RedirectToAction("Login", "Account");
            }

            if (string.IsNullOrWhiteSpace(customerName) || string.IsNullOrWhiteSpace(phoneNumber))
            {
                TempData["Error"] = "กรุณากรอกชื่อผู้จองและเบอร์โทรศัพท์ให้ครบถ้วน";
                return RedirectToAction("Index");
            }

            int selectedTableId = tableId ?? 0;
            if (selectedTableId == 0)
            {
                var autoTable = _db.DiningTables
                    .Where(t => t.IsActive == true && t.Capacity >= totalMember)
                    .OrderBy(t => t.Capacity)
                    .FirstOrDefault()
                    ?? _db.DiningTables.FirstOrDefault();

                if (autoTable == null)
                {
                    TempData["Error"] = "ขออภัย ยังไม่มีข้อมูลโต๊ะในระบบ กรุณาเพิ่มข้อมูลโต๊ะก่อน";
                    return RedirectToAction("Index");
                }
                selectedTableId = autoTable.Id;
            }

            bool isDuplicate = _db.TableBookings.Any(b =>
                b.TableId == selectedTableId &&
                b.BookingDate == bookingDate &&
                b.BookingTime == bookingTime &&
                b.Status == "CONFIRMED");

            if (isDuplicate)
            {
                TempData["Error"] = "โต๊ะและช่วงเวลาดังกล่าวถูกจองแล้ว กรุณาเลือกโต๊ะหรือเวลาอื่น";
                return RedirectToAction("Index");
            }

            var booking = new TableBooking
            {
                UserId = sessionUserId.Value,
                TableId = selectedTableId,
                CustomerName = customerName.Trim(),
                PhoneNumber = phoneNumber.Trim(),
                TotalMember = totalMember,
                BookingDate = bookingDate,
                BookingTime = bookingTime,
                Status = "CONFIRMED",
                CreatedAt = DateTime.Now,
                UpdatedAt = DateTime.Now
            };

            _db.TableBookings.Add(booking);
            _db.SaveChanges();

            TempData["Success"] = $"จองโต๊ะสำเร็จ คุณ {customerName} วันที่ {bookingDate:dd/MM/yyyy} เวลา {bookingTime:HH:mm} น.";
            return RedirectToAction("Index");
        }

        // 3. ยกเลิกการจองเฉพาะรายการของตนเอง
        [HttpPost]
        public IActionResult Cancel(int id)
        {
            int? sessionUserId = HttpContext.Session.GetInt32("UserId");
            if (sessionUserId == null)
            {
                return RedirectToAction("Login", "Account");
            }

            var booking = _db.TableBookings.FirstOrDefault(b => b.Id == id && b.UserId == sessionUserId.Value);
            if (booking != null)
            {
                booking.Status = "CANCELLED";
                booking.UpdatedAt = DateTime.Now;
                _db.SaveChanges();
                TempData["Success"] = "ยกเลิกการจองเรียบร้อยแล้ว";
            }
            return RedirectToAction("Index");
        }
    }
}