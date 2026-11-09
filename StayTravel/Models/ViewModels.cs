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
    }

    // ViewModel cho trang Thông tin tài khoản
    public class UserProfileViewModel
    {
        public User User { get; set; }
        public List<Booking> BookingHistory { get; set; }
        public List<Wishlist> Wishlists { get; set; }
    }

}