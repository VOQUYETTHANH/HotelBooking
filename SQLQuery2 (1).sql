
-- =====================================================
-- DATABASE: STAYTRAVELDB
-- WEBSITE: QUAN LY DAT PHONG KHACH SAN
-- =====================================================

 CREATE DATABASE HotelBookingDB;
 GO
 USE HotelBookingDB;
 GO


-- =====================================================
-- 1. TAO CAC BANG DU LIEU
-- =====================================================

-- 1.1. BANG VAI TRO
CREATE TABLE Roles
(
    RoleId INT IDENTITY(1,1) PRIMARY KEY,
    RoleName NVARCHAR(50) NOT NULL UNIQUE
);
GO


-- 1.2. BANG NGUOI DUNG
CREATE TABLE Users
(
    UserId INT IDENTITY(1,1) PRIMARY KEY,
    FullName NVARCHAR(100) NOT NULL,
    Email VARCHAR(100) NOT NULL UNIQUE,
    PasswordHash VARCHAR(255) NOT NULL,
    PhoneNumber VARCHAR(20),

    Avatar NVARCHAR(255)
        DEFAULT N'/Images/default-avatar.png',

    RoleId INT NOT NULL,

    CreatedAt DATETIME
        DEFAULT GETDATE(),

    CONSTRAINT FK_Users_Roles
        FOREIGN KEY (RoleId)
        REFERENCES Roles(RoleId)
);
GO


-- 1.3. BANG DIA DIEM DU LICH
CREATE TABLE Locations
(
    LocationId INT IDENTITY(1,1) PRIMARY KEY,
    LocationName NVARCHAR(100) NOT NULL,
    Aliases NVARCHAR(255),
    Description NVARCHAR(MAX),
    ImageUrl NVARCHAR(255),

    IsFeatured BIT
        DEFAULT 1
);
GO


-- 1.4. BANG KHACH SAN
CREATE TABLE Hotels
(
    HotelId INT IDENTITY(1,1) PRIMARY KEY,

    LocationId INT NOT NULL,

    HotelName NVARCHAR(150) NOT NULL,
    Address NVARCHAR(255) NOT NULL,
    Description NVARCHAR(MAX),

    StarRating INT DEFAULT 5,

    ImageUrl NVARCHAR(255),

    IsFeatured BIT DEFAULT 1,

    CONSTRAINT FK_Hotels_Locations
        FOREIGN KEY (LocationId)
        REFERENCES Locations(LocationId),

    CONSTRAINT CK_Hotels_StarRating
        CHECK (StarRating BETWEEN 1 AND 5)
);
GO


-- 1.5. BANG LOAI PHONG
CREATE TABLE RoomTypes
(
    RoomTypeId INT IDENTITY(1,1) PRIMARY KEY,

    TypeName NVARCHAR(50) NOT NULL UNIQUE
);
GO


-- 1.6. BANG PHONG KHACH SAN
CREATE TABLE Rooms
(
    RoomId INT IDENTITY(1,1) PRIMARY KEY,

    HotelId INT NOT NULL,
    RoomTypeId INT NOT NULL,

    RoomNumber NVARCHAR(20) NOT NULL,

    Price DECIMAL(18,2) NOT NULL,

    Capacity INT DEFAULT 2,

    Amenities NVARCHAR(MAX),

    OutsideView NVARCHAR(255),

    ImageUrl NVARCHAR(255),

    IsAvailable BIT DEFAULT 1,

    CONSTRAINT FK_Rooms_Hotels
        FOREIGN KEY (HotelId)
        REFERENCES Hotels(HotelId),

    CONSTRAINT FK_Rooms_RoomTypes
        FOREIGN KEY (RoomTypeId)
        REFERENCES RoomTypes(RoomTypeId),

    CONSTRAINT CK_Rooms_Price
        CHECK (Price >= 0),

    CONSTRAINT CK_Rooms_Capacity
        CHECK (Capacity >= 1),

    CONSTRAINT UQ_Rooms_Hotel_RoomNumber
        UNIQUE (HotelId, RoomNumber)
);
GO


