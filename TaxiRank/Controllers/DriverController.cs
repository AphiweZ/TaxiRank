using Newtonsoft.Json.Linq;
using Stripe;
using Stripe.Checkout;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data.Entity;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Mail;
using System.Text;
using System.Web;
using System.Web.Mvc;
using TaxiRank.Models;
using TaxiRank.Models.ViewModels;

namespace TaxiRank.Controllers
{
    public class DriverController : Controller
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
        private List<IdName> RouteOptions()
        {
            return _db.Routes.Where(x => x.Active).OrderBy(x => x.FromRankId).ThenBy(x => x.ToName).Select(x => new IdName { Id = x.RouteId, Name = x.ToName }).ToList();
        }
        private void StripeInit()
        {
            StripeConfiguration.ApiKey = ConfigurationManager.AppSettings["StripeSecretKey"];
        }

        private string CurrentRole()
        {
            return Session["Role"] == null ? "" : Session["Role"].ToString();
        }

        private ActionResult GuardDriver()
        {
            if (!IsLoggedIn()) return RedirectToAction("Login", "Account");
            if (CurrentRole() != "Driver") return RedirectToAction("Login", "Account");
            return null;
        }


        private FileContentResult Csv(string fileName, string csv)
        {
            var bytes = Encoding.UTF8.GetBytes(csv);
            return File(bytes, "text/csv", fileName);
        }





        [HttpGet]
        public ActionResult Ticket(int id)
        {
            var guard = GuardDriver();
            if (guard != null) return guard;

            var b = _db.Bookings.FirstOrDefault(x => x.BookingId == id);
            if (b == null) return HttpNotFound();

        
            var departure = _db.Departures.FirstOrDefault(d => d.DepartureId == b.DepartureId);
            if (departure == null) return HttpNotFound();

           
            var route = _db.Routes
                .Where(r => r.RouteId == departure.RouteId)
                .Select(r => r.ToName)
                .FirstOrDefault();

       
            var vehicleRegNo = _db.Vehicles
                .Where(v => v.VehicleId == departure.VehicleId)
                .Select(v => v.RegNo)
                .FirstOrDefault() ?? "N/A";

            
            var seatNumbers = string.Join(",", _db.BookingSeats
                .Where(s => s.BookingId == b.BookingId)
                .OrderBy(s => s.SeatNo)
                .Select(s => s.SeatNo));

            
            var vm = new TicketVM
            {
                Pnr = b.Pnr,
                Passenger = _db.Passengers.Where(p => p.PassengerId == b.PassengerId).Select(p => p.FullName).FirstOrDefault(),
                Route = route,
                DepartureDate = departure.Date,
                Time = departure.PlannedTime,
                Seats = seatNumbers,
                Fare = b.FareEach,
                Total = (decimal)b.ExtendedAmount,
                VehicleReg = vehicleRegNo 
            };

            return View(vm);
        }



        [HttpGet]
        public ActionResult BookingCreate(int departureId)
        {

            var guard = GuardDriver();
            if (guard != null) return guard;
            var seats = _db.SeatInventories.Where(s => s.DepartureId == departureId).OrderBy(s => s.SeatNo).Select(s => new SeatCellVM { SeatNo = s.SeatNo, Status = s.Status, BookingId = s.BookingId }).ToList();
            var fare = _db.Routes.Join(_db.Departures, r => r.RouteId, d => d.RouteId, (r, d) => new { r, d }).Where(x => x.d.DepartureId == departureId).Select(x => x.r.DefaultFare).FirstOrDefault();
            var vm = new BookingCreateVM { DepartureId = departureId, SeatsCount = 1, FareEach = fare, PaymentMethod = "Cash", Status = "Held", AvailableSeats = seats };
            return View(vm);
        }



