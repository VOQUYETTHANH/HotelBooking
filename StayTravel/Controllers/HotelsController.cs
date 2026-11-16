using StayTravel.Models;
using System;
using System.Data.Entity;
using System.Linq;
using System.Web.Mvc;
using System.Web.Script.Serialization;

namespace StayTravel.Controllers
{
    public class HotelsController : Controller
    {
        private HotelBookingDBEntities db = new HotelBookingDBEntities();

        // 2. Trang đặt phòng & 7. Bộ lọc & Tìm kiếm từ khóa (bao gồm Aliases)
        public ActionResult Index(string keyword, int? locationId, int? hotelId, int? roomTypeId, decimal? minPrice, decimal? maxPrice)
        {
            var query = db.Rooms.Include(r => r.Hotel).Include(r => r.Hotel.Location).Include(r => r.RoomType).AsQueryable();

            if (!string.IsNullOrEmpty(keyword))
            {
                string key = keyword.Trim().ToLower();
                query = query.Where(r => r.Hotel.HotelName.ToLower().Contains(key)
                                      || r.Hotel.Location.LocationName.ToLower().Contains(key)
                                      || (r.Hotel.Location.Aliases != null && r.Hotel.Location.Aliases.ToLower().Contains(key)));
            }

            if (locationId.HasValue) query = query.Where(r => r.Hotel.LocationId == locationId.Value);
            if (hotelId.HasValue) query = query.Where(r => r.HotelId == hotelId.Value);
            if (roomTypeId.HasValue) query = query.Where(r => r.RoomTypeId == roomTypeId.Value);
            if (minPrice.HasValue) query = query.Where(r => r.Price >= minPrice.Value);
            if (maxPrice.HasValue) query = query.Where(r => r.Price <= maxPrice.Value);

            var roomList = query.ToList();

            // Phòng bị xám: đang bảo trì / ngừng nhận đặt, hoặc đã kín lịch trong 30 ngày tới
            var unavailable = new System.Collections.Generic.Dictionary<int, string>();
            var fullIds = RoomAvailability.GetFullRoomIds(db, roomList.Select(r => r.RoomId));
            foreach (var r in roomList)
            {
                if (r.IsAvailable == false)
                    unavailable[r.RoomId] = "Tạm ngừng nhận đặt";
                else if (fullIds.Contains(r.RoomId))
                    unavailable[r.RoomId] = "Đã kín lịch " + RoomAvailability.FullCheckDays + " ngày tới";
            }

            var model = new RoomFilterViewModel
            {
                Keyword = keyword,
                LocationId = locationId,
                HotelId = hotelId,
                RoomTypeId = roomTypeId,
                MinPrice = minPrice,
                MaxPrice = maxPrice,
                Rooms = roomList,
                UnavailableRooms = unavailable,
                Locations = db.Locations.ToList(),
                Hotels = db.Hotels.ToList(),
                RoomTypes = db.RoomTypes.ToList()
            };

            return View(model);
        }

        // 3. Trang chi tiết Khách sạn
        public ActionResult Details(int id)
        {
            var hotel = db.Hotels.Include(h => h.Location)
                                 .Include(h => h.Reviews.Select(r => r.User))
                                 .Include(h => h.Rooms.Select(r => r.RoomType))
                                 .FirstOrDefault(h => h.HotelId == id);

            if (hotel == null) return HttpNotFound();
            return View(hotel); // Phải truyền object hotel ra View
        }

        // 4. Trang chi tiết Phòng
        public ActionResult RoomDetails(int id)
        {
            var room = db.Rooms.Include(r => r.Hotel).Include(r => r.RoomType).FirstOrDefault(r => r.RoomId == id);
            if (room == null) return HttpNotFound();

            // Thời gian biểu của phòng: khoảng đã có người đặt + khoảng còn trống (90 ngày tới)
            const int scheduleDays = 90;
            var busy = RoomAvailability.GetBusyRanges(db, id, true);
            ViewBag.ScheduleDays = scheduleDays;
            ViewBag.BusyRanges = busy;
            ViewBag.FreeRanges = RoomAvailability.GetFreeRanges(busy, scheduleDays);
            ViewBag.RangesJson = RoomAvailability.ToJson(busy);

            return View(room);
        }

