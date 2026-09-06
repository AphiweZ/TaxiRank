// ViewModels.cs
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace TaxiRank.Models.ViewModels
{
    public class IdName
    {
        public int Id { get; set; }
        public string Name { get; set; }
    }

    public class IdNameCode
    {
        public int Id { get; set; }
        public string Code { get; set; }
        public string Name { get; set; }
    }

    public class CoordinateVM
    {
        public string PlaceId { get; set; }
        public decimal? Latitude { get; set; }
        public decimal? Longitude { get; set; }
        public string Address { get; set; }
    }

    public class FileUploadVM
    {
        public int EntityId { get; set; }
        [Required]
        public string Entity { get; set; }
        [Required]
        public string FileName { get; set; }
        [Required]
        public byte[] FileData { get; set; }
    }

    public class PaginationVM
    {
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 20;
        public int TotalCount { get; set; }
    }

    public class RankCreateVM
    {
        public int RankId { get; set; }
        [Required, StringLength(150)]
        public string Name { get; set; }
        [Required, StringLength(150)]
        public string City { get; set; }
        [StringLength(150)]
        public string Province { get; set; }
        public string PlaceId { get; set; }
        [Range(-90, 90)]
        public decimal? Latitude { get; set; }
        [Range(-180, 180)]
        public decimal? Longitude { get; set; }
        public bool IsActive { get; set; } = true;
    }

    public class RankRowVM
    {
        public int RankId { get; set; }
        public string Name { get; set; }
        public string City { get; set; }
        public string Province { get; set; }
        public bool IsActive { get; set; }
    }

    public class RankListVM
    {
        public string City { get; set; }
        public string Search { get; set; }
        public List<IdName> CityOptions { get; set; } = new List<IdName>();
        public List<RankRowVM> Rows { get; set; } = new List<RankRowVM>();
        public PaginationVM Page { get; set; } = new PaginationVM();
    }

    public class VehicleCreateVM
    {
        public int VehicleId { get; set; }
        [Required, StringLength(11)]
        public string RegNo { get; set; }
        [StringLength(100)]
        public string Make { get; set; }
        [StringLength(100)]
        public string Model { get; set; }
        [Range(1950, 2500)]
        public int? Year { get; set; }
        [Range(1, 30)]
        public int Seats { get; set; }
        [Required]
        public int OwnerId { get; set; }
        public int? HomeRankId { get; set; }
        public bool Active { get; set; } = true
        ;
        public List<IdName> OwnerOptions { get; set; } = new List<IdName>();
        public List<IdName> RankOptions { get; set; } = new List<IdName>();
    }

    public class VehicleRowVM
    {
        public int VehicleId { get; set; }
        public string RegNo { get; set; }
        public string Make { get; set; }
        public string Model { get; set; }
        public int Seats { get; set; }
        public string OwnerName { get; set; }
        public string HomeRank { get; set; }
        public bool Active { get; set; }
    }

    public class VehicleListVM
    {
        public int? OwnerId { get; set; }
        public int? RankId { get; set; }
        public bool? Active { get; set; }
        public string Search { get; set; }
        public List<IdName> OwnerOptions { get; set; } = new List<IdName>();
        public List<IdName> RankOptions { get; set; } = new List<IdName>();
        public List<VehicleRowVM> Rows { get; set; } = new List<VehicleRowVM>();
        public PaginationVM Page { get; set; } = new PaginationVM();
    }

    public class VehicleDocumentUploadVM
    {
        [Required]
        public int VehicleId { get; set; }
        [Required, StringLength(50)]
        public string DocType { get; set; }
        [Required]
        public string FileName { get; set; }
        [Required]
        public byte[] FileData { get; set; }
    }

    public class OwnerConfigEditVM
    {
        [Required]
        public int OwnerId { get; set; }
        [Range(0, 100)]
        public decimal DriverSharePercent { get; set; }
        public List<IdName> OwnerOptions { get; set; } = new List<IdName>();
    }

    public class RouteCreateVM
    {
        public int RouteId { get; set; }
        [Required]
        public int FromRankId { get; set; }
        [Required, StringLength(200)]
        public string ToName { get; set; }
        public string ToPlaceId { get; set; }
        [Range(-90, 90)]
        public decimal? ToLatitude { get; set; }
        [Range(-180, 180)]
        public decimal? ToLongitude { get; set; }
        [Range(0, 1000000)]
        public decimal DefaultFare { get; set; }
        [Range(0, 1000000)]
        public decimal? DistanceKm { get; set; }
        public bool Active { get; set; } = true;
        public List<IdName> RankOptions { get; set; } = new List<IdName>();
    }

    public class FareRuleLineVM
    {
        public int FareRuleId { get; set; }
        [Required]
        public int RouteId { get; set; }
        [Required, RegularExpression("Base|Peak|Discount")]
        public string RuleType { get; set; }
        [Range(0, 1000000)]
        public decimal Amount { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
    }

    public class RouteWaypointLineVM
    {
        public int RouteWaypointId { get; set; }
        public int RouteId { get; set; }
        [Required, StringLength(200)]
        public string Name { get; set; }
        public string PlaceId { get; set; }
        [Range(-90, 90)]
        public decimal? Latitude { get; set; }
        [Range(-180, 180)]
        public decimal? Longitude { get; set; }
        [Range(1, 1000)]
        public int Sequence { get; set; }
    }

    public class RouteDetailVM
    {
        public int RouteId { get; set; }
        public string FromRank { get; set; }
        public string ToName { get; set; }
        public decimal DefaultFare { get; set; }
        public decimal? DistanceKm { get; set; }
        public bool Active { get; set; }
        public List<RouteWaypointLineVM> Waypoints { get; set; } = new List<RouteWaypointLineVM>();
        public List<FareRuleLineVM> FareRules { get; set; } = new List<FareRuleLineVM>();
    }

    public class RouteListRowVM
    {
        public int RouteId { get; set; }
        public string FromRank { get; set; }
        public string ToName { get; set; }
        public decimal DefaultFare { get; set; }
        public bool Active { get; set; }
    }

    public class RouteListVM
    {
        public int? FromRankId { get; set; }
        public string Search { get; set; }
        public bool? Active { get; set; }
        public List<IdName> RankOptions { get; set; } = new List<IdName>();
        public List<RouteListRowVM> Rows { get; set; } = new List<RouteListRowVM>();
        public PaginationVM Page { get; set; } = new PaginationVM();
    }

    public class DriverAssignmentCreateVM
    {
        [Required]
        public int DriverId { get; set; }
        [Required]
        public int VehicleId { get; set; }
        [Required]
        public DateTime StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public List<IdName> DriverOptions { get; set; } = new List<IdName>();
        public List<IdName> VehicleOptions { get; set; } = new List<IdName>();
    }

    public class DepartureCreateVM
    {
        public int DepartureId { get; set; }
        [Required]
        public int RouteId { get; set; }
        [Required]
        public DateTime Date { get; set; }
        [Required]
        public TimeSpan PlannedTime { get; set; }
        [Required]
        public int VehicleId { get; set; }
        [Required]
        public int DriverId { get; set; }
        [Range(1, 1000)]
        public int MinFill { get; set; }
        [RegularExpression("Queued|Boarding|Ready|Departed|Cancelled")]
        public string Status { get; set; } = "Queued";
        public bool ReadyChecklist_MinFill { get; set; }
        public bool ReadyChecklist_CashVerified { get; set; }
        public bool ReadyChecklist_FloatIssued { get; set; }
        public bool ReadyChecklist_VehicleCleared { get; set; }
        public bool ManagerOverrideReady { get; set; }
        public List<IdName> RouteOptions { get; set; } = new List<IdName>();
        public List<IdName> VehicleOptions { get; set; } = new List<IdName>();
        public List<IdName> DriverOptions { get; set; } = new List<IdName>();
    }

    public class SeatCellVM
    {
        public int SeatNo { get; set; }
        public string Status { get; set; }
        public int? BookingId { get; set; }
    }

    public class DepartureRowVM
    {
        public int DepartureId { get; set; }
        public DateTime Date { get; set; }
        public TimeSpan PlannedTime { get; set; }
        public string RouteName { get; set; }
        public string VehicleReg { get; set; }
        public string DriverName { get; set; }
        public int MinFill { get; set; }
        public string Status { get; set; }
        public int SeatsTotal { get; set; }
        public int SeatsBooked { get; set; }
    }

    public class DepartureDetailVM
    {
        public int DepartureId { get; set; }
        public string RouteName { get; set; }
        public DateTime Date { get; set; }
        public TimeSpan PlannedTime { get; set; }
        public string VehicleReg { get; set; }
        public string DriverName { get; set; }
        public string Status { get; set; }
        public int MinFill { get; set; }
        public List<SeatCellVM> SeatMap { get; set; } = new List<SeatCellVM>();
        public List<BookingRowVM> Bookings { get; set; } = new List<BookingRowVM>();
        public List<PaymentRowVM> Payments { get; set; } = new List<PaymentRowVM>();
        public List<ExpenseRowVM> Expenses { get; set; } = new List<ExpenseRowVM>();
        public List<FloatRowVM> Floats { get; set; } = new List<FloatRowVM>();
        public List<IncidentRowVM> Incidents { get; set; } = new List<IncidentRowVM>();
    }

    public class DepartureListVM
    {
        public int? FromRankId { get; set; }
        public int? RouteId { get; set; }
        public DateTime? FromDate { get; set; }
        public DateTime? ToDate { get; set; }
        public string Status { get; set; }
        public List<IdName> RankOptions { get; set; } = new List<IdName>();
        public List<IdName> RouteOptions { get; set; } = new List<IdName>();
        public List<IdName> StatusOptions { get; set; } = new List<IdName>();
        public List<DepartureRowVM> Rows { get; set; } = new List<DepartureRowVM>();
        public PaginationVM Page { get; set; } = new PaginationVM();
    }

    public class PassengerCreateVM
    {
        public int PassengerId { get; set; }
        [Required, StringLength(150)]
        public string FullName { get; set; }
        [Required, StringLength(30)]
        public string Phone { get; set; }
        [StringLength(150)]
        public string NextOfKinName { get; set; }
        [StringLength(30)]
        public string NextOfKinPhone { get; set; }
    }

    public class BookingSeatLineVM
    {
        [Range(1, 1000)]
        public int SeatNo { get; set; }
    }

    public class BookingCreateVM
    {
        public int BookingId { get; set; }
        [Required]
        public int DepartureId { get; set; }
        public string DepartureTime { get; set; } 
        public string RouteName { get; set; }
        public PassengerCreateVM Passenger { get; set; } = new PassengerCreateVM();
        [Range(1, 1000)]
        public int SeatsCount { get; set; }
        [Range(0, 1000000)]
        public decimal FareEach { get; set; }
        [Required, RegularExpression("Cash|Card")]
        public string PaymentMethod { get; set; }
        [RegularExpression("Held|Confirmed|Cancelled")]
        public string Status { get; set; } = "Held";
        public string Pnr { get; set; }
        public DateTime? ExpiresAt { get; set; }
        public List<BookingSeatLineVM> Seats {  get; set; } = new List<BookingSeatLineVM>();
        public List<SeatCellVM> AvailableSeats { get; set; } = new List<SeatCellVM>();
    }

    public class BookingRowVM
    {
        public int BookingId { get; set; }
        public string Pnr { get; set; }
        public string PassengerName { get; set; }
        public int SeatsCount { get; set; }
        public decimal FareEach { get; set; }
        public decimal ExtendedAmount { get; set; }
        public string Status { get; set; }
        public string PaymentMethod { get; set; }
    }

    public class BookingDetailVM
    {
        public int BookingId { get; set; }
        public string Pnr { get; set; }
        public string RouteName { get; set; }
        public DateTime DepartureDate { get; set; }
        public TimeSpan PlannedTime { get; set; }
        public string PassengerName { get; set; }
        public string PassengerPhone { get; set; }
        public int SeatsCount { get; set; }
        public decimal FareEach { get; set; }
        public decimal ExtendedAmount { get; set; }
        public string Status { get; set; }
        public string PaymentMethod { get; set; }
        public List<int> SeatNumbers { get; set; } = new List<int>();
        public List<PaymentRowVM> Payments { get; set; } = new List<PaymentRowVM>();
    }

    public class BookingListVM
    {
        public string Pnr { get; set; }
        public string PassengerSearch { get; set; }
        public string Status { get; set; }
        public string PaymentMethod { get; set; }
        public DateTime? FromDate { get; set; }
        public DateTime? ToDate { get; set; }
        public int? RouteId { get; set; }
        public List<IdName> StatusOptions { get; set; } = new List<IdName>();
        public List<IdName> PaymentOptions { get; set; } = new List<IdName>();
        public List<IdName> RouteOptions { get; set; } = new List<IdName>();
        public List<BookingRowVM> Rows { get; set; } = new List<BookingRowVM>();
        public PaginationVM Page { get; set; } = new PaginationVM();
    }

    public class PaymentCreateVM
    {
        [Required]
        public int BookingId { get; set; }
        [Range(0, 1000000)]
        public decimal Amount { get; set; }
        [Required, RegularExpression("Cash|Card")]
        public string Method { get; set; }
        public string Reference { get; set; }
    }

    public class PaymentRowVM
    {
        public int PaymentId { get; set; }
        public DateTime PaidAt { get; set; }
        public decimal Amount { get; set; }
        public string Method { get; set; }
        public string Reference { get; set; }
    }

    public class FloatIssueVM
    {
        [Required]
        public int DepartureId { get; set; }
        [Range(0, 1000000)]
        public decimal AmountIssued { get; set; }
        [Required, RegularExpression("Fuel|Toll|Other")]
        public string Purpose { get; set; }
    }

    public class FloatRowVM
    {
        public int FloatId { get; set; }
        public decimal AmountIssued { get; set; }
        public string Purpose { get; set; }
        public DateTime Timestamp { get; set; }
    }

    public class CashupCreateVM
    {
        [Required]
        public int DepartureId { get; set; }
        [Range(0, 1000000)]
        public decimal CountedAmount { get; set; }
    }

    public class CashupRowVM
    {
        public int BagId { get; set; }
        public decimal CountedAmount { get; set; }
        public DateTime Timestamp { get; set; }
        public string CountedBy { get; set; }
    }

    public class ExpenseCreateVM
    {
        [Required]
        public int DepartureId { get; set; }
        [Required, RegularExpression("Fuel|Toll|Fine|Other")]
        public string Type { get; set; }
        [Range(0, 1000000)]
        public decimal Amount { get; set; }
        public int? ProofAttachmentId { get; set; }
        public string FileName { get; set; }
        public byte[] FileData { get; set; }
    }

    public class ExpenseRowVM
    {
        public int ExpenseId { get; set; }
        public string Type { get; set; }
        public decimal Amount { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class IncidentCreateVM
    {
        [Required]
        public int DepartureId { get; set; }
        [Required, RegularExpression("Delay|Breakdown|RouteChange|Other")]
        public string Type { get; set; }
        [StringLength(1000)]
        public string Notes { get; set; }
    }

    public class IncidentRowVM
    {
        public int IncidentId { get; set; }
        public string Type { get; set; }
        public string Notes { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class AlertRuleEditVM
    {
        public int AlertRuleId { get; set; }
        [Range(0, 1)]
        public decimal MinFillComplianceThreshold { get; set; }
        [Range(0, 1000)]
        public int LateDepartureMinutes { get; set; }
        [Range(0, 1000000)]
        public decimal CashVarianceThreshold { get; set; }
        [Range(0, 1000)]
        public int IncidentSpikeThreshold { get; set; }
    }

    public class AlertRowVM
    {
        public int AlertId { get; set; }
        public string Type { get; set; }
        public string Entity { get; set; }
        public int? EntityId { get; set; }
        public string Severity { get; set; }
        public string Message { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? ResolvedAt { get; set; }
    }

    public class AlertListVM
    {
        public string Type { get; set; }
        public string Severity { get; set; }
        public string Status { get; set; }
        public DateTime? FromDate { get; set; }
        public DateTime? ToDate { get; set; }
        public List<IdName> TypeOptions { get; set; } = new List<IdName>();
        public List<IdName> SeverityOptions { get; set; } = new List<IdName>();
        public List<IdName> StatusOptions { get; set; } = new List<IdName>();
        public List<AlertRowVM> Rows { get; set; } = new List<AlertRowVM>();
        public PaginationVM Page { get; set; } = new PaginationVM();
    }

    public class ApprovalActionVM
    {
        [Required]
        public int ApprovalId { get; set; }
        [Required, RegularExpression("Approved|Rejected")]
        public string Decision { get; set; }
        [StringLength(500)]
        public string Notes { get; set; }
    }

    public class ApprovalRowVM
    {
        public int ApprovalId { get; set; }
        public string Entity { get; set; }
        public int EntityId { get; set; }
        public string Action { get; set; }
        public string Status { get; set; }
        public string RequestedBy { get; set; }
        public DateTime RequestedAt { get; set; }
    }

    public class ApprovalListVM
    {
        public string Status { get; set; } = "Pending";
        public List<IdName> StatusOptions { get; set; } = new List<IdName>();
        public List<ApprovalRowVM> Rows { get; set; } = new List<ApprovalRowVM>();
        public PaginationVM Page { get; set; } = new PaginationVM();
    }

    public class CharterRequestVM
    {
        public int CharterId { get; set; }
        [Required, StringLength(200)]
        public string Organizer { get; set; }
        [Required, StringLength(100)]
        public string Contact { get; set; }
        [Required]
        public DateTime Date { get; set; }
        public CoordinateVM Pickup { get; set; } = new CoordinateVM();
        public CoordinateVM Dropoff { get; set; } = new CoordinateVM();
        [Range(1, 1000)]
        public int Pax { get; set; }
        [RegularExpression("Requested|Quoted|Approved|Assigned|InProgress|Completed|Cancelled")]
        public string Status { get; set; } = "Requested";
        [Range(0, 100000000)]
        public decimal QuoteAmount { get; set; }
        public bool DepositPaid { get; set; }
        public List<CharterPassengerVM> Passengers { get; set; } = new List<CharterPassengerVM>();
        public List<IdName> VehicleOptions { get; set; } = new List<IdName>();
        public List<IdName> DriverOptions { get; set; } = new List<IdName>();
        public List<CharterVehicleAssignVM> Assignments { get; set; } = new List<CharterVehicleAssignVM>();
    }

    public class CharterPassengerVM
    {
        public int CharterPassengerId { get; set; }
        [Required, StringLength(150)]
        public string FullName { get; set; }
        [StringLength(30)]
        public string Phone { get; set; }
    }

    public class CharterVehicleAssignVM
    {
        public int CharterVehicleId { get; set; }
        public int CharterId { get; set; }
        [Required]
        public int VehicleId { get; set; }
        [Required]
        public int DriverId { get; set; }
    }

    public class CharterRowVM
    {
        public int CharterId { get; set; }
        public string Organizer { get; set; }
        public DateTime Date { get; set; }
        public string Status { get; set; }
        public decimal QuoteAmount { get; set; }
        public bool DepositPaid { get; set; }
    }

    public class CharterListVM
    {
        public string Status { get; set; }
        public DateTime? FromDate { get; set; }
        public DateTime? ToDate { get; set; }
        public List<IdName> StatusOptions { get; set; } = new List<IdName>();
        public List<CharterRowVM> Rows { get; set; } = new List<CharterRowVM>();
        public PaginationVM Page { get; set; } = new PaginationVM();
    }

    public class SettlementGenerateVM
    {
        [Required]
        public int OwnerId { get; set; }
        [Required]
        public string OwnerName { get; set; }
        public DateTime PeriodDay { get; set; }
        public List<SettlementRowVM> Settlement { get; set; }
        public List<IdName> OwnerOptions { get; set; } = new List<IdName>();
    }

    public class SettlementRowVM
    {
        public int SettlementId { get; set; }
        public string OwnerName { get; set; }
        public DateTime PeriodDay { get; set; }
        public decimal Gross { get; set; }
        public decimal Expenses { get; set; }
        public decimal DriverShare { get; set; }
        public decimal ManagerShare { get; set; }
        public decimal NetToOwner { get; set; }
    }

    public class SettlementListVM
    {
        public int? OwnerId { get; set; }
        public DateTime? FromDay { get; set; }
        public DateTime? ToDay { get; set; }
        public List<IdName> OwnerOptions { get; set; } = new List<IdName>();
        public List<SettlementRowVM> Rows { get; set; } = new List<SettlementRowVM>();
        public PaginationVM Page { get; set; } = new PaginationVM();
    }

    public class ReportingExportVM
    {
        public DateTime? FromDate { get; set; }
        public DateTime? ToDate { get; set; }
        public int? RouteId { get; set; }
        public int? RankId { get; set; }
        public List<IdName> RouteOptions { get; set; } = new List<IdName>();
        public List<IdName> RankOptions { get; set; } = new List<IdName>();
    }

    public class TelemetryPointVM
    {
        public DateTime Timestamp { get; set; }
        public decimal Latitude { get; set; }
        public decimal Longitude { get; set; }
        public decimal? SpeedKph { get; set; }
        public int? Battery { get; set; }
    }

    public class TelemetryListVM
    {
        public int DepartureId { get; set; }
        public List<TelemetryPointVM> Points { get; set; } = new List<TelemetryPointVM>();
    }

    public class AuditRowVM
    {
        public int AuditLogId { get; set; }
        public string EntityType { get; set; }
        public int EntityId { get; set; }
        public string Field { get; set; }
        public string OldValue { get; set; }
        public string NewValue { get; set; }
        public string UserName { get; set; }
        public DateTime Timestamp { get; set; }
    }

    public class AuditListVM
    {
        public string EntityType { get; set; }
        public int? EntityId { get; set; }
        public DateTime? FromDate { get; set; }
        public DateTime? ToDate { get; set; }
        public List<AuditRowVM> Rows { get; set; } = new List<AuditRowVM>();
        public PaginationVM Page { get; set; } = new PaginationVM();
    }

    public class MapsSearchVM
    {
        public string Query { get; set; }
        public List<CoordinateVM> Results { get; set; } = new List<CoordinateVM>();
    }

    public class EmailNotifyVM
    {
        [Required, StringLength(200)]
        public string ToEmail { get; set; }
        [Required, StringLength(200)]
        public string Subject { get; set; }
        [Required]
        public string HtmlBody { get; set; }
    }

    public class DashboardVM
    {
        public int ActiveDepartures { get; set; }
        public int BookingsToday { get; set; }
        public decimal CashCollectedToday { get; set; }
        public int AlertsOpen { get; set; }
        public List<DepartureRowVM> NextDepartures { get; set; } = new List<DepartureRowVM>();
        public List<AlertRowVM> RecentAlerts { get; set; } = new List<AlertRowVM>();
    }


    public class RankDashboardVM
    {
        public int? RankId { get; set; }
        public List<IdName> RankOptions { get; set; } = new List<IdName>();
        public DateTime Date { get; set; }
        public int TotalDepartures { get; set; }
        public int BoardingCount { get; set; }
        public int ReadyCount { get; set; }
        public int DepartedCount { get; set; }
        public List<DepartureRowVM> Departures { get; set; } = new List<DepartureRowVM>();
    }

    public class TicketVM
    {
        public string Pnr { get; set; }
        public string Passenger { get; set; }
        public string Route { get; set; }
        public DateTime DepartureDate { get; set; }
        public TimeSpan Time { get; set; }
        public string Seats { get; set; }
        public decimal Fare { get; set; }
        public decimal Total { get; set; }
        public string VehicleReg { get; set; }
        public string DriverName { get; set; }

        public List<DepartureRowVM> Vehicles { get; set; } = new List<DepartureRowVM>();
        public List<IdName> DriverOptions { get; set; } = new List<IdName>();
    }

    public class OwnerDashboardVM
    {
        public DateTime Date { get; set; }
        public int VehiclesCount { get; set; }
        public decimal PaymentsToday { get; set; }
        public decimal GrossLast7Days { get; set; }
        public decimal ExpensesLast7Days { get; set; }
        public decimal NetLast7Days { get; set; }
        public decimal GrossMonthToDate { get; set; }
        public List<DepartureRowVM> UpcomingDepartures { get; set; } = new List<DepartureRowVM>();
        public List<PaymentRowVM> RecentPayments { get; set; } = new List<PaymentRowVM>();
        public List<IncidentRowVM> RecentIncidents { get; set; } = new List<IncidentRowVM>();
    }

    public class OwnerVehicleListVM
    {
        public string Search { get; set; }
        public List<VehicleRowVM> Rows { get; set; } = new List<VehicleRowVM>();
    }

    public class OwnerDeparturesVM
    {
        public DateTime? FromDate { get; set; }
        public DateTime? ToDate { get; set; }
        public List<DepartureRowVM> Rows { get; set; } = new List<DepartureRowVM>();
    }

    public class OwnerBookingsVM
    {
        public DateTime? FromDate { get; set; }
        public DateTime? ToDate { get; set; }
        public string Pnr { get; set; }
        public List<BookingRowVM> Rows { get; set; } = new List<BookingRowVM>();
    }

    public class DriverDashboardVM
    {
        public DateTime Date { get; set; }
        public int TodayDeparturesCount { get; set; }
        public int TicketsConfirmedToday { get; set; }
        public int IncidentsToday { get; set; }
        public decimal CashCollectedToday { get; set; }
        public List<DepartureRowVM> TodayDepartures { get; set; } = new List<DepartureRowVM>();
        public List<PaymentRowVM> RecentPayments { get; set; } = new List<PaymentRowVM>();
    }

    public class DriverScheduleVM
    {
        public DateTime? FromDate { get; set; }
        public DateTime? ToDate { get; set; }
        public List<DepartureRowVM> Rows { get; set; } = new List<DepartureRowVM>();
    }

    public class CustomerDashboardVM
    {
        public DateTime Date { get; set; }
        public int UpcomingBookings { get; set; }
        public int TicketsConfirmedToday { get; set; }
        public decimal CashPaidToday { get; set; }
        public List<BookingRowVM> MyRecentBookings { get; set; } = new List<BookingRowVM>();
        public List<DepartureRowVM> SuggestedDepartures { get; set; } = new List<DepartureRowVM>();
    }

    public class CustomerSearchVM
    {
        public int? RouteId { get; set; }
        public DateTime Date { get; set; }
        public List<IdName> RouteOptions { get; set; } = new List<IdName>();
        public List<CustomerSearchRowVM> Rows { get; set; } = new List<CustomerSearchRowVM>();
    }

    public class CustomerSearchRowVM
    {
        public int DepartureId { get; set; }
        public DateTime Date { get; set; }
        public TimeSpan PlannedTime { get; set; }
        public string RouteName { get; set; }
        public string VehicleReg { get; set; }
        public int SeatsFree { get; set; }
        public decimal Fare { get; set; }
        public string Status { get; set; }
    }

    public class CustomerProfileVM
    {
        [Required]
        public string DisplayName { get; set; }
        [Required]
        public string Phone { get; set; }
        [EmailAddress]
        public string Email { get; set; }
    }

    public class EventRequestVM
    {
        public int CharterId { get; set; }
        [Required]
        public DateTime Date { get; set; }
        [Range(1, 100)]
        public int TaxiCount { get; set; }
        public bool ReturnTrip { get; set; }
        [Required, StringLength(50)]
        public string EventType { get; set; }
        public string PickupPlaceId { get; set; }
        public decimal? PickupLat { get; set; }
        public decimal? PickupLng { get; set; }
        public string DropPlaceId { get; set; }
        public decimal? DropLat { get; set; }
        public decimal? DropLng { get; set; }
        [Range(1, 2000)]
        public int Pax { get; set; }
        public string Notes { get; set; }
    }

    public class EventRowVM
    {
        public int CharterId { get; set; }
        public DateTime Date { get; set; }
        public string EventType { get; set; }
        public int TaxiCount { get; set; }
        public bool ReturnTrip { get; set; }
        public string Status { get; set; }
        public decimal QuoteAmount { get; set; }
        public bool IsPaid { get; set; }
    }

    public class CustomerEventsListVM
    {
        public List<EventRowVM> Rows { get; set; } = new List<EventRowVM>();
    }

    public class EventPaymentVM
    {
        public int CharterId { get; set; }
        public decimal Amount { get; set; }
        public string StripePublishableKey { get; set; }
        public string ClientSecret { get; set; }
    }

    public class ManagerEventListVM
    {
        public string Status { get; set; }
        public List<EventRowVM> Rows { get; set; } = new List<EventRowVM>();
    }

    public class ManagerEventDetailVM
    {
        public int CharterId { get; set; }
        public string Organizer { get; set; }
        public string Contact { get; set; }
        public DateTime Date { get; set; }
        public int TaxiCount { get; set; }
        public bool ReturnTrip { get; set; }
        public string EventType { get; set; }
        public string Status { get; set; }
        public decimal QuoteAmount { get; set; }
        public bool IsPaid { get; set; }
        public string PickupPlaceId { get; set; }
        public decimal? PickupLat { get; set; }
        public decimal? PickupLng { get; set; }
        public string DropPlaceId { get; set; }
        public decimal? DropLat { get; set; }
        public decimal? DropLng { get; set; }
    }

    public class ManagerEventApproveVM
    {
        [Required]
        public int CharterId { get; set; }
        [Range(0, 100000000)]
        public decimal QuoteAmount { get; set; }
        [Required, RegularExpression("Approved|Rejected")]
        public string Decision { get; set; }
    }

    public class ManagerEventAssignVM
    {
        [Required]
        public int CharterId { get; set; }
        public int RequiredTaxis { get; set; }
        public List<IdName> VehicleOptions { get; set; } = new List<IdName>();
        public List<IdName> DriverOptions { get; set; } = new List<IdName>();
        public List<AssignRow> Assignments { get; set; } = new List<AssignRow>();
        public class AssignRow
        {
            public int? VehicleId { get; set; }
            public int? DriverId { get; set; }
        }
    }

}
