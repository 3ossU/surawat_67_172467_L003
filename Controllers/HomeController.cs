using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using surawat_67_172467_L003.Models;
using surawat_67_172467_L003.ViewModels;
namespace surawat_67_172467_L003.Controllers;

public class HomeController : Controller
{
    private readonly Csi402dbContext _db;
    public HomeController(Csi402dbContext db)
    {
        _db = db;
    }
    public IActionResult Lab10(string? StdId)
    {
        var User = (from s in _db.Labstudents where s.StdId == StdId select s).FirstOrDefault();
        return View(User);
    }
    [HttpPost]
    public IActionResult Lab10(Labstudent data)
    {
        var User = (from s in _db.Labstudents where s.StdId == data.StdId select s).FirstOrDefault();
        User.StdPassword = data.StdPassword;
        User.StdName = data.StdName;
        User.StdLastname = data.StdLastname;
        _db.SaveChanges();
        return View(data);
    }
    public IActionResult Lab10_1(string? Stdid)
    {
        var Check = (from s in _db.Labstudents where s.StdId == Stdid select s).FirstOrDefault();
        if (Check != null)
        {
            _db.Remove(Check);
            _db.SaveChanges();
            return RedirectToAction("Lab9_1", "Home", new { status = "Delete success" });
        }
        else
        {
            return RedirectToAction("Lab9_1", "Home", new { status = "Delete unsuccess" });
        }

    }
    public IActionResult Lab9()
    {
        //create insert
        return View();
    }
    [HttpPost]
    public IActionResult Lab9(Labstudent data)
    {
        if (data.StdPassword != null)
        {
            if (ModelState.IsValid)
            {
                // Select * from Labstudents where StdId == "@StdId"
                var StdCheck = (from a in _db.Labstudents where a.StdId == data.StdId select a).FirstOrDefault();
                if (StdCheck == null)
                {
                    Labstudent s = new();
                    // Table2 a = new();
                    s.StdId = data.StdId;
                    s.StdPassword = data.StdPassword;
                    s.StdName = data.StdName;
                    s.StdLastname = data.StdLastname;
                    //a.ID = data.StdID;
                    //_db.Add(a);
                    _db.Add(s);
                    _db.SaveChanges();
                    return View(data);
                }
                else
                {
                    ViewBag.Status = "Student Id!!!!";
                    return View(data);
                }
            }
            else
            {
                ViewBag.Status = "Error!!!!";
                return View(data);
            }
        }
        else
        {
            ViewBag.Status = "Password NULl!!";
            return View(data);
        }
    }
    public IActionResult Lab9_1()
    {
        var StdList = (from s in _db.Labstudents select s).ToList();
        return View(StdList);
    }
    public IActionResult Index()
    {
        return View();
    }

    public IActionResult Privacy()
    {
        return View();
    }
    public IActionResult Admin()
    {
        return View();
    }
    public IActionResult Lab4()
    {
        return View();
    }

    // 1. หน้าสำหรับเปิดฟอร์ม
    public IActionResult Adduser()
    {
        return View();
    }

