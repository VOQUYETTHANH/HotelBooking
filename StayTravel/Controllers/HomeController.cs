using System.Linq;
using System.Web.Mvc;
using StayTravel.Models;

namespace StayTravel.Controllers
{
    public class HomeController : Controller
    {
        private HotelBookingDBEntities db = new HotelBookingDBEntities();

        public ActionResult Index()
        {

            ViewBag.Locations = db.Locations.Where(l => l.IsFeatured == true).ToList();
            ViewBag.FeaturedHotels = db.Hotels.Where(h => h.IsFeatured == true).Take(6).ToList();
            return View();
        }

        public ActionResult About() => View();
        public ActionResult Contact() => View();
        public ActionResult Privacy() => View();
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Contact(string fullName, string email, string phone, string subject, string message)
        {
            if (ModelState.IsValid)
            {
                // Tại đây bạn có thể lưu vào DB hoặc gửi Email phản hồi
                ViewBag.Success = "Cảm ơn bạn đã liên hệ! Chúng tôi sẽ phản hồi trong thời gian sớm nhất (tối đa 24h).";
                return View();
            }
            ViewBag.Error = "Vui lòng kiểm tra lại thông tin nhập vào!";
            return View();
        }
    }
}