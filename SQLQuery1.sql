DROP DATABASE IF EXISTS TaxiRankDb;
CREATE DATABASE TaxiRankDb3;
GO
USE TaxiRankDb3;
GO

CREATE TABLE Users (
    UserId INT IDENTITY(1,1) PRIMARY KEY,
    Username NVARCHAR(150) NOT NULL UNIQUE,
    Password NVARCHAR(200) NOT NULL,
    DisplayName NVARCHAR(150) NOT NULL,
    Role NVARCHAR(20) NOT NULL,
    Phone NVARCHAR(30) NULL,
    Email NVARCHAR(200) NULL,
    IsActive BIT NOT NULL DEFAULT 1,
    CreatedAt DATETIME NOT NULL DEFAULT GETDATE(),
    CreatedBy INT NULL,
    UpdatedAt DATETIME NULL,
    UpdatedBy INT NULL,
    CONSTRAINT CK_Users_Role CHECK (Role IN ('Admin','RankManager','Owner','Driver','Passenger'))
);

CREATE TABLE Ranks (
    RankId INT IDENTITY(1,1) PRIMARY KEY,
    Name NVARCHAR(150) NOT NULL,
    City NVARCHAR(150) NOT NULL,
    Province NVARCHAR(150) NULL,
    Latitude DECIMAL(9,6) NULL,
    Longitude DECIMAL(9,6) NULL,
    PlaceId NVARCHAR(128) NULL,
    IsActive BIT NOT NULL DEFAULT 1,
    CreatedAt DATETIME NOT NULL DEFAULT GETDATE(),
    CreatedBy INT NULL,
    UpdatedAt DATETIME NULL,
    UpdatedBy INT NULL,
    CONSTRAINT UQ_Ranks_NameCity UNIQUE (Name, City)
);

CREATE TABLE UserRanks (
    UserRankId INT IDENTITY(1,1) PRIMARY KEY,
    UserId INT NOT NULL,
    RankId INT NOT NULL,
    CreatedAt DATETIME NOT NULL DEFAULT GETDATE(),
    CreatedBy INT NULL,
    CONSTRAINT UQ_UserRanks UNIQUE (UserId, RankId),
    CONSTRAINT FK_UserRanks_User FOREIGN KEY (UserId) REFERENCES Users(UserId),
    CONSTRAINT FK_UserRanks_Rank FOREIGN KEY (RankId) REFERENCES Ranks(RankId)
);

CREATE TABLE Vehicles (
    VehicleId INT IDENTITY(1,1) PRIMARY KEY,
    RegNo NVARCHAR(50) NOT NULL UNIQUE,
    Make NVARCHAR(100) NULL,
    Model NVARCHAR(100) NULL,
    Year INT NULL,
    Seats INT NOT NULL,
    OwnerId INT NOT NULL,
    HomeRankId INT NULL,
    Active BIT NOT NULL DEFAULT 1,
    CreatedAt DATETIME NOT NULL DEFAULT GETDATE(),
    CreatedBy INT NULL,
    UpdatedAt DATETIME NULL,
    UpdatedBy INT NULL,
    CONSTRAINT FK_Vehicles_Owner FOREIGN KEY (OwnerId) REFERENCES Users(UserId),
    CONSTRAINT FK_Vehicles_HomeRank FOREIGN KEY (HomeRankId) REFERENCES Ranks(RankId)
);

CREATE TABLE VehicleDocuments (
    VehicleDocumentId INT IDENTITY(1,1) PRIMARY KEY,
    VehicleId INT NOT NULL,
    DocType NVARCHAR(50) NOT NULL,
    FileName NVARCHAR(255) NOT NULL,
    FileData VARBINARY(MAX) NOT NULL,
    UploadedBy INT NULL,
    UploadedAt DATETIME NOT NULL DEFAULT GETDATE(),
    CONSTRAINT FK_VehicleDocuments_Vehicle FOREIGN KEY (VehicleId) REFERENCES Vehicles(VehicleId)
);

