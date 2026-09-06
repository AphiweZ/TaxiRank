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
    public class RankManagerController : Controller
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

        private ActionResult GuardManagerOrConductor()
        {
            if (!IsLoggedIn()) return RedirectToAction("Login", "Account");
            var r = CurrentRole();
            if (r != "RankManager" && r != "Conductor") return RedirectToAction("Login", "Account");
            return null;
        }

       

        private string MapsKey()
        {
            var k = ConfigurationManager.AppSettings["GoogleMapsApiKey"];
            if (string.IsNullOrWhiteSpace(k)) return "AIzaSyCzGOGXloVFr8w-Pe53rgPWuQv-P3KnIaE";
            return k;
        }

        private ActionResult GuardManager()
        {
            if (!IsLoggedIn()) return RedirectToAction("Login", "Account");
            if (!string.Equals(CurrentRole(), "RankManager", StringComparison.OrdinalIgnoreCase))
                return RedirectToAction("Login", "Account");
            return null;
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

        private List<IdName> RankOptions()
        {
            var allowed = AllowedLocationIds().ToList();
            return _db.Ranks.Where(x => x.IsActive && (allowed.Count == 0 || allowed.Contains(x.RankId))).OrderBy(x => x.Name).Select(x => new IdName { Id = x.RankId, Name = x.Name + " - " + x.City }).ToList();
        }

        private List<IdName> RouteOptions()
        {
            var allowed = AllowedLocationIds().ToList();
            return _db.Routes.Where(x => x.Active && (allowed.Count == 0 || allowed.Contains(x.FromRankId))).OrderBy(x => x.FromRankId).ThenBy(x => x.ToName).Select(x => new IdName { Id = x.RouteId, Name = _db.Ranks.Where(r => r.RankId == x.FromRankId).Select(r => r.Name).FirstOrDefault() + " → " + x.ToName }).ToList();
        }

        private List<IdName> VehicleOptions()
        {
            var allowed = AllowedLocationIds().ToList();
            return _db.Vehicles.Where(x => x.Active && (allowed.Count == 0 || (x.HomeRankId.HasValue && allowed.Contains(x.HomeRankId.Value)))).OrderBy(x => x.RegNo).Select(x => new IdName { Id = x.VehicleId, Name = x.RegNo }).ToList();
        }
        private  List<IdName> UserOptions(string role)
        {
            var users = _db.Users
                .Where(u => u.Role == role && u.IsActive)
                .OrderBy(u => u.DisplayName)
                .Select(u => new IdName { Id = u.UserId, Name = u.DisplayName })
                .ToList();

            return users;
        }
        private void Audit(string entity, int id, string field, string oldV, string newV)
        {
            _db.AuditLogs.Add(new AuditLog { EntityType = entity, EntityId = id, Field = field, OldValue = oldV, NewValue = newV, UserId = CurrentUserId(), Timestamp = DateTime.Now });
        }

        private List<IdName> DriverOptions()
        {
            return _db.Users.Where(x => x.IsActive && x.Role == "Driver").OrderBy(x => x.DisplayName).Select(x => new IdName { Id = x.UserId, Name = x.DisplayName }).ToList();
        }

        private FileContentResult Csv(string name, string body)
        {
            return File(Encoding.UTF8.GetBytes(body), "text/csv", name);
        }

        #region Dashboard
        [HttpGet]
        public ActionResult Dashboard(int? rankId = null)
        {
            var guard = GuardManagerOrConductor();
            if (guard != null) return guard;
            var allowed = AllowedLocationIds().ToList();
            var chosenRank = rankId.HasValue ? rankId.Value : allowed.FirstOrDefault();
            var today = DateTime.Today;
            var q = _db.Departures.Where(x => x.Date == today);
            if (allowed.Count > 0) q = q.Where(x => allowed.Contains(_db.Routes.Where(r => r.RouteId == x.RouteId).Select(r => r.FromRankId).FirstOrDefault()));
            if (chosenRank > 0) q = q.Where(x => _db.Routes.Where(r => r.RouteId == x.RouteId).Select(r => r.FromRankId).FirstOrDefault() == chosenRank);
            var list = q.OrderBy(x => x.PlannedTime).ToList();
            var vm = new RankDashboardVM
            {
                RankId = chosenRank,
                RankOptions = RankOptions(),
                Date = today,
                TotalDepartures = list.Count,
                BoardingCount = list.Count(x => x.Status == "Boarding"),
                ReadyCount = list.Count(x => x.Status == "Ready"),
                DepartedCount = list.Count(x => x.Status == "Departed"),
                Departures = list.Select(x => new DepartureRowVM
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

        #region Ranks
        [HttpGet]
        public ActionResult RanksList(string city = null, string search = null)
        {
            var guard = GuardManagerOrConductor();
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
            var guard = GuardManagerOrConductor();
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
            var guard = GuardManagerOrConductor();
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
            var guard = GuardManagerOrConductor();
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
            var guard = GuardManagerOrConductor();
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
            var guard = GuardManagerOrConductor();
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
            var guard = GuardManager();
            if (guard != null) return guard;

            var vm = new VehicleCreateVM();
            vm.OwnerOptions = UserOptions("Owner");
            vm.RankOptions = RankOptions();

            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult VehicleCreate(VehicleCreateVM vm)
        {
            var guard = GuardManager();
            if (guard != null) return guard;

          
            vm.OwnerOptions = UserOptions("Owner");
            vm.RankOptions = RankOptions();

            if (!ModelState.IsValid)
            {
                return View(vm);
            }

            if (_db.Vehicles.Any(x => x.RegNo == vm.RegNo))
            {
                ModelState.AddModelError("RegNo", "Registration already exists");
                return View(vm);
            }

            try
            {
                var v = new Vehicle
                {
                    RegNo = vm.RegNo,
                    Make = vm.Make,
                    Model = vm.Model,
                    Year = vm.Year,
                    Seats = vm.Seats,
                    OwnerId = vm.OwnerId,
                    HomeRankId = vm.HomeRankId,
                    Active = vm.Active,
                    CreatedAt = DateTime.Now,
                    CreatedBy = CurrentUserId()
                };
                _db.Vehicles.Add(v);
                _db.SaveChanges();

                return RedirectToAction("VehiclesList");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(ex.ToString());
                ModelState.AddModelError("", "An error occurred while saving the vehicle. Please try again.");
                return View(vm);
            }
        }
        [HttpGet]
        public ActionResult VehicleEdit(int id)
        {
            var guard = GuardManager();
            if (guard != null) return guard;
            var v = _db.Vehicles.FirstOrDefault(x => x.VehicleId == id);
            if (v == null) return HttpNotFound();
            var vm = new VehicleCreateVM { VehicleId = v.VehicleId, RegNo = v.RegNo, Make = v.Make, Model = v.Model, Year = v.Year, Seats = v.Seats, OwnerId = v.OwnerId, HomeRankId = v.HomeRankId, Active = v.Active, OwnerOptions = UserOptions("Owner"), RankOptions = RankOptions() };
            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult VehicleEdit(VehicleCreateVM vm)
        {
            var guard = GuardManager();
            if (guard != null) return guard;
            vm.OwnerOptions = UserOptions("Owner");
            vm.RankOptions = RankOptions();
            if (!ModelState.IsValid) return View(vm);
            var v = _db.Vehicles.FirstOrDefault(x => x.VehicleId == vm.VehicleId);
            if (v == null) return HttpNotFound();
            v.Make = vm.Make;
            v.Model = vm.Model;
            v.Year = vm.Year;
            v.Seats = vm.Seats;
            v.OwnerId = vm.OwnerId;
            v.HomeRankId = vm.HomeRankId;
            v.Active = vm.Active;
            v.UpdatedAt = DateTime.Now;
            v.UpdatedBy = CurrentUserId();
            _db.SaveChanges();
            return RedirectToAction("VehiclesList");
        }


        [HttpGet]
        public ActionResult VehicleDocDownload(int id)
        {
            var guard = GuardManagerOrConductor();
            if (guard != null) return guard;
            var d = _db.VehicleDocuments.FirstOrDefault(x => x.VehicleDocumentId == id);
            if (d == null) return HttpNotFound();
            return File(d.FileData, "application/octet-stream", d.FileName);
        }

        [HttpGet]
        public ActionResult OwnerConfigEdit(int ownerId)
        {
            var guard = GuardManagerOrConductor();
            if (guard != null) return guard;
            var cfg = _db.OwnerConfigs.FirstOrDefault(x => x.OwnerId == ownerId);
            var vm = new OwnerConfigEditVM { OwnerId = ownerId, DriverSharePercent = cfg == null ? 30 : cfg.DriverSharePercent, OwnerOptions = UserOptions("Owner") };
            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult OwnerConfigEdit(OwnerConfigEditVM vm)
        {
            var guard = GuardManagerOrConductor();
            if (guard != null) return guard;
            vm.OwnerOptions = UserOptions("Owner");
            if (!ModelState.IsValid) return View(vm);
            var cfg = _db.OwnerConfigs.FirstOrDefault(x => x.OwnerId == vm.OwnerId);
            if (cfg == null)
            {
                cfg = new OwnerConfig { OwnerId = vm.OwnerId, DriverSharePercent = vm.DriverSharePercent, EffectiveFrom = DateTime.Now, CreatedAt = DateTime.Now, CreatedBy = CurrentUserId() };
                _db.OwnerConfigs.Add(cfg);
            }
            else
            {
                Audit("OwnerConfigs", cfg.OwnerConfigId, "DriverSharePercent", cfg.DriverSharePercent.ToString("0.00"), vm.DriverSharePercent.ToString("0.00"));
                cfg.DriverSharePercent = vm.DriverSharePercent;
            }
            _db.SaveChanges();
            return RedirectToAction("VehiclesList", new { ownerId = vm.OwnerId });
        }

        [HttpGet]
        public ActionResult DriverAssignmentCreate()
        {
            var guard = GuardManagerOrConductor();
            if (guard != null) return guard;
            var vm = new DriverAssignmentCreateVM { DriverOptions = UserOptions("Driver"), VehicleOptions = VehicleOptions(), StartDate = DateTime.Today };
            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult DriverAssignmentCreate(DriverAssignmentCreateVM vm)
        {
            var guard = GuardManagerOrConductor();
            if (guard != null) return guard;
            vm.DriverOptions = UserOptions("Driver");
            vm.VehicleOptions = VehicleOptions();
            if (!ModelState.IsValid) return View(vm);
            var a = new DriverAssignment { DriverId = vm.DriverId, VehicleId = vm.VehicleId, StartDate = vm.StartDate.Date, EndDate = vm.EndDate, CreatedAt = DateTime.Now, CreatedBy = CurrentUserId() };
            _db.DriverAssignments.Add(a);
            _db.SaveChanges();
            return RedirectToAction("VehiclesList");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult DriverAssignmentEnd(int id, DateTime endDate)
        {
            var guard = GuardManagerOrConductor();
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
            var guard = GuardManagerOrConductor();
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
            var guard = GuardManagerOrConductor();
            if (guard != null) return guard;
            var vm = new RouteCreateVM { RankOptions = RankOptions(), Active = true };
            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult RouteCreate(RouteCreateVM model)
        {
            var guard = GuardManagerOrConductor();
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
            var guard = GuardManagerOrConductor();
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
            var guard = GuardManagerOrConductor();
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
            var guard = GuardManagerOrConductor();
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
            var guard = GuardManagerOrConductor();
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
            var guard = GuardManagerOrConductor();
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
            var guard = GuardManagerOrConductor();
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
        public ActionResult CreateDeparture()
        {
            var guard = GuardManagerOrConductor();
            if (guard != null) return guard;
            var vm = new DepartureCreateVM { RouteOptions = RouteOptions(), VehicleOptions = VehicleOptions(), DriverOptions = DriverOptions(), Date = DateTime.Today, PlannedTime = new TimeSpan(8, 0, 0), MinFill = 10, Status = "Queued" };
            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult CreateDeparture(DepartureCreateVM model)
        {
            var guard = GuardManagerOrConductor();
            if (guard != null) return guard;
            model.RouteOptions = RouteOptions();
            model.VehicleOptions = VehicleOptions();
            model.DriverOptions = DriverOptions();
            if (!ModelState.IsValid) return View(model);
            var allowed = AllowedLocationIds().ToList();
            var fromRank = _db.Routes.Where(r => r.RouteId == model.RouteId).Select(r => r.FromRankId).FirstOrDefault();
            if (allowed.Count > 0 && !allowed.Contains(fromRank))
            {
                ModelState.AddModelError("", "Not allowed for this rank");
                return View(model);
            }
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
                return RedirectToAction("Detail", new { id = d.DepartureId });
            }
        }

        [HttpGet]
        public ActionResult Today(int? rankId = null, string status = null)
        {
            var guard = GuardManagerOrConductor();
            if (guard != null) return guard;
            var vm = new DepartureListVM { RouteOptions = RouteOptions(), RankOptions = RankOptions(), StatusOptions = new List<IdName> { new IdName { Id = 0, Name = "Queued" }, new IdName { Id = 0, Name = "Boarding" }, new IdName { Id = 0, Name = "Ready" }, new IdName { Id = 0, Name = "Departed" }, new IdName { Id = 0, Name = "Cancelled" } } };
            var allowed = AllowedLocationIds().ToList();
            var q = _db.Departures.Where(x => x.Date == DateTime.Today);
            if (allowed.Count > 0) q = q.Where(x => allowed.Contains(_db.Routes.Where(r => r.RouteId == x.RouteId).Select(r => r.FromRankId).FirstOrDefault()));
            if (rankId.HasValue) q = q.Where(x => _db.Routes.Where(r => r.RouteId == x.RouteId).Select(r => r.FromRankId).FirstOrDefault() == rankId.Value);
            if (!string.IsNullOrWhiteSpace(status)) q = q.Where(x => x.Status == status);
            vm.Rows = q.OrderBy(x => x.PlannedTime).ToList().Select(x => new DepartureRowVM
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
            if (id <= 0) return RedirectToAction("Today"); 
            var d = _db.Departures.FirstOrDefault(x => x.DepartureId == id);
            if (d == null) return RedirectToAction("Today");
            var guard = GuardManagerOrConductor();
            if (guard != null) return guard;
            
            if (d == null) return HttpNotFound();
            var routeFrom = _db.Routes.Where(r => r.RouteId == d.RouteId).Select(r => r.FromRankId).FirstOrDefault();
            var allowed = AllowedLocationIds().ToList();
            if (allowed.Count > 0 && !allowed.Contains(routeFrom)) return RedirectToAction("Today");
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

        #region Status
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult UpdateStatus(int id, string status, bool overrideReady = false)
        {
            var guard = GuardManagerOrConductor();
            if (guard != null) return guard;
            var d = _db.Departures.FirstOrDefault(x => x.DepartureId == id);
            if (d == null) return HttpNotFound();
            var fromRank = _db.Routes.Where(r => r.RouteId == d.RouteId).Select(r => r.FromRankId).FirstOrDefault();
            var allowed = AllowedLocationIds().ToList();
            if (allowed.Count > 0 && !allowed.Contains(fromRank)) return RedirectToAction("Today");
            d.Status = status;
            if (overrideReady) d.ManagerOverrideReady = true;
            d.UpdatedAt = DateTime.Now;
            d.UpdatedBy = CurrentUserId();
            _db.SaveChanges();
            return RedirectToAction("Detail", new { id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult ReadyChecklist(int id, bool minFill, bool cashVerified, bool floatIssued, bool vehicleCleared)
        {
            var guard = GuardManagerOrConductor();
            if (guard != null) return guard;
            var d = _db.Departures.FirstOrDefault(x => x.DepartureId == id);
            if (d == null) return HttpNotFound();
            var fromRank = _db.Routes.Where(r => r.RouteId == d.RouteId).Select(r => r.FromRankId).FirstOrDefault();
            var allowed = AllowedLocationIds().ToList();
            if (allowed.Count > 0 && !allowed.Contains(fromRank)) return RedirectToAction("Today");
            d.ReadyChecklist_MinFill = minFill;
            d.ReadyChecklist_CashVerified = cashVerified;
            d.ReadyChecklist_FloatIssued = floatIssued;
            d.ReadyChecklist_VehicleCleared = vehicleCleared;
            d.UpdatedAt = DateTime.Now;
            d.UpdatedBy = CurrentUserId();
            _db.SaveChanges();
            return RedirectToAction("Detail", new { id });
        }
        #endregion


       
        #endregion

        #region SeatOps
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult HoldSeat(int departureId, int seatNo, string pnr)
        {
            var guard = GuardManagerOrConductor();
            if (guard != null) return guard;
            var inv = _db.SeatInventories.FirstOrDefault(s => s.DepartureId == departureId && s.SeatNo == seatNo);
            if (inv == null) return RedirectToAction("Detail", new { id = departureId });
            var b = _db.Bookings.FirstOrDefault(x => x.DepartureId == departureId && x.Pnr == pnr);
            if (b == null) return RedirectToAction("Detail", new { id = departureId });
            if (inv.Status != "Free") return RedirectToAction("Detail", new { id = departureId });
            inv.Status = "Booked";
            inv.BookingId = b.BookingId;
            _db.BookingSeats.Add(new BookingSeat { BookingId = b.BookingId, DepartureId = departureId, SeatNo = seatNo, CreatedAt = DateTime.Now, CreatedBy = CurrentUserId() });
            _db.SaveChanges();
            return RedirectToAction("Detail", new { id = departureId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult ReleaseSeat(int departureId, int seatNo)
        {
            var guard = GuardManagerOrConductor();
            if (guard != null) return guard;
            var inv = _db.SeatInventories.FirstOrDefault(s => s.DepartureId == departureId && s.SeatNo == seatNo);
            if (inv == null) return RedirectToAction("Detail", new { id = departureId });
            var bookingId = inv.BookingId;
            inv.Status = "Free";
            inv.BookingId = null;
            var bs = _db.BookingSeats.FirstOrDefault(x => x.DepartureId == departureId && x.SeatNo == seatNo && x.BookingId == bookingId);
            if (bs != null) _db.BookingSeats.Remove(bs);
            _db.SaveChanges();
            return RedirectToAction("Detail", new { id = departureId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult SwapSeats(int departureId, int seatFrom, int seatTo)
        {
            var guard = GuardManagerOrConductor();
            if (guard != null) return guard;
            var a = _db.SeatInventories.FirstOrDefault(s => s.DepartureId == departureId && s.SeatNo == seatFrom);
            var b = _db.SeatInventories.FirstOrDefault(s => s.DepartureId == departureId && s.SeatNo == seatTo);
            if (a == null || b == null) return RedirectToAction("Detail", new { id = departureId });
            var bidA = a.BookingId;
            var bidB = b.BookingId;
            a.BookingId = bidB;
            b.BookingId = bidA;
            a.Status = bidB.HasValue ? "Booked" : "Free";
            b.Status = bidA.HasValue ? "Booked" : "Free";
            var bsA = _db.BookingSeats.FirstOrDefault(x => x.DepartureId == departureId && x.SeatNo == seatFrom && x.BookingId == bidA);
            var bsB = _db.BookingSeats.FirstOrDefault(x => x.DepartureId == departureId && x.SeatNo == seatTo && x.BookingId == bidB);
            if (bsA != null) bsA.SeatNo = seatTo;
            if (bsB != null) bsB.SeatNo = seatFrom;
            _db.SaveChanges();
            return RedirectToAction("Detail", new { id = departureId });
        }
        #endregion


        #region Money
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult IssueFloat(FloatIssueVM model)
        {
            var guard = GuardManagerOrConductor();
            if (guard != null) return guard;
            if (!ModelState.IsValid) return RedirectToAction("Detail", new { id = model.DepartureId });
            _db.Floats.Add(new Float { DepartureId = model.DepartureId, AmountIssued = model.AmountIssued, Purpose = model.Purpose, IssuedBy = CurrentUserId(), Timestamp = DateTime.Now });
            _db.SaveChanges();
            return RedirectToAction("Detail", new { id = model.DepartureId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Cashup(CashupCreateVM model)
        {
            var guard = GuardManagerOrConductor();
            if (guard != null) return guard;
            if (!ModelState.IsValid) return RedirectToAction("Detail", new { id = model.DepartureId });
            _db.CashupBags.Add(new CashupBag { DepartureId = model.DepartureId, CountedAmount = model.CountedAmount, CountedBy = CurrentUserId(), Timestamp = DateTime.Now });
            _db.SaveChanges();
            return RedirectToAction("Detail", new { id = model.DepartureId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult RecordExpense(ExpenseCreateVM model, HttpPostedFileBase proof)
        {
            var guard = GuardManagerOrConductor();
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
            return RedirectToAction("Detail", new { id = model.DepartureId });
        }
        #endregion

        #region Audit
        [HttpGet]
        public ActionResult AuditList(string entityType = null, int? entityId = null, DateTime? from = null, DateTime? to = null)
        {
            var guard = GuardManagerOrConductor();
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
        #endregion


        #region Incidents
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult LogIncident(IncidentCreateVM model, HttpPostedFileBase photo)
        {
            var guard = GuardManagerOrConductor();
            if (guard != null) return guard;
            var incident = new Incident { DepartureId = model.DepartureId, Type = model.Type, Notes = model.Notes, CreatedAt = DateTime.Now, CreatedBy = CurrentUserId() };
            _db.Incidents.Add(incident);
            _db.SaveChanges();
            if (photo != null && photo.ContentLength > 0)
            {
                using (var ms = new MemoryStream())
                {
                    photo.InputStream.CopyTo(ms);
                    var a = new Models.Attachment { Entity = "Incident", EntityId = incident.IncidentId, FileName = Path.GetFileName(photo.FileName), FileData = ms.ToArray(), UploadedAt = DateTime.Now, UploadedBy = CurrentUserId() };
                    _db.Attachments.Add(a);
                    _db.SaveChanges();
                }
            }
            return RedirectToAction("Detail", new { id = model.DepartureId });
        }
        #endregion

        #region ManifestsReports
        [HttpGet]
        public ActionResult ManifestCsv(int id)
        {
            var guard = GuardManagerOrConductor();
            if (guard != null) return guard;
            var d = _db.Departures.FirstOrDefault(x => x.DepartureId == id);
            if (d == null) return HttpNotFound();
            var rows = _db.Bookings.Where(b => b.DepartureId == id && b.Status != "Cancelled").OrderBy(b => b.BookingId).ToList();
            var sb = new StringBuilder();
            sb.AppendLine("PNR,Passenger,Phone,Seats,SeatNumbers,Amount,Status");
            foreach (var b in rows)
            {
                var pax = _db.Passengers.Where(p => p.PassengerId == b.PassengerId).Select(p => new { p.FullName, p.Phone }).FirstOrDefault();
                var seats = string.Join(" ", _db.BookingSeats.Where(s => s.BookingId == b.BookingId).OrderBy(s => s.SeatNo).Select(s => s.SeatNo));
                sb.AppendLine(string.Join(",", new[]
                {
                    b.Pnr,
                    pax == null ? "" : pax.FullName,
                    pax == null ? "" : pax.Phone,
                    b.SeatsCount.ToString(),
                    seats,
                    ((decimal)b.ExtendedAmount).ToString("0.00"),
                    b.Status
                }.Select(x => "\"" + (x ?? "").Replace("\"", "\"\"") + "\"")));
            }
            return Csv("manifest.csv", sb.ToString());
        }

        [HttpGet]
        public ActionResult CashSheetCsv(int id)
        {
            var guard = GuardManagerOrConductor();
            if (guard != null) return guard;
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
            return Csv("cashsheet.csv", sb.ToString());
        }
        #endregion

        #region Contacts
        [HttpGet]
        public ActionResult PassengerPhonesCsv(int id)
        {
            var guard = GuardManagerOrConductor();
            if (guard != null) return guard;
            var rows = (from b in _db.Bookings
                        join p in _db.Passengers on b.PassengerId equals p.PassengerId
                        where b.DepartureId == id && b.Status == "Confirmed"
                        select new { b.Pnr, p.FullName, p.Phone }).OrderBy(x => x.FullName).ToList();
            var sb = new StringBuilder();
            sb.AppendLine("PNR,Passenger,Phone");
            foreach (var r in rows)
            {
                sb.AppendLine(string.Join(",", new[]
                {
                    r.Pnr,
                    r.FullName ?? "",
                    r.Phone ?? ""
                }.Select(x => "\"" + x.Replace("\"", "\"\"") + "\"")));
            }
            return Csv("passenger_phones.csv", sb.ToString());
        }
        #endregion

        #region Maps
        [HttpGet]
        public ActionResult MapsSearch(string q)
        {
            var guard = GuardManagerOrConductor();
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

        #region Telemetry
        [HttpGet]
        public ActionResult TelemetryTrail(int departureId)
        {
            var guard = GuardManagerOrConductor();
            if (guard != null) return guard;
            var d = _db.Departures.FirstOrDefault(x => x.DepartureId == departureId);
            if (d == null) return HttpNotFound();
            var points = _db.Telemetries.Where(t => t.DepartureId == departureId)
                .OrderBy(t => t.Timestamp)
                .Select(t => new TelemetryPointVM
                {
                    Timestamp = t.Timestamp,
                    Latitude = t.Latitude,
                    Longitude = t.Longitude,
                    SpeedKph = t.SpeedKph,
                    Battery = t.Battery
                }).ToList();
            var vm = new TelemetryListVM { DepartureId = departureId, Points = points };
            return View(vm);
        }
        #endregion

        #region Notifications
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult SendNotification(EmailNotifyVM model)
        {
            var guard = GuardManagerOrConductor();
            if (guard != null) return guard;
            if (!ModelState.IsValid) return RedirectToAction("Dashboard");
            SendMail(model.ToEmail, model.Subject, model.HtmlBody);
            return RedirectToAction("Dashboard");
        }
        #endregion



        [HttpGet]
        public ActionResult EventRequests(string status = "Requested")
        {
            var g = GuardManager(); if (g != null) return g;
            var q = _db.Charters.AsQueryable();
            if (!string.IsNullOrWhiteSpace(status)) q = q.Where(c => c.Status == status);
            var rows = q.OrderByDescending(c => c.CreatedAt).Take(100).ToList().Select(c => new EventRowVM
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
            return View(new ManagerEventListVM { Status = status, Rows = rows });
        }

        [HttpGet]
        public ActionResult EventDetail(int id)
        {
            var g = GuardManager(); if (g != null) return g;
            var c = _db.Charters.FirstOrDefault(x => x.CharterId == id);
            if (c == null) return HttpNotFound();
            var vm = new ManagerEventDetailVM
            {
                CharterId = c.CharterId,
                Organizer = c.Organizer,
                Contact = c.Contact,
                Date = c.Date,
                TaxiCount = c.TaxiCount,
                ReturnTrip = c.ReturnTrip,
                EventType = c.EventType,
                Status = c.Status,
                QuoteAmount = c.QuoteAmount,
                IsPaid = c.IsPaid,
                PickupPlaceId = c.PickupPlaceId,
                PickupLat = c.PickupLat,
                PickupLng = c.PickupLng,
                DropPlaceId = c.DropPlaceId,
                DropLat = c.DropLat,
                DropLng = c.DropLng
            };
            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult EventApprove(ManagerEventApproveVM model)
        {
            var g = GuardManager(); if (g != null) return g;
            if (!ModelState.IsValid) return RedirectToAction("EventDetail", new { id = model.CharterId });
            var uid = CurrentUserId();
            var c = _db.Charters.FirstOrDefault(x => x.CharterId == model.CharterId);
            if (c == null) return HttpNotFound();
            if (model.Decision == "Approved")
            {
                c.Status = "Approved";
                c.QuoteAmount = model.QuoteAmount;
                c.ApprovedBy = uid;
                c.ApprovedAt = DateTime.Now;
                try
                {
                    var to = c.Contact;
                    if (!string.IsNullOrWhiteSpace(to))
                    {
                        var from = "mandlakanozulu@gmail.com";
                        var pass = "ymcnugshpifyttas";
                        using (var msg = new MailMessage())
                        {
                            msg.From = new MailAddress(from, "TaxiRank");
                            msg.To.Add(new MailAddress(to));
                            msg.Subject = "Event booking approved";
                            msg.IsBodyHtml = true;
                            msg.Body = "<div style='font-family:Arial,sans-serif'><h3>Event approved</h3><p>Your quote amount is ZAR " + c.QuoteAmount.ToString("0.00") + ".</p></div>";
                            using (var smtp = new SmtpClient("smtp.gmail.com", 587))
                            {
                                smtp.EnableSsl = true;
                                smtp.Credentials = new NetworkCredential(from, pass);
                                smtp.Send(msg);
                            }
                        }
                    }
                }
                catch { }
            }
            else
            {
                c.Status = "Rejected";
            }
            _db.SaveChanges();
            return RedirectToAction("EventDetail", new { id = model.CharterId });
        }

        [HttpGet]
        public ActionResult EventAssign(int id)
        {
            var g = GuardManager(); if (g != null) return g;
            var c = _db.Charters.FirstOrDefault(x => x.CharterId == id);
            if (c == null) return HttpNotFound();
            if (!c.IsPaid) return RedirectToAction("EventDetail", new { id });
            var vm = new ManagerEventAssignVM
            {
                CharterId = id,
                RequiredTaxis = c.TaxiCount,
                VehicleOptions = _db.Vehicles.Where(v => v.Active).OrderBy(v => v.RegNo).Select(v => new IdName { Id = v.VehicleId, Name = v.RegNo }).ToList(),
                DriverOptions = _db.Users.Where(u => u.Role == "Driver" && u.IsActive).OrderBy(u => u.DisplayName).Select(u => new IdName { Id = u.UserId, Name = u.DisplayName }).ToList()
            };
            var existing = _db.CharterVehicles.Where(cv => cv.CharterId == id).ToList();
            if (existing.Count > 0)
            {
                vm.Assignments = existing.Select(e => new ManagerEventAssignVM.AssignRow { VehicleId = e.VehicleId, DriverId = e.DriverId }).ToList();
            }
            else
            {
                for (int i = 0; i < vm.RequiredTaxis; i++) vm.Assignments.Add(new ManagerEventAssignVM.AssignRow());
            }
            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult EventAssign(ManagerEventAssignVM model)
        {
            var g = GuardManager(); if (g != null) return g;
            var c = _db.Charters.FirstOrDefault(x => x.CharterId == model.CharterId);
            if (c == null) return HttpNotFound();
            if (!c.IsPaid) return RedirectToAction("EventDetail", new { id = model.CharterId });
            var old = _db.CharterVehicles.Where(cv => cv.CharterId == model.CharterId).ToList();
            foreach (var o in old) _db.CharterVehicles.Remove(o);
            _db.SaveChanges();
            foreach (var row in model.Assignments)
            {
                if (row.VehicleId.HasValue && row.DriverId.HasValue)
                {
                    _db.CharterVehicles.Add(new CharterVehicle { CharterId = model.CharterId, VehicleId = row.VehicleId.Value, DriverId = row.DriverId.Value });
                }
            }
            _db.SaveChanges();
            return RedirectToAction("EventDetail", new { id = model.CharterId });
        }

    }
}
