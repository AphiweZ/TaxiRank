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
using Newtonsoft.Json.Linq;
using Stripe;
using Stripe.Checkout;
using TaxiRank.Models;
using TaxiRank.Models.ViewModels;

namespace TaxiRank.Controllers
{
    public class CustomerController : Controller
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

        private ActionResult GuardCustomer()
        {
            if (!IsLoggedIn()) return RedirectToAction("Login", "Account");
            if (CurrentRole() != "Customer") return RedirectToAction("Login", "Account");
            return null;
        }

        private void StripeInit()
        {
            StripeConfiguration.ApiKey = ConfigurationManager.AppSettings["StripeSecretKey"];
        }

        private FileContentResult Csv(string fileName, string csv)
        {
            var bytes = Encoding.UTF8.GetBytes(csv);
            return File(bytes, "text/csv", fileName);
        }

        private void SendMail(string to, string subject, string html)
        {
            if (string.IsNullOrWhiteSpace(to)) return;

            
            try
            {
               
                System.Net.ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;


                var from = "durbanstationassociation@gmail.com";
                var pass = "lkvm pjkh ccec eyub";

                using (var msg = new MailMessage())
                {
                   
                    msg.From = new MailAddress(from, "Durban Station Association");
                    msg.To.Add(new MailAddress(to));

                    msg.Subject = subject ?? string.Empty;
                    msg.Body = html ?? string.Empty;
                    msg.IsBodyHtml = true;
                    msg.SubjectEncoding = Encoding.UTF8;
                    msg.BodyEncoding = Encoding.UTF8;

                    using (var smtp = new SmtpClient("smtp.gmail.com", 587))
                    {
                        smtp.EnableSsl = true;
                        smtp.Credentials = new NetworkCredential(from, pass);

                        
                        smtp.Timeout = 8000; // ms

                        
                        smtp.Send(msg);
                    }
                }
            }
            catch
            {
                
            }
        }


        private List<IdName> RouteOptions()
        {
            return _db.Routes.Where(x => x.Active).OrderBy(x => x.FromRankId).ThenBy(x => x.ToName).Select(x => new IdName { Id = x.RouteId, Name = _db.Ranks.Where(r => r.RankId == x.FromRankId).Select(r => r.Name).FirstOrDefault() + " → " + x.ToName }).ToList();
        }

