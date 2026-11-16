using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Script.Serialization;

namespace StayTravel.Models
{
    // Một khoảng thời gian của phòng (tính theo ĐÊM: Start <= đêm < End).
    // Kind: "booked" (đã có người đặt), "direct" (đặt trực tiếp - chỉ Partner thấy),
    //       "blocked" (bảo trì / không nhận đặt), "free" (còn trống)
    public class BusyRange
    {
        public DateTime Start { get; set; }
        public DateTime End { get; set; }
        public string Kind { get; set; }
        public string Label { get; set; }
    }

    public static class RoomAvailability
    {
        // Số ngày tới được xét để quyết định "phòng đã kín lịch" (phòng bị xám ở trang Đặt phòng)
        public const int FullCheckDays = 30;
        // Số đêm tối đa cho 1 lần đặt
        public const int MaxNights = 30;

        public const string BlockDirect = "Đặt trực tiếp";
        public const string BlockMaintenance = "Bảo trì";
        public static readonly string[] BlockTypes = { BlockDirect, BlockMaintenance };

        // Đơn đã hủy / đã trả phòng thì không còn giữ phòng
        public static bool IsInactiveStatus(string status)
        {
            return status == "Đã hủy" || status == "Đã trả phòng";
        }

        // ---------- Các khoảng đã bận của 1 phòng (từ hôm nay trở đi) ----------
        public static List<BusyRange> GetBusyRanges(HotelBookingDBEntities db, int roomId, bool forPublic)
        {
            DateTime today = DateTime.Today;
            var result = new List<BusyRange>();

            var bookings = db.Bookings
                .Where(b => b.RoomId == roomId
                            && b.Status != "Đã hủy" && b.Status != "Đã trả phòng"
                            && b.CheckOutDate > today)
                .ToList();

            foreach (var b in bookings)
            {
                result.Add(new BusyRange
                {
                    Start = b.CheckInDate.Date,
                    End = b.CheckOutDate.Date,
                    Kind = "booked",
                    Label = "Đã có người đặt"
                });
            }

            var blocks = db.RoomBlocks.Where(k => k.RoomId == roomId && k.EndDate > today).ToList();
            foreach (var k in blocks)
            {
                bool direct = k.BlockType == BlockDirect;
                result.Add(new BusyRange
                {
                    Start = k.StartDate.Date,
                    End = k.EndDate.Date,
                    // Khách ngoài chỉ thấy "đã đặt" hoặc "không nhận đặt", không thấy ghi chú nội bộ
                    Kind = direct ? (forPublic ? "booked" : "direct") : "blocked",
                    Label = forPublic
                        ? (direct ? "Đã có người đặt" : "Phòng không nhận đặt")
                        : (direct ? "Đặt trực tiếp" : "Bảo trì") + (string.IsNullOrWhiteSpace(k.Note) ? "" : ": " + k.Note)
                });
            }

            return result.OrderBy(r => r.Start).ToList();
        }

        // ---------- Kiểm tra trùng lịch (đơn đặt + lịch do khách sạn sắp xếp) ----------
        public static bool HasOverlap(HotelBookingDBEntities db, int roomId, DateTime start, DateTime end, int excludeBookingId = 0)
        {
            start = start.Date;
            end = end.Date;

            bool bookingOverlap = db.Bookings.Any(b =>
                b.RoomId == roomId
                && b.BookingId != excludeBookingId
                && b.Status != "Đã hủy" && b.Status != "Đã trả phòng"
                && start < b.CheckOutDate && end > b.CheckInDate);
            if (bookingOverlap) return true;

            return db.RoomBlocks.Any(k => k.RoomId == roomId && start < k.EndDate && end > k.StartDate);
        }

