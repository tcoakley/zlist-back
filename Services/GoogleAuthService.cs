using Google.Apis.Auth;
using zListBack.Models;

namespace zListBack.Services
{
    public class GoogleAuthService
    {
        private readonly string _clientId;
        private readonly ILogger<GoogleAuthService> _logger;

        public GoogleAuthService(IConfiguration configuration, ILogger<GoogleAuthService> logger)
        {
            _clientId = configuration["GoogleAuth:ClientId"] ?? string.Empty;
            _logger = logger;
        }

        // Verifies the ID token's signature, expiry, and audience against Google's public keys.
        // Returns null if the token is invalid/expired/wasn't issued for this app.
        public virtual async Task<GoogleUserInfo?> VerifyIdTokenAsync(string credential)
        {
            try
            {
                var payload = await GoogleJsonWebSignature.ValidateAsync(credential, new GoogleJsonWebSignature.ValidationSettings
                {
                    Audience = new[] { _clientId }
                });

                if (!payload.EmailVerified)
                    return null;

                return new GoogleUserInfo
                {
                    Email = payload.Email,
                    FirstName = payload.GivenName,
                    LastName = payload.FamilyName
                };
            }
            catch (InvalidJwtException ex)
            {
                _logger.LogWarning(ex, "Google ID token verification failed.");
                return null;
            }
        }
    }
}
