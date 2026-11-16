using StayTravel.Models;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Web.Mvc;
using System.Web.Routing;

namespace StayTravel.Controllers
{
    // Trang dành riêng cho Partner (khách sạn): xử lý check-in tại quầy, đổi trạng thái đơn / phòng,
    // tự sắp xếp lịch phòng (khách đặt trực tiếp, bảo trì...).
    // Partner chỉ thấy phòng và đơn thuộc các khách sạn của mình (Hotels.PartnerId).
    [Authorize]
    public class PartnerController : Controller
    {
        private const int PartnerRoleId = 3; // 1 = Admin, 2 = Customer, 3 = Partner

        private HotelBookingDBEntities db = new HotelBookingDBEntities();
        private Partner partner;
        private List<int> myHotelIds;

        // ======================================================================
        // Chặn người không phải Partner + nạp danh sách khách sạn của Partner
        // ======================================================================
        protected override void OnActionExecuting(ActionExecutingContext filterContext)
        {
            base.OnActionExecuting(filterContext);

            string email = User.Identity.Name;
            var user = db.Users.FirstOrDefault(u => u.Email == email);

            if (user == null || user.RoleId != PartnerRoleId)
            {
                filterContext.Result = new RedirectToRouteResult(
                    new RouteValueDictionary(new { controller = "Account", action = "Login" }));
                return;
            }

            partner = db.Partners.FirstOrDefault(p => p.UserId == user.UserId);
            if (partner == null)
            {
                filterContext.Result = new RedirectToRouteResult(
                    new RouteValueDictionary(new { controller = "Account", action = "Login" }));
                return;
            }

            myHotelIds = db.Hotels.Where(h => h.PartnerId == partner.PartnerId)
                                  .Select(h => h.HotelId).ToList();

            ViewBag.PartnerName = partner.BusinessName;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) db.Dispose();
            base.Dispose(disposing);
        }

        // ======================================================================
        // 1. TỔNG QUAN: việc cần làm tại quầy hôm nay
        // ======================================================================
        public ActionResult Dashboard()
        {
            DateTime today = DateTime.Today;
            DateTime tomorrow = today.AddDays(1);

            var myBookings = db.Bookings
                .Include(b => b.User)
                .Include(b => b.Room)
                .Include(b => b.Room.Hotel)
                .Where(b => myHotelIds.Contains(b.Room.HotelId));

            var model = new PartnerDashboardViewModel
            {
                BusinessName = partner.BusinessName,
                Hotels = db.Hotels.Where(h => h.PartnerId == partner.PartnerId).ToList(),
                TotalRooms = db.Rooms.Count(r => myHotelIds.Contains(r.HotelId)),
                MaintenanceRooms = db.Rooms.Count(r => myHotelIds.Contains(r.HotelId) && r.IsAvailable == false),
                PendingCount = myBookings.Count(b => b.Status == "Chờ duyệt"),

                // Khách dự kiến đến hôm nay
                ArrivalsToday = myBookings
                    .Where(b => b.CheckInDate >= today && b.CheckInDate < tomorrow
                                && (b.Status == "Chờ duyệt" || b.Status == "Đã duyệt"))
                    .OrderBy(b => b.BookingId).ToList(),

                // Khách đang ở
                InHouse = myBookings
                    .Where(b => (b.Status == "Đã nhận phòng" || b.Status == "Đã thanh toán")
                                && b.CheckOutDate >= tomorrow && b.CheckInDate < tomorrow)
                    .OrderBy(b => b.CheckOutDate).ToList(),

                // Khách trả phòng hôm nay (hoặc trễ hạn trả)
                DeparturesToday = myBookings
                    .Where(b => (b.Status == "Đã nhận phòng" || b.Status == "Đã thanh toán")
                                && b.CheckOutDate < tomorrow)
                    .OrderBy(b => b.CheckOutDate).ToList(),

                // Quá hạn: đã qua ngày nhận phòng nhưng khách chưa nhận
                Overdue = myBookings
                    .Where(b => b.CheckInDate < today
                                && (b.Status == "Chờ duyệt" || b.Status == "Đã duyệt"))
                    .OrderBy(b => b.CheckInDate).ToList()
            };

            return View(model);
        }

