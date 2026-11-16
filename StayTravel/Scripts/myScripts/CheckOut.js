// Trang Xác nhận đặt phòng: lịch chọn ngày (khóa các ngày đã có người đặt) + kiểm tra dữ liệu phía trình duyệt.
// Việc kiểm tra thật sự vẫn nằm ở server (HotelsController.ProcessBooking).
(function () {
    var ranges = [];
    var jsonInput = document.getElementById("bookedRangesJson");
    if (jsonInput) {
        try { ranges = JSON.parse(jsonInput.value || "[]"); }
        catch (error) { console.error("Không đọc được lịch đặt phòng:", error); }
    }

    var checkInInput = document.getElementById("checkIn");
    var checkOutInput = document.getElementById("checkOut");
    var dateError = document.getElementById("dateError");
    var bookingForm = document.getElementById("bookingForm");
    var calendarEl = document.getElementById("roomCalendar");
    var summary = document.getElementById("selectionSummary");
    var bookedList = document.getElementById("bookedList");
    if (!checkInInput || !checkOutInput || !bookingForm || !calendarEl) return;

    var pricePerNight = parseFloat(bookingForm.getAttribute("data-price") || "0");
    var maxNights = parseInt(bookingForm.getAttribute("data-max-nights") || "30", 10);

    // Danh sách khoảng đã đặt dạng chữ
    if (bookedList) {
        if (ranges.length === 0) {
            bookedList.textContent = "Chưa có lịch đặt phòng - phòng đang trống hoàn toàn.";
        } else {
            ranges.forEach(function (r) {
                var p = document.createElement("div");
                p.textContent = "• " + RoomCalendar.viDate(r.start) + " → " + RoomCalendar.viDate(r.end) +
                    " (" + (r.kind === "blocked" ? "không nhận đặt" : "đã có người đặt") + ")";
                bookedList.appendChild(p);
            });
        }
    }

    function formatMoney(n) {
        return String(Math.round(n)).replace(/\B(?=(\d{3})+(?!\d))/g, ".") + " VNĐ";
    }

    function showError(message) {
        if (!dateError) return;
        dateError.textContent = message;
        dateError.style.display = message ? "block" : "none";
    }

    function updateSummary(ci, co) {
        checkInInput.value = ci || "";
        checkOutInput.value = co || "";
        showError("");
        if (!summary) return;
        if (ci && co) {
            var nights = RoomCalendar.nightsBetween(ci, co);
            summary.innerHTML = "Nhận phòng: <strong>" + RoomCalendar.viDate(ci) + "</strong> &nbsp;→&nbsp; " +
                "Trả phòng: <strong>" + RoomCalendar.viDate(co) + "</strong><br />" +
                nights + " đêm × " + formatMoney(pricePerNight) + " = <strong class=\"text-danger\">" +
                formatMoney(nights * pricePerNight) + "</strong>";
            summary.className = "alert alert-success small";
        } else if (ci) {
            summary.innerHTML = "Nhận phòng: <strong>" + RoomCalendar.viDate(ci) + "</strong>. Hãy chọn <strong>ngày trả phòng</strong>.";
            summary.className = "alert alert-warning small";
        } else {
            summary.textContent = "Hãy chọn ngày nhận phòng trên lịch.";
            summary.className = "alert alert-secondary small";
        }
    }

    var cal = new RoomCalendar(calendarEl, {
        ranges: ranges,
        selectable: true,
        months: 2,
        maxNights: maxNights,
        initialCheckIn: checkInInput.value,
        initialCheckOut: checkOutInput.value,
        onChange: updateSummary
    });
    updateSummary(cal.checkIn, cal.checkOut);

    var resetBtn = document.getElementById("resetDates");
    if (resetBtn) resetBtn.addEventListener("click", function () { cal.clear(); });

    // Chặn gửi form nếu chưa chọn đủ ngày
    bookingForm.addEventListener("submit", function (event) {
        if (!checkInInput.value || !checkOutInput.value) {
            event.preventDefault();
            showError("Vui lòng chọn ngày nhận phòng và ngày trả phòng trên lịch.");
            dateError.scrollIntoView({ behavior: "smooth", block: "center" });
        }
    });
})();
