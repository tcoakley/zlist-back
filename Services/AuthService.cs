using zListBack.Dtos;
using zListBack.Mappers;
using zListBack.Models;
using zListBack.Repositories;
using zListBack.Utils;

namespace zListBack.Services
{
    public class AuthService
    {
        private readonly RefreshTokenRepository _refreshTokenRepository;
        private readonly IUserRepository _userRepository;
        private readonly RecaptchaService _recaptchaService;
        private readonly EmailService _emailService;
        private readonly SubscriptionService _subscriptionService;

        public AuthService(
            RefreshTokenRepository refreshTokenRepository,
            IUserRepository userRepository,
            RecaptchaService recaptchaService,
            EmailService emailService,
            SubscriptionService subscriptionService)
        {
            _refreshTokenRepository = refreshTokenRepository;
            _userRepository = userRepository;
            _recaptchaService = recaptchaService;
            _emailService = emailService;
            _subscriptionService = subscriptionService;
        }

        public async Task<(string TokenString, DateTime ExpiresAt)> CreateRefreshToken(int userId)
        {
            var tokenString = TokenHelper.GenerateRefreshToken();
            var token = new RefreshToken
            {
                UserId = userId,
                Token = tokenString,
                ExpiresAt = DateTime.UtcNow.AddYears(5),
                CreatedAt = DateTime.UtcNow,
                Revoked = false
            };
            await _refreshTokenRepository.AddAsync(token);
            return (tokenString, token.ExpiresAt);
        }

        // Validates the current token, rotates it (invalidates old, stores new), and returns the
        // associated user plus the new token string. Returns null if the token is invalid/expired.
        public async Task<(User User, string NewTokenString, DateTime ExpiresAt)?> RotateRefreshToken(string currentToken)
        {
            var refreshToken = await _refreshTokenRepository.GetByTokenAsync(currentToken);
            if (refreshToken == null || refreshToken.ExpiresAt < DateTime.UtcNow || refreshToken.Revoked)
                return null;

            var user = refreshToken.User;
            if (user == null) return null;

            var newTokenString = TokenHelper.GenerateRefreshToken();
            var newToken = new RefreshToken
            {
                UserId = user.Id,
                Token = newTokenString,
                ExpiresAt = DateTime.UtcNow.AddYears(5),
                CreatedAt = DateTime.UtcNow,
                Revoked = false
            };

            await _refreshTokenRepository.InvalidateAsync(currentToken);
            await _refreshTokenRepository.AddAsync(newToken);

            return (user, newTokenString, newToken.ExpiresAt);
        }

        public async Task<Result<UserModel>> SignupUser(SignupRequest request)
        {
            var captchaValid = await _recaptchaService.VerifyAsync(request.CaptchaToken);
            if (!captchaValid)
                return Result<UserModel>.Fail("CAPTCHA verification failed. Please try again.");

            var user = new User
            {
                Email = request.Email,
                Password = request.Password,
                FirstName = request.FirstName,
                LastName = request.LastName
            };

            var result = await _userRepository.AddUserAsync(user);
            if (!result.Success)
                return Result<UserModel>.Fail(result.Message ?? "Failed to add user.");

            _ = _emailService.SendWelcomeEmail(user.Email, user.FirstName ?? user.Email);
            await _subscriptionService.ApplyPendingSponsorshipOnSignup(result.Model!.Id, request.Email);
            return Result<UserModel>.Ok(UserMapper.ToDto(result.Model!));
        }

        public async Task<Result<UserModel>> GetUserProfileWithPremium(int userId)
        {
            var result = await _userRepository.GetUserAsync(userId);
            if (!result.Success) return Result<UserModel>.Fail(result.Message ?? "Failed to retrieve user profile.");
            var dto = UserMapper.ToDto(result.Model!);
            dto.IsPremium = await _subscriptionService.IsPremium(userId);
            return Result<UserModel>.Ok(dto);
        }

        public async Task<Result<bool>> DeleteAccount(int userId)
        {
            var userResult = await _userRepository.GetUserAsync(userId);
            if (!userResult.Success || userResult.Model == null)
                return Result<bool>.Fail("User not found.");

            var user = userResult.Model;

            // Delete from DB first so the Stripe webhook (fired by CancelSubscriptionImmediately)
            // finds no user and skips the subscription-cancelled email.
            var result = await _userRepository.DeleteAccountAsync(userId, user.Email);
            if (!result.Success) return result;

            if (!string.IsNullOrEmpty(user.StripeSubscriptionId))
            {
                try
                {
                    await _subscriptionService.CancelSubscriptionImmediately(user.StripeSubscriptionId);
                }
                catch (Exception ex)
                {
                    _ = ex;
                }
            }

            _ = _emailService.SendAccountDeletedEmail(user.Email, user.FirstName ?? user.Email);
            return result;
        }
    }
}