-- 1.7. BANG DANH SACH YEU THICH
CREATE TABLE Wishlists
(
    WishlistId INT IDENTITY(1,1) PRIMARY KEY,

    UserId INT NOT NULL,
    HotelId INT NOT NULL,

    CreatedAt DATETIME DEFAULT GETDATE(),

    CONSTRAINT FK_Wishlists_Users
        FOREIGN KEY (UserId)
        REFERENCES Users(UserId),

    CONSTRAINT FK_Wishlists_Hotels
        FOREIGN KEY (HotelId)
        REFERENCES Hotels(HotelId),

    CONSTRAINT UQ_Wishlists_User_Hotel
        UNIQUE (UserId, HotelId)
);
GO


-- 1.8. BANG DANH GIA KHACH SAN
CREATE TABLE Reviews
(
    ReviewId INT IDENTITY(1,1) PRIMARY KEY,

    HotelId INT NOT NULL,
    UserId INT NOT NULL,

    Rating INT NOT NULL,

    Comment NVARCHAR(MAX),

    CreatedAt DATETIME DEFAULT GETDATE(),

    CONSTRAINT FK_Reviews_Hotels
        FOREIGN KEY (HotelId)
        REFERENCES Hotels(HotelId),

    CONSTRAINT FK_Reviews_Users
        FOREIGN KEY (UserId)
        REFERENCES Users(UserId),

    CONSTRAINT CK_Reviews_Rating
        CHECK (Rating BETWEEN 1 AND 5),

    CONSTRAINT UQ_Reviews_User_Hotel
        UNIQUE (UserId, HotelId)
);
GO


-- 1.9. BANG DON DAT PHONG
CREATE TABLE Bookings
(
    BookingId INT IDENTITY(1,1) PRIMARY KEY,

    UserId INT NOT NULL,
    RoomId INT NOT NULL,

    CheckInDate DATETIME NOT NULL,
    CheckOutDate DATETIME NOT NULL,

    TotalPrice DECIMAL(18,2) NOT NULL,

    Status NVARCHAR(50)
        DEFAULT N'Chờ duyệt',

    PaymentMethod NVARCHAR(50)
        DEFAULT N'Tiền mặt',

    CreatedAt DATETIME DEFAULT GETDATE(),

    CONSTRAINT FK_Bookings_Users
        FOREIGN KEY (UserId)
        REFERENCES Users(UserId),

    CONSTRAINT FK_Bookings_Rooms
        FOREIGN KEY (RoomId)
        REFERENCES Rooms(RoomId),

    CONSTRAINT CK_Bookings_Dates
        CHECK (CheckOutDate > CheckInDate),

    CONSTRAINT CK_Bookings_TotalPrice
        CHECK (TotalPrice >= 0)
);
GO


-- =====================================================
-- 2. THEM DU LIEU MAU
-- =====================================================


-- 2.1. THEM VAI TRO
INSERT INTO Roles (RoleName)
VALUES
    (N'Admin'),
    (N'Customer');
GO


-- 2.2. THEM TAI KHOAN MAU
INSERT INTO Users
(
    FullName,
    Email,
    PasswordHash,
    PhoneNumber,
    Avatar,
    RoleId
)
VALUES
(
    N'Quản Trị Viên',
    'admin@staytravel.com',
    '123456',
    '0909123456',
    N'/Images/admin-avatar.png',
    1
),
(
    N'Nguyễn Văn Minh',
    'minh@gmail.com',
    '123456',
    '0988765432',
    N'/Images/default-avatar.png',
    2
);
GO


-- 2.3. THEM LOAI PHONG
INSERT INTO RoomTypes (TypeName)
VALUES
    (N'Phòng Đơn'),
    (N'Phòng Đôi'),
    (N'Phòng VIP'),
    (N'Căn hộ gia đình');
GO