            [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult BookingCreate(BookingCreateVM model, int[] selectedSeats)
        {
          
            var guard = GuardDriver();
            if (guard != null) return guard;


          
            model.AvailableSeats = _db.SeatInventories
                .Where(s => s.DepartureId == model.DepartureId)
                .OrderBy(s => s.SeatNo)
                .Select(s => new SeatCellVM { SeatNo = s.SeatNo, Status = s.Status, BookingId = s.BookingId })
                .ToList();

            selectedSeats = selectedSeats ?? Array.Empty<int>();

            ModelState.Remove("SeatsCount");
            ModelState.Remove("FareEach");
            ModelState.Remove("PaymentMethod");
            ModelState.Remove("Status");


            model.SeatsCount = selectedSeats.Length;

           
            var fareEach = (from d in _db.Departures
                            join r in _db.Routes on d.RouteId equals r.RouteId
                            where d.DepartureId == model.DepartureId
                            select r.DefaultFare).FirstOrDefault();
            model.FareEach = fareEach;

            
            model.PaymentMethod = "Cash";
            model.Status = "Held";


            if (model.SeatsCount <= 0)
                ModelState.AddModelError(nameof(model.SeatsCount), "Please choose at least one seat.");
            if (string.IsNullOrWhiteSpace(model.Passenger?.FullName))
                ModelState.AddModelError("Passenger.FullName", "Passenger full name is required.");
            if (string.IsNullOrWhiteSpace(model.Passenger?.Phone))
                ModelState.AddModelError("Passenger.Phone", "Passenger phone is required.");


            if (!ModelState.IsValid) return View(model);


            using (var tx = _db.Database.BeginTransaction())
            {
              
                var freeOk = _db.SeatInventories.Count(s =>
                    s.DepartureId == model.DepartureId &&
                    selectedSeats.Contains(s.SeatNo) &&
                    s.Status == "Free") == selectedSeats.Length;

                if (!freeOk)
                {
                    ModelState.AddModelError("", "One or more selected seats are no longer available. Please reselect.");
                    return View(model);
                }


               
                var p = new Passenger
                {
                    FullName = model.Passenger.FullName,
                    Phone = model.Passenger.Phone,
                    NextOfKinName = model.Passenger.NextOfKinName,
                    NextOfKinPhone = model.Passenger.NextOfKinPhone,
                    CreatedAt = DateTime.Now,
                    CreatedBy = CurrentUserId()
                };
                _db.Passengers.Add(p);
                _db.SaveChanges(); 


                var b = new Booking
                {
                    DepartureId = model.DepartureId,
                    PassengerId = p.PassengerId,
                    SeatsCount = model.SeatsCount,
                    FareEach = model.FareEach,
                    PaymentMethod = model.PaymentMethod,
                    Status = "Confirmed", 
                    Pnr = "PNR" + DateTime.Now.Ticks.ToString().Substring(8),
                    ExpiresAt = null,
                    CreatedAt = DateTime.Now,
                    CreatedBy = CurrentUserId()
                };
                _db.Bookings.Add(b);
                _db.SaveChanges(); 


               
                foreach (var seat in selectedSeats)
                {
                    _db.BookingSeats.Add(new BookingSeat
                    {
                        BookingId = b.BookingId,
                        DepartureId = b.DepartureId,
                        SeatNo = seat,
                        CreatedAt = DateTime.Now,
                        CreatedBy = CurrentUserId()
                    });
                    var inv = _db.SeatInventories.First(s => s.DepartureId == b.DepartureId && s.SeatNo == seat);
                    inv.Status = "Booked";
                    inv.BookingId = b.BookingId;
                }
                _db.SaveChanges();


                
                _db.Payments.Add(new Payment
                {
                    BookingId = b.BookingId,
                    Amount = (decimal)b.ExtendedAmount,
                    Method = "Cash",
                    Reference = "CASH-" + b.Pnr,
                    PaidAt = DateTime.Now,
                    CreatedAt = DateTime.Now,
                    CreatedBy = CurrentUserId()
                });
                _db.SaveChanges();

                tx.Commit();

               
                return RedirectToAction("Ticket", new { id = b.BookingId });
            }
        }


        [HttpGet]

        public ActionResult Dashboard()
        {
            var guard = GuardDriver();
            if (guard != null) return guard;
            var uid = CurrentUserId();
            var today = DateTime.Today;
            var depsToday = _db.Departures.Where(d => d.DriverId == uid && d.Date == today).OrderBy(d => d.PlannedTime).ToList();
            var depIdsToday = depsToday.Select(x => x.DepartureId).ToList();
            var ticketsConfirmed = _db.Bookings.Count(b => depIdsToday.Contains(b.DepartureId) && b.Status == "Confirmed");
            var cashToday = _db.Payments.Where(p => _db.Bookings.Any(b => b.BookingId == p.BookingId && depIdsToday.Contains(b.DepartureId))).Select(p => (decimal?)p.Amount).DefaultIfEmpty(0).Sum().GetValueOrDefault();
            var incidentsToday = _db.Incidents.Count(i => depIdsToday.Contains(i.DepartureId) && DbFunctions.TruncateTime(i.CreatedAt) == today);
            var paymentsRecent = _db.Payments.Where(p => _db.Bookings.Any(b => b.BookingId == p.BookingId && _db.Departures.Any(d => d.DepartureId == b.DepartureId && d.DriverId == uid))).OrderByDescending(p => p.PaidAt).Take(10).ToList();
            var vm = new DriverDashboardVM
            {
                Date = today,
                TodayDeparturesCount = depsToday.Count,
                TicketsConfirmedToday = ticketsConfirmed,
                CashCollectedToday = cashToday,
                IncidentsToday = incidentsToday,
                TodayDepartures = depsToday.Select(x => new DepartureRowVM
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
                RecentPayments = paymentsRecent.Select(p => new PaymentRowVM
                {
                    PaymentId = p.PaymentId,
                    PaidAt = p.PaidAt,
                    Amount = p.Amount,
                    Method = p.Method,
                    Reference = p.Reference
                }).ToList()
            };
            return View(vm);
        }

        [HttpGet]
        public ActionResult MySchedule(DateTime? date = null) 
        {
            var guard = GuardDriver();
            if (guard != null) return guard;
            var uid = CurrentUserId();
            var vm = new DriverScheduleVM { FromDate = date }; 

            var q = _db.Departures.Where(d => d.DriverId == uid);

        
            if (date.HasValue)
            {
                DateTime startOfDay = date.Value.Date;

                DateTime nextDay = startOfDay.AddDays(1);
            
                q = q.Where(d => d.Date >= startOfDay && d.Date < nextDay);
            }

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
        public ActionResult Detail(int id)
        {
            var guard = GuardDriver();
            if (guard != null) return guard;
            var uid = CurrentUserId();
            var d = _db.Departures.FirstOrDefault(x => x.DepartureId == id && x.DriverId == uid);
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

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult UpdateStatus(int id, string status)
        {
            var guard = GuardDriver();
            if (guard != null) return guard;
            var uid = CurrentUserId();
            var d = _db.Departures.FirstOrDefault(x => x.DepartureId == id && x.DriverId == uid);
            if (d == null) return HttpNotFound();
            d.Status = status;
            d.UpdatedAt = DateTime.Now;
            d.UpdatedBy = uid;
            _db.SaveChanges();
            return RedirectToAction("Detail", new { id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult ReadyChecklist(int id, bool minFill, bool cashVerified, bool floatIssued, bool vehicleCleared)
        {
            var guard = GuardDriver();
            if (guard != null) return guard;
            var uid = CurrentUserId();
            var d = _db.Departures.FirstOrDefault(x => x.DepartureId == id && x.DriverId == uid);
            if (d == null) return HttpNotFound();
            d.ReadyChecklist_MinFill = minFill;
            d.ReadyChecklist_CashVerified = cashVerified;
            d.ReadyChecklist_FloatIssued = floatIssued;
            d.ReadyChecklist_VehicleCleared = vehicleCleared;
            d.UpdatedAt = DateTime.Now;
            d.UpdatedBy = uid;
            _db.SaveChanges();
            return RedirectToAction("Detail", new { id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult VerifyTicket(int departureId, string pnr)
        {
            var guard = GuardDriver();
            if (guard != null) return guard;
            var uid = CurrentUserId();
            var d = _db.Departures.Any(x => x.DepartureId == departureId && x.DriverId == uid);
            if (!d) return RedirectToAction("Dashboard");
            var b = _db.Bookings.FirstOrDefault(x => x.DepartureId == departureId && x.Pnr == pnr);
            if (b == null) TempData["VerifyResult"] = "NotFound";
            else TempData["VerifyResult"] = b.Status == "Confirmed" ? "Valid" : "NotConfirmed";
            return RedirectToAction("Detail", new { id = departureId });
        }

        [HttpGet]
        public ActionResult Tickets(int id)
        {
            var guard = GuardDriver();
            if (guard != null) return guard;
            var uid = CurrentUserId();

            var b = _db.Bookings.FirstOrDefault(x => x.BookingId == id);
            if (b == null) return HttpNotFound();

         
            var departure = _db.Departures.FirstOrDefault(d => d.DepartureId == b.DepartureId);
            if (departure == null) return HttpNotFound();

            var belongs = departure.DriverId == uid;
            if (!belongs) return RedirectToAction("Dashboard");

            var vehicle = _db.Vehicles.FirstOrDefault(v => v.VehicleId == departure.VehicleId);

            var route = _db.Routes
                .Where(r => r.RouteId == departure.RouteId)
                .Select(r => r.ToName)
                .FirstOrDefault();

            var vm = new TicketVM
            {
                Pnr = b.Pnr,
                Passenger = _db.Passengers.Where(p => p.PassengerId == b.PassengerId).Select(p => p.FullName).FirstOrDefault(),
                Route = route,

              
                DepartureDate = departure.Date,
                Time = departure.PlannedTime,

                Seats = string.Join(",", _db.BookingSeats.Where(s => s.BookingId == b.BookingId).OrderBy(s => s.SeatNo).Select(s => s.SeatNo)),
                Fare = b.FareEach,
                Total = (decimal)b.ExtendedAmount,

               
                VehicleReg = vehicle?.RegNo,
            };

            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Cashup(CashupCreateVM model)
        {
            var guard = GuardDriver();
            if (guard != null) return guard;
            var uid = CurrentUserId();
            var d = _db.Departures.Any(x => x.DepartureId == model.DepartureId && x.DriverId == uid);
            if (!d) return RedirectToAction("Dashboard");
            if (!ModelState.IsValid) return RedirectToAction("Detail", new { id = model.DepartureId });
            _db.CashupBags.Add(new CashupBag { DepartureId = model.DepartureId, CountedAmount = model.CountedAmount, CountedBy = uid, Timestamp = DateTime.Now });
            _db.SaveChanges();
            return RedirectToAction("Detail", new { id = model.DepartureId });
        }

        [HttpGet]
        public ActionResult PaymentsCsv(int id)
        {
            var guard = GuardDriver();
            if (guard != null) return guard;
            var uid = CurrentUserId();
            var belong = _db.Departures.Any(x => x.DepartureId == id && x.DriverId == uid);
            if (!belong) return RedirectToAction("Dashboard");
            var payments = _db.Payments.Where(p => _db.Bookings.Any(b => b.BookingId == p.BookingId && b.DepartureId == id)).OrderBy(p => p.PaidAt).ToList();
            var sb = new StringBuilder();
            sb.AppendLine("PaidAt,PNR,Amount,Method,Reference");
            foreach (var p in payments)
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
            return Csv("driver_payments.csv", sb.ToString());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult RecordExpense(ExpenseCreateVM model, HttpPostedFileBase proof)
        {
            var guard = GuardDriver();
            if (guard != null) return guard;
            var uid = CurrentUserId();
            var belong = _db.Departures.Any(x => x.DepartureId == model.DepartureId && x.DriverId == uid);
            if (!belong) return RedirectToAction("Dashboard");
            int? attachId = null;
            if (proof != null && proof.ContentLength > 0)
            {
                using (var ms = new MemoryStream())
                {
                    proof.InputStream.CopyTo(ms);
                    var a = new Models.Attachment { Entity = "Expense", EntityId = 0, FileName = Path.GetFileName(proof.FileName), FileData = ms.ToArray(), UploadedAt = DateTime.Now, UploadedBy = uid };
                    _db.Attachments.Add(a);
                    _db.SaveChanges();
                    attachId = a.AttachmentId;
                }
            }
            _db.Expenses.Add(new Expens { DepartureId = model.DepartureId, Type = model.Type, Amount = model.Amount, ProofAttachmentId = attachId, CreatedAt = DateTime.Now, CreatedBy = uid });
            _db.SaveChanges();
            return RedirectToAction("Detail", new { id = model.DepartureId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult LogIncident(IncidentCreateVM model, HttpPostedFileBase photo)
        {
            var guard = GuardDriver();
            if (guard != null) return guard;
            var uid = CurrentUserId();
            var belong = _db.Departures.Any(x => x.DepartureId == model.DepartureId && x.DriverId == uid);
            if (!belong) return RedirectToAction("Dashboard");
            var inc = new Incident { DepartureId = model.DepartureId, Type = model.Type, Notes = model.Notes, CreatedAt = DateTime.Now, CreatedBy = uid };
            _db.Incidents.Add(inc);
            _db.SaveChanges();
            if (photo != null && photo.ContentLength > 0)
            {
                using (var ms = new MemoryStream())
                {
                    photo.InputStream.CopyTo(ms);
                    var a = new Models.Attachment { Entity = "Incident", EntityId = inc.IncidentId, FileName = Path.GetFileName(photo.FileName), FileData = ms.ToArray(), UploadedAt = DateTime.Now, UploadedBy = uid };
                    _db.Attachments.Add(a);
                    _db.SaveChanges();
                }
            }
            return RedirectToAction("Detail", new { id = model.DepartureId });
        }

        [HttpGet]
        public ActionResult TelemetryTrail(int departureId)
        {
            var guard = GuardDriver();
            if (guard != null) return guard;

            var uid = CurrentUserId();
            var belong = _db.Departures.Any(x => x.DepartureId == departureId && x.DriverId == uid);
            if (!belong) return RedirectToAction("Dashboard");

            var points = _db.Telemetries
                .Where(t => t.DepartureId == departureId)
                .OrderBy(t => t.Timestamp)
                .Select(t => new TelemetryPointVM
                {
                    Timestamp = t.Timestamp,
                    Latitude = t.Latitude,
                    Longitude = t.Longitude,
                    SpeedKph = t.SpeedKph,
                    Battery = t.Battery
                })
                .ToList();

            var dest = (from d in _db.Departures
                        join r in _db.Routes on d.RouteId equals r.RouteId
                        where d.DepartureId == departureId
                        select new { r.ToLatitude, r.ToLongitude }).FirstOrDefault();

            double? dlat = dest == null || !dest.ToLatitude.HasValue ? (double?)null : Convert.ToDouble(dest.ToLatitude.Value);
            double? dlng = dest == null || !dest.ToLongitude.HasValue ? (double?)null : Convert.ToDouble(dest.ToLongitude.Value);
            ViewBag.DestLat = dlat;
            ViewBag.DestLng = dlng;

            var vm = new TelemetryListVM { DepartureId = departureId, Points = points };
            return View(vm);
        }

        [HttpPost]
        public ActionResult TelemetryUpdate()
        {
            try
            {
                using (var sr = new StreamReader(Request.InputStream))
                {
                    var json = sr.ReadToEnd();
                    var o = Newtonsoft.Json.Linq.JObject.Parse(json);

                    int departureId = (int)o["departureId"];
                    var guard = GuardDriver();
                    if (guard != null) return new HttpStatusCodeResult(HttpStatusCode.Forbidden);

                    var uid = CurrentUserId();
                    var belong = _db.Departures.Any(x => x.DepartureId == departureId && x.DriverId == uid);
                    if (!belong) return new HttpStatusCodeResult(HttpStatusCode.Forbidden);

                    decimal lat = Convert.ToDecimal((double)(o["lat"] ?? 0.0));
                    decimal lng = Convert.ToDecimal((double)(o["lng"] ?? 0.0));
                    decimal? speed = o["speedKph"]?.Type == Newtonsoft.Json.Linq.JTokenType.Null ? (decimal?)null
                                   : o["speedKph"] == null ? (decimal?)null
                                   : Convert.ToDecimal((double)o["speedKph"]);
                    int? battery = o["battery"]?.Type == Newtonsoft.Json.Linq.JTokenType.Null ? (int?)null
                                  : o["battery"] == null ? (int?)null
                                  : (int)o["battery"];

                    _db.Telemetries.Add(new Telemetry
                    {
                        DepartureId = departureId,
                        Timestamp = DateTime.Now,
                        Latitude = lat,
                        Longitude = lng,
                        SpeedKph = speed,
                        Battery = battery
                    });
                    _db.SaveChanges();

                    return Json(new { ok = true });
                }
            }
            catch
            {
                return Json(new { ok = false });
            }
        }


        [HttpGet]
        public ActionResult TripInfo(int departureId)
        {
            var guard = GuardDriver();
            if (guard != null) return guard;
            var uid = CurrentUserId();
            var dep = _db.Departures.FirstOrDefault(d => d.DepartureId == departureId && d.DriverId == uid);
            if (dep == null) return HttpNotFound();
            var route = _db.Routes.FirstOrDefault(r => r.RouteId == dep.RouteId);
            decimal? startLat = null, startLng = null, destLat = null, destLng = null;
            if (route != null)
            {
                startLat = _db.Ranks.Where(rk => rk.RankId == route.FromRankId).Select(rk => (decimal?)rk.Latitude).FirstOrDefault();
                startLng = _db.Ranks.Where(rk => rk.RankId == route.FromRankId).Select(rk => (decimal?)rk.Longitude).FirstOrDefault();
                destLat = route.ToLatitude;
                destLng = route.ToLongitude;
            }
            return Json(new { startLat = startLat, startLng = startLng, destLat = destLat, destLng = destLng }, JsonRequestBehavior.AllowGet);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult PushLocation(int departureId, decimal lat, decimal lng, decimal? speed = null, int? battery = null)
        {
            var guard = GuardDriver();
            if (guard != null) return guard;
            var uid = CurrentUserId();
            var belong = _db.Departures.Any(x => x.DepartureId == departureId && x.DriverId == uid);
            if (!belong) return new HttpStatusCodeResult(403);
            _db.Telemetries.Add(new Telemetry
            {
                DepartureId = departureId,
                Latitude = lat,
                Longitude = lng,
                SpeedKph = speed,
                Battery = battery,
                Timestamp = DateTime.Now
            });
            _db.SaveChanges();
            return new HttpStatusCodeResult(204);
        }
    }
}