        // ======================================================================
        // 2. QUẢN LÝ ĐƠN ĐẶT PHÒNG (tìm kiếm + đổi trạng thái bằng dropdown)
        // ======================================================================
        public ActionResult Bookings(string keyword, int? hotelId, string status, string quick)
        {
            DateTime today = DateTime.Today;
            DateTime tomorrow = today.AddDays(1);

            var query = db.Bookings
                .Include(b => b.User)
                .Include(b => b.Room)
                .Include(b => b.Room.Hotel)
                .Where(b => myHotelIds.Contains(b.Room.HotelId));

            if (!string.IsNullOrWhiteSpace(keyword))
            {
                string key = keyword.Trim();
                int idKey;
                bool isId = int.TryParse(key.TrimStart('#'), out idKey);
                query = query.Where(b =>
                    (isId && b.BookingId == idKey) ||
                    (b.User != null && (b.User.FullName.Contains(key) || b.User.Email.Contains(key)
                                        || (b.User.PhoneNumber != null && b.User.PhoneNumber.Contains(key)))) ||
                    b.Room.RoomNumber.Contains(key) ||
                    b.Room.Hotel.HotelName.Contains(key));
            }

            if (hotelId.HasValue && myHotelIds.Contains(hotelId.Value))
                query = query.Where(b => b.Room.HotelId == hotelId.Value);

            if (!string.IsNullOrWhiteSpace(status))
                query = query.Where(b => b.Status == status);

            // Lọc nhanh
            if (quick == "arrivals")
                query = query.Where(b => b.CheckInDate >= today && b.CheckInDate < tomorrow
                                         && (b.Status == "Chờ duyệt" || b.Status == "Đã duyệt"));
            else if (quick == "overdue")
                query = query.Where(b => b.CheckInDate < today
                                         && (b.Status == "Chờ duyệt" || b.Status == "Đã duyệt"));
            else if (quick == "inhouse")
                query = query.Where(b => (b.Status == "Đã nhận phòng" || b.Status == "Đã thanh toán"));

            ViewBag.Keyword = keyword;
            ViewBag.Status = status;
            ViewBag.Quick = quick;
            ViewBag.HotelId = new SelectList(db.Hotels.Where(h => h.PartnerId == partner.PartnerId), "HotelId", "HotelName", hotelId);
            ViewBag.OverdueCount = db.Bookings.Count(b => myHotelIds.Contains(b.Room.HotelId)
                                                          && b.CheckInDate < today
                                                          && (b.Status == "Chờ duyệt" || b.Status == "Đã duyệt"));

            return View(query.OrderByDescending(b => b.CreatedAt).ToList());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult UpdateBookingStatus(int id, string status, string cancelReason, string returnUrl)
        {
            var booking = db.Bookings.Include(b => b.Room).FirstOrDefault(b => b.BookingId == id);

            // Chỉ được sửa đơn thuộc khách sạn của mình
            if (booking == null || !myHotelIds.Contains(booking.Room.HotelId))
            {
                TempData["ErrorMessage"] = "Không tìm thấy đơn đặt phòng thuộc khách sạn của bạn.";
                return RedirectBack(returnUrl, "Bookings");
            }

            string error = BookingRules.TryChangeStatus(db, booking, status, cancelReason);
            if (error != null)
            {
                TempData["ErrorMessage"] = error;
                return RedirectBack(returnUrl, "Bookings");
            }

            db.SaveChanges();
            TempData["SuccessMessage"] = "Đã cập nhật đơn #" + booking.BookingId + " thành: " + status
                + (status == "Đã hủy" ? " (" + cancelReason + ")" : "") + ".";
            return RedirectBack(returnUrl, "Bookings");
        }

        // Hủy hàng loạt các đơn quá hạn (đã qua ngày nhận phòng mà khách không đến)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult CancelOverdue(string returnUrl)
        {
            DateTime today = DateTime.Today;
            var overdue = db.Bookings
                .Include(b => b.Room)
                .Where(b => myHotelIds.Contains(b.Room.HotelId)
                            && b.CheckInDate < today
                            && (b.Status == "Chờ duyệt" || b.Status == "Đã duyệt"))
                .ToList();

            foreach (var b in overdue)
            {
                b.Status = "Đã hủy";
                b.CancelReason = "Quá hạn";
            }
            db.SaveChanges();

            TempData["SuccessMessage"] = overdue.Count == 0
                ? "Không có đơn quá hạn nào."
                : "Đã hủy " + overdue.Count + " đơn quá hạn không nhận phòng.";
            return RedirectBack(returnUrl, "Bookings");
        }

        // ======================================================================
        // 3. QUẢN LÝ PHÒNG (tìm kiếm + đổi tình trạng phòng)
        // ======================================================================
        public ActionResult Rooms(string keyword, int? hotelId, int? roomTypeId, string state)
        {
            DateTime today = DateTime.Today;
            DateTime tomorrow = today.AddDays(1);

            var query = db.Rooms
                .Include(r => r.Hotel)
                .Include(r => r.RoomType)
                .Where(r => myHotelIds.Contains(r.HotelId));

            if (!string.IsNullOrWhiteSpace(keyword))
            {
                string key = keyword.Trim();
                query = query.Where(r => r.RoomNumber.Contains(key) || r.Hotel.HotelName.Contains(key));
            }
            if (hotelId.HasValue && myHotelIds.Contains(hotelId.Value))
                query = query.Where(r => r.HotelId == hotelId.Value);
            if (roomTypeId.HasValue)
                query = query.Where(r => r.RoomTypeId == roomTypeId.Value);

            var rooms = query.OrderBy(r => r.Hotel.HotelName).ThenBy(r => r.RoomNumber).ToList();
            var roomIds = rooms.Select(r => r.RoomId).ToList();

            // Đơn / lịch đang chiếm phòng trong đêm hôm nay
            var todaysBookings = db.Bookings
                .Where(b => roomIds.Contains(b.RoomId)
                            && b.Status != "Đã hủy" && b.Status != "Đã trả phòng"
                            && b.CheckInDate < tomorrow && b.CheckOutDate > today)
                .ToList();
            var todaysBlocks = db.RoomBlocks
                .Where(k => roomIds.Contains(k.RoomId) && k.StartDate < tomorrow && k.EndDate > today)
                .ToList();

            var rows = new List<PartnerRoomRow>();
            foreach (var r in rooms)
            {
                var row = new PartnerRoomRow { Room = r };
                var bk = todaysBookings.Where(b => b.RoomId == r.RoomId).OrderBy(b => b.CheckInDate).FirstOrDefault();
                var blk = todaysBlocks.FirstOrDefault(k => k.RoomId == r.RoomId);

                if (r.IsAvailable == false)
                {
                    row.StateKey = "maintenance"; row.StateText = "Ngừng nhận đặt"; row.StateBadge = "bg-secondary";
                }
                else if (bk != null && (bk.Status == "Đã nhận phòng" || bk.Status == "Đã thanh toán"))
                {
                    row.StateKey = "occupied"; row.StateText = "Đang có khách"; row.StateBadge = "bg-danger";
                    row.Detail = "Đơn #" + bk.BookingId + " - trả phòng " + bk.CheckOutDate.ToString("dd/MM/yyyy");
                }
                else if (bk != null)
                {
                    row.StateKey = "reserved"; row.StateText = "Đã đặt - chờ nhận phòng"; row.StateBadge = "bg-warning text-dark";
                    row.Detail = "Đơn #" + bk.BookingId + " - nhận " + bk.CheckInDate.ToString("dd/MM/yyyy");
                }
                else if (blk != null && blk.BlockType == RoomAvailability.BlockDirect)
                {
                    row.StateKey = "occupied"; row.StateText = "Đang có khách (đặt trực tiếp)"; row.StateBadge = "bg-danger";
                    row.Detail = (blk.Note ?? "") + " - đến " + blk.EndDate.ToString("dd/MM/yyyy");
                }
                else if (blk != null)
                {
                    row.StateKey = "blocked"; row.StateText = "Đang bảo trì"; row.StateBadge = "bg-secondary";
                    row.Detail = (blk.Note ?? "") + " - đến " + blk.EndDate.ToString("dd/MM/yyyy");
                }
                else
                {
                    row.StateKey = "free"; row.StateText = "Trống"; row.StateBadge = "bg-success";
                }
                rows.Add(row);
            }

            if (!string.IsNullOrWhiteSpace(state))
                rows = rows.Where(x => x.StateKey == state).ToList();

            ViewBag.Keyword = keyword;
            ViewBag.State = state;
            ViewBag.HotelId = new SelectList(db.Hotels.Where(h => h.PartnerId == partner.PartnerId), "HotelId", "HotelName", hotelId);
            ViewBag.RoomTypeId = new SelectList(db.RoomTypes, "RoomTypeId", "TypeName", roomTypeId);

            return View(rows);
        }

        // Bật / tắt nhận đặt cho phòng (Room.IsAvailable): true = sẵn sàng, false = bảo trì / ngừng nhận đặt
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult UpdateRoomStatus(int id, bool isAvailable, string returnUrl)
        {
            var room = db.Rooms.Find(id);
            if (room == null || !myHotelIds.Contains(room.HotelId))
            {
                TempData["ErrorMessage"] = "Không tìm thấy phòng thuộc khách sạn của bạn.";
                return RedirectBack(returnUrl, "Rooms");
            }

            room.IsAvailable = isAvailable;
            db.SaveChanges();
            TempData["SuccessMessage"] = "Phòng " + room.RoomNumber + " đã chuyển sang: "
                + (isAvailable ? "Sẵn sàng nhận đặt." : "Ngừng nhận đặt / bảo trì (không ảnh hưởng các đơn đã có).");
            return RedirectBack(returnUrl, "Rooms");
        }

        // ======================================================================
        // 4. LỊCH PHÒNG: khách sạn tự sắp xếp (đặt trực tiếp / bảo trì)
        // ======================================================================
        public ActionResult RoomSchedule(int id)
        {
            var room = db.Rooms.Include(r => r.Hotel).Include(r => r.RoomType).FirstOrDefault(r => r.RoomId == id);
            if (room == null || !myHotelIds.Contains(room.HotelId))
                return HttpNotFound();

            DateTime today = DateTime.Today;
            var busy = RoomAvailability.GetBusyRanges(db, id, false);

            var model = new RoomScheduleViewModel
            {
                Room = room,
                Bookings = db.Bookings.Include(b => b.User)
                    .Where(b => b.RoomId == id && b.Status != "Đã hủy" && b.Status != "Đã trả phòng" && b.CheckOutDate > today)
                    .OrderBy(b => b.CheckInDate).ToList(),
                Blocks = db.RoomBlocks.Where(k => k.RoomId == id && k.EndDate > today)
                    .OrderBy(k => k.StartDate).ToList(),
                RangesJson = RoomAvailability.ToJson(busy),
                FreeRanges = RoomAvailability.GetFreeRanges(busy, 60)
            };
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult AddBlock(int roomId, DateTime? startDate, DateTime? endDate, string blockType, string note)
        {
            var room = db.Rooms.Find(roomId);
            if (room == null || !myHotelIds.Contains(room.HotelId))
                return HttpNotFound();

            note = (note ?? "").Trim();

            if (!startDate.HasValue || !endDate.HasValue)
            {
                TempData["ErrorMessage"] = "Vui lòng chọn ngày bắt đầu và ngày kết thúc.";
                return RedirectToAction("RoomSchedule", new { id = roomId });
            }

            DateTime start = startDate.Value.Date;
            DateTime end = endDate.Value.Date;

            if (!RoomAvailability.BlockTypes.Contains(blockType))
            {
                TempData["ErrorMessage"] = "Loại lịch không hợp lệ.";
            }
            else if (start < DateTime.Today)
            {
                TempData["ErrorMessage"] = "Ngày bắt đầu không được ở trong quá khứ.";
            }
            else if (end <= start)
            {
                TempData["ErrorMessage"] = "Ngày kết thúc phải sau ngày bắt đầu.";
            }
            else if ((end - start).Days > 365)
            {
                TempData["ErrorMessage"] = "Một khoảng lịch tối đa 365 ngày.";
            }
            else if (note.Length > 255)
            {
                TempData["ErrorMessage"] = "Ghi chú tối đa 255 ký tự.";
            }
            else if (blockType == RoomAvailability.BlockDirect && note.Length < 2)
            {
                TempData["ErrorMessage"] = "Với khách đặt trực tiếp, vui lòng ghi tên / số điện thoại khách vào ghi chú.";
            }
            else if (RoomAvailability.HasOverlap(db, roomId, start, end))
            {
                TempData["ErrorMessage"] = "Khoảng ngày này đã có đơn đặt phòng hoặc lịch khác. Vui lòng chọn ngày khác.";
            }
            else
            {
                db.RoomBlocks.Add(new RoomBlock
                {
                    RoomId = roomId,
                    StartDate = start,
                    EndDate = end,
                    BlockType = blockType,
                    Note = string.IsNullOrEmpty(note) ? null : note,
                    CreatedAt = DateTime.Now
                });
                db.SaveChanges();
                TempData["SuccessMessage"] = "Đã thêm lịch \"" + blockType + "\" cho phòng " + room.RoomNumber
                    + " từ " + start.ToString("dd/MM/yyyy") + " đến " + end.ToString("dd/MM/yyyy") + ".";
            }

            return RedirectToAction("RoomSchedule", new { id = roomId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult DeleteBlock(int id)
        {
            var block = db.RoomBlocks.Include(k => k.Room).FirstOrDefault(k => k.BlockId == id);
            if (block == null || !myHotelIds.Contains(block.Room.HotelId))
                return HttpNotFound();

            int roomId = block.RoomId;
            db.RoomBlocks.Remove(block);
            db.SaveChanges();

            TempData["SuccessMessage"] = "Đã xóa lịch - các ngày đó của phòng được mở lại cho khách đặt.";
            return RedirectToAction("RoomSchedule", new { id = roomId });
        }

        // ----------------------------------------------------------------------
        private ActionResult RedirectBack(string returnUrl, string fallbackAction)
        {
            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
                return Redirect(returnUrl);
            return RedirectToAction(fallbackAction);
        }
    }
}
