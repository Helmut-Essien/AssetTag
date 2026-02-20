namespace MobileApp.Configuration
{
    public class ApiSettings
    {
        public string PrimaryApiUrl { get; set; } = "https://mugassetapi.runasp.net/";
        public string FallbackApiUrl { get; set; } = "https://localhost:7135/";
        public int RequestTimeout { get; set; } = 30;
    }
}