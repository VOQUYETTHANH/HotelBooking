using StayTravel.Models;
using System;
using System.Data.Entity;
using System.IO;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace StayTravel.Controllers
{
    // 10. Trang dành riêng cho Admin
    public class AdminController : Controller
    {
        private HotelBookingDBEntities db = new HotelBookingDBEntities();

        public ActionResult Dashboard()
        {
            ViewBag.TotalHotels = db.Hotels.Count();
            ViewBag.TotalBookings = db.Bookings.Count();
            ViewBag.TotalUsers = db.Users.Count();
            var bookings = db.Bookings.Include(b => b.User).Include(b => b.Room.Hotel).OrderByDescending(b => b.CreatedAt).ToList();
            return View(bookings);
        }

        [HttpPost]
        [Authorize]
        public ActionResult ProcessBooking(int roomId, DateTime checkIn, DateTime checkOut, string paymentMethod)
        {
            var userEmail = User.Identity.Name;
            var user = db.Users.FirstOrDefault(u => u.Email == userEmail);
            if (user == null) return RedirectToAction("Login", "Account");

            var room = db.Rooms.FirstOrDefault(r => r.RoomId == roomId && r.IsAvailable == true);
            if (room == null)
            {
                TempData["Error"] = "Rất tiếc, phòng vừa có người đặt hoặc không còn khả dụng.";
                return RedirectToAction("Index");
            }

            int days = (checkOut - checkIn).Days;
            if (days <= 0) days = 1;

            var booking = new Booking
            {
                UserId = user.UserId,
                RoomId = roomId,
                CheckInDate = checkIn,
                CheckOutDate = checkOut,
                TotalPrice = room.Price * days,
                Status = "Đã duyệt", 
                PaymentMethod = paymentMethod,
                CreatedAt = DateTime.Now
            };
            room.IsAvailable = false;

            db.Bookings.Add(booking);
            db.SaveChanges();

            TempData["SuccessMessage"] = "Đặt phòng thành công và đã được hệ thống tự động xác nhận!";
            return RedirectToAction("Profile", "Account");
        }

        public ActionResult ApproveBooking(int id)
        {
            var booking = db.Bookings.Find(id);
            if (booking != null)
            {
                booking.Status = "Đã duyệt";
                db.Entry(booking).Property("Status").IsModified = true; 
                db.SaveChanges();

                TempData["SuccessMessage"] = $"Đã duyệt thành công đơn đặt phòng #{booking.BookingId}!";
            }
            return RedirectToAction("BookingsList");
        }

        // HỦY ĐƠN ĐẶT PHÒNG (Bỏ [HttpPost] để chạy trực tiếp từ thẻ <a>)
        public ActionResult CancelBooking(int id)
        {
            var booking = db.Bookings.Find(id);
            if (booking != null)
            {
                booking.Status = "Đã hủy";
                db.Entry(booking).Property("Status").IsModified = true;

                // Mở lại phòng cho khách khác đặt
                var room = db.Rooms.Find(booking.RoomId);
                if (room != null)
                {
                    room.IsAvailable = true;
                }

                db.SaveChanges();
                TempData["SuccessMessage"] = $"Đã hủy đơn đặt phòng #{booking.BookingId} và mở lại phòng!";
            }
            return RedirectToAction("BookingsList");
        }

        // 4. QUẢN LÝ KHÁCH SẠN (THÊM, SỬA, XÓA)
        public ActionResult HotelsList()
        {
            var hotels = db.Hotels.Include(h => h.Location).ToList();
            return View(hotels);
        }

        public ActionResult CreateHotel()
        {
            ViewBag.LocationId = new SelectList(db.Locations, "LocationId", "LocationName");
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult CreateHotel(Hotel hotel, HttpPostedFileBase imageFile)
        {
            if (ModelState.IsValid)
            {
                if (imageFile != null && imageFile.ContentLength > 0)
                {
                    string fileName = Path.GetFileNameWithoutExtension(imageFile.FileName) + "_" + DateTime.Now.ToString("yyyyMMddHHmmss") + Path.GetExtension(imageFile.FileName);
                    string path = Path.Combine(Server.MapPath("~/Images/"), fileName);
                    imageFile.SaveAs(path);
                    hotel.ImageUrl = "/Images/" + fileName;
                }
                else
                {
                    hotel.ImageUrl = "/Images/hcm-hotel1.jpg";
                }

                db.Hotels.Add(hotel);
                db.SaveChanges();
                return RedirectToAction("HotelsList");
            }
            ViewBag.LocationId = new SelectList(db.Locations, "LocationId", "LocationName", hotel.LocationId);
            return View(hotel);
        }

        public ActionResult EditHotel(int id)
        {
            var hotel = db.Hotels.Find(id);
            if (hotel == null) return HttpNotFound();
            ViewBag.LocationId = new SelectList(db.Locations, "LocationId", "LocationName", hotel.LocationId);
            return View(hotel);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult EditHotel(Hotel hotel, HttpPostedFileBase imageFile)
        {
            if (ModelState.IsValid)
            {
                var existing = db.Hotels.Find(hotel.HotelId);
                if (existing != null)
                {
                    existing.HotelName = hotel.HotelName;
                    existing.LocationId = hotel.LocationId;
                    existing.Address = hotel.Address;
                    existing.StarRating = hotel.StarRating;
                    existing.Description = hotel.Description;
                    existing.IsFeatured = hotel.IsFeatured;

                    if (imageFile != null && imageFile.ContentLength > 0)
                    {
                        string fileName = Path.GetFileNameWithoutExtension(imageFile.FileName) + "_" + DateTime.Now.ToString("yyyyMMddHHmmss") + Path.GetExtension(imageFile.FileName);
                        string path = Path.Combine(Server.MapPath("~/Images/"), fileName);
                        imageFile.SaveAs(path);
                        existing.ImageUrl = "/Images/" + fileName;
                    }

                    db.SaveChanges();
                    return RedirectToAction("HotelsList");
                }
            }
            ViewBag.LocationId = new SelectList(db.Locations, "LocationId", "LocationName", hotel.LocationId);
            return View(hotel);
        }

        public ActionResult DeleteHotel(int id)
        {
            var hotel = db.Hotels.Find(id);
            if (hotel != null)
            {
                db.Hotels.Remove(hotel);
                db.SaveChanges();
            }
            return RedirectToAction("HotelsList");
        }

        // 5. QUẢN LÝ PHÒNG KHACH SẠN (THÊM, SỬA, XÓA)
        public ActionResult RoomsList()
        {
            var rooms = db.Rooms.Include(r => r.Hotel).Include(r => r.RoomType).ToList();
            return View(rooms);
        }

        public ActionResult CreateRoom()
        {
            ViewBag.HotelId = new SelectList(db.Hotels, "HotelId", "HotelName");
            ViewBag.RoomTypeId = new SelectList(db.RoomTypes, "RoomTypeId", "TypeName");
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult CreateRoom(Room room, HttpPostedFileBase imageFile)
        {
            if (ModelState.IsValid)
            {
                if (imageFile != null && imageFile.ContentLength > 0)
                {
                    string fileName = Path.GetFileNameWithoutExtension(imageFile.FileName) + "_" + DateTime.Now.ToString("yyyyMMddHHmmss") + Path.GetExtension(imageFile.FileName);
                    string path = Path.Combine(Server.MapPath("~/Images/"), fileName);
                    imageFile.SaveAs(path);
                    room.ImageUrl = "/Images/" + fileName;
                }
                else
                {
                    room.ImageUrl = "/Images/room-default1.jpg";
                }

                room.IsAvailable = true;
                db.Rooms.Add(room);
                db.SaveChanges();
                return RedirectToAction("RoomsList");
            }
            ViewBag.HotelId = new SelectList(db.Hotels, "HotelId", "HotelName", room.HotelId);
            ViewBag.RoomTypeId = new SelectList(db.RoomTypes, "RoomTypeId", "TypeName", room.RoomTypeId);
            return View(room);
        }

        public ActionResult EditRoom(int id)
        {
            var room = db.Rooms.Find(id);
            if (room == null) return HttpNotFound();
            ViewBag.HotelId = new SelectList(db.Hotels, "HotelId", "HotelName", room.HotelId);
            ViewBag.RoomTypeId = new SelectList(db.RoomTypes, "RoomTypeId", "TypeName", room.RoomTypeId);
            return View(room);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult EditRoom(Room room, HttpPostedFileBase imageFile)
        {
            if (ModelState.IsValid)
            {
                var existing = db.Rooms.Find(room.RoomId);
                if (existing != null)
                {
                    existing.HotelId = room.HotelId;
                    existing.RoomTypeId = room.RoomTypeId;
                    existing.RoomNumber = room.RoomNumber;
                    existing.Price = room.Price;
                    existing.Capacity = room.Capacity;
                    existing.Amenities = room.Amenities;
                    existing.OutsideView = room.OutsideView;
                    existing.IsAvailable = room.IsAvailable;

                    if (imageFile != null && imageFile.ContentLength > 0)
                    {
                        string fileName = Path.GetFileNameWithoutExtension(imageFile.FileName) + "_" + DateTime.Now.ToString("yyyyMMddHHmmss") + Path.GetExtension(imageFile.FileName);
                        string path = Path.Combine(Server.MapPath("~/Images/"), fileName);
                        imageFile.SaveAs(path);
                        existing.ImageUrl = "/Images/" + fileName;
                    }

                    db.SaveChanges();
                    return RedirectToAction("RoomsList");
                }
            }
            ViewBag.HotelId = new SelectList(db.Hotels, "HotelId", "HotelName", room.HotelId);
            ViewBag.RoomTypeId = new SelectList(db.RoomTypes, "RoomTypeId", "TypeName", room.RoomTypeId);
            return View(room);
        }

        public ActionResult DeleteRoom(int id)
        {
            var room = db.Rooms.Find(id);
            if (room != null)
            {
                db.Rooms.Remove(room);
                db.SaveChanges();
            }
            return RedirectToAction("RoomsList");
        }
        public ActionResult UsersList()
        {
            var adminCheck = CheckAdmin();
            if (adminCheck != null) return adminCheck;

            var users = db.Users.ToList();
            return View(users);
        }

        // 2. FORM THÊM MỚI NGƯỜI DÙNG
        [HttpGet]
        public ActionResult AddUser()
        {
            var adminCheck = CheckAdmin();
            if (adminCheck != null) return adminCheck;
            return View();
        }

        [HttpPost]
        public ActionResult AddUser(User model, string password)
        {
            var adminCheck = CheckAdmin();
            if (adminCheck != null) return adminCheck;

            if (ModelState.IsValid)
            {
                // Kiểm tra email trùng
                var existing = db.Users.FirstOrDefault(u => u.Email == model.Email);
                if (existing != null)
                {
                    ModelState.AddModelError("Email", "Email này đã được sử dụng!");
                    return View(model);
                }

                model.PasswordHash = password; 
                if (string.IsNullOrEmpty(model.Avatar))
                {
                    model.Avatar = "/Content/images/default-avatar.png"; // Avatar mặc định
                }
                db.Users.Add(model);
                db.SaveChanges();
                TempData["SuccessMessage"] = "Thêm người dùng thành công!";
                return RedirectToAction("UsersList");
            }
            return View(model);
        }

        // 3. FORM CHỈNH SỬA THÔNG TIN NGƯỜI DÙNG
        [HttpGet]
        public ActionResult EditUser(int id)
        {
            var adminCheck = CheckAdmin();
            if (adminCheck != null) return adminCheck;

            var user = db.Users.Find(id);
            if (user == null) return HttpNotFound();
            return View(user);
        }

        [HttpPost]
        public ActionResult EditUser(User model)
        {
            var adminCheck = CheckAdmin();
            if (adminCheck != null) return adminCheck;

            var user = db.Users.Find(model.UserId);
            if (user != null)
            {
                user.FullName = model.FullName;
                user.Email = model.Email;
                user.RoleId = model.RoleId; // Phân quyền (1: Admin, 2: Khách hàng...)
                user.Avatar = model.Avatar;

                db.SaveChanges();
                TempData["SuccessMessage"] = "Cập nhật thông tin thành công!";
                return RedirectToAction("UsersList");
            }
            return View(model);
        }

        // 4. XÓA NGƯỜI DÙNG
        public ActionResult DeleteUser(int id)
        {
            var adminCheck = CheckAdmin();
            if (adminCheck != null) return adminCheck;

            var user = db.Users.Find(id);
            if (user != null)
            {
                // Kiểm tra nếu user có lịch sử đặt phòng thì có thể cần xóa bảng liên quan trước hoặc báo lỗi
                db.Users.Remove(user);
                db.SaveChanges();
                TempData["SuccessMessage"] = "Đã xóa người dùng thành công!";
            }
            return RedirectToAction("UsersList");
        }

        // Hàm phụ trợ kiểm tra quyền Admin
        private ActionResult CheckAdmin()
        {
            var loggedUser = Session["User"] as StayTravel.Models.User;
            if (loggedUser == null || loggedUser.RoleId != 1)
            {
                return RedirectToAction("Login", "Account");
            }
            return null;
        }

        // HIỂN THỊ DANH SÁCH ĐƠN ĐẶT PHÒNG
        public ActionResult BookingsList()
        {
            var loggedSession = Session["User"] as StayTravel.Models.User;
            if (loggedSession == null || loggedSession.RoleId != 1)
            {
                return RedirectToAction("Login", "Account");
            }

            var bookings = db.Bookings.ToList();

            return View(bookings);
        }
    }
}