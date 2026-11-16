using StayTravel.Models;
using System;
using System.Data.Entity;
using System.IO;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using System.Web.Security;
using System.Text.RegularExpressions;

namespace StayTravel.Controllers
{
    public class AccountController : Controller
    {
        private HotelBookingDBEntities db = new HotelBookingDBEntities();

        public ActionResult Register() => View();


        // ==========================================
        // 1. XỬ LÝ ĐĂNG KÝ (REGISTER - POST)
        // ==========================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Register(User user, string ConfirmPassword)
        {
            // Kiểm tra Gmail
            string email = (user.Email ?? "").Trim();

            if (!Regex.IsMatch(
                email,
                @"^[A-Za-z0-9][A-Za-z0-9._%+-]*@gmail\.com$",
                RegexOptions.IgnoreCase))
            {
                ModelState.AddModelError(
                    "Email",
                    "Vui lòng nhập địa chỉ Gmail hợp lệ (@gmail.com)."
                );
            }

            // Kiểm tra họ tên
            if (string.IsNullOrWhiteSpace(user.FullName) || user.FullName.Trim().Length < 2)
            {
                ModelState.AddModelError("FullName", "Vui lòng nhập họ tên (ít nhất 2 ký tự).");
            }

            // Kiểm tra số điện thoại (không bắt buộc, nhưng nếu nhập thì phải đúng định dạng)
            if (!string.IsNullOrWhiteSpace(user.PhoneNumber) && !IsValidPhone(user.PhoneNumber))
            {
                ModelState.AddModelError("PhoneNumber", "Số điện thoại không hợp lệ (VD: 0988765432).");
            }

            // Kiểm tra mật khẩu
            string password = user.PasswordHash ?? "";

            string passwordPattern =
                @"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[^A-Za-z0-9]).{9,}$";

            if (!Regex.IsMatch(password, passwordPattern))
            {
                ModelState.AddModelError(
                    "PasswordHash",
                    "Mật khẩu phải có ít nhất 9 ký tự, gồm chữ thường, chữ hoa, số và ký tự đặc biệt."
                );
            }

            // Kiểm tra nhập lại mật khẩu
            if (password != (ConfirmPassword ?? ""))
            {
                ModelState.AddModelError(
                    "ConfirmPassword",
                    "Mật khẩu nhập lại không khớp."
                );
            }

            // Kiểm tra email đã tồn tại
            if (string.IsNullOrWhiteSpace(email) == false)
            {
                var checkEmail = db.Users.FirstOrDefault(
                    u => u.Email.ToLower() == email.ToLower()
                );

                if (checkEmail != null)
                {
                    ModelState.AddModelError(
                        "Email",
                        "Gmail này đã được sử dụng!"
                    );
                }
            }

            // Nếu có lỗi thì quay lại trang đăng ký
            if (!ModelState.IsValid)
            {
                user.Email = email;
                return View(user);
            }

            // Băm mật khẩu trước khi lưu vào Database
            user.PasswordHash = PasswordHelper.HashPassword(password);

            user.Email = email;
            user.RoleId = 2; // Customer
            user.Avatar = "/Images/default-avatar.png";
            user.CreatedAt = DateTime.Now;

            db.Users.Add(user);
            db.SaveChanges();

            TempData["SuccessMessage"] = "Đăng ký thành công! Vui lòng đăng nhập.";

            return RedirectToAction("Login");
        }


        // ==========================================
        // 2. XỬ LÝ ĐĂNG NHẬP (LOGIN - POST)
        // ==========================================
        public ActionResult Login() => View();
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Login(string email, string password, bool rememberMe = false)
        {
            if (string.IsNullOrWhiteSpace(email) ||
                string.IsNullOrEmpty(password))
            {
                ViewBag.Error = "Vui lòng nhập đầy đủ email và mật khẩu!";
                return View();
            }

            email = email.Trim();

            string hashedPassword = PasswordHelper.HashPassword(password);

            var user = db.Users.FirstOrDefault(
                u => u.Email == email && u.PasswordHash == hashedPassword
            );

            if (user != null)
            {
                FormsAuthentication.SetAuthCookie(user.Email, rememberMe);

                Session["User"] = user;
                Session["UserName"] = user.FullName;
                Session["UserRoleId"] = user.RoleId;

                if (user.RoleId == 1)
                {
                    return RedirectToAction("Dashboard", "Admin");
                }

                if (user.RoleId == 3)
                {
                    return RedirectToAction("Dashboard", "Partner");
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

            fullName = (fullName ?? "").Trim();
            phoneNumber = (phoneNumber ?? "").Trim();

            if (fullName.Length < 2 || fullName.Length > 100)
            {
                TempData["ErrorMessage"] = "Họ tên phải từ 2 đến 100 ký tự.";
                return RedirectToAction("Profile");
            }
            if (phoneNumber.Length > 0 && !IsValidPhone(phoneNumber))
            {
                TempData["ErrorMessage"] = "Số điện thoại không hợp lệ (VD: 0988765432).";
                return RedirectToAction("Profile");
            }
            if (avatarFile != null && avatarFile.ContentLength > 0)
            {
                string ext = Path.GetExtension(avatarFile.FileName).ToLower();
                if (ext != ".jpg" && ext != ".jpeg" && ext != ".png" && ext != ".gif" && ext != ".webp")
                {
                    TempData["ErrorMessage"] = "Ảnh đại diện chỉ nhận định dạng jpg, png, gif, webp.";
                    return RedirectToAction("Profile");
                }
                if (avatarFile.ContentLength > 2 * 1024 * 1024)
                {
                    TempData["ErrorMessage"] = "Ảnh đại diện tối đa 2MB.";
                    return RedirectToAction("Profile");
                }
            }

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

        // SĐT Việt Nam: 0xxxxxxxxx (10 số) hoặc +84xxxxxxxxx
        private static bool IsValidPhone(string phone)
        {
            return Regex.IsMatch((phone ?? "").Trim(), @"^(0|\+84)\d{9}$");
        }
    }
}