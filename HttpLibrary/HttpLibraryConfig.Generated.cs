namespace HttpLibrary
{
    public static partial class Constants
    {
        // Generated fallback constants for configuration file names.
        // These mirror the values declared in Directory.Build.props and are required
        // at compile time when the MSBuild-driven generation is not available.
        public const string DefaultsConfigFile = "defaults.json";
        public const string ClientsConfigFile = "clients.json";
        public const string CookiesFile = "cookies.json";

        // CLI log output path (file pattern). Program.cs uses this to initialize Serilog file sink.
        public const string LogOutputPath = "logs/app-{Date}.log";
    }
}
