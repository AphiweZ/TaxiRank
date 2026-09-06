using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data.Entity;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Mail;
using System.Text;
using System.Web;
using System.Web.Mvc;
using Newtonsoft.Json.Linq;
using Stripe;
using Stripe.Checkout;
using TaxiRank.Models;
using TaxiRank.Models.ViewModels;
using System.Globalization;


namespace TaxiRank.Controllers
{
    public class AdminController : Controller
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

        private IEnumerable<int> AllowedLocationIds()
        {
            var s = Session["AllowedLocationIds"] == null ? "" : Session["AllowedLocationIds"].ToString();
            if (string.IsNullOrWhiteSpace(s)) return new List<int>();
            return s.Split(',').Where(x => !string.IsNullOrWhiteSpace(x)).Select(int.Parse).ToList();
        }

        private ActionResult GuardAdmin()
        {
            if (!IsLoggedIn()) return RedirectToAction("Login", "Account");
            if (CurrentRole() != "Admin") return RedirectToAction("Login", "Account");
            return null;
        }

        private void StripeInit()
        {
            StripeConfiguration.ApiKey = ConfigurationManager.AppSettings["StripeSecretKey"];
        }

        private string MapsKey()
        {
            var k = ConfigurationManager.AppSettings["GoogleMapsApiKey"];
            if (string.IsNullOrWhiteSpace(k)) return "AIzaSyCzGOGXloVFr8w-Pe53rgPWuQv-P3KnIaE";
            return k;
        }

        private FileContentResult Csv(string fileName, string csv)
        {
            var bytes = Encoding.UTF8.GetBytes(csv);
            return File(bytes, "text/csv", fileName);
        }