CREATE TABLE OwnerConfigs (
    OwnerConfigId INT IDENTITY(1,1) PRIMARY KEY,
    OwnerId INT NOT NULL,
    DriverSharePercent DECIMAL(5,2) NOT NULL,
    EffectiveFrom DATETIME NOT NULL DEFAULT GETDATE(),
    CreatedAt DATETIME NOT NULL DEFAULT GETDATE(),
    CreatedBy INT NULL,
    CONSTRAINT UQ_OwnerConfigs_Owner UNIQUE (OwnerId),
    CONSTRAINT FK_OwnerConfigs_Owner FOREIGN KEY (OwnerId) REFERENCES Users(UserId)
);

CREATE TABLE Routes (
    RouteId INT IDENTITY(1,1) PRIMARY KEY,
    FromRankId INT NOT NULL,
    ToName NVARCHAR(200) NOT NULL,
    ToPlaceId NVARCHAR(128) NULL,
    ToLatitude DECIMAL(9,6) NULL,
    ToLongitude DECIMAL(9,6) NULL,
    DefaultFare DECIMAL(18,2) NOT NULL,
    DistanceKm DECIMAL(18,2) NULL,
    Active BIT NOT NULL DEFAULT 1,
    CreatedAt DATETIME NOT NULL DEFAULT GETDATE(),
    CreatedBy INT NULL,
    UpdatedAt DATETIME NULL,
    UpdatedBy INT NULL,
    CONSTRAINT FK_Routes_FromRank FOREIGN KEY (FromRankId) REFERENCES Ranks(RankId),
    CONSTRAINT UQ_Routes_From_To UNIQUE (FromRankId, ToName)
);

CREATE TABLE RouteWaypoints (
    RouteWaypointId INT IDENTITY(1,1) PRIMARY KEY,
    RouteId INT NOT NULL,
    Name NVARCHAR(200) NOT NULL,
    PlaceId NVARCHAR(128) NULL,
    Latitude DECIMAL(9,6) NULL,
    Longitude DECIMAL(9,6) NULL,
    Sequence INT NOT NULL,
    CreatedAt DATETIME NOT NULL DEFAULT GETDATE(),
    CreatedBy INT NULL,
    CONSTRAINT UQ_RouteWaypoints UNIQUE (RouteId, Sequence),
    CONSTRAINT FK_RouteWaypoints_Route FOREIGN KEY (RouteId) REFERENCES Routes(RouteId)
);

CREATE TABLE FareRules (
    FareRuleId INT IDENTITY(1,1) PRIMARY KEY,
    RouteId INT NOT NULL,
    RuleType NVARCHAR(20) NOT NULL,
    Amount DECIMAL(18,2) NOT NULL,
    StartDate DATE NULL,
    EndDate DATE NULL,
    CreatedAt DATETIME NOT NULL DEFAULT GETDATE(),
    CreatedBy INT NULL,
    CONSTRAINT CK_FareRules_Type CHECK (RuleType IN ('Base','Peak','Discount')),
    CONSTRAINT FK_FareRules_Route FOREIGN KEY (RouteId) REFERENCES Routes(RouteId)
);

CREATE TABLE MapsCache (
    MapsCacheId INT IDENTITY(1,1) PRIMARY KEY,
    PlaceId NVARCHAR(128) NOT NULL UNIQUE,
    Description NVARCHAR(255) NULL,
    Latitude DECIMAL(9,6) NULL,
    Longitude DECIMAL(9,6) NULL,
    Json NVARCHAR(MAX) NULL,
    CachedAt DATETIME NOT NULL DEFAULT GETDATE()
);

CREATE TABLE DriverAssignments (
    AssignmentId INT IDENTITY(1,1) PRIMARY KEY,
    DriverId INT NOT NULL,
    VehicleId INT NOT NULL,
    StartDate DATE NOT NULL,
    EndDate DATE NULL,
    CreatedAt DATETIME NOT NULL DEFAULT GETDATE(),
    CreatedBy INT NULL,
    CONSTRAINT FK_DriverAssignments_Driver FOREIGN KEY (DriverId) REFERENCES Users(UserId),
    CONSTRAINT FK_DriverAssignments_Vehicle FOREIGN KEY (VehicleId) REFERENCES Vehicles(VehicleId)
);

