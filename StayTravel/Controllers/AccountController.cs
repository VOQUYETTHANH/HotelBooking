using StayTravel.Models;
using System;
using System.Data.Entity;
using System.IO;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using System.Web.Security;

namespace StayTravel.Controllers
{
    public class AccountController : Controller
    {
        private HotelBookingDBEntities db = new HotelBookingDBEntities();

        public ActionResult Register() => View();

        // 1. Xử lý Đăng ký (Register - POST)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Register(User user)
        {
            if (ModelState.IsValid)
            {
                var checkEmail = db.Users.FirstOrDefault(u => u.Email == user.Email);
                if (checkEmail != null)
                {
                    ViewBag.Error = "Email này đã được sử dụng!";
                    return View(user);
                }

                // BĂM MẬT KHẨU TRƯỚC KHÍ LƯU VÀO DATABASE
                user.PasswordHash = PasswordHelper.HashPassword(user.PasswordHash);

                user.RoleId = 2; // Customer
                user.Avatar = "/Images/default-avatar.png";
                user.CreatedAt = DateTime.Now;

                db.Users.Add(user);
                db.SaveChanges();

                return RedirectToAction("Login");
            }
            return View(user);
        }
        public ActionResult Login()
        {
            return View();
        }
        // 2. Xử lý Đăng nhập (Login - POST)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Login(string email, string password, bool rememberMe = false)
        {
            string hashedPassword = PasswordHelper.HashPassword(password);
            var user = db.Users.FirstOrDefault(u => u.Email == email && u.PasswordHash == hashedPassword);

            if (user != null)
            {
                // 1. QUAN TRỌNG: Bật cookie xác thực của hệ thống ASP.NET (.NET Framework)
                FormsAuthentication.SetAuthCookie(user.Email, rememberMe);

                // 2. Lưu Session để hiển thị tên và avatar
                Session["User"] = user;
                Session["UserName"] = user.FullName;
                Session["UserRoleId"] = user.RoleId;

                if (user.RoleId == 1)
                {
                    return RedirectToAction("Dashboard", "Admin");
                }

                return RedirectToAction("Index", "Home");
            }

            ViewBag.Error = "Email hoặc mật khẩu không chính xác!";
            return View();
        }

        public ActionResult Logout()
        {
            FormsAuthentication.SignOut();
            Session.Clear();
            return RedirectToAction("Index", "Home");
        }

        // 6. Trang thông tin tài khoản, Lịch sử giao dịch & Wishlist
        [Authorize]
        public ActionResult Profile()
        {
            if (Session["User"] == null)
            {
                return RedirectToAction("Login", "Account");
            }

            var currentUser = (User)Session["User"];
            var user = db.Users.FirstOrDefault(u => u.UserId == currentUser.UserId);

            if (user == null)
            {
                return RedirectToAction("Login", "Account");
            }

            // Cập nhật lại Session để giao diện Layout dùng thông tin mới nhất
            Session["User"] = user;
            Session["UserName"] = user.FullName;

            var model = new UserProfileViewModel
            {
                User = user,
                BookingHistory = db.Bookings.Include(b => b.Room.Hotel).Where(b => b.UserId == user.UserId).OrderByDescending(b => b.CreatedAt).ToList(),
                Wishlists = db.Wishlists.Include(w => w.Hotel).Where(w => w.UserId == user.UserId).ToList()
            };

            return View(model);
        }

        // Xử lý Cập nhật thông tin cá nhân (Họ tên, SĐT, Avatar)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult UpdateProfile(string fullName, string phoneNumber, HttpPostedFileBase avatarFile)
        {
            if (Session["User"] == null)
            {
                return RedirectToAction("Login", "Account");
            }

            var currentUser = (User)Session["User"];
            var user = db.Users.Find(currentUser.UserId);

            if (user != null)
            {
                // Cập nhật Họ tên & SĐT
                user.FullName = fullName;
                user.PhoneNumber = phoneNumber;

                // Xử lý upload Avatar nếu có chọn ảnh mới
                if (avatarFile != null && avatarFile.ContentLength > 0)
                {
                    string fileName = Path.GetFileNameWithoutExtension(avatarFile.FileName);
                    string extension = Path.GetExtension(avatarFile.FileName);
                    fileName = fileName + "_" + DateTime.Now.ToString("yyyyMMddHHmmss") + extension;

                    string path = Path.Combine(Server.MapPath("~/Images/"), fileName);
                    avatarFile.SaveAs(path);

                    user.Avatar = "/Images/" + fileName;
                }

                db.SaveChanges();

                // Cập nhật lại Session
                Session["User"] = user;
                Session["UserName"] = user.FullName;

                TempData["SuccessMessage"] = "Cập nhật thông tin cá nhân thành công!";
            }

            return RedirectToAction("Profile");
        }
    }
}