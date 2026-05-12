using System.Text;
using System.Text.Encodings.Web;
using EventsApp.Data;
using EventsApp.Models;
using EventsApp.Services;
using EventsApp.Services.Email;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace EventsApp.Controllers.Api
{
    [ApiController]
    [Route("api/auth")]
    [IgnoreAntiforgeryToken]
    public class AuthApiController : ControllerBase
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly IConfiguration _config;
        private readonly SymmetricSecurityKey _jwtKey;
        private readonly ApplicationDbContext _db;
        private readonly IEmailConfirmationSender _confirmationSender;
        private readonly IEmailSender _emailSender;
        private readonly IAppLinkService _appLinks;

        public AuthApiController(
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager,
            IConfiguration config,
            SymmetricSecurityKey jwtKey,
            ApplicationDbContext db,
            IEmailConfirmationSender confirmationSender,
            IEmailSender emailSender,
            IAppLinkService appLinks)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _config = config;
            _jwtKey = jwtKey;
            _db = db;
            _confirmationSender = confirmationSender;
            _emailSender = emailSender;
            _appLinks = appLinks;
        }

        // POST /api/auth/login
        [HttpPost("login")]
        [EnableRateLimiting("auth")]
        public async Task<IActionResult> Login([FromBody] LoginRequest request)
        {
            if (!ModelState.IsValid)
                return BadRequest(new { error = "Невалидни данни." });

            var loginId = request.Email.Trim();
            var user = await _userManager.FindByEmailAsync(loginId)
                ?? await _userManager.FindByNameAsync(loginId);
            if (user == null)
                return Unauthorized(new { error = "Грешен имейл или парола." });

            var result = await _signInManager.CheckPasswordSignInAsync(user, request.Password, lockoutOnFailure: true);

            if (result.IsLockedOut)
                return StatusCode(429, new { error = "Акаунтът е заключен. Опитай след малко." });

            if (result.IsNotAllowed)
                return Unauthorized(new { error = "Потвърди имейла си преди да влезеш." });

            if (!result.Succeeded)
                return Unauthorized(new { error = "Грешен имейл или парола." });

            var token = await GenerateJwtTokenAsync(user);
            var userDto = await BuildUserDtoAsync(user);

            return Ok(new { token, user = userDto });
        }

        // POST /api/auth/register
        [HttpPost("register")]
        [EnableRateLimiting("auth")]
        public async Task<IActionResult> Register([FromBody] RegisterRequest request)
        {
            if (!ModelState.IsValid)
                return BadRequest(new { error = "Невалидни данни." });

            var user = new ApplicationUser
            {
                UserName = request.Email,
                Email = request.Email,
                FirstName = request.FirstName?.Trim(),
                LastName = request.LastName?.Trim(),
                EmailConfirmed = !_config.GetValue("Identity:RequireConfirmedAccount", true),
            };

            var result = await _userManager.CreateAsync(user, request.Password);

            if (!result.Succeeded)
            {
                var errors = result.Errors.Select(e => e.Description);
                return BadRequest(new { error = string.Join(" ", errors) });
            }

            await _userManager.AddToRoleAsync(user, "User");

            if (!user.EmailConfirmed)
            {
                // Send confirmation email (non-blocking — don't fail registration if email fails)
                _ = _confirmationSender.SendAsync(user, HttpContext.Request, returnUrl: null, organizerSignup: false);
                return Ok(new { message = "Регистрацията е успешна! Изпратихме ти имейл за потвърждение." });
            }

            var token = await GenerateJwtTokenAsync(user);
            var userDto = await BuildUserDtoAsync(user);

            return Ok(new { token, user = userDto });
        }

        // GET /api/auth/me
        [HttpGet("me")]
        [Authorize(Policy = "ApiAuth")]
        public async Task<IActionResult> Me()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId == null) return Unauthorized();

            var user = await _userManager.FindByIdAsync(userId);
            if (user == null) return NotFound();

            return Ok(await BuildUserDtoAsync(user));
        }

        // POST /api/auth/resend-confirmation
        [HttpPost("resend-confirmation")]
        [EnableRateLimiting("auth")]
        public async Task<IActionResult> ResendConfirmation([FromBody] ResendConfirmationRequest request)
        {
            var user = await _userManager.FindByEmailAsync(request.Email.Trim());
            if (user == null || await _userManager.IsEmailConfirmedAsync(user))
                return Ok(new { message = "Ако имейлът съществува и не е потвърден, ще получиш нов линк." });

            _ = _confirmationSender.SendAsync(user, HttpContext.Request, returnUrl: null, organizerSignup: false);
            return Ok(new { message = "Изпратихме нов линк за потвърждение." });
        }

        // POST /api/auth/forgot-password
        [HttpPost("forgot-password")]
        [EnableRateLimiting("auth")]
        public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequest request)
        {
            var user = await _userManager.FindByEmailAsync(request.Email.Trim());
            // Always return OK to avoid user enumeration
            if (user == null || !await _userManager.IsEmailConfirmedAsync(user))
                return Ok(new { message = "Ако имейлът е регистриран, ще получиш линк за смяна на парола." });

            var code = await _userManager.GeneratePasswordResetTokenAsync(user);
            code = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(code));
            var resetRequest = new PasswordResetRequest
            {
                Id = Guid.NewGuid().ToString("N"),
                UserId = user.Id,
                Email = user.Email!,
                Code = code,
                CreatedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.AddHours(2),
            };
            _db.PasswordResetRequests.Add(resetRequest);
            await _db.SaveChangesAsync();

            var resetUrl = _appLinks.ToAbsoluteUrl(HttpContext.Request, $"/reset-password?r={resetRequest.Id}");
            var encodedUrl = HtmlEncoder.Default.Encode(resetUrl);

            _ = _emailSender.SendEmailAsync(user.Email!, "Смяна на парола - Evento", $"""
                <div style="margin:0;padding:0;background:#f2f5ff">
                  <table role="presentation" width="100%" cellspacing="0" cellpadding="0" border="0" style="background:#f2f5ff;border-collapse:collapse">
                    <tr><td align="center" style="padding:28px 14px">
                      <table role="presentation" width="100%" cellspacing="0" cellpadding="0" border="0" style="max-width:560px;background:#fff;border-collapse:collapse;border-radius:18px;overflow:hidden">
                        <tr><td style="background:#4f46e5;color:#fff;padding:28px 30px;font-family:Arial,sans-serif">
                          <div style="font-size:12px;font-weight:800;letter-spacing:1px;text-transform:uppercase">Evento</div>
                          <h1 style="margin:14px 0 0;font-size:26px;color:#fff">Смяна на парола</h1>
                        </td></tr>
                        <tr><td style="padding:28px 30px;font-family:Arial,sans-serif;color:#111827;font-size:15px;line-height:1.55">
                          <p style="margin:0 0 18px">Получихме заявка за смяна на паролата ти. Натисни бутона по-долу:</p>
                          <table role="presentation" cellspacing="0" cellpadding="0" border="0" style="border-collapse:collapse;margin:0 0 20px">
                            <tr><td bgcolor="#4f46e5" style="border-radius:10px">
                              <a href="{encodedUrl}" target="_blank" rel="noopener" style="display:inline-block;padding:12px 22px;font-family:Arial,sans-serif;font-size:15px;font-weight:800;color:#fff;text-decoration:none;border-radius:10px">Смени паролата</a>
                            </td></tr>
                          </table>
                          <p style="margin:0 0 8px;color:#475569">Ако не си поискал смяна на парола, игнорирай този имейл. Линкът изтича след 2 часа.</p>
                        </td></tr>
                      </table>
                    </td></tr>
                  </table>
                </div>
                """);

            return Ok(new { message = "Ако имейлът е регистриран, ще получиш линк за смяна на парола." });
        }

        // POST /api/auth/reset-password
        [HttpPost("reset-password")]
        [EnableRateLimiting("auth")]
        public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequest request)
        {
            var now = DateTime.UtcNow;
            var resetRequest = await _db.PasswordResetRequests
                .AsTracking()
                .FirstOrDefaultAsync(r => r.Id == request.RequestId.Trim()
                    && r.UsedAt == null
                    && r.ExpiresAt > now);

            if (resetRequest == null)
                return BadRequest(new { error = "Линкът е невалиден или изтекъл. Поискай нов линк." });

            var user = await _userManager.FindByIdAsync(resetRequest.UserId);
            if (user == null) return BadRequest(new { error = "Акаунтът не е намерен." });

            string decodedCode;
            try { decodedCode = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(resetRequest.Code)); }
            catch { return BadRequest(new { error = "Невалиден линк." }); }

            var result = await _userManager.ResetPasswordAsync(user, decodedCode, request.NewPassword);
            if (!result.Succeeded)
                return BadRequest(new { error = result.Errors.FirstOrDefault()?.Description ?? "Грешка при смяна на паролата." });

            resetRequest.UsedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();

            return Ok(new { message = "Паролата е сменена успешно. Можеш да влезеш с новата парола." });
        }

        // POST /api/auth/logout
        [HttpPost("logout")]
        [Authorize(Policy = "ApiAuth")]
        public async Task<IActionResult> Logout()
        {
            await _signInManager.SignOutAsync();
            return Ok(new { message = "Излязохте успешно." });
        }

        // POST /api/auth/refresh  —  issue a fresh JWT for an already-authenticated user
        [HttpPost("refresh")]
        [Authorize(Policy = "ApiAuth")]
        public async Task<IActionResult> Refresh()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId == null) return Unauthorized();

            var user = await _userManager.FindByIdAsync(userId);
            if (user == null) return NotFound();

            var token = await GenerateJwtTokenAsync(user);
            return Ok(new { token });
        }

        // ── helpers ─────────────────────────────────────────────────────────────

        private async Task<string> GenerateJwtTokenAsync(ApplicationUser user)
        {
            var roles = await _userManager.GetRolesAsync(user);

            var claims = new List<Claim>
            {
                new(JwtRegisteredClaimNames.Sub,   user.Id),
                new(JwtRegisteredClaimNames.Email, user.Email!),
                new(JwtRegisteredClaimNames.UniqueName, user.UserName!),
                new(JwtRegisteredClaimNames.Jti,   Guid.NewGuid().ToString()),
                new(ClaimTypes.NameIdentifier,     user.Id),
                new(ClaimTypes.Name,               user.UserName!),
                new(ClaimTypes.Email,              user.Email!),
            };

            foreach (var role in roles)
                claims.Add(new Claim(ClaimTypes.Role, role));

            var expirationDays = _config.GetValue("Jwt:ExpirationDays", 7);
            var creds = new SigningCredentials(_jwtKey, SecurityAlgorithms.HmacSha256);

            var token = new JwtSecurityToken(
                issuer: "Evento",
                audience: "EventoUsers",
                claims: claims,
                expires: DateTime.UtcNow.AddDays(expirationDays),
                signingCredentials: creds);

            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        private async Task<object> BuildUserDtoAsync(ApplicationUser user)
        {
            var roles = await _userManager.GetRolesAsync(user);
            return new
            {
                id = user.Id,
                email = user.Email,
                userName = user.UserName,
                firstName = user.FirstName,
                lastName = user.LastName,
                profileImageUrl = user.ProfileImageUrl,
                bio = user.Bio,
                roles = roles.ToArray(),
            };
        }

        // PUT /api/auth/me
        [HttpPut("me")]
        [Authorize(Policy = "ApiAuth")]
        public async Task<IActionResult> UpdateMe([FromBody] UpdateMeRequest request)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId == null) return Unauthorized();

            var user = await _userManager.FindByIdAsync(userId);
            if (user == null) return NotFound();

            user.FirstName = string.IsNullOrWhiteSpace(request.FirstName) ? null : request.FirstName.Trim();
            user.LastName = string.IsNullOrWhiteSpace(request.LastName) ? null : request.LastName.Trim();
            user.Bio = string.IsNullOrWhiteSpace(request.Bio) ? null : request.Bio.Trim();
            user.ProfileImageUrl = string.IsNullOrWhiteSpace(request.ProfileImageUrl) ? null : request.ProfileImageUrl.Trim();

            var result = await _userManager.UpdateAsync(user);
            if (!result.Succeeded)
                return BadRequest(new { error = result.Errors.FirstOrDefault()?.Description });

            return Ok(await BuildUserDtoAsync(user));
        }

        // POST /api/auth/apply-organizer
        [HttpPost("apply-organizer")]
        [Authorize(Policy = "ApiAuth")]
        public async Task<IActionResult> ApplyOrganizer([FromBody] ApplyOrganizerRequest request)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId == null) return Unauthorized();

            var existing = await _db.OrganizerData.AnyAsync(o => o.OrganizerId == userId);
            if (existing) return BadRequest(new { error = "Вече имаш кандидатура." });

            if (string.IsNullOrWhiteSpace(request.OrganizationName))
                return BadRequest(new { error = "Въведи организация." });

            _db.OrganizerData.Add(new OrganizerData
            {
                OrganizerId = userId,
                OrganizationName = request.OrganizationName.Trim(),
                PhoneNumber = request.PhoneNumber?.Trim(),
                Country = request.Country?.Trim() ?? "BG",
                City = request.City?.Trim(),
                Website = request.Website?.Trim(),
                CompanyNumber = request.CompanyNumber?.Trim(),
                ReferralSource = request.ReferralSource?.Trim(),
                Description = request.Description?.Trim(),
                Approved = false,
            });
            await _db.SaveChangesAsync();

            return Ok(new { message = "Заявката е изпратена." });
        }

        // ── request models ───────────────────────────────────────────────────────

        public class LoginRequest
        {
            public string Email { get; set; } = "";
            public string Password { get; set; } = "";
        }

        public class ResendConfirmationRequest
        {
            public string Email { get; set; } = "";
        }

        public class ForgotPasswordRequest
        {
            public string Email { get; set; } = "";
        }

        public class ResetPasswordRequest
        {
            public string RequestId { get; set; } = "";
            public string NewPassword { get; set; } = "";
        }

        public class RegisterRequest
        {
            public string Email { get; set; } = "";
            public string Password { get; set; } = "";
            public string? FirstName { get; set; }
            public string? LastName { get; set; }
        }

        public class UpdateMeRequest
        {
            public string? FirstName { get; set; }
            public string? LastName { get; set; }
            public string? Bio { get; set; }
            public string? ProfileImageUrl { get; set; }
        }

        public class ApplyOrganizerRequest
        {
            public string OrganizationName { get; set; } = "";
            public string? PhoneNumber { get; set; }
            public string? Country { get; set; }
            public string? City { get; set; }
            public string? Website { get; set; }
            public string? CompanyNumber { get; set; }
            public string? ReferralSource { get; set; }
            public string? Description { get; set; }
        }
    }
}