CREATE TABLE Passengers (
    PassengerId INT IDENTITY(1,1) PRIMARY KEY,
    FullName NVARCHAR(150) NOT NULL,
    Phone NVARCHAR(30) NOT NULL,
    NextOfKinName NVARCHAR(150) NULL,
    NextOfKinPhone NVARCHAR(30) NULL,
    CreatedAt DATETIME NOT NULL DEFAULT GETDATE(),
    CreatedBy INT NULL
);

CREATE TABLE Departures (
    DepartureId INT IDENTITY(1,1) PRIMARY KEY,
    RouteId INT NOT NULL,
    Date DATE NOT NULL,
    PlannedTime TIME(0) NOT NULL,
    VehicleId INT NOT NULL,
    DriverId INT NOT NULL,
    MinFill INT NOT NULL,
    Status NVARCHAR(20) NOT NULL DEFAULT 'Queued',
    ReadyChecklist_MinFill BIT NOT NULL DEFAULT 0,
    ReadyChecklist_CashVerified BIT NOT NULL DEFAULT 0,
    ReadyChecklist_FloatIssued BIT NOT NULL DEFAULT 0,
    ReadyChecklist_VehicleCleared BIT NOT NULL DEFAULT 0,
    ManagerOverrideReady BIT NOT NULL DEFAULT 0,
    CreatedAt DATETIME NOT NULL DEFAULT GETDATE(),
    CreatedBy INT NULL,
    UpdatedAt DATETIME NULL,
    UpdatedBy INT NULL,
    CONSTRAINT CK_Departures_Status CHECK (Status IN ('Queued','Boarding','Ready','Departed','Cancelled')),
    CONSTRAINT FK_Departures_Route FOREIGN KEY (RouteId) REFERENCES Routes(RouteId),
    CONSTRAINT FK_Departures_Vehicle FOREIGN KEY (VehicleId) REFERENCES Vehicles(VehicleId),
    CONSTRAINT FK_Departures_Driver FOREIGN KEY (DriverId) REFERENCES Users(UserId)
);

CREATE TABLE Bookings (
    BookingId INT IDENTITY(1,1) PRIMARY KEY,
    DepartureId INT NOT NULL,
    PassengerId INT NOT NULL,
    SeatsCount INT NOT NULL,
    FareEach DECIMAL(18,2) NOT NULL,
    PaymentMethod NVARCHAR(10) NOT NULL,
    Status NVARCHAR(12) NOT NULL,
    Pnr NVARCHAR(20) NOT NULL UNIQUE,
    QrBlob VARBINARY(MAX) NULL,
    ExpiresAt DATETIME NULL,
    CreatedAt DATETIME NOT NULL DEFAULT GETDATE(),
    CreatedBy INT NULL,
    ExtendedAmount AS (CONVERT(DECIMAL(18,2), SeatsCount) * FareEach) PERSISTED,
    CONSTRAINT CK_Bookings_PaymentMethod CHECK (PaymentMethod IN ('Cash','Card')),
    CONSTRAINT CK_Bookings_Status CHECK (Status IN ('Held','Confirmed','Cancelled')),
    CONSTRAINT FK_Bookings_Departure FOREIGN KEY (DepartureId) REFERENCES Departures(DepartureId),
    CONSTRAINT FK_Bookings_Passenger FOREIGN KEY (PassengerId) REFERENCES Passengers(PassengerId)
);

CREATE TABLE SeatInventory (
    SeatInventoryId INT IDENTITY(1,1) PRIMARY KEY,
    DepartureId INT NOT NULL,
    SeatNo INT NOT NULL,
    Status NVARCHAR(10) NOT NULL DEFAULT 'Free',
    BookingId INT NULL,
    CreatedAt DATETIME NOT NULL DEFAULT GETDATE(),
    CreatedBy INT NULL,
    CONSTRAINT CK_SeatInventory_Status CHECK (Status IN ('Free','Held','Booked')),
    CONSTRAINT UQ_SeatInventory UNIQUE (DepartureId, SeatNo),
    CONSTRAINT FK_SeatInventory_Departure FOREIGN KEY (DepartureId) REFERENCES Departures(DepartureId),
    CONSTRAINT FK_SeatInventory_Booking FOREIGN KEY (BookingId) REFERENCES Bookings(BookingId)
);

