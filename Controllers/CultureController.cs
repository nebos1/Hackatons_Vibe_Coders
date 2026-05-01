using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;

namespace EventsApp.Controllers
{
    public class CultureController : Controller
    {
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Set(string culture, string returnUrl = "/")
        {
            if (!string.IsNullOrEmpty(culture))
            {
                Response.Cookies.Append(
                    CookieRequestCultureProvider.DefaultCookieName,
                    CookieRequestCultureProvider.MakeCookieValue(new RequestCulture(culture)),
                    new CookieOptions
                    {
                        Expires = DateTimeOffset.UtcNow.AddYears(1),
                        IsEssential = true,
                        SameSite = SameSiteMode.Lax
                    });
            }

            if (!Url.IsLocalUrl(returnUrl))
                returnUrl = "/";

            return LocalRedirect(returnUrl);
        }
    }
}
