using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data.Entity;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Mail;
using System.Text;
using System.Web;
using System.Web.Mvc;
using TaxiRank.Models;
using TaxiRank.Models.ViewModels;

namespace TaxiRank.Controllers
{
    public class OwnerController : Controller
    {
        private readonly TaxiRankDb3Entities2 _db = new TaxiRankDb3Entities2();

        private bool IsLoggedIn()
        {
            return Session["UserId"] != null && Convert.ToInt32(Session["UserId"]) > 0;
        }

        private int CurrentUserId()
        {
            return Session["UserId"] == null ? 0 : Convert.ToInt32(Session["UserId"]);
        }

        private string CurrentRole()
        {
            return Session["Role"] == null ? "" : Session["Role"].ToString();
        }

        private ActionResult GuardOwner()
        {
            if (!IsLoggedIn()) return RedirectToAction("Login", "Account");
            if (CurrentRole() != "Owner") return RedirectToAction("Login", "Account");
            return null;
        }

        private FileContentResult Csv(string fileName, string csv)
        {
            var bytes = Encoding.UTF8.GetBytes(csv);
            return File(bytes, "text/csv", fileName);
        }

        private void SendMail(string to, string subject, string html)
        {
            if (string.IsNullOrWhiteSpace(to)) return;
            var from = "mandlakanozulu@gmail.com";
            var pass = "ymcnugshpifyttas";
            using (var msg = new MailMessage())
            {
                msg.From = new MailAddress(from, "TaxiRank");
                msg.To.Add(new MailAddress(to));
                msg.Subject = subject;
                msg.Body = html;
                msg.IsBodyHtml = true;
                using (var smtp = new SmtpClient("smtp.gmail.com", 587))
                {
                    smtp.EnableSsl = true;
                    smtp.Credentials = new NetworkCredential(from, pass);
                    smtp.Send(msg);
                }
            }
        }

        private List<int> OwnerVehicleIds(int ownerId)
        {
            return _db.Vehicles.Where(v => v.OwnerId == ownerId && v.Active).Select(v => v.VehicleId).ToList();
        }
        private List<IdName> UserOptions(string role = null)
        {
            var q = _db.Users.Where(x => x.IsActive);
            if (!string.IsNullOrWhiteSpace(role)) q = q.Where(x => x.Role == role);
            return q.OrderBy(x => x.DisplayName).Select(x => new IdName { Id = x.UserId, Name = x.DisplayName }).ToList();
        }
        private List<IdName> DriverOptions()
        {
            return _db.Users.Where(x => x.IsActive && x.Role == "Driver").OrderBy(x => x.DisplayName).Select(x => new IdName { Id = x.UserId, Name = x.DisplayName }).ToList();
        }

        private List<IdName> VehicleOptionsForOwner()
        {
            var uid = CurrentUserId();
            return _db.Vehicles.Where(v => v.OwnerId == uid && v.Active).OrderBy(v => v.RegNo).Select(v => new IdName { Id = v.VehicleId, Name = v.RegNo }).ToList();
        }

