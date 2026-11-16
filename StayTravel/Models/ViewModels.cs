using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
namespace StayTravel.Models
{
    // ViewModel cho bộ lọc tìm kiếm tại trang Đặt phòng
    public class RoomFilterViewModel
    {
        public string Keyword { get; set; }
        public int? LocationId { get; set; }
        public int? HotelId { get; set; }
        public int? RoomTypeId { get; set; }
        public decimal? MinPrice { get; set; }
        public decimal? MaxPrice { get; set; }
        public List<Room> Rooms { get; set; }
        public List<Location> Locations { get; set; }
        public List<Hotel> Hotels { get; set; }
        public List<RoomType> RoomTypes { get; set; }

        // RoomId -> lý do phòng bị xám (kín lịch / tạm ngừng nhận đặt)
        public Dictionary<int, string> UnavailableRooms { get; set; } = new Dictionary<int, string>();
    }

    // ViewModel cho trang Thông tin tài khoản
    public class UserProfileViewModel
    {
        public User User { get; set; }
        public List<Booking> BookingHistory { get; set; }
        public List<Wishlist> Wishlists { get; set; }
    }

    // ================= PARTNER =================
    public class PartnerDashboardViewModel
    {
        public string BusinessName { get; set; }
        public List<Hotel> Hotels { get; set; }
        public int TotalRooms { get; set; }
        public int MaintenanceRooms { get; set; }
        public int PendingCount { get; set; }
        public List<Booking> ArrivalsToday { get; set; }
        public List<Booking> InHouse { get; set; }
        public List<Booking> DeparturesToday { get; set; }
        public List<Booking> Overdue { get; set; }
    }

    public class PartnerRoomRow
    {
        public Room Room { get; set; }
        // free | reserved | occupied | blocked | maintenance
        public string StateKey { get; set; }
        public string StateText { get; set; }
        public string StateBadge { get; set; }
        public string Detail { get; set; }
    }

    public class RoomScheduleViewModel
    {
        public Room Room { get; set; }
        public List<Booking> Bookings { get; set; }
        public List<RoomBlock> Blocks { get; set; }
        public string RangesJson { get; set; }
        public List<BusyRange> FreeRanges { get; set; }
    }

}