        // ---------- Phòng nào "kín lịch" trong FullCheckDays ngày tới ----------
        public static HashSet<int> GetFullRoomIds(HotelBookingDBEntities db, IEnumerable<int> roomIds)
        {
            var full = new HashSet<int>();
            var ids = roomIds.Distinct().ToList();
            if (ids.Count == 0) return full;

            DateTime from = DateTime.Today;
            DateTime to = from.AddDays(FullCheckDays);

            var bookings = db.Bookings
                .Where(b => ids.Contains(b.RoomId)
                            && b.Status != "Đã hủy" && b.Status != "Đã trả phòng"
                            && b.CheckInDate < to && b.CheckOutDate > from)
                .Select(b => new { b.RoomId, Start = b.CheckInDate, End = b.CheckOutDate })
                .ToList();

            var blocks = db.RoomBlocks
                .Where(k => ids.Contains(k.RoomId) && k.StartDate < to && k.EndDate > from)
                .Select(k => new { k.RoomId, Start = k.StartDate, End = k.EndDate })
                .ToList();

            var all = bookings.Concat(blocks).ToList();

            foreach (int id in ids)
            {
                var busy = new bool[FullCheckDays];
                foreach (var r in all.Where(x => x.RoomId == id))
                {
                    for (int i = 0; i < FullCheckDays; i++)
                    {
                        DateTime night = from.AddDays(i);
                        if (night >= r.Start.Date && night < r.End.Date) busy[i] = true;
                    }
                }
                if (busy.All(x => x)) full.Add(id);
            }
            return full;
        }

        // ---------- Các khoảng còn trống trong N ngày tới ----------
        public static List<BusyRange> GetFreeRanges(List<BusyRange> busy, int days)
        {
            DateTime from = DateTime.Today;
            DateTime to = from.AddDays(days);
            var free = new List<BusyRange>();

            DateTime cursor = from;
            foreach (var r in busy.OrderBy(x => x.Start))
            {
                if (r.End <= cursor) continue;
                if (r.Start >= to) break;
                if (r.Start > cursor)
                    free.Add(new BusyRange { Start = cursor, End = r.Start, Kind = "free", Label = "Còn trống" });
                if (r.End > cursor) cursor = r.End;
            }
            if (cursor < to)
                free.Add(new BusyRange { Start = cursor, End = to, Kind = "free", Label = "Còn trống" });
            return free;
        }

        // ---------- JSON cho JS (ngày dạng yyyy-MM-dd) ----------
        public static string ToJson(IEnumerable<BusyRange> ranges)
        {
            var list = ranges.Select(r => new
            {
                start = r.Start.ToString("yyyy-MM-dd"),
                end = r.End.ToString("yyyy-MM-dd"),
                kind = r.Kind,
                label = r.Label
            }).ToList();
            return new JavaScriptSerializer().Serialize(list);
        }
    }

    // Quy tắc đổi trạng thái đơn đặt phòng - dùng chung cho Admin và Partner
    public static class BookingRules
    {
        public static readonly string[] AllStatuses =
        {
            "Chờ duyệt", "Đã duyệt", "Đã nhận phòng", "Đã thanh toán", "Đã trả phòng", "Đã hủy"
        };

        public static readonly string[] CancelReasons = { "Khách hủy", "No-show", "Quá hạn" };

        // Trả về null nếu đổi thành công, ngược lại trả về thông báo lỗi.
        // (Chưa SaveChanges - nơi gọi tự lưu.)
        public static string TryChangeStatus(HotelBookingDBEntities db, Booking booking, string newStatus, string cancelReason)
        {
            if (string.IsNullOrWhiteSpace(newStatus) || !AllStatuses.Contains(newStatus))
                return "Trạng thái được chọn không hợp lệ.";

            if (newStatus == "Đã hủy" && (string.IsNullOrWhiteSpace(cancelReason) || !CancelReasons.Contains(cancelReason)))
                return "Vui lòng chọn lý do hủy (Khách hủy / No-show / Quá hạn).";

            if (newStatus == "Đã nhận phòng" && booking.CheckInDate.Date > DateTime.Today)
                return "Chưa đến ngày nhận phòng (" + booking.CheckInDate.ToString("dd/MM/yyyy") + ") nên chưa thể chuyển sang \"Đã nhận phòng\".";

            // Khôi phục đơn đã hủy / đã trả phòng -> phải kiểm tra phòng còn trống không
            bool wasInactive = RoomAvailability.IsInactiveStatus(booking.Status);
            bool willBeActive = !RoomAvailability.IsInactiveStatus(newStatus);
            if (wasInactive && willBeActive
                && RoomAvailability.HasOverlap(db, booking.RoomId, booking.CheckInDate, booking.CheckOutDate, booking.BookingId))
                return "Không thể khôi phục đơn: khoảng ngày này phòng đã có đơn hoặc lịch khác.";

            booking.Status = newStatus;
            booking.CancelReason = newStatus == "Đã hủy" ? cancelReason : null;
            return null;
        }
    }
}
