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
    [Authorize]
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
        // QUẢN LÝ PHÒNG: tìm kiếm và lọc theo địa điểm, khách sạn,
        // loại phòng và tình trạng sử dụng.
        public ActionResult RoomsList(string keyword, int? locationId, int? hotelId,
                                      int? roomTypeId, string roomStatus)
        {
            var adminCheck = CheckAdmin();
            if (adminCheck != null) return adminCheck;

            var rooms = db.Rooms
                .Include(r => r.Hotel)
                .Include(r => r.Hotel.Location)
                .Include(r => r.RoomType)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(keyword))
            {
                string key = keyword.Trim();
                rooms = rooms.Where(r =>
                    r.RoomNumber.Contains(key) ||
                    r.Hotel.HotelName.Contains(key));
            }

            if (locationId.HasValue)
                rooms = rooms.Where(r => r.Hotel.LocationId == locationId.Value);

            if (hotelId.HasValue)
                rooms = rooms.Where(r => r.HotelId == hotelId.Value);

            if (roomTypeId.HasValue)
                rooms = rooms.Where(r => r.RoomTypeId == roomTypeId.Value);

            DateTime today = DateTime.Today;
            if (roomStatus == "available")
            {
                rooms = rooms.Where(r => r.IsAvailable == true &&
                    !db.Bookings.Any(b => b.RoomId == r.RoomId &&
                        b.Status != "Đã hủy" && b.Status != "Đã trả phòng"));
            }
            else if (roomStatus == "occupied")
            {
                rooms = rooms.Where(r => db.Bookings.Any(b =>
                    b.RoomId == r.RoomId &&
                    (b.Status == "Đã nhận phòng" || b.Status == "Đã thanh toán") &&
                    b.CheckInDate <= today && b.CheckOutDate > today));
            }
            else if (roomStatus == "approved")
            {
                rooms = rooms.Where(r => db.Bookings.Any(b =>
                    b.RoomId == r.RoomId && b.Status == "Đã duyệt"));
            }

            ViewBag.Keyword = keyword;
            ViewBag.LocationId = new SelectList(db.Locations, "LocationId", "LocationName", locationId);
            ViewBag.HotelId = new SelectList(db.Hotels, "HotelId", "HotelName", hotelId);
            ViewBag.RoomTypeId = new SelectList(db.RoomTypes, "RoomTypeId", "TypeName", roomTypeId);
            ViewBag.RoomStatus = roomStatus;

            return View(rooms.OrderBy(r => r.Hotel.HotelName).ThenBy(r => r.RoomNumber).ToList());
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
            var adminCheck = CheckAdmin();
            if (adminCheck != null) return adminCheck;

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
            var adminCheck = CheckAdmin();
            if (adminCheck != null) return adminCheck;

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

                // Phân quyền:
                // 1 = Admin
                // 2 = Khách hàng
                // 3 = Partner
                user.RoleId = model.RoleId;

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

        // HIỂN THỊ DANH SÁCH ĐƠN ĐẶT PHÒNG + TÌM KIẾM/BỘ LỌC
        public ActionResult BookingsList(string keyword, int? hotelId, string status)
        {
            var adminCheck = CheckAdmin();
            if (adminCheck != null) return adminCheck;

            var bookings = db.Bookings
                .Include(b => b.User)
                .Include(b => b.Room)
                .Include(b => b.Room.Hotel)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(keyword))
            {
                string key = keyword.Trim();
                bookings = bookings.Where(b =>
                    (b.User != null && b.User.FullName.Contains(key)) ||
                    (b.Room != null && b.Room.Hotel != null &&
                     b.Room.Hotel.HotelName.Contains(key)));
            }

            if (hotelId.HasValue)
                bookings = bookings.Where(b => b.Room.HotelId == hotelId.Value);

            if (!string.IsNullOrWhiteSpace(status))
                bookings = bookings.Where(b => b.Status == status);

            ViewBag.Keyword = keyword;
            ViewBag.HotelId = new SelectList(db.Hotels, "HotelId", "HotelName", hotelId);
            ViewBag.Status = status;

            return View(bookings.OrderByDescending(b => b.CreatedAt).ToList());
        }

        // Cập nhật trạng thái đơn bằng POST; chỉ lưu sau khi quản trị viên xác nhận.
        // Quy tắc (lý do hủy, khôi phục đơn, trùng lịch...) nằm chung ở BookingRules.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult UpdateBookingStatus(int id, string status, string cancelReason)
        {
            var adminCheck = CheckAdmin();
            if (adminCheck != null) return adminCheck;

            var booking = db.Bookings.Find(id);
            if (booking == null)
            {
                TempData["ErrorMessage"] = "Không tìm thấy đơn đặt phòng.";
                return RedirectToAction("BookingsList");
            }

            string error = BookingRules.TryChangeStatus(db, booking, status, cancelReason);
            if (error != null)
            {
                TempData["ErrorMessage"] = error;
                return RedirectToAction("BookingsList");
            }

            // Phòng trống hay không được tính theo NGÀY dựa trên các đơn đang giữ phòng,
            // nên không còn bật/tắt Room.IsAvailable theo từng đơn nữa.
            db.SaveChanges();
            TempData["SuccessMessage"] =
                "Đã cập nhật trạng thái đơn đặt phòng #" + booking.BookingId + " thành: " + status + ".";

            return RedirectToAction("BookingsList");
        }
    }
}