-- 2.4. THEM DIA DIEM DU LICH
INSERT INTO Locations
(
    LocationName,
    Aliases,
    Description,
    ImageUrl
)
VALUES
(
    N'TP. Hồ Chí Minh',
    N'hcm, tp.hcm, ho chi minh, sai gon',
    N'Thành phố năng động và sầm uất bậc nhất',
    N'/Images/hcm.jpg'
),
(
    N'Đà Nẵng',
    N'dn, da nang, đà nẵng',
    N'Thành phố biển đáng sống',
    N'/Images/danang.jpg'
),
(
    N'Hà Nội',
    N'hn, ha noi, thủ đô',
    N'Thủ đô nghìn năm văn hiến với phố cổ trầm mặc',
    N'/Images/hanoi.jpg'
),
(
    N'Nha Trang',
    N'nt, nha trang, khanh hoa',
    N'Thành phố biển với vịnh biển đẹp bậc nhất thế giới',
    N'/Images/nhatrang.jpg'
),
(
    N'Đà Lạt',
    N'dl, da lat, đà lạt, tp ngàn hoa',
    N'Thành phố ngàn hoa mộng mơ và se lạnh',
    N'/Images/dalat.jpg'
);
GO


-- =====================================================
-- 2.5. THEM KHACH SAN
-- MOI DIA DIEM CO 3 KHACH SAN
-- =====================================================

DECLARE @HcmId INT;
DECLARE @DanangId INT;
DECLARE @HanoiId INT;
DECLARE @NhatrangId INT;
DECLARE @DalatId INT;

SELECT @HcmId = LocationId
FROM Locations
WHERE LocationName = N'TP. Hồ Chí Minh';

SELECT @DanangId = LocationId
FROM Locations
WHERE LocationName = N'Đà Nẵng';

SELECT @HanoiId = LocationId
FROM Locations
WHERE LocationName = N'Hà Nội';

SELECT @NhatrangId = LocationId
FROM Locations
WHERE LocationName = N'Nha Trang';

SELECT @DalatId = LocationId
FROM Locations
WHERE LocationName = N'Đà Lạt';


INSERT INTO Hotels
(
    LocationId,
    HotelName,
    Address,
    Description,
    StarRating,
    ImageUrl,
    IsFeatured
)
VALUES

-- TP. HO CHI MINH
(@HcmId, N'Rex Hotel Saigon',
 N'141 Nguyễn Huệ, Quận 1, TP. HCM',
 N'Khách sạn lịch sử ngay trung tâm phố đi bộ.',
 5, N'/Images/hcm-hotel1.jpg', 1),

(@HcmId, N'The Reverie Saigon',
 N'22-36 Nguyễn Huệ, Quận 1, TP. HCM',
 N'Khách sạn phong cách hoàng gia Ý xa hoa bậc nhất.',
 5, N'/Images/hcm-hotel2.jpg', 1),

(@HcmId, N'Hotel Des Arts Saigon',
 N'76-78 Nguyễn Thị Minh Khai, Quận 3, TP. HCM',
 N'Không gian nghệ thuật mang hơi hướng Đông Dương.',
 5, N'/Images/hcm-hotel3.jpg', 1),


-- DA NANG
(@DanangId, N'InterContinental Danang',
 N'Bãi Bắc, Thọ Quang, Sơn Trà, Đà Nẵng',
 N'Khu nghỉ dưỡng sang trọng bậc nhất bán đảo Sơn Trà.',
 5, N'/Images/danang-hotel1.jpg', 1),

(@DanangId, N'Novotel Danang Premier',
 N'36 Bạch Đằng, Hải Châu, Đà Nẵng',
 N'Khách sạn hiện đại bên bờ sông Hàn thơ mộng.',
 4, N'/Images/danang-hotel2.jpg', 1),

(@DanangId, N'Furama Resort Danang',
 N'105 Võ Nguyên Giáp, Ngũ Hành Sơn, Đà Nẵng',
 N'Khu nghỉ dưỡng 5 sao danh tiếng cửa ngõ di sản.',
 5, N'/Images/danang-hotel3.jpg', 1),


-- HA NOI
(@HanoiId, N'Capella Hanoi',
 N'11 Lê Phụng Hưng, Hoàn Kiếm, Hà Nội',
 N'Khách sạn nghệ thuật phong cách Đông Dương.',
 5, N'/Images/hanoi-hotel1.jpg', 1),

