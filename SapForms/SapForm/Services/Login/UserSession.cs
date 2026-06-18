using Microsoft.AspNetCore.Components.Authorization;
using System.Security.Claims;

namespace SapForm.Services.Login
{

    public class UserSession(AuthenticationStateProvider authProvider)
    {
        private readonly AuthenticationStateProvider _authProvider = authProvider;
        public ClaimsPrincipal? CurrentUser { get; private set; }

        public async Task<ClaimsPrincipal> GetUserAsync()
        {
            AuthenticationState state = await _authProvider.GetAuthenticationStateAsync();
            return state.User;
        }
        public void SignIn(ClaimsPrincipal principal)
        {
            CurrentUser = principal;
        }

        public void SignOut()
        {
            CurrentUser = new ClaimsPrincipal(new ClaimsIdentity());
        }

        public bool IsLoggedIn => CurrentUser?.Identity?.IsAuthenticated ?? false;
        public async Task<int?> GetUserIdAsync()
        {
            ClaimsPrincipal user = await GetUserAsync();
            var id = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            return int.TryParse(id, out var guid) ? guid : null;
        }

        public async Task<string?> GetEmailAsync()
        {
            ClaimsPrincipal user = await GetUserAsync();
            return user.FindFirst(ClaimTypes.Email)?.Value; 
        }

        public async Task<string?> GetUserNameAsync()
        {
            ClaimsPrincipal user = await GetUserAsync();
            return user.FindFirst(ClaimTypes.Name)?.Value;
        }

        public async Task<List<string>> GetRolesAsync()
        {
            ClaimsPrincipal user = await GetUserAsync();
            return user.Claims
                .Where(c => c.Type == ClaimTypes.Role)
                .Select(c => c.Value)
                .ToList();
        }
    }
}