        #region Dashboard
        [HttpGet]
        public ActionResult Dashboard()
        {
            var guard = GuardOwner();
            if (guard != null) return guard;
            var uid = CurrentUserId();
            var vids = OwnerVehicleIds(uid);
            var today = DateTime.Today;
            var start7 = today.AddDays(-6);
            var startMonth = new DateTime(today.Year, today.Month, 1);
            var depIdsToday = _db.Departures.Where(d => vids.Contains(d.VehicleId) && d.Date == today).Select(d => d.DepartureId).ToList();
            var depIds7 = _db.Departures.Where(d => vids.Contains(d.VehicleId) && d.Date >= start7 && d.Date <= today).Select(d => d.DepartureId).ToList();
            var depIdsMonth = _db.Departures.Where(d => vids.Contains(d.VehicleId) && d.Date >= startMonth && d.Date <= today).Select(d => d.DepartureId).ToList();
            var paymentsToday = _db.Payments.Where(p => _db.Bookings.Any(b => b.BookingId == p.BookingId && depIdsToday.Contains(b.DepartureId))).Select(p => (decimal?)p.Amount).DefaultIfEmpty(0).Sum().GetValueOrDefault();
            var gross7 = _db.Payments.Where(p => _db.Bookings.Any(b => b.BookingId == p.BookingId && depIds7.Contains(b.DepartureId))).Select(p => (decimal?)p.Amount).DefaultIfEmpty(0).Sum().GetValueOrDefault();
            var grossMonth = _db.Payments.Where(p => _db.Bookings.Any(b => b.BookingId == p.BookingId && depIdsMonth.Contains(b.DepartureId))).Select(p => (decimal?)p.Amount).DefaultIfEmpty(0).Sum().GetValueOrDefault();
            var expenses7 = _db.Expenses.Where(e => depIds7.Contains(e.DepartureId)).Select(e => (decimal?)e.Amount).DefaultIfEmpty(0).Sum().GetValueOrDefault();
            var upcoming = _db.Departures.Where(d => vids.Contains(d.VehicleId) && d.Date >= today).OrderBy(d => d.Date).ThenBy(d => d.PlannedTime).Take(8).ToList();
            var recentPays = _db.Payments.Where(p => _db.Bookings.Any(b => b.BookingId == p.BookingId && vids.Contains(_db.Departures.Where(d => d.DepartureId == b.DepartureId).Select(d => d.VehicleId).FirstOrDefault()))).OrderByDescending(p => p.PaidAt).Take(10).ToList();
            var incidentIds = _db.Incidents.Where(i => vids.Contains(_db.Departures.Where(d => d.DepartureId == i.DepartureId).Select(d => d.VehicleId).FirstOrDefault())).OrderByDescending(i => i.CreatedAt).Take(10).ToList();
            var vm = new OwnerDashboardVM
            {
                Date = today,
                VehiclesCount = vids.Count,
                PaymentsToday = paymentsToday,
                GrossLast7Days = gross7,
                ExpensesLast7Days = expenses7,
                NetLast7Days = gross7 - expenses7,
                GrossMonthToDate = grossMonth,
                UpcomingDepartures = upcoming.Select(x => new DepartureRowVM
                {
                    DepartureId = x.DepartureId,
                    Date = x.Date,
                    PlannedTime = x.PlannedTime,
                    RouteName = _db.Routes.Where(r => r.RouteId == x.RouteId).Select(r => r.ToName).FirstOrDefault(),
                    VehicleReg = _db.Vehicles.Where(v => v.VehicleId == x.VehicleId).Select(v => v.RegNo).FirstOrDefault(),
                    DriverName = _db.Users.Where(u => u.UserId == x.DriverId).Select(u => u.DisplayName).FirstOrDefault(),
                    MinFill = x.MinFill,
                    Status = x.Status,
                    SeatsTotal = _db.Vehicles.Where(v => v.VehicleId == x.VehicleId).Select(v => (int?)v.Seats).FirstOrDefault().GetValueOrDefault(),
                    SeatsBooked = _db.SeatInventories.Count(s => s.DepartureId == x.DepartureId && s.Status == "Booked")
                }).ToList(),
                RecentPayments = recentPays.Select(p => new PaymentRowVM
                {
                    PaymentId = p.PaymentId,
                    PaidAt = p.PaidAt,
                    Amount = p.Amount,
                    Method = p.Method,
                    Reference = p.Reference
                }).ToList(),
                RecentIncidents = incidentIds.Select(i => new IncidentRowVM
                {
                    IncidentId = i.IncidentId,
                    Type = i.Type,
                    Notes = i.Notes,
                    CreatedAt = i.CreatedAt
                }).ToList()
            };
            return View(vm);
        }
        #endregion

        #region Vehicles
        [HttpGet]
        public ActionResult Vehicles(string search = null)
        {
            var guard = GuardOwner();
            if (guard != null) return guard;
            var uid = CurrentUserId();
            var q = from v in _db.Vehicles
                    where v.OwnerId == uid
                    join r in _db.Ranks on v.HomeRankId equals r.RankId into rr
                    from r in rr.DefaultIfEmpty()
                    select new { v, r };
            if (!string.IsNullOrWhiteSpace(search))
            {
                var s = search.ToLower();
                q = q.Where(x => x.v.RegNo.ToLower().Contains(s) || (x.v.Make ?? "").ToLower().Contains(s) || (x.v.Model ?? "").ToLower().Contains(s));
            }
            var vm = new OwnerVehicleListVM
            {
                Search = search,
                Rows = q.OrderBy(x => x.v.RegNo).ToList().Select(x => new VehicleRowVM
                {
                    VehicleId = x.v.VehicleId,
                    RegNo = x.v.RegNo,
                    Make = x.v.Make,
                    Model = x.v.Model,
                    Seats = x.v.Seats,
                    HomeRank = x.r == null ? "" : x.r.Name,
                    OwnerName = "",
                    Active = x.v.Active
                }).ToList()
            };
            return View(vm);
        }