        #region Dashboard
        [HttpGet]
        public ActionResult Dashboard()
        {
            var guard = GuardCustomer();
            if (guard != null) return guard;
            var uid = CurrentUserId();
            var today = DateTime.Today;
            var myRecent = _db.Bookings.Where(b => b.CreatedBy == uid).OrderByDescending(b => b.CreatedAt).Take(10).ToList();
            var depIdsTodayMine = _db.Bookings.Where(b => b.CreatedBy == uid).Join(_db.Departures, b => b.DepartureId, d => d.DepartureId, (b, d) => new { b, d }).Where(x => x.d.Date == today).Select(x => x.d.DepartureId).Distinct().ToList();
            var paidToday = _db.Payments.Where(p => _db.Bookings.Any(b => b.BookingId == p.BookingId && b.CreatedBy == uid && DbFunctions.TruncateTime(p.PaidAt) == today)).Select(p => (decimal?)p.Amount).DefaultIfEmpty(0).Sum().GetValueOrDefault();
            var ticketsConfirmed = _db.Bookings.Count(b => b.CreatedBy == uid && b.Status == "Confirmed" && _db.Departures.Any(d => d.DepartureId == b.DepartureId && d.Date == today));
            var suggest = _db.Departures.Where(d => d.Date >= today && (d.Status == "Queued" || d.Status == "Boarding")).OrderBy(d => d.Date).ThenBy(d => d.PlannedTime).Take(8).ToList();
            var vm = new CustomerDashboardVM
            {
                Date = today,
                UpcomingBookings = _db.Bookings.Count(b => b.CreatedBy == uid && _db.Departures.Any(d => d.DepartureId == b.DepartureId && d.Date >= today) && b.Status != "Cancelled"),
                TicketsConfirmedToday = ticketsConfirmed,
                CashPaidToday = paidToday,
                MyRecentBookings = myRecent.Select(b => new BookingRowVM
                {
                    BookingId = b.BookingId,
                    Pnr = b.Pnr,
                    PassengerName = _db.Passengers.Where(p => p.PassengerId == b.PassengerId).Select(p => p.FullName).FirstOrDefault(),
                    SeatsCount = b.SeatsCount,
                    FareEach = b.FareEach,
                    ExtendedAmount = (decimal)b.ExtendedAmount,
                    Status = b.Status,
                    PaymentMethod = b.PaymentMethod
                }).ToList(),
                SuggestedDepartures = suggest.Select(x => new DepartureRowVM
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
                }).ToList()
            };
            return View(vm);
        }
        #endregion

        #region Search
        [HttpGet]
        public ActionResult Search(int? routeId = null, DateTime? date = null)
        {
            var guard = GuardCustomer();
            if (guard != null) return guard;
            var target = date.HasValue ? date.Value.Date : DateTime.Today;
            var vm = new CustomerSearchVM { RouteOptions = RouteOptions(), Date = target, RouteId = routeId };
            var q = _db.Departures.Where(d => d.Date == target && (d.Status == "Queued" || d.Status == "Boarding" || d.Status == "Ready"));
            if (routeId.HasValue) q = q.Where(d => d.RouteId == routeId.Value);
            var rows = q.OrderBy(d => d.PlannedTime).ToList();
            foreach (var d in rows)
            {
                var seatsTotal = _db.Vehicles.Where(v => v.VehicleId == d.VehicleId).Select(v => (int?)v.Seats).FirstOrDefault().GetValueOrDefault();
                var booked = _db.SeatInventories.Count(s => s.DepartureId == d.DepartureId && s.Status == "Booked");
                var fare = _db.Routes.Where(r => r.RouteId == d.RouteId).Select(r => r.DefaultFare).FirstOrDefault();
                vm.Rows.Add(new CustomerSearchRowVM
                {
                    DepartureId = d.DepartureId,
                    Date = d.Date,
                    PlannedTime = d.PlannedTime,
                    RouteName = _db.Routes.Where(r => r.RouteId == d.RouteId).Select(r => r.ToName).FirstOrDefault(),
                    VehicleReg = _db.Vehicles.Where(v => v.VehicleId == d.VehicleId).Select(v => v.RegNo).FirstOrDefault(),
                    SeatsFree = Math.Max(seatsTotal - booked, 0),
                    Fare = fare,
                    Status = d.Status
                });
            }
            return View(vm);
        }
        #endregion

        #region Booking
        [HttpGet]
        public ActionResult Book(int departureId)
        {
            var guard = GuardCustomer();
            if (guard != null) return guard;
            var d = _db.Departures.FirstOrDefault(x => x.DepartureId == departureId);
            if (d == null) return HttpNotFound();
            if (!(d.Status == "Queued" || d.Status == "Boarding" || d.Status == "Ready")) return RedirectToAction("Search");
            var seats = _db.SeatInventories.Where(s => s.DepartureId == departureId).OrderBy(s => s.SeatNo).Select(s => new SeatCellVM { SeatNo = s.SeatNo, Status = s.Status, BookingId = s.BookingId }).ToList();
            var fare = _db.Routes.Join(_db.Departures, r => r.RouteId, dd => dd.RouteId, (r, dd) => new { r, dd }).Where(x => x.dd.DepartureId == departureId).Select(x => x.r.DefaultFare).FirstOrDefault();
            var vm = new BookingCreateVM { DepartureId = departureId, SeatsCount = 1, FareEach = fare, PaymentMethod = "Card", Status = "Held", AvailableSeats = seats };
            return View(vm);
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Book(BookingCreateVM model, int[] selectedSeats)
        {
            var guard = GuardCustomer();
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

            
            model.PaymentMethod = "Card";
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
                    Status = model.Status,
                    Pnr = "PNR" + DateTime.Now.Ticks.ToString().Substring(8),
                    ExpiresAt = DateTime.Now.AddMinutes(20),
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
                tx.Commit();

                return RedirectToAction("Checkout", new { id = b.BookingId });
            }
        }



        [HttpGet]
        public ActionResult Checkout(int id)
        {
            var guard = GuardCustomer();
            if (guard != null) return guard;
            var uid = CurrentUserId();
            var b = _db.Bookings.FirstOrDefault(x => x.BookingId == id && x.CreatedBy == uid);
            if (b == null) return HttpNotFound();
            if (b.Status == "Confirmed") return RedirectToAction("Ticket", new { id = b.BookingId });
            var route = _db.Routes.Where(r => r.RouteId == _db.Departures.Where(d => d.DepartureId == b.DepartureId).Select(d => d.RouteId).FirstOrDefault()).Select(r => r.ToName).FirstOrDefault();
            var vm = new BookingDetailVM
            {
                BookingId = b.BookingId,
                Pnr = b.Pnr,
                RouteName = route,
                DepartureDate = _db.Departures.Where(d => d.DepartureId == b.DepartureId).Select(d => d.Date).FirstOrDefault(),
                PlannedTime = _db.Departures.Where(d => d.DepartureId == b.DepartureId).Select(d => d.PlannedTime).FirstOrDefault(),
                PassengerName = _db.Passengers.Where(p => p.PassengerId == b.PassengerId).Select(p => p.FullName).FirstOrDefault(),
                PassengerPhone = _db.Passengers.Where(p => p.PassengerId == b.PassengerId).Select(p => p.Phone).FirstOrDefault(),
                SeatsCount = b.SeatsCount,
                FareEach = b.FareEach,
                ExtendedAmount = (decimal)b.ExtendedAmount,
                Status = b.Status,
                PaymentMethod = b.PaymentMethod,
                SeatNumbers = _db.BookingSeats.Where(s => s.BookingId == b.BookingId).Select(s => s.SeatNo).ToList()
            };
            return View(vm);
        }

        

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Cancel(int id)
        {
            var guard = GuardCustomer();
            if (guard != null) return guard;
            var uid = CurrentUserId();
            using (var tx = _db.Database.BeginTransaction())
            {
                var b = _db.Bookings.FirstOrDefault(x => x.BookingId == id && x.CreatedBy == uid);
                if (b == null) return HttpNotFound();
                var dep = _db.Departures.FirstOrDefault(d => d.DepartureId == b.DepartureId);
                if (dep == null) return HttpNotFound();
                var cutoff = dep.Date.Add(dep.PlannedTime).AddMinutes(-60);
                if (DateTime.Now > cutoff) return RedirectToAction("MyBookings");
                foreach (var seat in _db.BookingSeats.Where(x => x.BookingId == id).ToList())
                {
                    var inv = _db.SeatInventories.FirstOrDefault(s => s.DepartureId == b.DepartureId && s.SeatNo == seat.SeatNo);
                    if (inv != null)
                    {
                        inv.Status = "Free";
                        inv.BookingId = null;
                    }
                    _db.BookingSeats.Remove(seat);
                }
                b.Status = "Cancelled";
                _db.SaveChanges();
                tx.Commit();
                return RedirectToAction("MyBookings");
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult ConfirmCard(int id)
        {
            var guard = GuardCustomer();
            if (guard != null) return guard;

            var uid = CurrentUserId();
            var b = _db.Bookings.FirstOrDefault(x => x.BookingId == id && x.CreatedBy == uid);
            if (b == null) return HttpNotFound();
            if (b.Status == "Confirmed") return RedirectToAction("Ticket", new { id = b.BookingId });

            StripeConfiguration.ApiKey = ConfigurationManager.AppSettings["StripeSecretKey"];
            var amountCents = (long)Math.Round(((decimal)b.ExtendedAmount) * 100m, 0);

            var successUrl = Url.Action("Ticket", "Customer", new { id = b.BookingId }, protocol: Request.Url.Scheme)
                            + "?session_id={CHECKOUT_SESSION_ID}";
            var cancelUrl = Url.Action("Checkout", "Customer", new { id = b.BookingId }, protocol: Request.Url.Scheme);

            var options = new SessionCreateOptions
            {
                Mode = "payment",
                SuccessUrl = successUrl,
                CancelUrl = cancelUrl,
                PaymentMethodTypes = new List<string> { "card" },
                LineItems = new List<SessionLineItemOptions>
        {
            new SessionLineItemOptions
            {
                Quantity = 1,
                PriceData = new SessionLineItemPriceDataOptions
                {
                    Currency = "zar",
                    UnitAmount = amountCents,
                    ProductData = new SessionLineItemPriceDataProductDataOptions
                    {
                        Name = "Ticket " + b.Pnr
                    }
                }
            }
        }
            };

            var service = new SessionService();
            var session = service.Create(options);
            return Redirect(session.Url);
        }

        [HttpGet]
        public ActionResult Ticket(int id)
        {
            var guard = GuardCustomer();
            if (guard != null) return guard;

            var uid = CurrentUserId();
            var b = _db.Bookings.FirstOrDefault(x => x.BookingId == id && x.CreatedBy == uid);
            if (b == null) return HttpNotFound();

            var csId = Request["session_id"] ?? Request["cs_id"];
            if (!string.IsNullOrWhiteSpace(csId))
            {
                StripeConfiguration.ApiKey = ConfigurationManager.AppSettings["StripeSecretKey"];
                var sService = new SessionService();
                var s = sService.Get(csId);
                if (s.PaymentStatus == "paid" && b.Status != "Confirmed")
                {
                    _db.Payments.Add(new Payment
                    {
                        BookingId = b.BookingId,
                        Amount = (decimal)b.ExtendedAmount,
                        Method = "Card",
                        Reference = s.PaymentIntentId,
                        PaidAt = DateTime.Now,
                        CreatedAt = DateTime.Now,
                        CreatedBy = uid
                    });
                    b.Status = "Confirmed";
                    b.ExpiresAt = null;
                    _db.SaveChanges();
                }
            }

            var departure = _db.Departures.FirstOrDefault(d => d.DepartureId == b.DepartureId);
            if (departure == null) return HttpNotFound();
          
            var vehicle = _db.Vehicles.FirstOrDefault(v => v.VehicleId == departure.VehicleId);


            var route = _db.Routes.Where(r => r.RouteId == departure.RouteId).Select(r => r.ToName).FirstOrDefault();

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
        public ActionResult ResendTicketEmail(int id)
        {
            var guard = GuardCustomer();
            if (guard != null) return guard;
            var uid = CurrentUserId();
            var b = _db.Bookings.FirstOrDefault(x => x.BookingId == id && x.CreatedBy == uid);
            if (b == null) return HttpNotFound();

            var email = _db.Users.Where(u => u.UserId == uid).Select(u => u.Email).FirstOrDefault();

            if (!string.IsNullOrWhiteSpace(email))
            {
             
                var departure = _db.Departures
                    .Where(d => d.DepartureId == b.DepartureId)
                    .Select(d => new
                    {
                        d.RouteId,
                        d.Date,
                        d.PlannedTime,
                        d.VehicleId
                    })
                    .FirstOrDefault();

                var route = _db.Routes
                    .Where(r => r.RouteId == departure.RouteId)
                    .Select(r => r.ToName)
                    .FirstOrDefault();

                var vehicleReg = _db.Vehicles
                    .Where(v => v.VehicleId == departure.VehicleId)
                    .Select(v => v.RegNo)
                    .FirstOrDefault();

                var seats = string.Join(",", _db.BookingSeats
                    .Where(s => s.BookingId == b.BookingId)
                    .OrderBy(s => s.SeatNo)
                    .Select(s => s.SeatNo));


               
                string containerStyle = "max-width:300px; border:1px dashed #000; padding:15px; font-family: 'Courier New', Courier, monospace; font-size: 14px; line-height: 1.5; color: #000;";

               
                string divider = "<p style='margin: 10px 0; border-top: 1px dashed #000; line-height: 0;'></p>";

                var html = $"<div style='{containerStyle}'>"
                         + $"<h3 style='text-align: center; margin-top: 0; margin-bottom: 5px; font-size: 18px;'>TAXI RANK TICKET</h3>"
                         + $"<p style='text-align: center; margin-bottom: 10px;'>PNR: <strong>{b.Pnr}</strong></p>"
                         + divider
                         + $"<p style='margin: 5px 0;'><strong>ROUTE:</strong> {route}</p>"
                         + $"<p style='margin: 5px 0;'><strong>DATE:</strong> {departure.Date.ToString("yyyy-MM-dd")}</p>"
                         + $"<p style='margin: 5px 0;'><strong>TIME:</strong> {departure.PlannedTime.ToString()}</p>"
                         + $"<p style='margin: 5px 0;'><strong>VEHICLE:</strong> {vehicleReg}</p>"
                         + divider
                         + $"<p style='margin: 5px 0;'><strong>SEATS:</strong> {seats}</p>"
                         + divider
                         + $"<p style='font-size: 18px; font-weight: bold; text-align: right; margin-top: 10px;'>TOTAL: R{((decimal)b.ExtendedAmount).ToString("0.00")}</p>"
                         + $"<p style='text-align: center; margin-top: 20px;'>Thank You For Booking!</p>"
                         + "</div>";

                SendMail(email, "Your TaxiRank Ticket " + b.Pnr, html);
            }

            return RedirectToAction("Ticket", new { id = b.BookingId });
        }
        #endregion

        #region MyBookings
        [HttpGet]
        public ActionResult MyBookings(DateTime? from = null, DateTime? to = null, string status = null)
        {
            var guard = GuardCustomer();
            if (guard != null) return guard;
            var uid = CurrentUserId();
            var q = _db.Bookings.Where(b => b.CreatedBy == uid);
            if (from.HasValue) q = q.Where(b => b.CreatedAt >= from.Value);
            if (to.HasValue) q = q.Where(b => b.CreatedAt <= to.Value);
            if (!string.IsNullOrWhiteSpace(status)) q = q.Where(b => b.Status == status);
            var rows = q.OrderByDescending(b => b.CreatedAt).ToList().Select(b => new BookingRowVM
            {
                BookingId = b.BookingId,
                Pnr = b.Pnr,
                PassengerName = _db.Passengers.Where(p => p.PassengerId == b.PassengerId).Select(p => p.FullName).FirstOrDefault(),
                SeatsCount = b.SeatsCount,
                FareEach = b.FareEach,
                ExtendedAmount = (decimal)b.ExtendedAmount,
                Status = b.Status,
                PaymentMethod = b.PaymentMethod
            }).ToList();
            ViewBag.Rows = rows;
            return View();
        }

        [HttpGet]
        public ActionResult MyBookingsCsv(DateTime? from = null, DateTime? to = null)
        {
            var guard = GuardCustomer();
            if (guard != null) return guard;
            var uid = CurrentUserId();
            var q = _db.Bookings.Where(b => b.CreatedBy == uid);
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
            return Csv("my_bookings.csv", sb.ToString());
        }
        #endregion

        #region Profile
        [HttpGet]
        public ActionResult Profile()
        {
            var guard = GuardCustomer();
            if (guard != null) return guard;
            var uid = CurrentUserId();
            var u = _db.Users.FirstOrDefault(x => x.UserId == uid);
            if (u == null) return HttpNotFound();
            var vm = new CustomerProfileVM { DisplayName = u.DisplayName, Phone = u.Phone, Email = u.Email };
            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Profile(CustomerProfileVM model)
        {
            var guard = GuardCustomer();
            if (guard != null) return guard;
            if (!ModelState.IsValid) return View(model);
            var uid = CurrentUserId();
            var u = _db.Users.FirstOrDefault(x => x.UserId == uid);
            if (u == null) return HttpNotFound();
            u.DisplayName = model.DisplayName;
            u.Phone = model.Phone;
            u.Email = model.Email;
            u.UpdatedAt = DateTime.Now;
            u.UpdatedBy = uid;
            _db.SaveChanges();
            return RedirectToAction("Dashboard");
        }
        #endregion



        [HttpGet]
        public ActionResult RequestEvent()
        {
            var g = GuardCustomer(); if (g != null) return g;
            var vm = new EventRequestVM { Date = DateTime.Today.AddDays(1), TaxiCount = 1, Pax = 1 };
            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult RequestEvent(EventRequestVM model)
        {
            var g = GuardCustomer(); if (g != null) return g;

            var uid = CurrentUserId();

          
            decimal? pLat = null, pLng = null, dLat = null, dLng = null;
            decimal tmp;
            var inv = System.Globalization.CultureInfo.InvariantCulture;

            if (decimal.TryParse((Request["PickupLat"] ?? "").Replace(",", "."), System.Globalization.NumberStyles.Any, inv, out tmp)) pLat = tmp;
            if (decimal.TryParse((Request["PickupLng"] ?? "").Replace(",", "."), System.Globalization.NumberStyles.Any, inv, out tmp)) pLng = tmp;
            if (decimal.TryParse((Request["DropLat"] ?? "").Replace(",", "."), System.Globalization.NumberStyles.Any, inv, out tmp)) dLat = tmp;
            if (decimal.TryParse((Request["DropLng"] ?? "").Replace(",", "."), System.Globalization.NumberStyles.Any, inv, out tmp)) dLng = tmp;

            var ch = new Charter
            {
                Organizer = _db.Users.Where(u => u.UserId == uid).Select(u => u.DisplayName).FirstOrDefault() ?? "Customer",
                Contact = _db.Users.Where(u => u.UserId == uid).Select(u => u.Email).FirstOrDefault() ?? "",
                Date = model.Date.Date,
                PickupPlaceId = model.PickupPlaceId,
                PickupLat = pLat,
                PickupLng = pLng,
                DropPlaceId = model.DropPlaceId,
                DropLat = dLat,
                DropLng = dLng,
                Pax = model.Pax,
                TaxiCount = model.TaxiCount,
                ReturnTrip = model.ReturnTrip,
                EventType = model.EventType,
                Status = "Requested",
                QuoteAmount = 0m,
                DepositPaid = false,
                CreatedAt = DateTime.Now,
                CreatedBy = uid,
                CustomerId = uid
            };

            _db.Charters.Add(ch);
            _db.SaveChanges();

            return RedirectToAction("MyEvents");
        }

        [HttpGet]
        public ActionResult MyEvents()
        {
            var g = GuardCustomer(); if (g != null) return g;
            var uid = CurrentUserId();
            var rows = _db.Charters.Where(c => c.CustomerId == uid).OrderByDescending(c => c.CreatedAt).ToList()
                .Select(c => new EventRowVM
                {
                    CharterId = c.CharterId,
                    Date = c.Date,
                    EventType = c.EventType,
                    TaxiCount = c.TaxiCount,
                    ReturnTrip = c.ReturnTrip,
                    Status = c.Status,
                    QuoteAmount = c.QuoteAmount,
                    IsPaid = c.IsPaid
                }).ToList();
            return View(new CustomerEventsListVM { Rows = rows });
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult DeclineEvent(int id)
        {
            var g = GuardCustomer();
            if (g != null) return g;

            var uid = CurrentUserId();
            var c = _db.Charters.FirstOrDefault(x => x.CharterId == id && x.CustomerId == uid);

            if (c == null) return HttpNotFound();

            // Check if the event can be declined
            if (c.Status == "Approved" && !c.IsPaid)
            {
                // Use "Rejected" to align with the existing database constraint
                c.Status = "Rejected";
                _db.SaveChanges();
            }

            return RedirectToAction("MyEvents");
        }

        [HttpGet]
        public ActionResult PayEvent(int id)
        {
            var g = GuardCustomer();
            if (g != null) return g;
            var uid = CurrentUserId();
            var c = _db.Charters.FirstOrDefault(x => x.CharterId == id && x.CustomerId == uid);

            if (c == null) return HttpNotFound();

           
            if (c.Status == "Declined" || c.IsPaid)
            {
                return RedirectToAction("MyEvents");
            }

          
            if (c.Status != "Approved")
            {
                return RedirectToAction("MyEvents");
            }

            StripeConfiguration.ApiKey = System.Configuration.ConfigurationManager.AppSettings["StripeSecretKey"];
            var service = new PaymentIntentService();
            var pi = service.Create(new PaymentIntentCreateOptions
            {
                Amount = (long)Math.Round(c.QuoteAmount * 100m),
                Currency = "zar",
                AutomaticPaymentMethods = new PaymentIntentAutomaticPaymentMethodsOptions { Enabled = true },
                Metadata = new Dictionary<string, string> { { "charterId", c.CharterId.ToString() } }
            });

            c.StripePaymentIntentId = pi.Id;
            _db.SaveChanges();

            var vm = new EventPaymentVM
            {
                CharterId = c.CharterId,
                Amount = c.QuoteAmount,
                StripePublishableKey = System.Configuration.ConfigurationManager.AppSettings["StripePublishableKey"],
                ClientSecret = pi.ClientSecret
            };

            return View(vm);
        }
        [HttpGet]
        public ActionResult ContinuePayment(int id)
        {
            return RedirectToAction("PayEvent", new { id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult PayEventConfirm(int id, string paymentIntentId)
        {
            var g = GuardCustomer(); if (g != null) return g;
            var uid = CurrentUserId();
            var c = _db.Charters.FirstOrDefault(x => x.CharterId == id && x.CustomerId == uid);
            if (c == null) return HttpNotFound();
            if (string.IsNullOrWhiteSpace(paymentIntentId) || c.StripePaymentIntentId != paymentIntentId) return RedirectToAction("MyEvents");
            StripeConfiguration.ApiKey = System.Configuration.ConfigurationManager.AppSettings["StripeSecretKey"];
            var service = new PaymentIntentService();
            var pi = service.Get(paymentIntentId);
            if (pi.Status == "succeeded")
            {
                c.IsPaid = true;
                _db.CharterPayments.Add(new CharterPayment
                {
                    CharterId = c.CharterId,
                    Amount = c.QuoteAmount,
                    StripePaymentIntentId = paymentIntentId,
                    CreatedBy = uid,
                    PaidAt = DateTime.Now
                });
                _db.SaveChanges();
            }
            return RedirectToAction("MyEvents");
        }
    }
}