    // 2. Action สำหรับรับข้อมูลตอนกดปุ่มบันทึก
    [HttpPost]
    public IActionResult Adduser(string FirstName, string LastName, string PhoneNumber,
                                 DateTime BirthDate, string Gender, string Role, string Password)
    {
        // ตรวจสอบความถูกต้องของรหัสผ่าน
        if (string.IsNullOrEmpty(Password))
        {
            ViewBag.Status = "กรุณากรอกรหัสผ่าน!";
            return View();
        }

        // ตรวจสอบว่าเบอร์โทรศัพท์ซ้ำในระบบหรือไม่ (สไตล์เดียวกับ Lab 9)
        var userCheck = (from u in _db.Users
                         where u.PhoneNumber == PhoneNumber
                         select u).FirstOrDefault();

        if (userCheck == null)
        {
            // แมปค่า Role จากหน้าเว็บให้ตรงกับ RoleId ในฐานข้อมูล:
            // 1 = CUSTOMER, 2 = ADMIN, 3 = MANAGER, 4 = CHEF, 5 = DELIVERY
            int selectedRoleId = 1;
            if (Role == "Manager") selectedRoleId = 3;
            else if (Role == "Chef") selectedRoleId = 4;
            else if (Role == "Rider" || Role == "Delivery") selectedRoleId = 5;
            else if (Role == "Admin") selectedRoleId = 2;
            else selectedRoleId = 1;

            // สร้าง Object User และผูกค่าที่ส่งมาจากหน้าฟอร์ม
            User newUser = new User();
            newUser.FirstName = FirstName;
            newUser.LastName = LastName;
            newUser.PhoneNumber = PhoneNumber;
            newUser.PasswordHash = Password; // ใช้รหัสผ่านที่กรอกมาโดยตรง
            newUser.Gender = Gender;
            newUser.Birthdate = DateOnly.FromDateTime(BirthDate);
            newUser.RoleId = selectedRoleId;

            // ค่าเริ่มต้นของระบบ
            newUser.IsActive = true;
            newUser.IsDeleted = false;
            newUser.CreatedAt = DateTime.Now;

            // บันทึกลงฐานข้อมูล
            _db.Users.Add(newUser);
            _db.SaveChanges();

            // บันทึกสำเร็จ กลับไปแสดงรายการผู้ใช้ที่หน้า Userlist ทันที
            return RedirectToAction("Userlist", "Home");
        }
        else
        {
            // แจ้งเตือนเมื่อเบอร์โทรมีอยู่แล้วในระบบ
            ViewBag.Status = "เบอร์โทรศัพท์นี้ถูกใช้งานไปแล้ว!";
            return View();
        }
    }
    public IActionResult Userlist(string? searchString)
    {
        // ดึงข้อมูลเหมือน Lab 9_1
        var userList = (from u in _db.Users
                        where u.IsDeleted == false
                        select u).ToList();

        // ถ้ามีการพิมพ์ค้นหา
        if (!string.IsNullOrEmpty(searchString))
        {
            userList = userList.Where(u => u.FirstName.Contains(searchString)
                                        || u.LastName.Contains(searchString)
                                        || u.PhoneNumber.Contains(searchString)
                                        || u.Id.ToString() == searchString).ToList();
        }

        return View(userList);
    }
    // ดึงรายละเอียดผู้ใช้และประวัติออเดอร์ 
    [HttpGet]
    public IActionResult GetUserDetailsJson(int id)
    {
    
        var user = (from u in _db.Users
                    where u.Id == id && u.IsDeleted == false
                    select u).FirstOrDefault();

        if (user == null)
        {
            return NotFound();
        }

        // ดึงเลขออเดอร์จากตาราง Orders 10 รายการ
        var orderIds = (from o in _db.Orders
                        where o.UserId == id
                        orderby o.CreatedAt descending
                        select o.OrderNumber).Take(10).ToList();

        // คำนวณอายุ
        string ageDisplay = "-";
        if (user.Birthdate.HasValue)
        {
            var today = DateOnly.FromDateTime(DateTime.Today);
            int age = today.Year - user.Birthdate.Value.Year;
            if (user.Birthdate.Value > today.AddYears(-age)) age--;
            ageDisplay = age > 0 ? age.ToString() : "-";
        }

        // อันนี้ขอ hardcode ก่อน เดียวค่อยไปเชื่อมกับ database จริงทีหลัง
        string roleName = "-";
        if (user.RoleId == 1) roleName = "Customer";
        else if (user.RoleId == 2) roleName = "Admin";
        else if (user.RoleId == 3) roleName = "Manager";
        else if (user.RoleId == 4) roleName = "Chef";
        else if (user.RoleId == 5) roleName = "Delivery";

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

    //ปรับสถานะ ระงับบัญชี / ยกเลิกระงับ
    [HttpPost]
    public IActionResult ToggleSuspendUser(int id)
    {
        var user = (from u in _db.Users
                    where u.Id == id
                    select u).FirstOrDefault();

        if (user != null)
        {
            user.IsActive = !user.IsActive; // สลับสถานะจริงใน Database
            _db.SaveChanges();
            return Json(new { success = true, isActive = user.IsActive });
        }

        return Json(new { success = false, message = "ไม่พบผู้ใช้" });
    }

    // ลบบัญชี
    [HttpPost]
    public IActionResult DeleteUser(int id)
    {
        var user = (from u in _db.Users
                    where u.Id == id
                    select u).FirstOrDefault();

        if (user != null)
        {
            _db.Users.Remove(user);
            _db.SaveChanges();
        }

        return RedirectToAction("Userlist", "Home");
    }

    public IActionResult Register()
    {
        return View();
    }

    [HttpPost]
    public IActionResult Register(RegisterViewModel User)
    {
        if (ModelState.IsValid)
        {
            // 1. ตรวจสอบว่าเบอร์โทรซ้ำหรือไม่ (สไตล์เดียวกับ Lab 9)
            var userCheck = (from u in _db.Users
                             where u.PhoneNumber == User.Phone
                             select u).FirstOrDefault();

            if (userCheck == null)
            {
                // 2. สร้าง Entity User และนำค่าจาก Form มาใส่
                User newUser = new User();
                newUser.FirstName = User.Name;
                newUser.LastName = User.Lastname;
                newUser.PhoneNumber = User.Phone;
                newUser.PasswordHash = User.Password;
                newUser.Gender = User.Gender;
                newUser.Birthdate = DateOnly.FromDateTime(User.Birthdate);

                // กำหนดค่า Default ที่ตาราง Users ต้องการ
                newUser.RoleId = 1; // 1 = CUSTOMER ตามตาราง Role ของคุณ
                newUser.IsActive = true;
                newUser.IsDeleted = false;
                newUser.CreatedAt = DateTime.Now;

                // 3. เพิ่มข้อมูลและบันทึกลงฐานข้อมูล
                _db.Users.Add(newUser);
                _db.SaveChanges();

                // สมัครเสร็จแล้วส่งไปหน้า Login
                return RedirectToAction("Login", "Home");
            }
            else
            {
                ViewBag.Status = "เบอร์โทรนี้ถูกใช้งานแล้ว!";
                return View(User);
            }
        }
        else
        {
            ViewBag.Status = "เกิดข้อผิดพลาด กรุณากรอกข้อมูลให้ครบถ้วน!";
            return View(User);
        }
    }
    public IActionResult DisplayRegister(string Name, string LastName, string Phone, string Gender, DateTime Birthdate)
    {
        var User = new RegisterViewModel();
        User.Name = Name;
        User.Lastname = LastName;
        User.Gender = Gender;
        User.Phone = Phone;
        User.Birthdate = Birthdate;
        return View(User);
    }
    public IActionResult Login()
    {
        return View();
    }
    [HttpPost]
    public IActionResult Login(LoginViewModel User)
    {
        if (ModelState.IsValid)
        {
            // 1. ตรวจสอบข้อมูลผู้ใช้จากตาราง Users ใน Database (แบบ Lab 9)
            var userCheck = (from u in _db.Users
                             where u.PhoneNumber == User.Phone
                                && u.PasswordHash == User.Password
                                && u.IsDeleted == false
                             select u).FirstOrDefault();

            // 2. ถ้าพบข้อมูลผู้ใช้ในระบบ (ล็อกอินสำเร็จ)
            if (userCheck != null)
            {
                // ส่งต่อไปยังหน้า DisplayLogin พร้อมส่งข้อมูลที่จำเป็นไปด้วย
                return RedirectToAction("DisplayLogin", "Home", new
                {
                    Phone = userCheck.PhoneNumber,
                    Password = userCheck.PasswordHash
                });
            }
            else
            {
                // ถ้าไม่พบข้อมูล หรือรหัสผ่านไม่ถูกต้อง
                ViewBag.Status = "เบอร์โทรศัพท์หรือรหัสผ่านไม่ถูกต้อง!";
                return View(User);
            }
        }

        ViewBag.Status = "กรุณากรอกข้อมูลให้ครบถ้วน!";
        return View(User);
    }
    public IActionResult DisplayLogin(string Phone, string Password)
    {
        var User = new LoginViewModel();
        User.Phone = Phone;
        User.Password = Password;
        return View(User);
    }

    public IActionResult EditProfile(int id)
    {
        var user = (from u in _db.Users
                    where u.Id == id && u.IsDeleted == false
                    select u).FirstOrDefault();

        if (user == null)
        {
            return NotFound();
        }

        return View(user);
    }

    [HttpPost]
    public IActionResult EditProfile(User data, string? NewPassword, string? ConfirmPassword)
    {
        var user = (from u in _db.Users
                    where u.Id == data.Id
                    select u).FirstOrDefault();

        if (user == null)
        {
            ViewBag.Status = "ไม่พบผู้ใช้งานในระบบ!";
            return View(data);
        }

        // เบอร์โทรศัพท์ใหม่ซ้ำกับผู้อื่นหรือไม่
        var phoneDuplicate = (from u in _db.Users
                              where u.PhoneNumber == data.PhoneNumber && u.Id != data.Id && u.IsDeleted == false
                              select u).FirstOrDefault();

        if (phoneDuplicate != null)
        {
            ViewBag.Status = "เบอร์โทรศัพท์นี้มีผู้อื่นใช้งานแล้ว กรุณาใช้เบอร์อื่น!";
            return View(data);
        }

        // ตรวจสอบการเปลี่ยนรหัสผ่าน
        if (!string.IsNullOrWhiteSpace(NewPassword))
        {
            if (NewPassword != ConfirmPassword)
            {
                ViewBag.Status = "รหัสผ่านใหม่และยืนยันรหัสผ่านไม่ตรงกัน!";
                return View(data);
            }

            user.PasswordHash = NewPassword; 
        }

        user.FirstName = data.FirstName;
        user.LastName = data.LastName;
        user.Gender = data.Gender;
        user.Birthdate = data.Birthdate;
        user.PhoneNumber = data.PhoneNumber;
        user.UpdatedAt = DateTime.Now;

        _db.SaveChanges(); 
        ViewBag.Status = "บันทึกข้อมูลเรียบร้อยแล้ว!";
        return View(user);
    }
    public IActionResult Lab5(string Name, string Lastname)
    {
        var Data = new Lab5ViewModel();
        Data.Name = Name;
        Data.Lastname = Lastname;

        Data.UserId = "ABC67";
        Data.Password = "hud32s";
        Data.Address = "123/123 bankok";
        Data.Age = 20;
        Data.Hight = 176;
        Data.Weight = 100;


        return View(Data);
    }
    public IActionResult Lab5_2()
    {
        var data = new List<Lab5ViewModel>
       {
           new Lab5ViewModel{Name = "a",Lastname="b"},
           new Lab5ViewModel{Name = "a2",Lastname="b2"},
       };

        return View(data);
    }


    public IActionResult Lab5_3()
    {

        return View();
    }
    [HttpPost]
    public IActionResult Lab5_3(Lab5ViewModel data)
    {
        string a, b, c;
        a = data.Name;
        b = data.Lastname;
        c = a + " " + b;
        ViewBag.Name = c;
        // return View(data);
        return RedirectToAction("Lab5", "Home", new { Name = data.Name, Lastname = data.Lastname });
    }



































    public IActionResult Lab2()
    {
        char Grade = 'A';
        string Name, Lastname;
        Name = "Surawat";
        Lastname = "Saengcharoendsuklert";
        int T, Y, A, Sum;
        T = 49;
        Y = 40;
        A = 2;
        Sum = Y + A;
        ViewBag.Sum = Sum;

        if (Sum > 49)
        {
            ViewBag.Result = "คุณมี iq มากกว่า";
        }
        if (Sum > 40)
        {
            ViewBag.Result = "มีค่ามากกว่า40";
        }
        else
        {
            ViewBag.Result = "คุณจน";
        }

        return View();
    }
    public IActionResult AssessMent3()
    {
        string Name, Surname, Language, Room, Year;
        Name = "Surawat";
        Surname = "Saengcharoensuklert";
        Language = "Js,HTML,CSS,React,Next.js";
        Room = "6767";
        Year = "3";
        int E1, E2, E3, E4, E5, E6, E7, E8, E9, E10, Sum;
        E1 = 4;
        E2 = 5;
        E3 = 10;
        E4 = 9;
        E5 = 7;
        E6 = 7;
        E7 = 9;
        E8 = 9;
        E9 = 8;
        E10 = 2;
        Sum = E1 + E2 + E3 + E4 + E5 + E6 + E7 + E8 + E9 + E10;
        ViewBag.Sum = Sum;
        ViewBag.Name = Name;
        ViewBag.Surname = Surname;
        ViewBag.Language = Language;
        ViewBag.Room = Room;
        ViewBag.Year = Year;
        if (Sum >= 80)
        {
            ViewBag.Result = "A";
        }
        else if (Sum > 75)
        {
            ViewBag.Result = "B+";
        }
        else if (Sum > 69)
        {
            ViewBag.Result = "B";
        }
        else if (Sum > 65)
        {
            ViewBag.Result = "C+";
        }
        else if (Sum > 59)
        {
            ViewBag.Result = "c";
        }
        else if (Sum > 55)
        {
            ViewBag.Result = "D+";
        }
        else if (Sum > 49)
        {
            ViewBag.Result = "D";
        }
        else
        {
            ViewBag.Result = "F";
        }



        return View();
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