        // 9. Trang thanh toán
        [Authorize]
        public ActionResult Checkout(int roomId)
        {
            var room = db.Rooms
                .Include(r => r.Hotel)
                .Include(r => r.RoomType)
                .FirstOrDefault(r => r.RoomId == roomId);

            if (room == null)
                return HttpNotFound();

            if (room.IsAvailable == false)
            {
                TempData["Error"] = "Phòng này hiện tạm ngừng nhận đặt.";
                return RedirectToAction("RoomDetails", new { id = roomId });
            }

            LoadBookedRanges(roomId);

            return View(room);
        }

        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public ActionResult ProcessBooking(
    int roomId,
    DateTime? checkIn,
    DateTime? checkOut,
    string paymentMethod)
        {
            // Tim nguoi dung dang dang nhap
            string userEmail = User.Identity.Name;

            var user = db.Users.FirstOrDefault(
                u => u.Email == userEmail
            );

            if (user == null)
                return RedirectToAction("Login", "Account");

            // Tim phong
            var room = db.Rooms
                .Include(r => r.Hotel)
                .Include(r => r.RoomType)
                .FirstOrDefault(r => r.RoomId == roomId);

            if (room == null)
                return HttpNotFound();

            // Phong dang bao tri / ngung nhan dat
            if (room.IsAvailable == false)
            {
                ViewBag.Error = "Phòng này hiện tạm ngừng nhận đặt.";
                LoadBookedRanges(roomId);
                return View("Checkout", room);
            }

            // Kiem tra ngay
            if (!checkIn.HasValue || !checkOut.HasValue)
            {
                ViewBag.Error = "Vui lòng chọn ngày nhận phòng và trả phòng.";
                LoadBookedRanges(roomId);
                return View("Checkout", room);
            }

            // Giu lai ngay da chon de hien lai tren lich neu bi bao loi
            ViewBag.PrevCheckIn = checkIn.Value.ToString("yyyy-MM-dd");
            ViewBag.PrevCheckOut = checkOut.Value.ToString("yyyy-MM-dd");

            DateTime startDate = checkIn.Value.Date;
            DateTime endDate = checkOut.Value.Date;

            if (startDate < DateTime.Today)
            {
                ViewBag.Error = "Ngày nhận phòng không được ở trong quá khứ.";
                LoadBookedRanges(roomId);
                return View("Checkout", room);
            }

            if (endDate <= startDate)
            {
                ViewBag.Error = "Ngày trả phòng phải sau ngày nhận phòng.";
                LoadBookedRanges(roomId);
                return View("Checkout", room);
            }

            if ((endDate - startDate).Days > RoomAvailability.MaxNights)
            {
                ViewBag.Error = "Mỗi lần đặt tối đa " + RoomAvailability.MaxNights + " đêm.";
                LoadBookedRanges(roomId);
                return View("Checkout", room);
            }

            // Chi chap nhan phuong thuc thanh toan hop le
            if (paymentMethod != "Tiền mặt" &&
                paymentMethod != "Chuyển khoản")
            {
                ViewBag.Error = "Phương thức thanh toán không hợp lệ.";
                LoadBookedRanges(roomId);
                return View("Checkout", room);
            }

            // Kiem tra trung lich dat phong
            // (gom ca don dat phong va lich do khach san tu sap xep)
            bool isBooked = RoomAvailability.HasOverlap(db, roomId, startDate, endDate);

            if (isBooked)
            {
                ViewBag.Error =
                    "Khoảng thời gian bạn chọn đã có người đặt. Vui lòng chọn ngày khác.";

                LoadBookedRanges(roomId);
                return View("Checkout", room);
            }

            // Tinh tong tien
            int days = (endDate - startDate).Days;

            var booking = new Booking
            {
                UserId = user.UserId,
                RoomId = roomId,
                CheckInDate = startDate,
                CheckOutDate = endDate,
                TotalPrice = room.Price * days,
                Status = "Chờ duyệt",
                PaymentMethod = paymentMethod,
                CreatedAt = DateTime.Now
            };

            try
            {
                db.Bookings.Add(booking);
                db.SaveChanges();

                TempData["SuccessMessage"] =
                    "Đặt phòng thành công! Đơn đặt phòng của bạn đang chờ xác nhận.";

                return RedirectToAction("Profile", "Account");
            }
            catch (Exception)
            {
                ViewBag.Error =
                    "Không thể lưu đơn đặt phòng. Vui lòng thử lại.";

                LoadBookedRanges(roomId);
                return View("Checkout", room);
            }
        }

        private void LoadBookedRanges(int roomId)
        {
            // Khach chi thay "da co nguoi dat" / "khong nhan dat", khong thay ghi chu noi bo cua khach san
            var ranges = RoomAvailability.GetBusyRanges(db, roomId, true);
            ViewBag.BookedRanges = RoomAvailability.ToJson(ranges);
        }

        [HttpPost]
        [Authorize]
        public ActionResult ToggleWishlist(int hotelId)
        {
            var userEmail = User.Identity.Name;
            var user = db.Users.FirstOrDefault(u => u.Email == userEmail);
            if (user == null) return RedirectToAction("Login", "Account");

            var existing = db.Wishlists.FirstOrDefault(w => w.UserId == user.UserId && w.HotelId == hotelId);
            if (existing != null)
            {
                // Nếu đã có thì xóa khỏi yêu thích
                db.Wishlists.Remove(existing);
                TempData["SuccessMessage"] = "Đã xóa khỏi danh sách yêu thích!";
            }
            else
            {
                // Nếu chưa có thì thêm vào yêu thích
                var wishlist = new Wishlist
                {
                    UserId = user.UserId,
                    HotelId = hotelId,
                    CreatedAt = DateTime.Now
                };
                db.Wishlists.Add(wishlist);
                TempData["SuccessMessage"] = "Đã thêm vào danh sách yêu thích thành công!";
            }
            db.SaveChanges();
            return Redirect(Request.UrlReferrer?.ToString() ?? Url.Action("Index", "Home"));
        }
    }
}