namespace zListBack.Dtos
{
    public class GoogleLoginRequest
    {
        // The ID token JWT returned by Google Identity Services on the frontend.
        public required string Credential { get; set; }
    }
}