(@HanoiId, N'Sofitel Legend Metropole Hanoi',
 N'15 Ngô Quyền, Hoàn Kiếm, Hà Nội',
 N'Biểu tượng lịch sử sang trọng từ năm 1901.',
 5, N'/Images/hanoi-hotel2.jpg', 1),

(@HanoiId, N'Lotte Hotel Hanoi',
 N'54 Liễu Giai, Ba Đình, Hà Nội',
 N'Khách sạn cao cấp kết hợp không gian hiện đại.',
 5, N'/Images/hanoi-hotel3.jpg', 1),


-- NHA TRANG
(@NhatrangId, N'Vinpearl Luxury Nha Trang',
 N'Đảo Hòn Tre, Vĩnh Nguyên, Nha Trang',
 N'Khu nghỉ dưỡng biển đẳng cấp quốc tế.',
 5, N'/Images/nhatrang-hotel1.jpg', 1),

(@NhatrangId, N'Sheraton Nha Trang Hotel',
 N'26-28 Trần Phú, Lộc Thọ, Nha Trang',
 N'Khách sạn hướng biển tuyệt đẹp ngay cung đường Trần Phú.',
 5, N'/Images/nhatrang-hotel2.jpg', 1),

(@NhatrangId, N'Amiana Resort Nha Trang',
 N'Phạm Văn Đồng, Vĩnh Hòa, Nha Trang',
 N'Khu nghỉ dưỡng yên bình với bãi biển cát trắng riêng tư.',
 5, N'/Images/nhatrang-hotel3.jpg', 1),


-- DA LAT
(@DalatId, N'Dalat Edensee Lake Resort',
 N'Hồ Tuyền Lâm, Phường 4, Đà Lạt',
 N'Khu nghỉ dưỡng biệt lập bên hồ Tuyền Lâm.',
 5, N'/Images/dalat-hotel1.jpg', 1),

(@DalatId, N'Dalat Palace Heritage Hotel',
 N'2 Trần Phú, Phường 3, Đà Lạt',
 N'Khách sạn di sản mang kiến trúc Pháp cổ kính.',
 5, N'/Images/dalat-hotel2.jpg', 1),

(@DalatId, N'Colline Hotel Dalat',
 N'10 Phan Bội Châu, Phường 1, Đà Lạt',
 N'Khách sạn hiện đại ngay khu vực chợ Đà Lạt.',
 4, N'/Images/dalat-hotel3.jpg', 1);
GO


-- =====================================================
-- 2.6. THEM PHONG MAU
-- MOI KHACH SAN CO 2 PHONG
-- =====================================================

DECLARE @Double INT;
DECLARE @Vip INT;

SELECT @Double = RoomTypeId
FROM RoomTypes
WHERE TypeName = N'Phòng Đôi';

SELECT @Vip = RoomTypeId
FROM RoomTypes
WHERE TypeName = N'Phòng VIP';


-- PHONG DOI
INSERT INTO Rooms
(
    HotelId,
    RoomTypeId,
    RoomNumber,
    Price,
    Capacity,
    Amenities,
    OutsideView,
    ImageUrl,
    IsAvailable
)
SELECT
    HotelId,
    @Double,
    N'101',
    2500000,
    2,
    N'Wifi, Tivi, Điều hòa, Bồn tắm',
    N'Trung tâm thành phố',
    N'/Images/room-default1.jpg',
    1
FROM Hotels;


-- PHONG VIP
INSERT INTO Rooms
(
    HotelId,
    RoomTypeId,
    RoomNumber,
    Price,
    Capacity,
    Amenities,
    OutsideView,
    ImageUrl,
    IsAvailable
)
SELECT
    HotelId,
    @Vip,
    N'102',
    5500000,
    2,
    N'Smart Tivi 65 inch, Minibar, Quản gia riêng',
    N'Hướng đẹp toàn cảnh',
    N'/Images/room-default2.jpg',
    1
FROM Hotels;
GO


-- =====================================================
-- 2.7. THEM DANH GIA MAU
-- =====================================================

