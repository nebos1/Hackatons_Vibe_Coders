using EventsApp.Data;
using EventsApp.Models;
using Microsoft.AspNetCore.Authentication;
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

        public AuthApiController(
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager,
            IConfiguration config,
            SymmetricSecurityKey jwtKey,
            ApplicationDbContext db)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _config = config;
            _jwtKey = jwtKey;
            _db = db;
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
                return Ok(new { message = "Регистрацията е успешна! Потвърди имейла си преди да влезеш." });

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
