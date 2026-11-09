using System;
using System.Data.Entity;
using System.Linq;
using System.Web.Mvc;
using StayTravel.Models;

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

            var model = new RoomFilterViewModel
            {
                Keyword = keyword,
                LocationId = locationId,
                HotelId = hotelId,
                RoomTypeId = roomTypeId,
                MinPrice = minPrice,
                MaxPrice = maxPrice,
                Rooms = query.ToList(),
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
            return View(room);
        }

        // 9. Trang thanh toán
        [Authorize]
        public ActionResult Checkout(int roomId)
        {
            var room = db.Rooms.Include(r => r.Hotel).FirstOrDefault(r => r.RoomId == roomId);
            if (room == null) return HttpNotFound();
            return View(room);
        }

        [HttpPost]
        [Authorize]
        public ActionResult ProcessBooking(int roomId, DateTime checkIn, DateTime checkOut, string paymentMethod)
        {
            var userEmail = User.Identity.Name;
            var user = db.Users.FirstOrDefault(u => u.Email == userEmail);
            var room = db.Rooms.Find(roomId);

            int days = (checkOut - checkIn).Days;
            if (days <= 0) days = 1;

            var booking = new Booking
            {
                UserId = user.UserId,
                RoomId = roomId,
                CheckInDate = checkIn,
                CheckOutDate = checkOut,
                TotalPrice = room.Price * days,
                Status = "Chờ duyệt",
                PaymentMethod = paymentMethod,
                CreatedAt = DateTime.Now
            };

            db.Bookings.Add(booking);
            db.SaveChanges();

            return RedirectToAction("Profile", "Account");
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