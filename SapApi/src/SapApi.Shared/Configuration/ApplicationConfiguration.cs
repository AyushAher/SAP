namespace SapApi.Shared.Configuration
{
    public class ApplicationConfiguration
    {
        public const string Label = "ApplicationConfiguration";
        public string SapServiceLayerUrl { get; set; }
        public string AuthServiceUrl { get; set; }
        public bool SkipSapLoginOnUserAuth { get; set; }
    }
}