CREATE TABLE BookingSeats (
    BookingSeatId INT IDENTITY(1,1) PRIMARY KEY,
    BookingId INT NOT NULL,
    DepartureId INT NOT NULL,
    SeatNo INT NOT NULL,
    CreatedAt DATETIME NOT NULL DEFAULT GETDATE(),
    CreatedBy INT NULL,
    CONSTRAINT UQ_BookingSeats UNIQUE (BookingId, SeatNo),
    CONSTRAINT FK_BookingSeats_Booking FOREIGN KEY (BookingId) REFERENCES Bookings(BookingId)
);

CREATE TABLE Payments (
    PaymentId INT IDENTITY(1,1) PRIMARY KEY,
    BookingId INT NOT NULL,
    Amount DECIMAL(18,2) NOT NULL,
    Method NVARCHAR(10) NOT NULL,
    Reference NVARCHAR(100) NULL,
    PaidAt DATETIME NOT NULL DEFAULT GETDATE(),
    CreatedAt DATETIME NOT NULL DEFAULT GETDATE(),
    CreatedBy INT NULL,
    CONSTRAINT CK_Payments_Method CHECK (Method IN ('Cash','Card')),
    CONSTRAINT FK_Payments_Booking FOREIGN KEY (BookingId) REFERENCES Bookings(BookingId)
);

CREATE TABLE CashupBags (
    BagId INT IDENTITY(1,1) PRIMARY KEY,
    DepartureId INT NOT NULL,
    CountedAmount DECIMAL(18,2) NOT NULL,
    CountedBy INT NOT NULL,
    Timestamp DATETIME NOT NULL DEFAULT GETDATE(),
    CONSTRAINT FK_CashupBags_Departure FOREIGN KEY (DepartureId) REFERENCES Departures(DepartureId),
    CONSTRAINT FK_CashupBags_User FOREIGN KEY (CountedBy) REFERENCES Users(UserId)
);

CREATE TABLE Floats (
    FloatId INT IDENTITY(1,1) PRIMARY KEY,
    DepartureId INT NOT NULL,
    AmountIssued DECIMAL(18,2) NOT NULL,
    Purpose NVARCHAR(10) NOT NULL,
    IssuedBy INT NOT NULL,
    Timestamp DATETIME NOT NULL DEFAULT GETDATE(),
    CONSTRAINT CK_Floats_Purpose CHECK (Purpose IN ('Fuel','Toll','Other')),
    CONSTRAINT FK_Floats_Departure FOREIGN KEY (DepartureId) REFERENCES Departures(DepartureId),
    CONSTRAINT FK_Floats_User FOREIGN KEY (IssuedBy) REFERENCES Users(UserId)
);

CREATE TABLE Telemetry (
    TelemetryId INT IDENTITY(1,1) PRIMARY KEY,
    DepartureId INT NOT NULL,
    Latitude DECIMAL(9,6) NOT NULL,
    Longitude DECIMAL(9,6) NOT NULL,
    SpeedKph DECIMAL(5,2) NULL,
    Battery INT NULL,
    Timestamp DATETIME NOT NULL DEFAULT GETDATE(),
    CONSTRAINT FK_Telemetry_Departure FOREIGN KEY (DepartureId) REFERENCES Departures(DepartureId)
);

CREATE TABLE WaypointChecks (
    WaypointCheckId INT IDENTITY(1,1) PRIMARY KEY,
    DepartureId INT NOT NULL,
    RouteWaypointId INT NOT NULL,
    ReachedAt DATETIME NOT NULL DEFAULT GETDATE(),
    CONSTRAINT FK_WaypointChecks_Departure FOREIGN KEY (DepartureId) REFERENCES Departures(DepartureId),
    CONSTRAINT FK_WaypointChecks_Waypoint FOREIGN KEY (RouteWaypointId) REFERENCES RouteWaypoints(RouteWaypointId)
);

CREATE TABLE Incidents (
    IncidentId INT IDENTITY(1,1) PRIMARY KEY,
    DepartureId INT NOT NULL,
    Type NVARCHAR(20) NOT NULL,
    Notes NVARCHAR(1000) NULL,
    CreatedAt DATETIME NOT NULL DEFAULT GETDATE(),
    CreatedBy INT NULL,
    CONSTRAINT CK_Incidents_Type CHECK (Type IN ('Delay','Breakdown','RouteChange','Other')),
    CONSTRAINT FK_Incidents_Departure FOREIGN KEY (DepartureId) REFERENCES Departures(DepartureId)
);