INSERT INTO Reviews
(
    HotelId,
    UserId,
    Rating,
    Comment
)
SELECT
    HotelId,
    2,
    5,
    N'Dịch vụ tuyệt vời, phòng ốc sạch sẽ và sang trọng!'
FROM Hotels
WHERE HotelId <= 3;
GO


-- =====================================================
-- 2.8. THEM DANH SACH YEU THICH MAU
-- =====================================================

INSERT INTO Wishlists (UserId, HotelId)
SELECT
    2,
    HotelId
FROM Hotels
WHERE HotelId IN (1, 4, 7);
GO


-- =====================================================
-- 3. KIEM TRA DU LIEU SAU KHI KHOI TAO
-- =====================================================

SELECT
    N'Roles' AS TableName,
    COUNT(*) AS Total
FROM Roles

UNION ALL

SELECT N'Users', COUNT(*) FROM Users

UNION ALL

SELECT N'Locations', COUNT(*) FROM Locations

UNION ALL

SELECT N'Hotels', COUNT(*) FROM Hotels

UNION ALL

SELECT N'RoomTypes', COUNT(*) FROM RoomTypes

UNION ALL

SELECT N'Rooms', COUNT(*) FROM Rooms

UNION ALL

SELECT N'Reviews', COUNT(*) FROM Reviews

UNION ALL

SELECT N'Wishlists', COUNT(*) FROM Wishlists

UNION ALL

SELECT N'Bookings', COUNT(*) FROM Bookings;
GO




CREATE TABLE Partners
(
    PartnerId INT IDENTITY(1,1) PRIMARY KEY,

    UserId INT NOT NULL UNIQUE,

    BusinessName NVARCHAR(150) NOT NULL,

    ContactName NVARCHAR(100),

    PhoneNumber VARCHAR(20),

    BusinessEmail VARCHAR(100),

    BusinessAddress NVARCHAR(255),

    TaxCode VARCHAR(50),

    CONSTRAINT FK_Partners_Users
        FOREIGN KEY (UserId)
        REFERENCES Users(UserId)
);
GO

ALTER TABLE Hotels
ADD PartnerId INT NULL;
GO

ALTER TABLE Hotels
ADD CONSTRAINT FK_Hotels_Partners
FOREIGN KEY (PartnerId)
REFERENCES Partners(PartnerId);
GO



IF OBJECT_ID('tempdb..#PartnerSeed') IS NOT NULL
    DROP TABLE #PartnerSeed;