        private void SendMail(string to, string subject, string html)
        {
            if (string.IsNullOrWhiteSpace(to)) return;

            var from = "durbanstationassociation@gmail.com";
            var pass = "lkvm pjkh ccec eyub";
            using (var msg = new MailMessage())
            {
                msg.From = new MailAddress(from, "Durban Station Association");
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

        private List<IdName> RankOptions()
        {
            return _db.Ranks.Where(x => x.IsActive).OrderBy(x => x.Name).Select(x => new IdName { Id = x.RankId, Name = x.Name + " - " + x.City }).ToList();
        }

        private List<IdName> UserOptions(string role = null)
        {
            var q = _db.Users.Where(x => x.IsActive);
            if (!string.IsNullOrWhiteSpace(role)) q = q.Where(x => x.Role == role);
            return q.OrderBy(x => x.DisplayName).Select(x => new IdName { Id = x.UserId, Name = x.DisplayName }).ToList();
        }

        private List<IdName> VehicleOptions()
        {
            return _db.Vehicles.Where(x => x.Active).OrderBy(x => x.RegNo).Select(x => new IdName { Id = x.VehicleId, Name = x.RegNo }).ToList();
        }

        private List<IdName> RouteOptions()
        {
            return _db.Routes.Where(x => x.Active).OrderBy(x => x.FromRankId).ThenBy(x => x.ToName).Select(x => new IdName { Id = x.RouteId, Name = x.ToName }).ToList();
        }

        private void Audit(string entity, int id, string field, string oldV, string newV)
        {
            _db.AuditLogs.Add(new AuditLog { EntityType = entity, EntityId = id, Field = field, OldValue = oldV, NewValue = newV, UserId = CurrentUserId(), Timestamp = DateTime.Now });
        }
        /*
        #region Dashboard
        [HttpGet]
        public ActionResult Dashboard()
        {
            var guard = GuardAdmin();
            if (guard != null) return guard;
            var today = DateTime.Today;
            var next = _db.Departures.Include(x => x.Route).Include(x => x.Vehicle).Include(x => x.User).Where(x => x.Date >= today).OrderBy(x => x.Date).ThenBy(x => x.PlannedTime).Take(10).ToList();
            var recentAlerts = _db.Alerts.OrderByDescending(x => x.CreatedAt).Take(10).ToList();
            var vm = new DashboardVM
            {
                ActiveDepartures = _db.Departures.Count(x => x.Status == "Boarding" || x.Status == "Ready"),
                BookingsToday = _db.Bookings.Count(x => DbFunctions.TruncateTime(x.CreatedAt) == today),
                CashCollectedToday = _db.Payments.Where(x => DbFunctions.TruncateTime(x.PaidAt) == today).Select(x => (decimal?)x.Amount).DefaultIfEmpty(0).Sum().GetValueOrDefault(),
                AlertsOpen = _db.Alerts.Count(x => x.ResolvedAt == null),
                NextDepartures = next.Select(x => new DepartureRowVM
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
                RecentAlerts = recentAlerts.Select(a => new AlertRowVM
                {
                    AlertId = a.AlertId,
                    Type = a.Type,
                    Entity = a.Entity,
                    EntityId = a.EntityId,
                    Severity = a.Severity,
                    Message = a.Message,
                    CreatedAt = a.CreatedAt,
                    ResolvedAt = a.ResolvedAt
                }).ToList()
            };
            return View(vm);
        }
        #endregion
        */
        #region Dashboard
        [HttpGet]
        public ActionResult Dashboard()
        {
            var guard = GuardAdmin();
            if (guard != null) return guard;
            return RedirectToAction("UsersList");
        }
        #endregion

        #region Users
        [HttpGet]
        public ActionResult UsersList(string role = null, string search = null)
        {
            var guard = GuardAdmin();
            if (guard != null) return guard;
            var q = _db.Users.AsQueryable();
            if (!string.IsNullOrWhiteSpace(role)) q = q.Where(x => x.Role == role);
            if (!string.IsNullOrWhiteSpace(search))
            {
                var s = search.ToLower();
                q = q.Where(x => x.Username.ToLower().Contains(s) || x.DisplayName.ToLower().Contains(s) || (x.Email ?? "").ToLower().Contains(s));
            }
            var rows = q.OrderBy(x => x.DisplayName).ToList();
            return View(rows);
        }

        [HttpGet]
        public ActionResult UserCreate()
        {
            var guard = GuardAdmin();
            if (guard != null) return guard;
            return View();
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult UserCreate(string Username, string Password, string DisplayName, string Role, string Phone, string Email)
        {
            var guard = GuardAdmin();
            if (guard != null) return guard;
            if (string.IsNullOrWhiteSpace(Username) || string.IsNullOrWhiteSpace(Password) || string.IsNullOrWhiteSpace(DisplayName) || string.IsNullOrWhiteSpace(Role))
            {
                ModelState.AddModelError("", "All required fields must be provided");
                return View();
            }
            if (_db.Users.Any(x => x.Username == Username))
            {
                ModelState.AddModelError("", "Username already exists");
                return View();
            }
            var u = new User { Username = Username, Password = Password, DisplayName = DisplayName, Role = Role, Phone = Phone, Email = Email, IsActive = true, CreatedAt = DateTime.Now, CreatedBy = CurrentUserId() };
            _db.Users.Add(u);
            _db.SaveChanges();
            return RedirectToAction("UsersList");
        }

        [HttpGet]
        public ActionResult UserEdit(int id)
        {
            var guard = GuardAdmin();
            if (guard != null) return guard;
            var u = _db.Users.FirstOrDefault(x => x.UserId == id);
            if (u == null) return HttpNotFound();
            return View(u);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult UserEdit(int UserId, string DisplayName, string Phone, string Email, string Role, bool IsActive)
        {
            var guard = GuardAdmin();
            if (guard != null) return guard;
            var u = _db.Users.FirstOrDefault(x => x.UserId == UserId);
            if (u == null) return HttpNotFound();
            u.DisplayName = DisplayName;
            u.Phone = Phone;
            u.Email = Email;
            Audit("Users", u.UserId, "Role", u.Role, Role);
            u.Role = Role;
            u.IsActive = IsActive;
            u.UpdatedAt = DateTime.Now;
            u.UpdatedBy = CurrentUserId();
            _db.SaveChanges();
            return RedirectToAction("UsersList");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult UserToggleActive(int id)
        {
            var guard = GuardAdmin();
            if (guard != null) return guard;
            var u = _db.Users.FirstOrDefault(x => x.UserId == id);
            if (u == null) return HttpNotFound();
            u.IsActive = !u.IsActive;
            u.UpdatedAt = DateTime.Now;
            u.UpdatedBy = CurrentUserId();
            _db.SaveChanges();
            return RedirectToAction("UsersList");
        }

        [HttpGet]
        public ActionResult UserRanks(int id)
        {
            var guard = GuardAdmin();
            if (guard != null) return guard;
            ViewBag.User = _db.Users.FirstOrDefault(x => x.UserId == id);
            ViewBag.Ranks = RankOptions();
            var assigned = _db.UserRanks.Where(x => x.UserId == id).Select(x => x.RankId).ToList();
            return View(assigned);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult UserRanks(int userId, int[] rankIds)
        {
            var guard = GuardAdmin();
            if (guard != null) return guard;
            rankIds = rankIds ?? new int[0];
            var current = _db.UserRanks.Where(x => x.UserId == userId).ToList();
            foreach (var r in current.Where(x => !rankIds.Contains(x.RankId)).ToList())
            {
                _db.UserRanks.Remove(r);
            }
            foreach (var rid in rankIds.Where(x => !current.Any(c => c.RankId == x)))
            {
                _db.UserRanks.Add(new UserRank { UserId = userId, RankId = rid, CreatedAt = DateTime.Now, CreatedBy = CurrentUserId() });
            }
            _db.SaveChanges();
            return RedirectToAction("UsersList");
        }
        #endregion

     /*   #region Ranks
        [HttpGet]
        public ActionResult RanksList(string city = null, string search = null)
        {
            var guard = GuardAdmin();
            if (guard != null) return guard;
            var vm = new RankListVM();
            vm.City = city;
            vm.Search = search;
            vm.CityOptions = _db.Ranks.GroupBy(x => x.City).OrderBy(x => x.Key).Select(g => new IdName { Id = 0, Name = g.Key }).ToList();
            var q = _db.Ranks.AsQueryable();
            if (!string.IsNullOrWhiteSpace(city)) q = q.Where(x => x.City == city);
            if (!string.IsNullOrWhiteSpace(search))
            {
                var s = search.ToLower();
                q = q.Where(x => x.Name.ToLower().Contains(s) || x.City.ToLower().Contains(s));
            }
            vm.Rows = q.OrderBy(x => x.City).ThenBy(x => x.Name).ToList().Select(x => new RankRowVM { RankId = x.RankId, Name = x.Name, City = x.City, Province = x.Province, IsActive = x.IsActive }).ToList();
            return View(vm);
        }

        [HttpGet]
        public ActionResult RankCreate()
        {
            var guard = GuardAdmin();
            if (guard != null) return guard;
            return View(new RankCreateVM());
        }

        private decimal? ParseCoord(string input)
        {
            if (string.IsNullOrWhiteSpace(input)) return null;
            var s = input.Trim().Replace(",", ".");
            if (decimal.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var val))
                return Math.Round(val, 6);
            if (decimal.TryParse(input, NumberStyles.Any, CultureInfo.CurrentCulture, out val))
                return Math.Round(val, 6);
            return null;
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult RankCreate(RankCreateVM model)
        {
            var guard = GuardAdmin();
            if (guard != null) return guard;

            var lat = ParseCoord(Request["Latitude"]);
            var lng = ParseCoord(Request["Longitude"]);
            var placeText = Request["PlaceId"];

            if (!ModelState.IsValid)
                return View(model);

            var r = new Rank
            {
                Name = model.Name,
                City = model.City,
                Province = model.Province,
                PlaceId = placeText,
                Latitude = lat,
                Longitude = lng,
                IsActive = model.IsActive,
                CreatedAt = DateTime.Now,
                CreatedBy = CurrentUserId()
            };
            _db.Ranks.Add(r);
            _db.SaveChanges();
            return RedirectToAction("RanksList");
        }


        [HttpGet]
        public ActionResult RankEdit(int id)
        {
            var guard = GuardAdmin();
            if (guard != null) return guard;
            var r = _db.Ranks.FirstOrDefault(x => x.RankId == id);
            if (r == null) return HttpNotFound();
            var vm = new RankCreateVM { RankId = r.RankId, Name = r.Name, City = r.City, Province = r.Province, PlaceId = r.PlaceId, Latitude = r.Latitude, Longitude = r.Longitude, IsActive = r.IsActive };
            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult RankEdit(RankCreateVM model)
        {
            var guard = GuardAdmin();
            if (guard != null) return guard;

            var lat = ParseCoord(Request["Latitude"]);
            var lng = ParseCoord(Request["Longitude"]);
            var placeText = Request["PlaceId"];

            if (!ModelState.IsValid)
                return View(model);

            var r = _db.Ranks.FirstOrDefault(x => x.RankId == model.RankId);
            if (r == null) return HttpNotFound();

            r.Name = model.Name;
            r.City = model.City;
            r.Province = model.Province;
            r.PlaceId = placeText;
            r.Latitude = lat;
            r.Longitude = lng;
            r.IsActive = model.IsActive;
            r.UpdatedAt = DateTime.Now;
            r.UpdatedBy = CurrentUserId();

            _db.SaveChanges();
            return RedirectToAction("RanksList");
        }

        #endregion

        #region Vehicles
        [HttpGet]
        public ActionResult VehiclesList(int? ownerId = null, int? rankId = null, bool? active = null, string search = null)
        {
            var guard = GuardAdmin();
            if (guard != null) return guard;
            var vm = new VehicleListVM { OwnerId = ownerId, RankId = rankId, Active = active, Search = search };
            vm.OwnerOptions = UserOptions("Owner");
            vm.RankOptions = RankOptions();
            var q = from v in _db.Vehicles
                    join o in _db.Users on v.OwnerId equals o.UserId
                    join r in _db.Ranks on v.HomeRankId equals r.RankId into rr
                    from r in rr.DefaultIfEmpty()
                    select new { v, o, r };
            if (ownerId.HasValue) q = q.Where(x => x.o.UserId == ownerId.Value);
            if (rankId.HasValue) q = q.Where(x => x.v.HomeRankId == rankId.Value);
            if (active.HasValue) q = q.Where(x => x.v.Active == active.Value);
            if (!string.IsNullOrWhiteSpace(search))
            {
                var s = search.ToLower();
                q = q.Where(x => x.v.RegNo.ToLower().Contains(s) || (x.v.Make ?? "").ToLower().Contains(s) || (x.v.Model ?? "").ToLower().Contains(s));
            }
            vm.Rows = q.OrderBy(x => x.v.RegNo).ToList().Select(x => new VehicleRowVM { VehicleId = x.v.VehicleId, RegNo = x.v.RegNo, Make = x.v.Make, Model = x.v.Model, Seats = x.v.Seats, OwnerName = x.o.DisplayName, HomeRank = x.r == null ? "" : x.r.Name, Active = x.v.Active }).ToList();
            return View(vm);
        }

        [HttpGet]
        public ActionResult VehicleCreate()
        {
            var guard = GuardAdmin();
            if (guard != null) return guard;
            var vm = new VehicleCreateVM { OwnerOptions = UserOptions("Owner"), RankOptions = RankOptions() };
            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult VehicleCreate(VehicleCreateVM model)
        {
            var guard = GuardAdmin();
            if (guard != null) return guard;
            model.OwnerOptions = UserOptions("Owner");
            model.RankOptions = RankOptions();
            if (!ModelState.IsValid) return View(model);
            if (_db.Vehicles.Any(x => x.RegNo == model.RegNo))
            {
                ModelState.AddModelError("", "Registration already exists");
                return View(model);
            }
            var v = new Vehicle { RegNo = model.RegNo, Make = model.Make, Model = model.Model, Year = model.Year, Seats = model.Seats, OwnerId = model.OwnerId, HomeRankId = model.HomeRankId, Active = model.Active, CreatedAt = DateTime.Now, CreatedBy = CurrentUserId() };
            _db.Vehicles.Add(v);
            _db.SaveChanges();
            return RedirectToAction("VehiclesList");
        }

        [HttpGet]
        public ActionResult VehicleEdit(int id)
        {
            var guard = GuardAdmin();
            if (guard != null) return guard;
            var v = _db.Vehicles.FirstOrDefault(x => x.VehicleId == id);
            if (v == null) return HttpNotFound();
            var vm = new VehicleCreateVM { VehicleId = v.VehicleId, RegNo = v.RegNo, Make = v.Make, Model = v.Model, Year = v.Year, Seats = v.Seats, OwnerId = v.OwnerId, HomeRankId = v.HomeRankId, Active = v.Active, OwnerOptions = UserOptions("Owner"), RankOptions = RankOptions() };
            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult VehicleEdit(VehicleCreateVM model)
        {
            var guard = GuardAdmin();
            if (guard != null) return guard;
            model.OwnerOptions = UserOptions("Owner");
            model.RankOptions = RankOptions();
            if (!ModelState.IsValid) return View(model);
            var v = _db.Vehicles.FirstOrDefault(x => x.VehicleId == model.VehicleId);
            if (v == null) return HttpNotFound();
            v.Make = model.Make;
            v.Model = model.Model;
            v.Year = model.Year;
            v.Seats = model.Seats;
            v.OwnerId = model.OwnerId;
            v.HomeRankId = model.HomeRankId;
            v.Active = model.Active;
            v.UpdatedAt = DateTime.Now;
            v.UpdatedBy = CurrentUserId();
            _db.SaveChanges();
            return RedirectToAction("VehiclesList");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult VehicleDocUpload(VehicleDocumentUploadVM model, HttpPostedFileBase upload)
        {
            var guard = GuardAdmin();
            if (guard != null) return guard;
            if (upload != null && upload.ContentLength > 0)
            {
                using (var ms = new MemoryStream())
                {
                    upload.InputStream.CopyTo(ms);
                    model.FileData = ms.ToArray();
                    model.FileName = Path.GetFileName(upload.FileName);
                }
            }
            if (!ModelState.IsValid) return RedirectToAction("VehiclesList");
            var doc = new VehicleDocument { VehicleId = model.VehicleId, DocType = model.DocType, FileName = model.FileName, FileData = model.FileData, UploadedAt = DateTime.Now, UploadedBy = CurrentUserId() };
            _db.VehicleDocuments.Add(doc);
            _db.SaveChanges();
            return RedirectToAction("VehiclesList");
        }

        [HttpGet]
        public ActionResult VehicleDocDownload(int id)
        {
            var guard = GuardAdmin();
            if (guard != null) return guard;
            var d = _db.VehicleDocuments.FirstOrDefault(x => x.VehicleDocumentId == id);
            if (d == null) return HttpNotFound();
            return File(d.FileData, "application/octet-stream", d.FileName);
        }

        [HttpGet]
        public ActionResult OwnerConfigEdit(int ownerId)
        {
            var guard = GuardAdmin();
            if (guard != null) return guard;
            var cfg = _db.OwnerConfigs.FirstOrDefault(x => x.OwnerId == ownerId);
            var vm = new OwnerConfigEditVM { OwnerId = ownerId, DriverSharePercent = cfg == null ? 30 : cfg.DriverSharePercent, OwnerOptions = UserOptions("Owner") };
            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult OwnerConfigEdit(OwnerConfigEditVM model)
        {
            var guard = GuardAdmin();
            if (guard != null) return guard;
            model.OwnerOptions = UserOptions("Owner");
            if (!ModelState.IsValid) return View(model);
            var cfg = _db.OwnerConfigs.FirstOrDefault(x => x.OwnerId == model.OwnerId);
            if (cfg == null)
            {
                cfg = new OwnerConfig { OwnerId = model.OwnerId, DriverSharePercent = model.DriverSharePercent, EffectiveFrom = DateTime.Now, CreatedAt = DateTime.Now, CreatedBy = CurrentUserId() };
                _db.OwnerConfigs.Add(cfg);
            }
            else
            {
                Audit("OwnerConfigs", cfg.OwnerConfigId, "DriverSharePercent", cfg.DriverSharePercent.ToString("0.00"), model.DriverSharePercent.ToString("0.00"));
                cfg.DriverSharePercent = model.DriverSharePercent;
            }
            _db.SaveChanges();
            return RedirectToAction("VehiclesList", new { ownerId = model.OwnerId });
        }

        [HttpGet]
        public ActionResult DriverAssignmentCreate()
        {
            var guard = GuardAdmin();
            if (guard != null) return guard;
            var vm = new DriverAssignmentCreateVM { DriverOptions = UserOptions("Driver"), VehicleOptions = VehicleOptions(), StartDate = DateTime.Today };
            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult DriverAssignmentCreate(DriverAssignmentCreateVM model)
        {
            var guard = GuardAdmin();
            if (guard != null) return guard;
            model.DriverOptions = UserOptions("Driver");
            model.VehicleOptions = VehicleOptions();
            if (!ModelState.IsValid) return View(model);
            var a = new DriverAssignment { DriverId = model.DriverId, VehicleId = model.VehicleId, StartDate = model.StartDate.Date, EndDate = model.EndDate, CreatedAt = DateTime.Now, CreatedBy = CurrentUserId() };
            _db.DriverAssignments.Add(a);
            _db.SaveChanges();
            return RedirectToAction("VehiclesList");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult DriverAssignmentEnd(int id, DateTime endDate)
        {
            var guard = GuardAdmin();
            if (guard != null) return guard;
            var a = _db.DriverAssignments.FirstOrDefault(x => x.AssignmentId == id);
            if (a == null) return HttpNotFound();
            a.EndDate = endDate.Date;
            _db.SaveChanges();
            return RedirectToAction("VehiclesList");
        }
        #endregion

        #region Routes
        [HttpGet]
        public ActionResult RoutesList(int? fromRankId = null, string search = null, bool? active = null)
        {
            var guard = GuardAdmin();
            if (guard != null) return guard;
            var vm = new RouteListVM { FromRankId = fromRankId, Search = search, Active = active, RankOptions = RankOptions() };
            var q = _db.Routes.AsQueryable();
            if (fromRankId.HasValue) q = q.Where(x => x.FromRankId == fromRankId.Value);
            if (!string.IsNullOrWhiteSpace(search))
            {
                var s = search.ToLower();
                q = q.Where(x => x.ToName.ToLower().Contains(s));
            }
            if (active.HasValue) q = q.Where(x => x.Active == active.Value);
            vm.Rows = q.OrderBy(x => x.ToName).Select(x => new RouteListRowVM { RouteId = x.RouteId, FromRank = _db.Ranks.Where(r => r.RankId == x.FromRankId).Select(r => r.Name).FirstOrDefault(), ToName = x.ToName, DefaultFare = x.DefaultFare, Active = x.Active }).ToList();
            return View(vm);
        }

        [HttpGet]
        public ActionResult RouteCreate()
        {
            var guard = GuardAdmin();
            if (guard != null) return guard;
            var vm = new RouteCreateVM { RankOptions = RankOptions(), Active = true };
            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult RouteCreate(RouteCreateVM model)
        {
            var guard = GuardAdmin();
            if (guard != null) return guard;
            model.RankOptions = RankOptions();
            if (!ModelState.IsValid) return View(model);
            var r = new Route { FromRankId = model.FromRankId, ToName = model.ToName, ToPlaceId = model.ToPlaceId, ToLatitude = model.ToLatitude, ToLongitude = model.ToLongitude, DefaultFare = model.DefaultFare, DistanceKm = model.DistanceKm, Active = model.Active, CreatedAt = DateTime.Now, CreatedBy = CurrentUserId() };
            _db.Routes.Add(r);
            _db.SaveChanges();
            return RedirectToAction("RoutesList");
        }

        [HttpGet]
        public ActionResult RouteDetail(int id)
        {
            var guard = GuardAdmin();
            if (guard != null) return guard;
            var r = _db.Routes.FirstOrDefault(x => x.RouteId == id);
            if (r == null) return HttpNotFound();
            var vm = new RouteDetailVM
            {
                RouteId = r.RouteId,
                FromRank = _db.Ranks.Where(x => x.RankId == r.FromRankId).Select(x => x.Name).FirstOrDefault(),
                ToName = r.ToName,
                DefaultFare = r.DefaultFare,
                DistanceKm = r.DistanceKm,
                Active = r.Active,
                Waypoints = _db.RouteWaypoints.Where(w => w.RouteId == r.RouteId).OrderBy(w => w.Sequence).Select(w => new RouteWaypointLineVM { RouteWaypointId = w.RouteWaypointId, RouteId = w.RouteId, Name = w.Name, PlaceId = w.PlaceId, Latitude = w.Latitude, Longitude = w.Longitude, Sequence = w.Sequence }).ToList(),
                FareRules = _db.FareRules.Where(f => f.RouteId == r.RouteId).OrderBy(f => f.RuleType).Select(f => new FareRuleLineVM { FareRuleId = f.FareRuleId, RouteId = f.RouteId, RuleType = f.RuleType, Amount = f.Amount, StartDate = f.StartDate.HasValue ? f.StartDate.Value : (DateTime?)null, EndDate = f.EndDate.HasValue ? f.EndDate.Value : (DateTime?)null }).ToList()
            };
            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult RouteToggle(int id)
        {
            var guard = GuardAdmin();
            if (guard != null) return guard;
            var r = _db.Routes.FirstOrDefault(x => x.RouteId == id);
            if (r == null) return HttpNotFound();
            r.Active = !r.Active;
            _db.SaveChanges();
            return RedirectToAction("RouteDetail", new { id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult WaypointAdd(RouteWaypointLineVM model)
        {
            var guard = GuardAdmin();
            if (guard != null) return guard;
            if (!ModelState.IsValid) return RedirectToAction("RouteDetail", new { id = model.RouteId });
            var w = new RouteWaypoint { RouteId = model.RouteId, Name = model.Name, PlaceId = model.PlaceId, Latitude = model.Latitude, Longitude = model.Longitude, Sequence = model.Sequence, CreatedAt = DateTime.Now, CreatedBy = CurrentUserId() };
            _db.RouteWaypoints.Add(w);
            _db.SaveChanges();
            return RedirectToAction("RouteDetail", new { id = model.RouteId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult WaypointRemove(int id)
        {
            var guard = GuardAdmin();
            if (guard != null) return guard;
            var w = _db.RouteWaypoints.FirstOrDefault(x => x.RouteWaypointId == id);
            if (w == null) return HttpNotFound();
            var rid = w.RouteId;
            _db.RouteWaypoints.Remove(w);
            _db.SaveChanges();
            return RedirectToAction("RouteDetail", new { id = rid });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult FareRuleAdd(FareRuleLineVM model)
        {
            var guard = GuardAdmin();
            if (guard != null) return guard;
            if (!ModelState.IsValid) return RedirectToAction("RouteDetail", new { id = model.RouteId });
            var f = new FareRule { RouteId = model.RouteId, RuleType = model.RuleType, Amount = model.Amount, StartDate = model.StartDate, EndDate = model.EndDate, CreatedAt = DateTime.Now, CreatedBy = CurrentUserId() };
            _db.FareRules.Add(f);
            _db.SaveChanges();
            return RedirectToAction("RouteDetail", new { id = model.RouteId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult FareRuleRemove(int id)
        {
            var guard = GuardAdmin();
            if (guard != null) return guard;
            var f = _db.FareRules.FirstOrDefault(x => x.FareRuleId == id);
            if (f == null) return HttpNotFound();
            var rid = f.RouteId;
            _db.FareRules.Remove(f);
            _db.SaveChanges();
            return RedirectToAction("RouteDetail", new { id = rid });
        }
        #endregion

        #region Departures
        [HttpGet]
        public ActionResult DeparturesList(int? routeId = null, string status = null, DateTime? from = null, DateTime? to = null)
        {
            var guard = GuardAdmin();
            if (guard != null) return guard;
            var vm = new DepartureListVM { RouteId = routeId, Status = status, FromDate = from, ToDate = to, RouteOptions = RouteOptions(), RankOptions = RankOptions(), StatusOptions = new List<IdName> { new IdName { Id = 0, Name = "Queued" }, new IdName { Id = 0, Name = "Boarding" }, new IdName { Id = 0, Name = "Ready" }, new IdName { Id = 0, Name = "Departed" }, new IdName { Id = 0, Name = "Cancelled" } } };
            var q = _db.Departures.AsQueryable();
            if (routeId.HasValue) q = q.Where(x => x.RouteId == routeId.Value);
            if (!string.IsNullOrWhiteSpace(status)) q = q.Where(x => x.Status == status);
            if (from.HasValue) q = q.Where(x => x.Date >= from.Value.Date);
            if (to.HasValue) q = q.Where(x => x.Date <= to.Value.Date);
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
        public ActionResult DepartureCreate()
        {
            var guard = GuardAdmin();
            if (guard != null) return guard;
            var vm = new DepartureCreateVM { RouteOptions = RouteOptions(), VehicleOptions = VehicleOptions(), DriverOptions = UserOptions("Driver"), Date = DateTime.Today, PlannedTime = new TimeSpan(8, 0, 0), MinFill = 10, Status = "Queued" };
            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult DepartureCreate(DepartureCreateVM model)
        {
            var guard = GuardAdmin();
            if (guard != null) return guard;
            model.RouteOptions = RouteOptions();
            model.VehicleOptions = VehicleOptions();
            model.DriverOptions = UserOptions("Driver");
            if (!ModelState.IsValid) return View(model);
            using (var tx = _db.Database.BeginTransaction())
            {
                var d = new Departure
                {
                    RouteId = model.RouteId,
                    Date = model.Date.Date,
                    PlannedTime = model.PlannedTime,
                    VehicleId = model.VehicleId,
                    DriverId = model.DriverId,
                    MinFill = model.MinFill,
                    Status = model.Status,
                    ReadyChecklist_MinFill = false,
                    ReadyChecklist_CashVerified = false,
                    ReadyChecklist_FloatIssued = false,
                    ReadyChecklist_VehicleCleared = false,
                    ManagerOverrideReady = false,
                    CreatedAt = DateTime.Now,
                    CreatedBy = CurrentUserId()
                };
                _db.Departures.Add(d);
                _db.SaveChanges();
                var seats = _db.Vehicles.Where(v => v.VehicleId == model.VehicleId).Select(v => v.Seats).FirstOrDefault();
                for (int i = 1; i <= seats; i++)
                {
                    _db.SeatInventories.Add(new SeatInventory { DepartureId = d.DepartureId, SeatNo = i, Status = "Free", CreatedAt = DateTime.Now, CreatedBy = CurrentUserId() });
                }
                _db.SaveChanges();
                tx.Commit();
                return RedirectToAction("DepartureDetail", new { id = d.DepartureId });
            }
        }

        [HttpGet]
        public ActionResult DepartureDetail(int id)
        {
            var guard = GuardAdmin();
            if (guard != null) return guard;
            var d = _db.Departures.FirstOrDefault(x => x.DepartureId == id);
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
        public ActionResult DepartureStatus(int id, string status, bool overrideReady = false)
        {
            var guard = GuardAdmin();
            if (guard != null) return guard;
            var d = _db.Departures.FirstOrDefault(x => x.DepartureId == id);
            if (d == null) return HttpNotFound();
            d.Status = status;
            if (overrideReady) d.ManagerOverrideReady = true;
            d.UpdatedAt = DateTime.Now;
            d.UpdatedBy = CurrentUserId();
            _db.SaveChanges();
            return RedirectToAction("DepartureDetail", new { id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult ReadyChecklist(int id, bool minFill, bool cashVerified, bool floatIssued, bool vehicleCleared)
        {
            var guard = GuardAdmin();
            if (guard != null) return guard;
            var d = _db.Departures.FirstOrDefault(x => x.DepartureId == id);
            if (d == null) return HttpNotFound();
            d.ReadyChecklist_MinFill = minFill;
            d.ReadyChecklist_CashVerified = cashVerified;
            d.ReadyChecklist_FloatIssued = floatIssued;
            d.ReadyChecklist_VehicleCleared = vehicleCleared;
            _db.SaveChanges();
            return RedirectToAction("DepartureDetail", new { id });
        }
        #endregion

        #region Bookings
        [HttpGet]
        public ActionResult BookingCreate(int departureId)
        {
            var guard = GuardAdmin();
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
            var guard = GuardAdmin();
            if (guard != null) return guard;
            model.AvailableSeats = _db.SeatInventories.Where(s => s.DepartureId == model.DepartureId).OrderBy(s => s.SeatNo).Select(s => new SeatCellVM { SeatNo = s.SeatNo, Status = s.Status, BookingId = s.BookingId }).ToList();
            if (!ModelState.IsValid) return View(model);
            selectedSeats = selectedSeats ?? new int[0];
            if (selectedSeats.Length != model.SeatsCount)
            {
                ModelState.AddModelError("", "Seat selection mismatch");
                return View(model);
            }
            using (var tx = _db.Database.BeginTransaction())
            {
                var freeOk = _db.SeatInventories.Count(s => s.DepartureId == model.DepartureId && selectedSeats.Contains(s.SeatNo) && s.Status == "Free") == selectedSeats.Length;
                if (!freeOk)
                {
                    ModelState.AddModelError("", "Some seats are not available");
                    return View(model);
                }
                var p = new Passenger { FullName = model.Passenger.FullName, Phone = model.Passenger.Phone, NextOfKinName = model.Passenger.NextOfKinName, NextOfKinPhone = model.Passenger.NextOfKinPhone, CreatedAt = DateTime.Now, CreatedBy = CurrentUserId() };
                _db.Passengers.Add(p);
                _db.SaveChanges();
                var b = new Booking
                {
                    DepartureId = model.DepartureId,
                    PassengerId = p.PassengerId,
                    SeatsCount = model.SeatsCount,
                    FareEach = model.FareEach,
                    PaymentMethod = model.PaymentMethod,
                    Status = "Held",
                    Pnr = "PNR" + DateTime.Now.Ticks.ToString().Substring(8),
                    ExpiresAt = DateTime.Now.AddMinutes(20),
                    CreatedAt = DateTime.Now,
                    CreatedBy = CurrentUserId()
                };
                _db.Bookings.Add(b);
                _db.SaveChanges();
                foreach (var seat in selectedSeats)
                {
                    _db.BookingSeats.Add(new BookingSeat { BookingId = b.BookingId, DepartureId = b.DepartureId, SeatNo = seat, CreatedAt = DateTime.Now, CreatedBy = CurrentUserId() });
                    var inv = _db.SeatInventories.First(s => s.DepartureId == b.DepartureId && s.SeatNo == seat);
                    inv.Status = "Booked";
                    inv.BookingId = b.BookingId;
                }
                _db.SaveChanges();
                if (model.PaymentMethod == "Cash")
                {
                    _db.Payments.Add(new Payment { BookingId = b.BookingId, Amount = (decimal)b.ExtendedAmount, Method = "Cash", Reference = "CASH-" + b.Pnr, PaidAt = DateTime.Now, CreatedAt = DateTime.Now, CreatedBy = CurrentUserId() });
                    b.Status = "Confirmed";
                    b.ExpiresAt = null;
                    _db.SaveChanges();
                }
                tx.Commit();
                return RedirectToAction("BookingDetail", new { id = b.BookingId });
            }
        }
        // AdminController
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult BookingConfirmCard(int id)
        {
            var guard = GuardAdmin();
            if (guard != null) return guard;
            var b = _db.Bookings.FirstOrDefault(x => x.BookingId == id);
            if (b == null) return HttpNotFound();

            Stripe.StripeConfiguration.ApiKey = ConfigurationManager.AppSettings["StripeSecretKey"];
            var amountCents = (long)Math.Round(((decimal)b.ExtendedAmount) * 100m, 0);

            var options = new Stripe.Checkout.SessionCreateOptions
            {
                Mode = "payment",
                SuccessUrl = Url.Action("BookingDetail", "Admin", new { id = b.BookingId }, protocol: Request.Url.Scheme) + "&cs_id={CHECKOUT_SESSION_ID}",
                CancelUrl = Url.Action("BookingDetail", "Admin", new { id = b.BookingId }, protocol: Request.Url.Scheme),
                PaymentMethodTypes = new List<string> { "card" },
                LineItems = new List<Stripe.Checkout.SessionLineItemOptions>
        {
            new Stripe.Checkout.SessionLineItemOptions
            {
                Quantity = 1,
                PriceData = new Stripe.Checkout.SessionLineItemPriceDataOptions
                {
                    Currency = "zar",
                    UnitAmount = amountCents,
                    ProductData = new Stripe.Checkout.SessionLineItemPriceDataProductDataOptions
                    {
                        Name = "Ticket " + b.Pnr
                    }
                }
            }
        }
            };

            var service = new Stripe.Checkout.SessionService();
            var session = service.Create(options);
            return Redirect(session.Url);
        }

        [HttpGet]
        public ActionResult BookingDetail(int id)
        {
            var guard = GuardAdmin();
            if (guard != null) return guard;
            var b = _db.Bookings.FirstOrDefault(x => x.BookingId == id);
            if (b == null) return HttpNotFound();

            var csId = Request["cs_id"];
            if (!string.IsNullOrWhiteSpace(csId))
            {
                Stripe.StripeConfiguration.ApiKey = ConfigurationManager.AppSettings["StripeSecretKey"];
                var sService = new Stripe.Checkout.SessionService();
                var s = sService.Get(csId);
                if (s.PaymentStatus == "paid")
                {
                    if (b.Status != "Confirmed")
                    {
                        _db.Payments.Add(new Payment { BookingId = b.BookingId, Amount = (decimal)b.ExtendedAmount, Method = "Card", Reference = s.PaymentIntentId, PaidAt = DateTime.Now, CreatedAt = DateTime.Now, CreatedBy = CurrentUserId() });
                        b.Status = "Confirmed";
                        b.ExpiresAt = null;
                        _db.SaveChanges();
                    }
                }
            }

            var rname = _db.Routes.Where(r => r.RouteId == _db.Departures.Where(d => d.DepartureId == b.DepartureId).Select(d => d.RouteId).FirstOrDefault()).Select(r => r.ToName).FirstOrDefault();
            var ddate = _db.Departures.Where(d => d.DepartureId == b.DepartureId).Select(d => d.Date).FirstOrDefault();
            var dtime = _db.Departures.Where(d => d.DepartureId == b.DepartureId).Select(d => d.PlannedTime).FirstOrDefault();
            var pax = _db.Passengers.FirstOrDefault(p => p.PassengerId == b.PassengerId);
            var vm = new BookingDetailVM
            {
                BookingId = b.BookingId,
                Pnr = b.Pnr,
                RouteName = rname,
                DepartureDate = ddate,
                PlannedTime = dtime,
                PassengerName = pax == null ? "" : pax.FullName,
                PassengerPhone = pax == null ? "" : pax.Phone,
                SeatsCount = b.SeatsCount,
                FareEach = b.FareEach,
                ExtendedAmount = (decimal)b.ExtendedAmount,
                Status = b.Status,
                PaymentMethod = b.PaymentMethod,
                SeatNumbers = _db.BookingSeats.Where(s => s.BookingId == b.BookingId).Select(s => s.SeatNo).ToList(),
                Payments = _db.Payments.Where(p => p.BookingId == b.BookingId).OrderByDescending(p => p.PaidAt).Select(p => new PaymentRowVM { PaymentId = p.PaymentId, PaidAt = p.PaidAt, Amount = p.Amount, Method = p.Method, Reference = p.Reference }).ToList()
            };
            return View(vm);
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult BookingCancel(int id)
        {
            var guard = GuardAdmin();
            if (guard != null) return guard;
            using (var tx = _db.Database.BeginTransaction())
            {
                var b = _db.Bookings.FirstOrDefault(x => x.BookingId == id);
                if (b == null) return HttpNotFound();
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
                return RedirectToAction("DepartureDetail", new { id = b.DepartureId });
            }
        }

        [HttpGet]
        public ActionResult BookingsList(string pnr = null, string status = null, string method = null, DateTime? from = null, DateTime? to = null, int? routeId = null)
        {
            var guard = GuardAdmin();
            if (guard != null) return guard;
            var vm = new BookingListVM { Pnr = pnr, Status = status, PaymentMethod = method, FromDate = from, ToDate = to, RouteId = routeId, StatusOptions = new List<IdName> { new IdName { Id = 0, Name = "Held" }, new IdName { Id = 0, Name = "Confirmed" }, new IdName { Id = 0, Name = "Cancelled" } }, PaymentOptions = new List<IdName> { new IdName { Id = 0, Name = "Cash" }, new IdName { Id = 0, Name = "Card" } }, RouteOptions = RouteOptions() };
            var q = _db.Bookings.AsQueryable();
            if (!string.IsNullOrWhiteSpace(pnr)) q = q.Where(x => x.Pnr == pnr);
            if (!string.IsNullOrWhiteSpace(status)) q = q.Where(x => x.Status == status);
            if (!string.IsNullOrWhiteSpace(method)) q = q.Where(x => x.PaymentMethod == method);
            if (from.HasValue) q = q.Where(x => x.CreatedAt >= from.Value);
            if (to.HasValue) q = q.Where(x => x.CreatedAt <= to.Value);
            if (routeId.HasValue) q = q.Where(x => _db.Departures.Where(d => d.DepartureId == x.DepartureId).Select(d => d.RouteId).FirstOrDefault() == routeId.Value);
            vm.Rows = q.OrderByDescending(x => x.CreatedAt).ToList().Select(b => new BookingRowVM { BookingId = b.BookingId, Pnr = b.Pnr, PassengerName = _db.Passengers.Where(p => p.PassengerId == b.PassengerId).Select(p => p.FullName).FirstOrDefault(), SeatsCount = b.SeatsCount, FareEach = b.FareEach, ExtendedAmount = (decimal)b.ExtendedAmount, Status = b.Status, PaymentMethod = b.PaymentMethod }).ToList();
            return View(vm);
        }

        [HttpGet]
        public ActionResult BookingsExportCsv(DateTime? from = null, DateTime? to = null)
        {
            var guard = GuardAdmin();
            if (guard != null) return guard;
            var q = _db.Bookings.AsQueryable();
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
                  b.ExtendedAmount.GetValueOrDefault().ToString("0.00"),

                    b.Status,
                    b.PaymentMethod
                }.Select(x => "\"" + x.Replace("\"","\"\"") + "\"")));
            }
            return Csv("bookings.csv", sb.ToString());
        }
        #endregion

        #region Money
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult FloatIssue(FloatIssueVM model)
        {
            var guard = GuardAdmin();
            if (guard != null) return guard;
            if (!ModelState.IsValid) return RedirectToAction("DepartureDetail", new { id = model.DepartureId });
            _db.Floats.Add(new Float { DepartureId = model.DepartureId, AmountIssued = model.AmountIssued, Purpose = model.Purpose, IssuedBy = CurrentUserId(), Timestamp = DateTime.Now });
            _db.SaveChanges();
            return RedirectToAction("DepartureDetail", new { id = model.DepartureId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Cashup(CashupCreateVM model)
        {
            var guard = GuardAdmin();
            if (guard != null) return guard;
            if (!ModelState.IsValid) return RedirectToAction("DepartureDetail", new { id = model.DepartureId });
            _db.CashupBags.Add(new CashupBag { DepartureId = model.DepartureId, CountedAmount = model.CountedAmount, CountedBy = CurrentUserId(), Timestamp = DateTime.Now });
            _db.SaveChanges();
            return RedirectToAction("DepartureDetail", new { id = model.DepartureId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult ExpenseCreate(ExpenseCreateVM model, HttpPostedFileBase proof)
        {
            var guard = GuardAdmin();
            if (guard != null) return guard;
            int? attachId = null;
            if (proof != null && proof.ContentLength > 0)
            {
                using (var ms = new MemoryStream())
                {
                    proof.InputStream.CopyTo(ms);
                    var a = new Models.Attachment { Entity = "Expense", EntityId = 0, FileName = Path.GetFileName(proof.FileName), FileData = ms.ToArray(), UploadedAt = DateTime.Now, UploadedBy = CurrentUserId() };
                    _db.Attachments.Add(a);
                    _db.SaveChanges();
                    attachId = a.AttachmentId;
                }
            }
            _db.Expenses.Add(new Expens { DepartureId = model.DepartureId, Type = model.Type, Amount = model.Amount, ProofAttachmentId = attachId, CreatedAt = DateTime.Now, CreatedBy = CurrentUserId() });
            _db.SaveChanges();
            return RedirectToAction("DepartureDetail", new { id = model.DepartureId });
        }
        #endregion

        #region AlertsApprovals
        [HttpGet]
        public ActionResult AlertRulesEdit()
        {
            var guard = GuardAdmin();
            if (guard != null) return guard;
            var r = _db.AlertRules.OrderByDescending(x => x.AlertRuleId).FirstOrDefault();
            if (r == null)
            {
                r = new AlertRule { MinFillComplianceThreshold = 0.80m, LateDepartureMinutes = 10, CashVarianceThreshold = 100, IncidentSpikeThreshold = 3, CreatedAt = DateTime.Now, CreatedBy = CurrentUserId() };
                _db.AlertRules.Add(r);
                _db.SaveChanges();
            }
            var vm = new AlertRuleEditVM { AlertRuleId = r.AlertRuleId, MinFillComplianceThreshold = r.MinFillComplianceThreshold, LateDepartureMinutes = r.LateDepartureMinutes, CashVarianceThreshold = r.CashVarianceThreshold, IncidentSpikeThreshold = r.IncidentSpikeThreshold };
            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult AlertRulesEdit(AlertRuleEditVM model)
        {
            var guard = GuardAdmin();
            if (guard != null) return guard;
            var r = _db.AlertRules.FirstOrDefault(x => x.AlertRuleId == model.AlertRuleId);
            if (r == null) return HttpNotFound();
            r.MinFillComplianceThreshold = model.MinFillComplianceThreshold;
            r.LateDepartureMinutes = model.LateDepartureMinutes;
            r.CashVarianceThreshold = model.CashVarianceThreshold;
            r.IncidentSpikeThreshold = model.IncidentSpikeThreshold;
            _db.SaveChanges();
            return RedirectToAction("AlertsList");
        }

        [HttpGet]
        public ActionResult AlertsList(string type = null, string severity = null, string status = null, DateTime? from = null, DateTime? to = null)
        {
            var guard = GuardAdmin();
            if (guard != null) return guard;
            var vm = new AlertListVM { Type = type, Severity = severity, Status = status, FromDate = from, ToDate = to, TypeOptions = new List<IdName> { new IdName { Id = 0, Name = "MinFillBreach" }, new IdName { Id = 0, Name = "LateDeparture" }, new IdName { Id = 0, Name = "CashVariance" }, new IdName { Id = 0, Name = "IncidentSpike" } }, SeverityOptions = new List<IdName> { new IdName { Id = 0, Name = "Low" }, new IdName { Id = 0, Name = "Medium" }, new IdName { Id = 0, Name = "High" } }, StatusOptions = new List<IdName> { new IdName { Id = 0, Name = "Open" }, new IdName { Id = 0, Name = "Resolved" } } };
            var q = _db.Alerts.AsQueryable();
            if (!string.IsNullOrWhiteSpace(type)) q = q.Where(x => x.Type == type);
            if (!string.IsNullOrWhiteSpace(severity)) q = q.Where(x => x.Severity == severity);
            if (!string.IsNullOrWhiteSpace(status))
            {
                if (status == "Open") q = q.Where(x => x.ResolvedAt == null);
                if (status == "Resolved") q = q.Where(x => x.ResolvedAt != null);
            }
            if (from.HasValue) q = q.Where(x => x.CreatedAt >= from.Value);
            if (to.HasValue) q = q.Where(x => x.CreatedAt <= to.Value);
            vm.Rows = q.OrderByDescending(x => x.CreatedAt).Select(a => new AlertRowVM { AlertId = a.AlertId, Type = a.Type, Entity = a.Entity, EntityId = a.EntityId, Severity = a.Severity, Message = a.Message, CreatedAt = a.CreatedAt, ResolvedAt = a.ResolvedAt }).ToList();
            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult AlertResolve(int id)
        {
            var guard = GuardAdmin();
            if (guard != null) return guard;
            var a = _db.Alerts.FirstOrDefault(x => x.AlertId == id);
            if (a == null) return HttpNotFound();
            a.ResolvedAt = DateTime.Now;
            a.ResolvedBy = CurrentUserId();
            _db.SaveChanges();
            return RedirectToAction("AlertsList");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult RunAlertsNow()
        {
            var guard = GuardAdmin();
            if (guard != null) return guard;
            var r = _db.AlertRules.OrderByDescending(x => x.AlertRuleId).FirstOrDefault();
            if (r == null) return RedirectToAction("AlertsList");
            var now = DateTime.Now;
            var depLate = _db.Departures.Where(d => d.Status == "Queued" || d.Status == "Boarding").ToList().Where(d => d.Date.Add(d.PlannedTime) < now.AddMinutes(-r.LateDepartureMinutes)).Select(d => d.DepartureId).ToList();
            foreach (var id in depLate)
            {
                _db.Alerts.Add(new Alert { Type = "LateDeparture", Entity = "Departure", EntityId = id, Severity = "Medium", Message = "Late departure", CreatedAt = DateTime.Now });
            }
            var depFill = _db.Departures.Where(d => d.Status == "Boarding" || d.Status == "Ready").ToList();
            foreach (var d in depFill)
            {
                var total = _db.Vehicles.Where(v => v.VehicleId == d.VehicleId).Select(v => v.Seats).FirstOrDefault();
                var booked = _db.SeatInventories.Count(s => s.DepartureId == d.DepartureId && s.Status == "Booked");
                var compliance = total == 0 ? 0 : (decimal)booked / (decimal)total;
                if (compliance < r.MinFillComplianceThreshold)
                {
                    _db.Alerts.Add(new Alert { Type = "MinFillBreach", Entity = "Departure", EntityId = d.DepartureId, Severity = "Low", Message = "Low fill rate", CreatedAt = DateTime.Now });
                }
            }
            _db.SaveChanges();
            return RedirectToAction("AlertsList");
        }

        [HttpGet]
        public ActionResult ApprovalsList(string status = "Pending")
        {
            var guard = GuardAdmin();
            if (guard != null) return guard;
            var vm = new ApprovalListVM { Status = status, StatusOptions = new List<IdName> { new IdName { Id = 0, Name = "Pending" }, new IdName { Id = 0, Name = "Approved" }, new IdName { Id = 0, Name = "Rejected" } } };
            var q = _db.Approvals.AsQueryable();
            if (!string.IsNullOrWhiteSpace(status)) q = q.Where(x => x.Status == status);
            vm.Rows = q.OrderByDescending(x => x.RequestedAt).ToList().Select(x => new ApprovalRowVM { ApprovalId = x.ApprovalId, Entity = x.Entity, EntityId = x.EntityId, Action = x.Action, Status = x.Status, RequestedBy = _db.Users.Where(u => u.UserId == x.RequestedBy).Select(u => u.DisplayName).FirstOrDefault(), RequestedAt = x.RequestedAt }).ToList();
            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult ApprovalsAction(ApprovalActionVM model)
        {
            var guard = GuardAdmin();
            if (guard != null) return guard;
            var a = _db.Approvals.FirstOrDefault(x => x.ApprovalId == model.ApprovalId);
            if (a == null) return HttpNotFound();
            a.Status = model.Decision;
            a.ApprovedAt = DateTime.Now;
            a.ApprovedBy = CurrentUserId();
            a.Notes = model.Notes;
            _db.SaveChanges();
            return RedirectToAction("ApprovalsList");
        }
        #endregion

        #region Telemetry
        [HttpGet]
        public ActionResult TelemetryTrail(int departureId)
        {
            var guard = GuardAdmin();
            if (guard != null) return guard;
            var points = _db.Telemetries.Where(t => t.DepartureId == departureId).OrderBy(t => t.Timestamp).Select(t => new TelemetryPointVM { Timestamp = t.Timestamp, Latitude = t.Latitude, Longitude = t.Longitude, SpeedKph = t.SpeedKph, Battery = t.Battery }).ToList();
            var vm = new TelemetryListVM { DepartureId = departureId, Points = points };
            return View(vm);
        }
        #endregion

        #region Charters
        [HttpGet]
        public ActionResult ChartersList(string status = null, DateTime? from = null, DateTime? to = null)
        {
            var guard = GuardAdmin();
            if (guard != null) return guard;
            var vm = new CharterListVM { Status = status, FromDate = from, ToDate = to, StatusOptions = new List<IdName> { new IdName { Id = 0, Name = "Requested" }, new IdName { Id = 0, Name = "Quoted" }, new IdName { Id = 0, Name = "Approved" }, new IdName { Id = 0, Name = "Assigned" }, new IdName { Id = 0, Name = "InProgress" }, new IdName { Id = 0, Name = "Completed" }, new IdName { Id = 0, Name = "Cancelled" } } };
            var q = _db.Charters.AsQueryable();
            if (!string.IsNullOrWhiteSpace(status)) q = q.Where(x => x.Status == status);
            if (from.HasValue) q = q.Where(x => x.Date >= from.Value);
            if (to.HasValue) q = q.Where(x => x.Date <= to.Value);
            vm.Rows = q.OrderByDescending(x => x.Date).Select(x => new CharterRowVM { CharterId = x.CharterId, Organizer = x.Organizer, Date = x.Date, Status = x.Status, QuoteAmount = x.QuoteAmount, DepositPaid = x.DepositPaid }).ToList();
            return View(vm);
        }

        [HttpGet]
        public ActionResult CharterCreate()
        {
            var guard = GuardAdmin();
            if (guard != null) return guard;
            var vm = new CharterRequestVM { VehicleOptions = VehicleOptions(), DriverOptions = UserOptions("Driver"), Date = DateTime.Today.AddDays(1) };
            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult CharterCreate(CharterRequestVM model)
        {
            var guard = GuardAdmin();
            if (guard != null) return guard;
            model.VehicleOptions = VehicleOptions();
            model.DriverOptions = UserOptions("Driver");
            if (!ModelState.IsValid) return View(model);
            var c = new Charter
            {
                Organizer = model.Organizer,
                Contact = model.Contact,
                Date = model.Date.Date,
                PickupPlaceId = model.Pickup.PlaceId,
                PickupLat = model.Pickup.Latitude,
                PickupLng = model.Pickup.Longitude,
                DropPlaceId = model.Dropoff.PlaceId,
                DropLat = model.Dropoff.Latitude,
                DropLng = model.Dropoff.Longitude,
                Pax = model.Pax,
                Status = model.Status,
                QuoteAmount = model.QuoteAmount,
                DepositPaid = model.DepositPaid,
                CreatedAt = DateTime.Now,
                CreatedBy = CurrentUserId()
            };
            _db.Charters.Add(c);
            _db.SaveChanges();
            foreach (var a in model.Assignments)
            {
                _db.CharterVehicles.Add(new CharterVehicle { CharterId = c.CharterId, VehicleId = a.VehicleId, DriverId = a.DriverId });
            }
            foreach (var p in model.Passengers)
            {
                _db.CharterPassengers.Add(new CharterPassenger { CharterId = c.CharterId, FullName = p.FullName, Phone = p.Phone, CreatedAt = DateTime.Now, CreatedBy = CurrentUserId() });
            }
            _db.SaveChanges();
            return RedirectToAction("ChartersList");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult CharterStatus(int id, string status, bool depositPaid = false)
        {
            var guard = GuardAdmin();
            if (guard != null) return guard;
            var c = _db.Charters.FirstOrDefault(x => x.CharterId == id);
            if (c == null) return HttpNotFound();
            c.Status = status;
            c.DepositPaid = depositPaid || c.DepositPaid;
            _db.SaveChanges();
            return RedirectToAction("ChartersList");
        }
        #endregion

        #region SettlementsReporting
        [HttpGet]
        public ActionResult SettlementsList(int? ownerId = null, DateTime? from = null, DateTime? to = null)
        {
            var guard = GuardAdmin();
            if (guard != null) return guard;
            var vm = new SettlementListVM { OwnerId = ownerId, FromDay = from, ToDay = to, OwnerOptions = UserOptions("Owner") };
            var q = _db.Settlements.AsQueryable();
            if (ownerId.HasValue) q = q.Where(x => x.OwnerId == ownerId.Value);
            if (from.HasValue) q = q.Where(x => x.PeriodDay >= from.Value);
            if (to.HasValue) q = q.Where(x => x.PeriodDay <= to.Value);
            vm.Rows = q.OrderByDescending(x => x.PeriodDay).ToList().Select(x => new SettlementRowVM { SettlementId = x.SettlementId, OwnerName = _db.Users.Where(u => u.UserId == x.OwnerId).Select(u => u.DisplayName).FirstOrDefault(), PeriodDay = x.PeriodDay, Gross = x.Gross, Expenses = x.Expenses, DriverShare = x.DriverShare, NetToOwner = x.NetToOwner }).ToList();
            return View(vm);
        }

        [HttpGet]
        public ActionResult SettlementGenerate()
        {
            var guard = GuardAdmin();
            if (guard != null) return guard;
            var vm = new SettlementGenerateVM { OwnerOptions = UserOptions("Owner"), PeriodDay = DateTime.Today.AddDays(-1) };
            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult SettlementGenerate(SettlementGenerateVM model)
        {
            var guard = GuardAdmin();
            if (guard != null) return guard;
            model.OwnerOptions = UserOptions("Owner");
            if (!ModelState.IsValid) return View(model);
            var ownerVehicles = _db.Vehicles.Where(v => v.OwnerId == model.OwnerId).Select(v => v.VehicleId).ToList();
            var depIds = _db.Departures.Where(d => ownerVehicles.Contains(d.VehicleId) && d.Date == model.PeriodDay.Date).Select(d => d.DepartureId).ToList();
            var gross = _db.Payments.Where(p => _db.Bookings.Any(b => b.BookingId == p.BookingId && depIds.Contains(b.DepartureId))).Select(p => (decimal?)p.Amount).DefaultIfEmpty(0).Sum().GetValueOrDefault();
            var expenses = _db.Expenses.Where(e => depIds.Contains(e.DepartureId)).Select(e => (decimal?)e.Amount).DefaultIfEmpty(0).Sum().GetValueOrDefault();
            var sharePct = _db.OwnerConfigs.Where(o => o.OwnerId == model.OwnerId).Select(o => (decimal?)o.DriverSharePercent).FirstOrDefault().GetValueOrDefault(30);
            var driverShare = Math.Round(gross * sharePct / 100m, 2);
            var net = gross - expenses - driverShare;
            var s = _db.Settlements.FirstOrDefault(x => x.OwnerId == model.OwnerId && x.PeriodDay == model.PeriodDay.Date);
            if (s == null)
            {
                s = new Settlement { OwnerId = model.OwnerId, PeriodDay = model.PeriodDay.Date, Gross = gross, Expenses = expenses, DriverShare = driverShare, NetToOwner = net, CreatedAt = DateTime.Now, CreatedBy = CurrentUserId() };
                _db.Settlements.Add(s);
            }
            else
            {
                s.Gross = gross;
                s.Expenses = expenses;
                s.DriverShare = driverShare;
                s.NetToOwner = net;
            }
            _db.SaveChanges();
            return RedirectToAction("SettlementsList", new { ownerId = model.OwnerId });
        }

        [HttpGet]
        public ActionResult DeparturesExportCsv(DateTime? from = null, DateTime? to = null)
        {
            var guard = GuardAdmin();
            if (guard != null) return guard;
            var q = _db.Departures.AsQueryable();
            if (from.HasValue) q = q.Where(x => x.Date >= from.Value);
            if (to.HasValue) q = q.Where(x => x.Date <= to.Value);
            var rows = q.OrderBy(x => x.Date).ThenBy(x => x.PlannedTime).ToList();
            var sb = new StringBuilder();
            sb.AppendLine("Date,Time,Route,Vehicle,Driver,Status,MinFill,Booked,Seats");
            foreach (var d in rows)
            {
                var route = _db.Routes.Where(r => r.RouteId == d.RouteId).Select(r => r.ToName).FirstOrDefault();
                var veh = _db.Vehicles.Where(v => v.VehicleId == d.VehicleId).Select(v => v.RegNo).FirstOrDefault();
                var driver = _db.Users.Where(u => u.UserId == d.DriverId).Select(u => u.DisplayName).FirstOrDefault();
                var seats = _db.Vehicles.Where(v => v.VehicleId == d.VehicleId).Select(v => v.Seats).FirstOrDefault();
                var booked = _db.SeatInventories.Count(s => s.DepartureId == d.DepartureId && s.Status == "Booked");
                sb.AppendLine(string.Join(",", new[] { d.Date.ToString("yyyy-MM-dd"), d.PlannedTime.ToString(), route, veh, driver, d.Status, d.MinFill.ToString(), booked.ToString(), seats.ToString() }.Select(x => "\"" + x.Replace("\"", "\"\"") + "\"")));
            }
            return Csv("departures.csv", sb.ToString());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult ReportingArchiveUpload(string periodMonth, HttpPostedFileBase file)
        {
            var guard = GuardAdmin();
            if (guard != null) return guard;
            if (file == null || file.ContentLength == 0) return RedirectToAction("Dashboard");
            using (var ms = new MemoryStream())
            {
                file.InputStream.CopyTo(ms);
                var ra = new ReportingArchive { PeriodMonth = periodMonth, FileName = Path.GetFileName(file.FileName), FileData = ms.ToArray(), CreatedAt = DateTime.Now, CreatedBy = CurrentUserId() };
                _db.ReportingArchives.Add(ra);
                _db.SaveChanges();
            }
            return RedirectToAction("Dashboard");
        }

        [HttpGet]
        public ActionResult ReportingArchiveDownload(int id)
        {
            var guard = GuardAdmin();
            if (guard != null) return guard;
            var r = _db.ReportingArchives.FirstOrDefault(x => x.ArchiveId == id);
            if (r == null) return HttpNotFound();
            return File(r.FileData, "application/octet-stream", r.FileName);
        }
        #endregion

        #region Attachments
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult AttachmentUpload(FileUploadVM model, HttpPostedFileBase file)
        {
            var guard = GuardAdmin();
            if (guard != null) return guard;
            if (file == null || file.ContentLength == 0) return RedirectToAction("Dashboard");
            using (var ms = new MemoryStream())
            {
                file.InputStream.CopyTo(ms);
                var a = new Models.Attachment { Entity = model.Entity, EntityId = model.EntityId, FileName = string.IsNullOrWhiteSpace(model.FileName) ? Path.GetFileName(file.FileName) : model.FileName, FileData = ms.ToArray(), UploadedAt = DateTime.Now, UploadedBy = CurrentUserId() };
                _db.Attachments.Add(a);
                _db.SaveChanges();
            }
            return RedirectToAction("Dashboard");
        }

        [HttpGet]
        public ActionResult AttachmentDownload(int id)
        {
            var guard = GuardAdmin();
            if (guard != null) return guard;
            var a = _db.Attachments.FirstOrDefault(x => x.AttachmentId == id);
            if (a == null) return HttpNotFound();
            return File(a.FileData, "application/octet-stream", a.FileName);
        }
        #endregion

        #region Maps
        [HttpGet]
        public ActionResult MapsSearch(string q)
        {
            var guard = GuardAdmin();
            if (guard != null) return guard;
            var vm = new MapsSearchVM { Query = q, Results = new List<CoordinateVM>() };
            if (string.IsNullOrWhiteSpace(q)) return View(vm);
            var cached = _db.MapsCaches.Where(x => (x.Description ?? "").Contains(q)).OrderByDescending(x => x.CachedAt).Take(10).ToList();
            vm.Results.AddRange(cached.Select(x => new CoordinateVM { PlaceId = x.PlaceId, Latitude = x.Latitude, Longitude = x.Longitude, Address = x.Description }));
            if (vm.Results.Count < 5)
            {
                using (var client = new HttpClient())
                {
                    var url = "https://maps.googleapis.com/maps/api/place/textsearch/json?query=" + HttpUtility.UrlEncode(q) + "&key=" + MapsKey();
                    var json = client.GetStringAsync(url).Result;
                    var jo = JObject.Parse(json);
                    var arr = jo["results"] as JArray;
                    if (arr != null)
                    {
                        foreach (var r in arr.Take(5))
                        {
                            var pid = r["place_id"] == null ? null : r["place_id"].ToString();
                            var name = r["name"] == null ? null : r["name"].ToString();
                            var addr = r["formatted_address"] == null ? null : r["formatted_address"].ToString();
                            var lat = r["geometry"]?["location"]?["lat"] == null ? 0 : r["geometry"]["location"]["lat"].Value<decimal>();
                            var lng = r["geometry"]?["location"]?["lng"] == null ? 0 : r["geometry"]["location"]["lng"].Value<decimal>();
                            vm.Results.Add(new CoordinateVM { PlaceId = pid, Latitude = lat, Longitude = lng, Address = name + " - " + addr });
                            if (!_db.MapsCaches.Any(x => x.PlaceId == pid))
                            {
                                _db.MapsCaches.Add(new MapsCache { PlaceId = pid, Description = name + " - " + addr, Latitude = lat, Longitude = lng, Json = r.ToString(), CachedAt = DateTime.Now });
                            }
                        }
                        _db.SaveChanges();
                    }
                }
            }
            return View(vm);
        }
        #endregion

        #region Notifications
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult SendNotification(EmailNotifyVM model)
        {
            var guard = GuardAdmin();
            if (guard != null) return guard;
            if (!ModelState.IsValid) return RedirectToAction("Dashboard");
            SendMail(model.ToEmail, model.Subject, model.HtmlBody);
            return RedirectToAction("Dashboard");
        }
        #endregion

        #region Audit
        [HttpGet]
        public ActionResult AuditList(string entityType = null, int? entityId = null, DateTime? from = null, DateTime? to = null)
        {
            var guard = GuardAdmin();
            if (guard != null) return guard;
            var vm = new AuditListVM { EntityType = entityType, EntityId = entityId, FromDate = from, ToDate = to };
            var q = _db.AuditLogs.AsQueryable();
            if (!string.IsNullOrWhiteSpace(entityType)) q = q.Where(x => x.EntityType == entityType);
            if (entityId.HasValue) q = q.Where(x => x.EntityId == entityId.Value);
            if (from.HasValue) q = q.Where(x => x.Timestamp >= from.Value);
            if (to.HasValue) q = q.Where(x => x.Timestamp <= to.Value);
            vm.Rows = q.OrderByDescending(x => x.Timestamp).Select(x => new AuditRowVM { AuditLogId = x.AuditLogId, EntityType = x.EntityType, EntityId = x.EntityId, Field = x.Field, OldValue = x.OldValue, NewValue = x.NewValue, UserName = _db.Users.Where(u => u.UserId == x.UserId).Select(u => u.DisplayName).FirstOrDefault(), Timestamp = x.Timestamp }).ToList();
            return View(vm);
        }
        #endregion*/
    }
}
