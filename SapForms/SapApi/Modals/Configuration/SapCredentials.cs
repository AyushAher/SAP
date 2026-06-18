namespace SapApi.Modals.Configuration
{
    public class SapCredentials
    {
        public static string Label = "SapCredentials";
        public string? Username { get; set; }
        public string? Password { get; set; }
        public string? CompanyDb { get; set; }
    }
}