CREATE TABLE Expenses (
    ExpenseId INT IDENTITY(1,1) PRIMARY KEY,
    DepartureId INT NOT NULL,
    Type NVARCHAR(10) NOT NULL,
    Amount DECIMAL(18,2) NOT NULL,
    ProofAttachmentId INT NULL,
    CreatedAt DATETIME NOT NULL DEFAULT GETDATE(),
    CreatedBy INT NULL,
    CONSTRAINT CK_Expenses_Type CHECK (Type IN ('Fuel','Toll','Fine','Other')),
    CONSTRAINT FK_Expenses_Departure FOREIGN KEY (DepartureId) REFERENCES Departures(DepartureId)
);

CREATE TABLE Settlements (
    SettlementId INT IDENTITY(1,1) PRIMARY KEY,
    OwnerId INT NOT NULL,
    PeriodDay DATE NOT NULL,
    Gross DECIMAL(18,2) NOT NULL,
    Expenses DECIMAL(18,2) NOT NULL,
    DriverShare DECIMAL(18,2) NOT NULL,
    NetToOwner DECIMAL(18,2) NOT NULL,
    CreatedAt DATETIME NOT NULL DEFAULT GETDATE(),
    CreatedBy INT NULL,
    CONSTRAINT UQ_Settlements UNIQUE (OwnerId, PeriodDay),
    CONSTRAINT FK_Settlements_Owner FOREIGN KEY (OwnerId) REFERENCES Users(UserId)
);

CREATE TABLE Charters (
    CharterId INT IDENTITY(1,1) PRIMARY KEY,
    Organizer NVARCHAR(200) NOT NULL,
    Contact NVARCHAR(100) NOT NULL,
    Date DATE NOT NULL,
    PickupPlaceId NVARCHAR(128) NULL,
    PickupLat DECIMAL(9,6) NULL,
    PickupLng DECIMAL(9,6) NULL,
    DropPlaceId NVARCHAR(128) NULL,
    DropLat DECIMAL(9,6) NULL,
    DropLng DECIMAL(9,6) NULL,
    Pax INT NOT NULL,
    Status NVARCHAR(20) NOT NULL DEFAULT 'Requested',
    QuoteAmount DECIMAL(18,2) NOT NULL,
    DepositPaid BIT NOT NULL DEFAULT 0,
    CreatedAt DATETIME NOT NULL DEFAULT GETDATE(),
    CreatedBy INT NULL,
    CONSTRAINT CK_Charters_Status CHECK (Status IN ('Requested','Quoted','Approved','Assigned','InProgress','Completed','Cancelled'))
);

CREATE TABLE CharterVehicles (
    CharterVehicleId INT IDENTITY(1,1) PRIMARY KEY,
    CharterId INT NOT NULL,
    VehicleId INT NOT NULL,
    DriverId INT NOT NULL,
    CONSTRAINT UQ_CharterVehicles UNIQUE (CharterId, VehicleId),
    CONSTRAINT FK_CharterVehicles_Charter FOREIGN KEY (CharterId) REFERENCES Charters(CharterId),
    CONSTRAINT FK_CharterVehicles_Vehicle FOREIGN KEY (VehicleId) REFERENCES Vehicles(VehicleId),
    CONSTRAINT FK_CharterVehicles_Driver FOREIGN KEY (DriverId) REFERENCES Users(UserId)
);

CREATE TABLE CharterPassengers (
    CharterPassengerId INT IDENTITY(1,1) PRIMARY KEY,
    CharterId INT NOT NULL,
    FullName NVARCHAR(150) NOT NULL,
    Phone NVARCHAR(30) NULL,
    CreatedAt DATETIME NOT NULL DEFAULT GETDATE(),
    CreatedBy INT NULL,
    CONSTRAINT FK_CharterPassengers_Charter FOREIGN KEY (CharterId) REFERENCES Charters(CharterId)
);

