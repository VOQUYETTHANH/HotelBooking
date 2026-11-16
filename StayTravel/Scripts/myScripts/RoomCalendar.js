/*
 * RoomCalendar - lịch phòng bằng JS thuần (không dùng thư viện).
 *
 * Quy ước: ngày được lưu dạng chuỗi "yyyy-MM-dd". Một khoảng {start, end} chiếm các ĐÊM
 * start <= đêm < end (ngày end là ngày trả phòng nên vẫn có thể là ngày nhận phòng của người khác).
 *
 * Dùng:
 *   var cal = new RoomCalendar(document.getElementById("roomCalendar"), {
 *       ranges: [{start:"2026-11-20", end:"2026-11-23", kind:"booked", label:"..."}],
 *       selectable: true,        // true: chọn ngày đặt phòng; false: chỉ xem
 *       months: 2,               // số tháng hiển thị cạnh nhau
 *       maxNights: 30,
 *       initialCheckIn: "...", initialCheckOut: "...",
 *       onChange: function (checkIn, checkOut) { }
 *   });
 */
(function (global) {
    "use strict";

    var MONTH_NAMES = ["Tháng 1", "Tháng 2", "Tháng 3", "Tháng 4", "Tháng 5", "Tháng 6",
        "Tháng 7", "Tháng 8", "Tháng 9", "Tháng 10", "Tháng 11", "Tháng 12"];
    var DAY_NAMES = ["T2", "T3", "T4", "T5", "T6", "T7", "CN"];

    function pad(n) { return n < 10 ? "0" + n : "" + n; }
    function fmt(d) { return d.getFullYear() + "-" + pad(d.getMonth() + 1) + "-" + pad(d.getDate()); }
    function parse(s) {
        var p = s.split("-");
        return new Date(parseInt(p[0], 10), parseInt(p[1], 10) - 1, parseInt(p[2], 10));
    }
    function addDays(d, n) { return new Date(d.getFullYear(), d.getMonth(), d.getDate() + n); }
    function today() { var t = new Date(); return new Date(t.getFullYear(), t.getMonth(), t.getDate()); }
    function nightsBetween(a, b) { return Math.round((parse(b) - parse(a)) / 86400000); }
    function viDate(s) { var p = s.split("-"); return p[2] + "/" + p[1] + "/" + p[0]; }

    function injectStyle() {
        if (document.getElementById("rc-style")) return;
        var css = [
            ".rc{font-family:inherit;user-select:none}",
            ".rc-months{display:flex;flex-wrap:wrap;gap:20px}",
            ".rc-month{flex:1 1 240px;min-width:240px}",
            ".rc-head{display:flex;align-items:center;justify-content:space-between;margin-bottom:6px;font-weight:700}",
            ".rc-nav{border:1px solid #ced4da;background:#fff;border-radius:6px;width:30px;height:30px;line-height:1;cursor:pointer}",
            ".rc-nav:disabled{opacity:.35;cursor:not-allowed}",
            ".rc-grid{display:grid;grid-template-columns:repeat(7,1fr);gap:3px}",
            ".rc-dow{text-align:center;font-size:12px;color:#6c757d;padding:2px 0}",
            ".rc-day{height:36px;border:1px solid transparent;border-radius:6px;text-align:center;line-height:34px;font-size:14px;background:#fff}",
            ".rc-empty{visibility:hidden}",
            ".rc-past{color:#bbb;background:#f5f5f5;text-decoration:line-through}",
            ".rc-booked{background:#f8d7da;color:#842029;border-color:#f1aeb5;cursor:not-allowed}",
            ".rc-direct{background:#ffe5b4;color:#7a4b00;border-color:#ffd08a;cursor:not-allowed}",
            ".rc-blocked{background:#d6d8db;color:#495057;border-color:#bfc3c7;cursor:not-allowed}",
            ".rc-free{background:#e7f6ec;color:#146c43;border-color:#b6e2c5}",
            ".rc-unreach{background:#f1f1f1;color:#b5b5b5;cursor:not-allowed}",
            ".rc-selectable{cursor:pointer}",
            ".rc-selectable:hover{outline:2px solid #ffc107}",
            ".rc-start,.rc-end{background:#ffc107!important;color:#000!important;font-weight:700;border-color:#e0a800!important}",
            ".rc-range{background:#fff3cd!important;color:#664d03!important}",
            ".rc-coday{box-shadow:inset 0 0 0 2px #198754}",
            ".rc-legend{display:flex;flex-wrap:wrap;gap:14px;margin-top:10px;font-size:13px}",
            ".rc-legend span i{display:inline-block;width:14px;height:14px;border-radius:3px;margin-right:5px;vertical-align:-2px;border:1px solid #bbb}",
            ".rc-msg{margin-top:8px;font-size:13px;color:#b02a37;min-height:18px}"
        ].join("\n");
        var el = document.createElement("style");
        el.id = "rc-style";
        el.appendChild(document.createTextNode(css));
        document.head.appendChild(el);
    }

    function RoomCalendar(container, options) {
        injectStyle();
        var self = this;
        options = options || {};
        this.el = container;
        this.selectable = !!options.selectable;
        this.monthsToShow = options.months || 2;
        this.maxNights = options.maxNights || 30;
        this.onChange = options.onChange || function () { };
        this.today = today();
        this.todayStr = fmt(this.today);
        this.ranges = options.ranges || [];

        // busy[ngày] = kind của đêm đó
        this.busy = {};
        this.ranges.forEach(function (r) {
            var d = parse(r.start), end = parse(r.end);
            while (d < end) {
                self.busy[fmt(d)] = { kind: r.kind || "booked", label: r.label || "" };
                d = addDays(d, 1);
            }
        });

        this.checkIn = null;
        this.checkOut = null;
        if (this.selectable && options.initialCheckIn && options.initialCheckOut
            && this.isRangeFree(options.initialCheckIn, options.initialCheckOut)
            && options.initialCheckIn >= this.todayStr) {
            this.checkIn = options.initialCheckIn;
            this.checkOut = options.initialCheckOut;
        }

        var first = this.checkIn ? parse(this.checkIn) : this.today;
        this.viewYear = first.getFullYear();
        this.viewMonth = first.getMonth();

        this.render();
    }

    // đêm d có trống không (và không ở quá khứ)
    RoomCalendar.prototype.isNightFree = function (s) {
        return !this.busy[s] && s >= this.todayStr;
    };

    // tất cả các đêm trong [a, b) đều trống?
    RoomCalendar.prototype.isRangeFree = function (a, b) {
        var d = parse(a), end = parse(b);
        if (end <= d) return false;
        while (d < end) {
            if (!this.isNightFree(fmt(d))) return false;
            d = addDays(d, 1);
        }
        return true;
    };

    // Sau khi đã chọn check-in: ngày trả phòng xa nhất được phép
    RoomCalendar.prototype.maxCheckOut = function () {
        if (!this.checkIn) return null;
        var limit = fmt(addDays(parse(this.checkIn), this.maxNights));
        var d = addDays(parse(this.checkIn), 1);
        var guard = 0;
        // đi tới đêm bận đầu tiên: chính ngày đó vẫn được chọn làm ngày trả phòng
        var start = parse(this.checkIn);
        var cur = start;
        while (guard < 400) {
            var s = fmt(cur);
            if (this.busy[s]) return s < limit ? s : limit;
            if (s >= limit) return limit;
            cur = addDays(cur, 1);
            guard++;
        }
        return limit;
    };

    RoomCalendar.prototype.setMessage = function (text) {
        var m = this.el.querySelector(".rc-msg");
        if (m) m.textContent = text || "";
    };

    RoomCalendar.prototype.clear = function () {
        this.checkIn = null;
        this.checkOut = null;
        this.render();
        this.onChange(null, null);
    };

    RoomCalendar.prototype.handleClick = function (s) {
        if (!this.selectable) return;
        var maxOut;

        // Bắt đầu chọn lại từ đầu
        if (!this.checkIn || this.checkOut) {
            if (!this.isNightFree(s)) return;
            this.checkIn = s;
            this.checkOut = null;
            this.render();
            this.setMessage("");
            this.onChange(this.checkIn, null);
            return;
        }

        // Đã có check-in, đang chọn check-out
        if (s <= this.checkIn) {
            if (!this.isNightFree(s)) return;
            this.checkIn = s;
            this.render();
            this.onChange(this.checkIn, null);
            return;
        }

        maxOut = this.maxCheckOut();
        if (s > maxOut) {
            this.setMessage("Khoảng ngày này vượt quá ngày đã có người đặt hoặc quá " + this.maxNights + " đêm. Vui lòng chọn ngày trả phòng sớm hơn.");
            return;
        }
        this.checkOut = s;
        this.render();
        this.setMessage("");
        this.onChange(this.checkIn, this.checkOut);
    };

    RoomCalendar.prototype.changeMonth = function (delta) {
        var m = this.viewMonth + delta, y = this.viewYear;
        while (m < 0) { m += 12; y--; }
        while (m > 11) { m -= 12; y++; }
        this.viewMonth = m;
        this.viewYear = y;
        this.render();
    };

    RoomCalendar.prototype.render = function () {
        var self = this;
        var maxOut = (this.selectable && this.checkIn && !this.checkOut) ? this.maxCheckOut() : null;
        var root = document.createElement("div");
        root.className = "rc";

        var wrap = document.createElement("div");
        wrap.className = "rc-months";
        root.appendChild(wrap);

        var minMonthIdx = this.today.getFullYear() * 12 + this.today.getMonth();

        for (var mi = 0; mi < this.monthsToShow; mi++) {
            var y = this.viewYear, m = this.viewMonth + mi;
            while (m > 11) { m -= 12; y++; }

            var box = document.createElement("div");
            box.className = "rc-month";

            var head = document.createElement("div");
            head.className = "rc-head";

            var prev = document.createElement("button");
            prev.type = "button"; prev.className = "rc-nav"; prev.innerHTML = "&lsaquo;";
            prev.disabled = (this.viewYear * 12 + this.viewMonth) <= minMonthIdx;
            prev.style.visibility = mi === 0 ? "visible" : "hidden";
            prev.addEventListener("click", function () { self.changeMonth(-1); });

            var next = document.createElement("button");
            next.type = "button"; next.className = "rc-nav"; next.innerHTML = "&rsaquo;";
            next.style.visibility = mi === this.monthsToShow - 1 ? "visible" : "hidden";
            next.addEventListener("click", function () { self.changeMonth(1); });

            var title = document.createElement("span");
            title.textContent = MONTH_NAMES[m] + " " + y;

            head.appendChild(prev); head.appendChild(title); head.appendChild(next);
            box.appendChild(head);

            var grid = document.createElement("div");
            grid.className = "rc-grid";
            DAY_NAMES.forEach(function (n) {
                var c = document.createElement("div");
                c.className = "rc-dow"; c.textContent = n; grid.appendChild(c);
            });

            var firstDay = new Date(y, m, 1);
            var offset = (firstDay.getDay() + 6) % 7; // thứ 2 = 0
            for (var e = 0; e < offset; e++) {
                var empty = document.createElement("div");
                empty.className = "rc-day rc-empty"; grid.appendChild(empty);
            }

            var daysInMonth = new Date(y, m + 1, 0).getDate();
            for (var day = 1; day <= daysInMonth; day++) {
                var d = new Date(y, m, day);
                var s = fmt(d);
                var cell = document.createElement("div");
                cell.className = "rc-day";
                cell.textContent = day;
                cell.setAttribute("data-date", s);

                var isPast = s < this.todayStr;
                var busyInfo = this.busy[s];

                if (isPast) {
                    cell.className += " rc-past";
                } else if (busyInfo) {
                    cell.className += " rc-" + busyInfo.kind;
                    cell.title = busyInfo.label || "Đã có người đặt";
                } else {
                    cell.className += " rc-free";
                }

                if (this.selectable) {
                    var clickable = false;

                    if (!this.checkIn || this.checkOut) {
                        clickable = !isPast && !busyInfo;
                    } else if (s <= this.checkIn) {
                        clickable = !isPast && !busyInfo;
                    } else {
                        // đang chọn ngày trả phòng
                        clickable = s <= maxOut;
                        if (!clickable) cell.className += " rc-unreach";
                        // ngày bận đầu tiên vẫn chọn làm ngày trả phòng được
                        if (clickable && busyInfo) {
                            cell.className += " rc-coday";
                            cell.title = "Có thể chọn làm ngày trả phòng";
                        }
                    }

                    if (clickable) cell.className += " rc-selectable";

                    if (this.checkIn && s === this.checkIn) cell.className += " rc-start";
                    if (this.checkOut && s === this.checkOut) cell.className += " rc-end";
                    if (this.checkIn && this.checkOut && s > this.checkIn && s < this.checkOut) cell.className += " rc-range";

                    if (clickable) {
                        (function (str) {
                            cell.addEventListener("click", function () { self.handleClick(str); });
                        })(s);
                    }
                }

                grid.appendChild(cell);
            }

            box.appendChild(grid);
            wrap.appendChild(box);
        }

        var legend = document.createElement("div");
        legend.className = "rc-legend";
        var items = [
            ["#e7f6ec", "Còn trống"],
            ["#f8d7da", "Đã có người đặt"]
        ];
        if (this.hasKind("direct")) items.push(["#ffe5b4", "Đặt trực tiếp"]);
        if (this.hasKind("blocked")) items.push(["#d6d8db", "Không nhận đặt / bảo trì"]);
        if (this.selectable) items.push(["#ffc107", "Ngày bạn chọn"]);
        items.forEach(function (it) {
            var sp = document.createElement("span");
            sp.innerHTML = '<i style="background:' + it[0] + '"></i>' + it[1];
            legend.appendChild(sp);
        });
        root.appendChild(legend);

        var msg = document.createElement("div");
        msg.className = "rc-msg";
        root.appendChild(msg);

        this.el.innerHTML = "";
        this.el.appendChild(root);
    };

    RoomCalendar.prototype.hasKind = function (kind) {
        for (var i = 0; i < this.ranges.length; i++) {
            if (this.ranges[i].kind === kind) return true;
        }
        return false;
    };

    RoomCalendar.nightsBetween = nightsBetween;
    RoomCalendar.viDate = viDate;
    global.RoomCalendar = RoomCalendar;

})(window);
