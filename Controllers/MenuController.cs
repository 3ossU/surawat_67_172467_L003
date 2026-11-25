using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using surawat_67_172467_L003.Models;
using surawat_67_172467_L003.ViewModels;

namespace surawat_67_172467_L003.Controllers;

public class MenuController : Controller
{
    private readonly Csi402dbContext _db;
    public MenuController(Csi402dbContext db)
    {
        _db = db;
    }

    public IActionResult Menu(int? categoryId)
    {
        var today = DateOnly.FromDateTime(DateTime.Now);

        // 1. ดึงหมวดหมู่ทั้งหมดจากตาราง categories (รวมหมวดหมู่โปรโมชั่นที่มีอยู่แล้ว)
        var categories = _db.Categories
            .Where(c => c.IsActive == true && c.IsDeleted == false)
            .OrderBy(c => c.DisplayOrder)
            .ToList();

        ViewBag.Categories = categories;
        ViewBag.SelectedCategory = categoryId;

        // เช็กว่าหมวดหมู่ที่กำลังกดเลือกอยู่ คือหมวดหมู่ "โปรโมชั่น" หรือไม่
        var selectedCatObj = categories.FirstOrDefault(c => c.Id == categoryId);
        bool isPromoCategory = selectedCatObj != null && selectedCatObj.Name.Contains("โปรโมชั่น");
        ViewBag.IsPromoCategory = isPromoCategory;

        // 2. ดึงโปรโมชั่นที่เปิดใช้งานอยู่และยังไม่หมดอายุ
        var promotions = _db.Promotions
            .Include(p => p.PromotionItems)
                .ThenInclude(pi => pi.Food)
            .Where(p => p.IsActive == true
                     && p.IsDeleted == false
                     && p.EndDate >= today)
            .OrderByDescending(p => p.Id)
            .ToList();

        ViewBag.Promotions = promotions;

        // 3. หากกดเลือกหมวดหมู่ "โปรโมชั่น" ไม่ต้องดึงอาหารปกติมาแสดง
        if (isPromoCategory)
        {
            return View(new List<Food>());
        }

        // 4. กรองอาหารตาม categoryId ที่กดเลือก
        var foodsQuery = _db.Foods
            .Where(f => f.IsAvailable == true && f.IsDeleted == false);

        if (categoryId.HasValue)
        {
            foodsQuery = foodsQuery.Where(f => f.CategoryId == categoryId.Value);
        }

        var foods = foodsQuery.OrderByDescending(f => f.Id).ToList();
        return View(foods);
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}