CREATE TABLE Attachments (
    AttachmentId INT IDENTITY(1,1) PRIMARY KEY,
    Entity NVARCHAR(50) NOT NULL,
    EntityId INT NOT NULL,
    FileName NVARCHAR(255) NOT NULL,
    FileData VARBINARY(MAX) NOT NULL,
    UploadedBy INT NULL,
    UploadedAt DATETIME NOT NULL DEFAULT GETDATE()
);

CREATE TABLE Alerts (
    AlertId INT IDENTITY(1,1) PRIMARY KEY,
    Type NVARCHAR(30) NOT NULL,
    Entity NVARCHAR(50) NULL,
    EntityId INT NULL,
    Severity NVARCHAR(10) NOT NULL,
    Message NVARCHAR(500) NULL,
    CreatedAt DATETIME NOT NULL DEFAULT GETDATE(),
    ResolvedAt DATETIME NULL,
    ResolvedBy INT NULL,
    CONSTRAINT CK_Alerts_Type CHECK (Type IN ('MinFillBreach','LateDeparture','CashVariance','IncidentSpike')),
    CONSTRAINT CK_Alerts_Severity CHECK (Severity IN ('Low','Medium','High'))
);

CREATE TABLE AlertRules (
    AlertRuleId INT IDENTITY(1,1) PRIMARY KEY,
    MinFillComplianceThreshold DECIMAL(5,2) NOT NULL DEFAULT 0.80,
    LateDepartureMinutes INT NOT NULL DEFAULT 10,
    CashVarianceThreshold DECIMAL(18,2) NOT NULL DEFAULT 100.00,
    IncidentSpikeThreshold INT NOT NULL DEFAULT 3,
    CreatedAt DATETIME NOT NULL DEFAULT GETDATE(),
    CreatedBy INT NULL
);

CREATE TABLE Approvals (
    ApprovalId INT IDENTITY(1,1) PRIMARY KEY,
    Entity NVARCHAR(50) NOT NULL,
    EntityId INT NOT NULL,
    Action NVARCHAR(50) NOT NULL,
    RequestedBy INT NOT NULL,
    RequestedAt DATETIME NOT NULL DEFAULT GETDATE(),
    Status NVARCHAR(20) NOT NULL DEFAULT 'Pending',
    ApprovedBy INT NULL,
    ApprovedAt DATETIME NULL,
    Notes NVARCHAR(500) NULL,
    CONSTRAINT CK_Approvals_Status CHECK (Status IN ('Pending','Approved','Rejected'))
);

CREATE TABLE ReportingArchive (
    ArchiveId INT IDENTITY(1,1) PRIMARY KEY,
    PeriodMonth CHAR(7) NOT NULL,
    FileName NVARCHAR(255) NOT NULL,
    FileData VARBINARY(MAX) NOT NULL,
    CreatedAt DATETIME NOT NULL DEFAULT GETDATE(),
    CreatedBy INT NULL,
    CONSTRAINT UQ_ReportingArchive UNIQUE (PeriodMonth, FileName)
);

CREATE TABLE AuditLog (
    AuditLogId INT IDENTITY(1,1) PRIMARY KEY,
    EntityType NVARCHAR(50) NOT NULL,
    EntityId INT NOT NULL,
    Field NVARCHAR(100) NOT NULL,
    OldValue NVARCHAR(1000) NULL,
    NewValue NVARCHAR(1000) NULL,
    UserId INT NULL,
    Timestamp DATETIME NOT NULL DEFAULT GETDATE()
);

ALTER TABLE Expenses
ADD CONSTRAINT FK_Expenses_Attachment FOREIGN KEY (ProofAttachmentId) REFERENCES Attachments(AttachmentId);

CREATE INDEX IX_Departures_Status ON Departures(Status);
CREATE INDEX IX_Bookings_Departure ON Bookings(DepartureId);
CREATE INDEX IX_Payments_Booking ON Payments(BookingId);
CREATE INDEX IX_Telemetry_Departure_Timestamp ON Telemetry(DepartureId, Timestamp);

INSERT INTO Users (Username, Password, DisplayName, Role, Phone, Email, IsActive)
VALUES ('Admin@gmail.com', 'Admin@2004', 'System Admin', 'Admin', '079 435 3238', 'Admin@gmail.com', 1);

