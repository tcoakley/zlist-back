namespace zListBack.Utils
{
    public static class TokenHelper
    {
        public static string GenerateRefreshToken()
        {
            return Convert.ToBase64String(Guid.NewGuid().ToByteArray()) +
                   Convert.ToBase64String(Guid.NewGuid().ToByteArray());
        }
    }

}
