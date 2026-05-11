namespace zListBack.Dtos
{
    public class SignupRequest
    {
        public string Email { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public string CaptchaToken { get; set; } = string.Empty;
    }
}