INSERT INTO Users (Username, Password, DisplayName, Role, Phone, Email, IsActive)
VALUES ('owner1@taxi', 'OwnerPass', 'Owner One', 'Owner', '079 554 2312', 'owner1@taxi.com', 1),
       ('driver1@taxi', 'DriverPass', 'Mthobisi Gwala', 'Driver', '085 238 1219', 'driver1@taxi.com', 1),
       ('manager1@taxi', 'ManagerPass', 'Samule Khoza', 'RankManager', '081 737 9291', 'manager1@taxi.com', 1);

INSERT INTO Ranks (Name, City, Province, Latitude, Longitude, IsActive, CreatedBy)
VALUES ('Durban Station Rank', 'Durban', 'KwaZulu-Natal', -29.8587, 31.0218, 1, 1),
       ('PMB Central Rank', 'Pietermaritzburg', 'KwaZulu-Natal', -29.6006, 30.3789, 1, 1);

DECLARE @OwnerId INT = (SELECT UserId FROM Users WHERE Username='owner1@taxi');
DECLARE @DriverId INT = (SELECT UserId FROM Users WHERE Username='driver1@taxi');
DECLARE @ManagerId INT = (SELECT UserId FROM Users WHERE Username='manager1@taxi');
DECLARE @DurbanRankId INT = (SELECT RankId FROM Ranks WHERE Name='Durban Station Rank');
DECLARE @PMBRankId INT = (SELECT RankId FROM Ranks WHERE Name='PMB Central Rank');

INSERT INTO UserRanks (UserId, RankId, CreatedBy) VALUES (@ManagerId, @DurbanRankId, 1);

INSERT INTO Vehicles (RegNo, Make, Model, Year, Seats, OwnerId, HomeRankId, Active, CreatedBy)
VALUES ('ND 123-456', 'Toyota', 'Quantum', 2022, 15, @OwnerId, @DurbanRankId, 1, 1);

DECLARE @VehicleId INT = SCOPE_IDENTITY();

INSERT INTO OwnerConfigs (OwnerId, DriverSharePercent, CreatedBy) VALUES (@OwnerId, 30.00, 1);

INSERT INTO DriverAssignments (DriverId, VehicleId, StartDate, CreatedBy)
VALUES (@DriverId, @VehicleId, CAST(GETDATE() AS DATE), 1);

INSERT INTO Routes (FromRankId, ToName, DefaultFare, Active, CreatedBy)
VALUES (@DurbanRankId, 'PMB Central Rank', 50.00, 1, 1);

DECLARE @RouteId INT = SCOPE_IDENTITY();

INSERT INTO RouteWaypoints (RouteId, Name, Sequence, CreatedBy)
VALUES (@RouteId, 'Pinetown', 1, 1);

INSERT INTO FareRules (RouteId, RuleType, Amount, CreatedBy)
VALUES (@RouteId, 'Base', 50.00, 1);

INSERT INTO Passengers (FullName, Phone, CreatedBy)
VALUES ('Olwethu Khuzwayo ', '0812345678', 1);

DECLARE @PassengerId INT = SCOPE_IDENTITY();

INSERT INTO Departures (RouteId, Date, PlannedTime, VehicleId, DriverId, MinFill, Status, CreatedBy)
VALUES (@RouteId, CAST(GETDATE() AS DATE), CONVERT(TIME(0), DATEADD(HOUR, 1, GETDATE())), @VehicleId, @DriverId, 10, 'Queued', 1);

DECLARE @DepartureId INT = SCOPE_IDENTITY();

DECLARE @Seat INT = 1, @TotalSeats INT = (SELECT Seats FROM Vehicles WHERE VehicleId=@VehicleId);
WHILE @Seat <= @TotalSeats
BEGIN
    INSERT INTO SeatInventory (DepartureId, SeatNo, Status, CreatedBy) VALUES (@DepartureId, @Seat, 'Free', 1);
    SET @Seat = @Seat + 1;
END

INSERT INTO Bookings (DepartureId, PassengerId, SeatsCount, FareEach, PaymentMethod, Status, Pnr, CreatedBy)
VALUES (@DepartureId, @PassengerId, 2, 50.00, 'Cash', 'Confirmed', 'P12345', 1);

