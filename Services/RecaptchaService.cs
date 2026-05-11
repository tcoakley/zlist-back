using System.Text.Json.Serialization;

namespace zListBack.Services
{
    public class RecaptchaService
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly string _secretKey;

        public RecaptchaService(IHttpClientFactory httpClientFactory, IConfiguration configuration)
        {
            _httpClientFactory = httpClientFactory;
            _secretKey = configuration["Recaptcha:SecretKey"] ?? string.Empty;
        }

        public async Task<bool> VerifyAsync(string token)
        {
            if (string.IsNullOrWhiteSpace(token)) return false;

            var client = _httpClientFactory.CreateClient();
            var response = await client.PostAsync(
                "https://www.google.com/recaptcha/api/siteverify",
                new FormUrlEncodedContent(new[]
                {
                    new KeyValuePair<string, string>("secret", _secretKey),
                    new KeyValuePair<string, string>("response", token)
                }));

            if (!response.IsSuccessStatusCode) return false;

            var result = await response.Content.ReadFromJsonAsync<RecaptchaResponse>();
            return result?.Success ?? false;
        }
    }

    file class RecaptchaResponse
    {
        [JsonPropertyName("success")]
        public bool Success { get; set; }
    }
}