        [HttpGet]
        public ActionResult VehicleDetail(int id)
        {
            var guard = GuardOwner();
            if (guard != null) return guard;
            var uid = CurrentUserId();
            var v = _db.Vehicles.FirstOrDefault(x => x.VehicleId == id && x.OwnerId == uid);
            if (v == null) return HttpNotFound();
            var docs = _db.VehicleDocuments.Where(d => d.VehicleId == id).OrderByDescending(d => d.UploadedAt).ToList();
            ViewBag.Vehicle = v;
            ViewBag.Documents = docs;
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult VehicleDocUpload(VehicleDocumentUploadVM model, HttpPostedFileBase upload)
        {
            var guard = GuardOwner();
            if (guard != null) return guard;
            var uid = CurrentUserId();
            var v = _db.Vehicles.FirstOrDefault(x => x.VehicleId == model.VehicleId && x.OwnerId == uid);
            if (v == null) return HttpNotFound();
            if (upload != null && upload.ContentLength > 0)
            {
                using (var ms = new MemoryStream())
                {
                    upload.InputStream.CopyTo(ms);
                    var doc = new VehicleDocument { VehicleId = model.VehicleId, DocType = model.DocType, FileName = Path.GetFileName(upload.FileName), FileData = ms.ToArray(), UploadedAt = DateTime.Now, UploadedBy = uid };
                    _db.VehicleDocuments.Add(doc);
                    _db.SaveChanges();
                }
            }
            return RedirectToAction("VehicleDetail", new { id = model.VehicleId });
        }

        [HttpGet]
        public ActionResult VehicleDocDownload(int id)
        {
            var guard = GuardOwner();
            if (guard != null) return guard;
            var uid = CurrentUserId();
            var d = _db.VehicleDocuments.FirstOrDefault(x => x.VehicleDocumentId == id && _db.Vehicles.Any(v => v.VehicleId == x.VehicleId && v.OwnerId == uid));
            if (d == null) return HttpNotFound();
            return File(d.FileData, "application/octet-stream", d.FileName);
        }
        #endregion

        #region Assignments
        [HttpGet]
        public ActionResult Assignments()
        {
            var guard = GuardOwner();
            if (guard != null) return guard;
            var uid = CurrentUserId();
            var vids = OwnerVehicleIds(uid);
            var rows = _db.DriverAssignments.Where(a => vids.Contains(a.VehicleId)).OrderByDescending(a => a.StartDate).ToList();
            ViewBag.Rows = rows;
            return View();
        }

        [HttpGet]
        public ActionResult AssignmentCreate()
        {
            var guard = GuardOwner();
            if (guard != null) return guard;
            var vm = new DriverAssignmentCreateVM { DriverOptions = DriverOptions(), VehicleOptions = VehicleOptionsForOwner(), StartDate = DateTime.Today };
            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult AssignmentCreate(DriverAssignmentCreateVM model)
        {
            var guard = GuardOwner();
            if (guard != null) return guard;
            model.DriverOptions = DriverOptions();
            model.VehicleOptions = VehicleOptionsForOwner();
            if (!ModelState.IsValid) return View(model);
            var uid = CurrentUserId();
            var owns = _db.Vehicles.Any(v => v.VehicleId == model.VehicleId && v.OwnerId == uid);
            if (!owns) return View(model);
            var a = new DriverAssignment { DriverId = model.DriverId, VehicleId = model.VehicleId, StartDate = model.StartDate.Date, EndDate = model.EndDate, CreatedAt = DateTime.Now, CreatedBy = uid };
            _db.DriverAssignments.Add(a);
            _db.SaveChanges();
            return RedirectToAction("Assignments");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult AssignmentEnd(int id, DateTime endDate)
        {
            var guard = GuardOwner();
            if (guard != null) return guard;
            var uid = CurrentUserId();
            var a = _db.DriverAssignments.FirstOrDefault(x => x.AssignmentId == id && _db.Vehicles.Any(v => v.VehicleId == x.VehicleId && v.OwnerId == uid));
            if (a == null) return HttpNotFound();
            a.EndDate = endDate.Date;
            _db.SaveChanges();
            return RedirectToAction("Assignments");
        }
        #endregion

        #region Departures
        [HttpGet]
        public ActionResult Departures(DateTime? from = null, DateTime? to = null)
        {
            var guard = GuardOwner();
            if (guard != null) return guard;
            var uid = CurrentUserId();
            var vids = OwnerVehicleIds(uid);
            var vm = new OwnerDeparturesVM { FromDate = from, ToDate = to };
            var q = _db.Departures.Where(d => vids.Contains(d.VehicleId));
            if (from.HasValue) q = q.Where(d => d.Date >= from.Value.Date);
            if (to.HasValue) q = q.Where(d => d.Date <= to.Value.Date);
            vm.Rows = q.OrderByDescending(x => x.Date).ThenBy(x => x.PlannedTime).ToList().Select(x => new DepartureRowVM
            {
                DepartureId = x.DepartureId,
                Date = x.Date,
                PlannedTime = x.PlannedTime,
                RouteName = _db.Routes.Where(r => r.RouteId == x.RouteId).Select(r => r.ToName).FirstOrDefault(),
                VehicleReg = _db.Vehicles.Where(v => v.VehicleId == x.VehicleId).Select(v => v.RegNo).FirstOrDefault(),
                DriverName = _db.Users.Where(u => u.UserId == x.DriverId).Select(u => u.DisplayName).FirstOrDefault(),
                MinFill = x.MinFill,
                Status = x.Status,
                SeatsTotal = _db.Vehicles.Where(v => v.VehicleId == x.VehicleId).Select(v => (int?)v.Seats).FirstOrDefault().GetValueOrDefault(),
                SeatsBooked = _db.SeatInventories.Count(s => s.DepartureId == x.DepartureId && s.Status == "Booked")
            }).ToList();
            return View(vm);
        }

        [HttpGet]
        public ActionResult DepartureDetail(int id)
        {
            var guard = GuardOwner();
            if (guard != null) return guard;
            var uid = CurrentUserId();
            var d = _db.Departures.FirstOrDefault(x => x.DepartureId == id && _db.Vehicles.Any(v => v.VehicleId == x.VehicleId && v.OwnerId == uid));
            if (d == null) return HttpNotFound();
            var vm = new DepartureDetailVM
            {
                DepartureId = d.DepartureId,
                RouteName = _db.Routes.Where(r => r.RouteId == d.RouteId).Select(r => r.ToName).FirstOrDefault(),
                Date = d.Date,
                PlannedTime = d.PlannedTime,
                VehicleReg = _db.Vehicles.Where(v => v.VehicleId == d.VehicleId).Select(v => v.RegNo).FirstOrDefault(),
                DriverName = _db.Users.Where(u => u.UserId == d.DriverId).Select(u => u.DisplayName).FirstOrDefault(),
                Status = d.Status,
                MinFill = d.MinFill,
                SeatMap = _db.SeatInventories.Where(s => s.DepartureId == id).OrderBy(s => s.SeatNo).Select(s => new SeatCellVM { SeatNo = s.SeatNo, Status = s.Status, BookingId = s.BookingId }).ToList(),
                Bookings = _db.Bookings.Where(b => b.DepartureId == id).OrderByDescending(b => b.CreatedAt).Select(b => new BookingRowVM { BookingId = b.BookingId, Pnr = b.Pnr, PassengerName = _db.Passengers.Where(p => p.PassengerId == b.PassengerId).Select(p => p.FullName).FirstOrDefault(), SeatsCount = b.SeatsCount, FareEach = b.FareEach, ExtendedAmount = (decimal)b.ExtendedAmount, Status = b.Status, PaymentMethod = b.PaymentMethod }).ToList(),
                Payments = _db.Payments.Where(p => _db.Bookings.Any(b => b.BookingId == p.BookingId && b.DepartureId == id)).OrderByDescending(p => p.PaidAt).Select(p => new PaymentRowVM { PaymentId = p.PaymentId, PaidAt = p.PaidAt, Amount = p.Amount, Method = p.Method, Reference = p.Reference }).ToList(),
                Expenses = _db.Expenses.Where(e => e.DepartureId == id).OrderByDescending(e => e.CreatedAt).Select(e => new ExpenseRowVM { ExpenseId = e.ExpenseId, Type = e.Type, Amount = e.Amount, CreatedAt = e.CreatedAt }).ToList(),
                Floats = _db.Floats.Where(f => f.DepartureId == id).OrderByDescending(f => f.Timestamp).Select(f => new FloatRowVM { FloatId = f.FloatId, AmountIssued = f.AmountIssued, Purpose = f.Purpose, Timestamp = f.Timestamp }).ToList(),
                Incidents = _db.Incidents.Where(i => i.DepartureId == id).OrderByDescending(i => i.CreatedAt).Select(i => new IncidentRowVM { IncidentId = i.IncidentId, Type = i.Type, Notes = i.Notes, CreatedAt = i.CreatedAt }).ToList()
            };
            return View(vm);
        }
        #endregion

        #region Bookings
        [HttpGet]
        public ActionResult Bookings(DateTime? from = null, DateTime? to = null, string pnr = null)
        {
            var guard = GuardOwner();
            if (guard != null) return guard;
            var uid = CurrentUserId();
            var vids = OwnerVehicleIds(uid);
            var depIds = _db.Departures.Where(d => vids.Contains(d.VehicleId)).Select(d => d.DepartureId).ToList();
            var vm = new OwnerBookingsVM { FromDate = from, ToDate = to, Pnr = pnr };
            var q = _db.Bookings.Where(b => depIds.Contains(b.DepartureId));
            if (from.HasValue) q = q.Where(b => b.CreatedAt >= from.Value);
            if (to.HasValue) q = q.Where(b => b.CreatedAt <= to.Value);
            if (!string.IsNullOrWhiteSpace(pnr)) q = q.Where(b => b.Pnr == pnr);
            vm.Rows = q.OrderByDescending(b => b.CreatedAt).ToList().Select(b => new BookingRowVM { BookingId = b.BookingId, Pnr = b.Pnr, PassengerName = _db.Passengers.Where(p => p.PassengerId == b.PassengerId).Select(p => p.FullName).FirstOrDefault(), SeatsCount = b.SeatsCount, FareEach = b.FareEach, ExtendedAmount = (decimal)b.ExtendedAmount, Status = b.Status, PaymentMethod = b.PaymentMethod }).ToList();
            return View(vm);
        }



        [HttpGet]
        public ActionResult BookingsCsv(DateTime? from = null, DateTime? to = null)
        {
            var guard = GuardOwner();
            if (guard != null) return guard;
            var uid = CurrentUserId();
            var vids = OwnerVehicleIds(uid);
            var depIds = _db.Departures.Where(d => vids.Contains(d.VehicleId)).Select(d => d.DepartureId).ToList();
            var q = _db.Bookings.Where(b => depIds.Contains(b.DepartureId));
            if (from.HasValue) q = q.Where(x => x.CreatedAt >= from.Value);
            if (to.HasValue) q = q.Where(x => x.CreatedAt <= to.Value);
            var rows = q.OrderBy(x => x.CreatedAt).ToList();
            var sb = new StringBuilder();
            sb.AppendLine("CreatedAt,PNR,Route,DepartureDate,Passenger,Seats,FareEach,Total,Status,Method");
            foreach (var b in rows)
            {
                var route = _db.Routes.Where(r => r.RouteId == _db.Departures.Where(d => d.DepartureId == b.DepartureId).Select(d => d.RouteId).FirstOrDefault()).Select(r => r.ToName).FirstOrDefault();
                var ddate = _db.Departures.Where(d => d.DepartureId == b.DepartureId).Select(d => d.Date).FirstOrDefault();
                var pax = _db.Passengers.Where(p => p.PassengerId == b.PassengerId).Select(p => p.FullName).FirstOrDefault();
                sb.AppendLine(string.Join(",", new[]
                {
                    b.CreatedAt.ToString("s"),
                    b.Pnr,
                    route,
                    ddate.ToString("yyyy-MM-dd"),
                    pax,
                    b.SeatsCount.ToString(),
                    b.FareEach.ToString("0.00"),
                    ((decimal)b.ExtendedAmount).ToString("0.00"),
                    b.Status,
                    b.PaymentMethod
                }.Select(x => "\"" + x.Replace("\"", "\"\"") + "\"")));
            }
            return Csv("owner_bookings.csv", sb.ToString());
        }
        #endregion

        #region Settlements

        [HttpGet]
        public ActionResult Settlements(int? ownerId = null, DateTime? from = null, DateTime? to = null)
        {
            var guard = GuardOwner();
            if (guard != null) return guard;
            var vm = new SettlementListVM { OwnerId = ownerId, FromDay = from, ToDay = to, OwnerOptions = UserOptions("Owner") };
            var q = _db.Settlements.AsQueryable();
            if (ownerId.HasValue) q = q.Where(x => x.OwnerId == ownerId.Value);
            if (from.HasValue) q = q.Where(x => x.PeriodDay >= from.Value);
            if (to.HasValue) q = q.Where(x => x.PeriodDay <= to.Value);
            vm.Rows = q.OrderByDescending(x => x.PeriodDay).ToList().Select(x => new SettlementRowVM { SettlementId = x.SettlementId, OwnerName = _db.Users.Where(u => u.UserId == x.OwnerId).Select(u => u.DisplayName).FirstOrDefault(), PeriodDay = x.PeriodDay, Gross = x.Gross, Expenses = x.Expenses, DriverShare = x.DriverShare, ManagerShare = x.ManagerShare, NetToOwner = x.NetToOwner }).ToList();
            return View(vm);
        }


        #endregion

        #region Payments
        [HttpGet]
        public ActionResult PaymentsCsv(DateTime? from = null, DateTime? to = null)
        {
            var guard = GuardOwner();
            if (guard != null) return guard;
            var uid = CurrentUserId();
            var vids = OwnerVehicleIds(uid);
            var depIds = _db.Departures.Where(d => vids.Contains(d.VehicleId)).Select(d => d.DepartureId).ToList();
            var q = _db.Payments.Where(p => _db.Bookings.Any(b => b.BookingId == p.BookingId && depIds.Contains(b.DepartureId)));
            if (from.HasValue) q = q.Where(p => p.PaidAt >= from.Value);
            if (to.HasValue) q = q.Where(p => p.PaidAt <= to.Value);
            var rows = q.OrderBy(p => p.PaidAt).ToList();
            var sb = new StringBuilder();
            sb.AppendLine("PaidAt,PNR,Amount,Method,Reference");
            foreach (var p in rows)
            {
                var pnr = _db.Bookings.Where(b => b.BookingId == p.BookingId).Select(b => b.Pnr).FirstOrDefault();
                sb.AppendLine(string.Join(",", new[]
                {
                    p.PaidAt.ToString("s"),
                    pnr,
                    p.Amount.ToString("0.00"),
                    p.Method,
                    p.Reference
                }.Select(x => "\"" + (x ?? "").Replace("\"", "\"\"") + "\"")));
            }
            return Csv("owner_payments.csv", sb.ToString());
        }
        #endregion

        #region Incidents
        [HttpGet]
        public ActionResult Incidents(DateTime? from = null, DateTime? to = null)
        {
            var guard = GuardOwner();
            if (guard != null) return guard;
            var uid = CurrentUserId();
            var vids = OwnerVehicleIds(uid);
            var q = _db.Incidents.Where(i => vids.Contains(_db.Departures.Where(d => d.DepartureId == i.DepartureId).Select(d => d.VehicleId).FirstOrDefault()));
            if (from.HasValue) q = q.Where(i => i.CreatedAt >= from.Value);
            if (to.HasValue) q = q.Where(i => i.CreatedAt <= to.Value);
            var rows = q.OrderByDescending(i => i.CreatedAt).Select(i => new IncidentRowVM { IncidentId = i.IncidentId, Type = i.Type, Notes = i.Notes, CreatedAt = i.CreatedAt }).ToList();
            ViewBag.Rows = rows;
            return View();
        }
        #endregion

        #region Notifications
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult NotifyDriver(int driverId, string subject, string message)
        {
            var guard = GuardOwner();
            if (guard != null) return guard;
            var email = _db.Users.Where(u => u.UserId == driverId && u.Role == "Driver").Select(u => u.Email).FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(email)) SendMail(email, subject, message);
            return RedirectToAction("Dashboard");
        }
        #endregion
    }
}