SELECT
    H.HotelId,
    H.HotelName,
    H.Address,

    -- Email duoc tao tu ten khach san
    LOWER(
        REPLACE(
            REPLACE(
                REPLACE(
                    REPLACE(
                        REPLACE(H.HotelName, ' ', ''),
                        '''', ''
                    ),
                    '-', ''
                ),
                '.', ''
            ),
            '&', 'and'
        )
    ) + '@staytravel.test' AS PartnerEmail,

    -- Nguoi dai dien mau
    CASE H.HotelId
        WHEN 1 THEN N'Nguyễn Văn An'
        WHEN 2 THEN N'Trần Minh Tuấn'
        WHEN 3 THEN N'Lê Hoàng Nam'
        WHEN 4 THEN N'Phạm Quốc Bảo'
        WHEN 5 THEN N'Nguyễn Thành Đạt'
        WHEN 6 THEN N'Trần Quốc Huy'
        WHEN 7 THEN N'Lê Minh Đức'
        WHEN 8 THEN N'Phạm Văn Long'
        WHEN 9 THEN N'Nguyễn Hoàng Anh'
        WHEN 10 THEN N'Trần Minh Khang'
        WHEN 11 THEN N'Lê Quốc Khánh'
        WHEN 12 THEN N'Nguyễn Tuấn Kiệt'
        WHEN 13 THEN N'Phạm Minh Quân'
        WHEN 14 THEN N'Trần Hoàng Phúc'
        WHEN 15 THEN N'Lê Thành Công'
        ELSE N'Nguyễn Văn Bình'
    END AS ContactName,

    -- So dien thoai mau
    CONCAT(
        '090',
        RIGHT('0000000' + CAST(H.HotelId AS VARCHAR(7)), 7)
    ) AS PhoneNumber,

    -- Ma so thue mau
    CONCAT(
        '031',
        RIGHT('0000000' + CAST(H.HotelId AS VARCHAR(7)), 7)
    ) AS TaxCode

INTO #PartnerSeed
FROM Hotels H;


-- ==========================================
-- BUOC 6: TAO TAI KHOAN DOI TAC TRONG USERS
-- ==========================================

INSERT INTO Users (
    FullName,
    Email,
    PasswordHash,
    PhoneNumber,
    RoleId
)
SELECT
    S.ContactName,
    S.PartnerEmail,
    '123456',
    S.PhoneNumber,
    R.RoleId

FROM #PartnerSeed S
CROSS JOIN Roles R

WHERE R.RoleName = N'Partner'
AND NOT EXISTS (
    SELECT 1
    FROM Users U
    WHERE U.Email = S.PartnerEmail
);


-- ==========================================
-- BUOC 7: CAP NHAT BANG PARTNERS
-- ==========================================

-- Cap nhat doi tac da ton tai
UPDATE P
SET
    P.UserId = U.UserId,
    P.BusinessName = S.HotelName,
    P.ContactName = S.ContactName,
    P.PhoneNumber = S.PhoneNumber,
    P.BusinessEmail = S.PartnerEmail,
    P.BusinessAddress = S.Address,
    P.TaxCode = S.TaxCode

FROM Partners P
JOIN Hotels H
    ON H.PartnerId = P.PartnerId
JOIN #PartnerSeed S
    ON S.HotelId = H.HotelId
JOIN Users U
    ON U.Email = S.PartnerEmail;


-- Them doi tac neu khach san chua co
INSERT INTO Partners (
    UserId,
    BusinessName,
    ContactName,
    PhoneNumber,
    BusinessEmail,
    BusinessAddress,
    TaxCode
)
SELECT
    U.UserId,
    S.HotelName,
    S.ContactName,
    S.PhoneNumber,
    S.PartnerEmail,
    S.Address,
    S.TaxCode

FROM #PartnerSeed S
JOIN Users U
    ON U.Email = S.PartnerEmail

WHERE NOT EXISTS (
    SELECT 1
    FROM Partners P
    WHERE P.UserId = U.UserId
);


-- ==========================================
-- BUOC 8: GAN DOI TAC VAO KHACH SAN
-- ==========================================

UPDATE H
SET H.PartnerId = P.PartnerId

FROM Hotels H
JOIN #PartnerSeed S
    ON S.HotelId = H.HotelId
JOIN Users U
    ON U.Email = S.PartnerEmail
JOIN Partners P
    ON P.UserId = U.UserId;


-- ==========================================
-- BUOC 9: KIEM TRA KET QUA
-- ==========================================

SELECT
    H.HotelId,
    H.HotelName,
    P.PartnerId,
    P.UserId,
    U.Email AS AccountEmail,
    P.BusinessName,
    P.ContactName,
    P.PhoneNumber,
    P.BusinessEmail,
    P.BusinessAddress,
    P.TaxCode

FROM Hotels H
JOIN Partners P
    ON H.PartnerId = P.PartnerId
JOIN Users U
    ON P.UserId = U.UserId

ORDER BY H.HotelId;


DROP TABLE #PartnerSeed;
GO



DECLARE @Single INT;
DECLARE @Family INT;

-- Lấy mã loại phòng đơn
SELECT @Single = RoomTypeId
FROM RoomTypes
WHERE TypeName = N'Phòng Đơn';

-- Lấy mã loại căn hộ gia đình
SELECT @Family = RoomTypeId
FROM RoomTypes
WHERE TypeName = N'Căn hộ gia đình';


-- =====================================
-- PHÒNG ĐƠN (MỖI KHÁCH SẠN 1 PHÒNG)
-- =====================================

INSERT INTO Rooms
(
    HotelId,
    RoomTypeId,
    RoomNumber,
    Price,
    Capacity,
    Amenities,
    OutsideView,
    ImageUrl,
    IsAvailable
)
SELECT
    HotelId,
    @Single,
    N'103',
    1500000,
    1,
    N'Wifi, Tivi, Điều hòa, Phòng tắm riêng',
    N'Trung tâm thành phố',
    N'/Images/room-default1.jpg',
    1
FROM Hotels;


-- =====================================
-- CĂN HỘ GIA ĐÌNH (MỖI KHÁCH SẠN 1 PHÒNG)
-- =====================================

INSERT INTO Rooms
(
    HotelId,
    RoomTypeId,
    RoomNumber,
    Price,
    Capacity,
    Amenities,
    OutsideView,
    ImageUrl,
    IsAvailable
)
SELECT
    HotelId,
    @Family,
    N'104',
    4500000,
    4,
    N'Wifi, Smart Tivi, Điều hòa, Bếp riêng, Phòng khách',
    N'Hướng nhìn toàn cảnh',
    N'/Images/room-default2.jpg',
    1
FROM Hotels;

GO



-- 1. Ly do huy don (Khach huy / No-show / Qua han)
IF COL_LENGTH('dbo.Bookings', 'CancelReason') IS NULL
    ALTER TABLE dbo.Bookings ADD CancelReason NVARCHAR(50) NULL;
GO

-- 2. Bang lich phong do khach san tu sap xep:
--    - 'Đặt trực tiếp': khach dat tai quay / khong qua website
--    - 'Bảo trì'      : phong tam khong nhan khach trong khoang ngay do
IF OBJECT_ID('dbo.RoomBlocks', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.RoomBlocks
    (
        BlockId   INT IDENTITY(1,1) PRIMARY KEY,
        RoomId    INT NOT NULL,
        StartDate DATE NOT NULL,          -- dem dau tien bi giu
        EndDate   DATE NOT NULL,          -- ngay tra phong (khong tinh dem nay)
        BlockType NVARCHAR(30) NOT NULL,  -- N'Đặt trực tiếp' | N'Bảo trì'
        Note      NVARCHAR(255) NULL,     -- ten khach / SDT / ghi chu
        CreatedAt DATETIME NULL DEFAULT GETDATE(),

        CONSTRAINT FK_RoomBlocks_Rooms FOREIGN KEY (RoomId) REFERENCES dbo.Rooms(RoomId) ON DELETE CASCADE,
        CONSTRAINT CK_RoomBlocks_Dates CHECK (EndDate > StartDate)
    );
    CREATE INDEX IX_RoomBlocks_Room_Dates ON dbo.RoomBlocks (RoomId, StartDate, EndDate);
END
GO

-- 3. (Tuy chon) Dua cac phong dang bi IsAvailable = 0 do logic cu ve lai 1.
--    Truoc day he thong tu dat IsAvailable = 0 moi khi co don; gio phong
--    trong duoc tinh theo ngay, IsAvailable chi dung cho "bao tri / ngung nhan dat".
--    Bo comment dong duoi neu ban muon reset:
UPDATE dbo.Rooms SET IsAvailable = 1;
GO



-- KIEM TRA KHACH SAN THEO DIA DIEM
SELECT
    L.LocationName,
    COUNT(H.HotelId) AS TotalHotels
FROM Locations L
LEFT JOIN Hotels H
    ON L.LocationId = H.LocationId
GROUP BY L.LocationName;
GO


-- KIEM TRA PHONG THEO KHACH SAN
SELECT
    H.HotelName,
    COUNT(R.RoomId) AS TotalRooms
FROM Hotels H
LEFT JOIN Rooms R
    ON H.HotelId = R.HotelId
GROUP BY H.HotelName;
GO

-- 1. Cập nhật trạng thái các đơn cũ thành "Đã duyệt"
UPDATE Bookings 
SET Status = N'Đã duyệt' 
WHERE Status = N'Chờ duyệt';
GO

-- 2. Khóa tất cả các phòng có đơn đặt đang hiệu lực / đã duyệt
UPDATE Rooms 
SET IsAvailable = 0 
WHERE RoomId IN (
    SELECT RoomId FROM Bookings WHERE Status = N'Đã duyệt'
);
GO