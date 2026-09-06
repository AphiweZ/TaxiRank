using System;
using System.Linq;
using System.Net;
using System.Net.Mail;
using System.Security.Cryptography;
using System.Text;
using System.Web.Mvc;
using TaxiRank.Models;
using TaxiRank.Models.ViewModels;
using System.ComponentModel.DataAnnotations;

namespace TaxiRank.Controllers
{
    public class AccountController : Controller
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

        private string BuildAllowedLocationIds(int userId)
        {
            var q = _db.UserRanks.Where(x => x.UserId == userId).Select(x => x.RankId);
            var list = q.ToList();
            return string.Join(",", list);
        }

        private void SignInUser(User u)
        {
            Session["UserId"] = u.UserId;
            Session["Role"] = u.Role;
            Session["DisplayName"] = u.DisplayName;
            Session["AllowedLocationIds"] = BuildAllowedLocationIds(u.UserId);
        }

        private void SignOutUser()
        {
            Session.Clear();
            Session.Abandon();
        }

        private ActionResult RedirectToDashboard(string role = null)
        {
            role = (role ?? CurrentRole() ?? "").Trim();
            if (string.Equals(role, "Admin", StringComparison.OrdinalIgnoreCase)) return RedirectToAction("Dashboard", "Admin");
            if (string.Equals(role, "Customer", StringComparison.OrdinalIgnoreCase)) return RedirectToAction("Dashboard", "Customer");
            if (string.Equals(role, "Driver", StringComparison.OrdinalIgnoreCase)) return RedirectToAction("Dashboard", "Driver");
            if (string.Equals(role, "Owner", StringComparison.OrdinalIgnoreCase)) return RedirectToAction("Dashboard", "Owner");
            if (string.Equals(role, "RankManager", StringComparison.OrdinalIgnoreCase)) return RedirectToAction("Dashboard", "RankManager");
            return RedirectToAction("Index", "Home");
        }

        public class RegisterVM
        {
            [Required, StringLength(150)]
            public string Username { get; set; }
            [Required, StringLength(200)]
            public string Password { get; set; }
            [Required, System.ComponentModel.DataAnnotations.Compare("Password")]
            public string ConfirmPassword { get; set; }
            [Required, StringLength(150)]
            public string DisplayName { get; set; }
            [StringLength(30)]
            public string Phone { get; set; }
            [StringLength(200)]
            public string Email { get; set; }
        }

        public class LoginVM
        {
            [Required]
            public string UsernameOrEmail { get; set; }
            [Required]
            public string Password { get; set; }
        }

        public class ForgotPasswordVM
        {
            [Required]
            public string UsernameOrEmail { get; set; }
        }

        public class ChangePasswordVM
        {
            [Required]
            public string CurrentPassword { get; set; }
            [Required, StringLength(200)]
            public string NewPassword { get; set; }
            [Required, System.ComponentModel.DataAnnotations.Compare("NewPassword")]
            public string ConfirmNewPassword { get; set; }
        }

        [HttpGet]
        public ActionResult Register()
        {
            if (IsLoggedIn()) return RedirectToDashboard();
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Register(RegisterVM model)
        {
            if (IsLoggedIn()) return RedirectToDashboard();
            if (!ModelState.IsValid) return View(model);
            var exists = _db.Users.Any(x => x.Username == model.Username);
            if (exists)
            {
                ModelState.AddModelError("", "Username already exists");
                return View(model);
            }
            var u = new User
            {
                Username = model.Username,
                Password = model.Password,
                DisplayName = model.DisplayName,
                Role = "Customer",
                Phone = model.Phone,
                Email = model.Email,
                IsActive = true,
                CreatedAt = DateTime.Now
            };
            _db.Users.Add(u);
            _db.SaveChanges();
            SignInUser(u);
            return RedirectToDashboard(u.Role);
        }

        [HttpGet]
        public ActionResult Login()
        {
            if (IsLoggedIn()) return RedirectToDashboard();
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Login(LoginVM model)
        {
            if (IsLoggedIn()) return RedirectToDashboard();
            if (!ModelState.IsValid) return View(model);
            var q = _db.Users.Where(x => x.IsActive);
            var user = q.FirstOrDefault(x =>
                (x.Username == model.UsernameOrEmail || x.Email == model.UsernameOrEmail) &&
                x.Password == model.Password);
            if (user == null)
            {
                ModelState.AddModelError("", "Invalid credentials");
                return View(model);
            }
            SignInUser(user);
            return RedirectToDashboard(user.Role);
        }

        [HttpGet]
        public ActionResult Logout()
        {
            SignOutUser();
            return RedirectToAction("Login");
        }

        [HttpGet]
        public ActionResult ForgotPassword()
        {
            if (IsLoggedIn()) return RedirectToDashboard();
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult ForgotPassword(ForgotPasswordVM model)
        {
            if (IsLoggedIn()) return RedirectToDashboard();
            if (!ModelState.IsValid) return View(model);
            var user = _db.Users.FirstOrDefault(x => x.Username == model.UsernameOrEmail || x.Email == model.UsernameOrEmail);
            if (user == null)
            {
                ModelState.AddModelError("", "Account not found");
                return View(model);
            }
            var temp = GenerateTempPassword();
            user.Password = temp;
            _db.SaveChanges();
            try
            {
                SendMail(user.Email ?? user.Username, "Password Reset", BuildResetEmailBody(user.DisplayName, temp));
            }
            catch { }
            TempData["Msg"] = "If the account exists, a new password has been sent.";
            return RedirectToAction("Login");
        }

        [HttpGet]
        public ActionResult ResetPassword()
        {
            if (!IsLoggedIn()) return RedirectToAction("Login");
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult ResetPassword(ChangePasswordVM model)
        {
            if (!IsLoggedIn()) return RedirectToAction("Login");
            if (!ModelState.IsValid) return View(model);
            var uid = CurrentUserId();
            var user = _db.Users.FirstOrDefault(x => x.UserId == uid);
            if (user == null) return RedirectToAction("Login");
            if (user.Password != model.CurrentPassword)
            {
                ModelState.AddModelError("", "Current password is incorrect");
                return View(model);
            }
            user.Password = model.NewPassword;
            _db.SaveChanges();
            TempData["Msg"] = "Password updated";
            return RedirectToDashboard(user.Role);
        }

        private string GenerateTempPassword()
        {
            var bytes = new byte[6];
            using (var rng = RandomNumberGenerator.Create()) rng.GetBytes(bytes);
            var s = Convert.ToBase64String(bytes).Replace("+", "").Replace("/", "").Replace("=", "");
            if (s.Length < 8) s = s.PadRight(8, 'X');
            return "TR" + s.Substring(0, 8);
        }

        private string BuildResetEmailBody(string name, string tempPassword)
        {
            var sb = new StringBuilder();
            sb.Append("<div style='font-family:Inter,Arial,sans-serif;padding:16px'>");
            sb.Append("<h2> Account Password Reset</h2>");
            sb.Append("<p>Dear ").Append(WebUtility.HtmlEncode(name ?? "User")).Append(",</p>");
            sb.Append("<p>Your temporary password is:</p>");
            sb.Append("<p style='font-size:18px;font-weight:700;letter-spacing:1px'>").Append(WebUtility.HtmlEncode(tempPassword)).Append("</p>");
            sb.Append("<p>Use it to sign in and then change your password from your profile.</p>");
            sb.Append("<p>Regards,<br/>Durban Station Association</p>");
            sb.Append("</div>");
            return sb.ToString();
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
    }
}