DECLARE @BookingId INT = SCOPE_IDENTITY();

INSERT INTO BookingSeats (BookingId, DepartureId, SeatNo, CreatedBy)
VALUES (@BookingId, @DepartureId, 1, 1),
       (@BookingId, @DepartureId, 2, 1);

UPDATE SeatInventory SET Status='Booked', BookingId=@BookingId WHERE DepartureId=@DepartureId AND SeatNo IN (1,2);

INSERT INTO Payments (BookingId, Amount, Method, CreatedBy)
VALUES (@BookingId, 100.00, 'Cash', 1);

INSERT INTO AlertRules (MinFillComplianceThreshold, LateDepartureMinutes, CashVarianceThreshold, IncidentSpikeThreshold, CreatedBy)
VALUES (0.80, 10, 100.00, 3, 1);


IF EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = 'CK_Users_Role' AND parent_object_id = OBJECT_ID('dbo.Users'))
BEGIN
    ALTER TABLE dbo.Users DROP CONSTRAINT CK_Users_Role;
END;

ALTER TABLE dbo.Users
ADD CONSTRAINT CK_Users_Role
CHECK (Role IN ('Admin','RankManager','Owner','Driver','Passenger','Customer'));

ALTER TABLE Charters
ADD CustomerId INT NULL,
    TaxiCount INT NOT NULL DEFAULT 1,
    ReturnTrip BIT NOT NULL DEFAULT 0,
    EventType NVARCHAR(50) NULL,
    ApprovedBy INT NULL,
    ApprovedAt DATETIME NULL,
    IsPaid BIT NOT NULL DEFAULT 0,
    StripePaymentIntentId NVARCHAR(100) NULL;

ALTER TABLE Charters
ADD CONSTRAINT FK_Charters_Customer FOREIGN KEY (CustomerId) REFERENCES Users(UserId),
    CONSTRAINT FK_Charters_ApprovedBy FOREIGN KEY (ApprovedBy) REFERENCES Users(UserId);

ALTER TABLE Charters
DROP CONSTRAINT CK_Charters_Status;


ALTER TABLE Charters
ADD CONSTRAINT CK_Charters_Status CHECK (Status IN ('Requested','Quoted','Approved','Assigned','InProgress','Completed','Cancelled','Rejected','Declined'));

ALTER TABLE OwnerConfigs
ADD ManagerSharePercent DECIMAL(5,2) NOT NULL DEFAULT 3.00;
ALTER TABLE Settlements
ADD ManagerShare DECIMAL(18,2) NOT NULL DEFAULT 0.00;

ALTER TABLE Users
ALTER COLUMN Phone NVARCHAR(15) NULL;

ALTER TABLE Users
ALTER COLUMN Password NVARCHAR(25) NOT NULL;

ALTER TABLE Users
ALTER COLUMN DisplayName NVARCHAR(25) NOT NULL;

ALTER TABLE Users
ALTER COLUMN Email NVARCHAR(250) NULL;

ALTER TABLE Passengers
ALTER COLUMN FullName NVARCHAR(25) NOT NULL;


ALTER TABLE Passengers
ALTER COLUMN NextOfKinName NVARCHAR(25) NULL;

ALTER TABLE Passengers
ALTER COLUMN NextOfKinPhone NVARCHAR(15) NULL;


ALTER TABLE Passengers
ALTER COLUMN Phone NVARCHAR(10) NOT NULL;

ALTER TABLE Vehicles
ALTER COLUMN RegNo NVARCHAR(10) NOT NULL;

IF OBJECT_ID('dbo.CharterPayments','U') IS NULL
BEGIN
    CREATE TABLE CharterPayments(
        CharterPaymentId INT IDENTITY(1,1) PRIMARY KEY,
        CharterId INT NOT NULL,
        Amount DECIMAL(18,2) NOT NULL,
        StripePaymentIntentId NVARCHAR(100) NOT NULL,
        PaidAt DATETIME NOT NULL DEFAULT GETDATE(),
        CreatedBy INT NULL,
        CONSTRAINT FK_CharterPayments_Charter FOREIGN KEY (CharterId) REFERENCES Charters(CharterId)
    );
END