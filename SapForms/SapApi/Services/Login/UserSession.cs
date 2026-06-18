using System.Security.Claims;

namespace SapApi.Services.Login
{
    public class UserSession
    {
        public ClaimsPrincipal? CurrentUser { get; private set; }

        public void SignIn(ClaimsPrincipal principal)
        {
            CurrentUser = principal;
        }

        public void SignOut()
        {
            CurrentUser = new ClaimsPrincipal(new ClaimsIdentity());
        }

        public bool IsLoggedIn => CurrentUser?.Identity?.IsAuthenticated ?? false;
    }
}