using System.Text.Json.Serialization;

namespace HttpLibrary
{
	/// <summary>
	/// Application configuration for runtime settings
	/// </summary>
	public class ApplicationConfiguration
	{
		public bool EnableCertificatePinning { get; set; }
		public bool EnableCustomDns { get; set; }
		public bool EnableTrafficMonitoring { get; set; }
		public bool EnableMutualTls { get; set; }
		public CertificatePinningConfig? CertificatePinning { get; set; }
		public CustomDnsConfig? CustomDns { get; set; }
		public MutualTlsConfig? MutualTls { get; set; }
		public FileUploadConfig FileUpload { get; set; } = new FileUploadConfig();
		public ProgressDisplayConfig ProgressDisplay { get; set; } = new ProgressDisplayConfig();

		// Per-client callback configuration keyed by client name (case-insensitive).
		// This allows application.json to provide callback settings for specific clients.
		public System.Collections.Generic.Dictionary<string, ClientCallbackSettings>? Clients { get; set; }

		// Metrics display configuration (controls whether CLI prints client metrics and when)
		public MetricsDisplayConfig MetricsDisplay { get; set; } = new MetricsDisplayConfig();

		// Optional alias mappings for the alias:// scheme. Key is alias name, value is base URL (absolute).
		// Example: { "billing": "https://billing.api.example" }
		public System.Collections.Generic.Dictionary<string, string>? Aliases { get; set; }
	}

	public class ClientCallbackSettings
	{
		public bool? EnableCertificatePinning { get; set; }
		public CertificatePinningConfig? CertificatePinning { get; set; }
		public bool? EnableCustomDns { get; set; }
		public CustomDnsConfig? CustomDns { get; set; }
		public bool? EnableTrafficMonitoring { get; set; }
		public bool? EnableMutualTls { get; set; }
		public MutualTlsConfig? MutualTls { get; set; }
	}

	public class CertificatePinningConfig
	{
		public System.Collections.Generic.List<string> PinnedThumbprints { get; set; } = new System.Collections.Generic.List<string>();
	}

	public class CustomDnsConfig
	{
		public SocketOptions? SocketOptions { get; set; }
	}

	public class SocketOptions
	{
		public bool NoDelay { get; set; } = true;
		public int SendTimeout { get; set; } = 5000;
		public int ReceiveTimeout { get; set; } = 5000;
		public int SendBufferSize { get; set; } = 65536;
		public int ReceiveBufferSize { get; set; } = 65536;
	}

	public class MutualTlsConfig
	{
		public string CertificateSubjectName { get; set; } = "MyClientCert";
		public string StoreLocation { get; set; } = "CurrentUser";
		public string StoreName { get; set; } = "My";

		public System.Security.Cryptography.X509Certificates.StoreLocation GetStoreLocation()
		{
			return StoreLocation switch
			{
				"CurrentUser" => System.Security.Cryptography.X509Certificates.StoreLocation.CurrentUser,
				"LocalMachine" => System.Security.Cryptography.X509Certificates.StoreLocation.LocalMachine,
				_ => System.Security.Cryptography.X509Certificates.StoreLocation.CurrentUser
			};
		}

		public System.Security.Cryptography.X509Certificates.StoreName GetStoreName()
		{
			return StoreName switch
			{
				"My" => System.Security.Cryptography.X509Certificates.StoreName.My,
				"Root" => System.Security.Cryptography.X509Certificates.StoreName.Root,
				"TrustedPeople" => System.Security.Cryptography.X509Certificates.StoreName.TrustedPeople,
				"AddressBook" => System.Security.Cryptography.X509Certificates.StoreName.AddressBook,
				"AuthRoot" => System.Security.Cryptography.X509Certificates.StoreName.AuthRoot,
				"CertificateAuthority" => System.Security.Cryptography.X509Certificates.StoreName.CertificateAuthority,
				"Disallowed" => System.Security.Cryptography.X509Certificates.StoreName.Disallowed,
				"TrustedPublisher" => System.Security.Cryptography.X509Certificates.StoreName.TrustedPublisher,
				_ => System.Security.Cryptography.X509Certificates.StoreName.My
			};
		}
	}

	public class FileUploadConfig
	{
		public long MaxFileSizeBytes { get; set; } = 100_000_000;
	}

	public class ProgressDisplayConfig
	{
		public long MinimumFileSizeBytes { get; set; } = 131072;

		/// <summary>
		/// Minimum number of bytes that will trigger an intermediate progress event when a single read
		/// contributes a large number of bytes. Defaults to64 KB.
		/// </summary>
		public long ProgressEventGranularityBytes { get; set; } = 64 * 1024;

		/// <summary>
		/// Minimum delta in bytes between progress updates for the CLI progress display. Defaults to16 KB.
		/// </summary>
		public long ProgressUpdateDeltaBytes { get; set; } = 16 * 1024;

		/// <summary>
		/// Minimum time in milliseconds between progress updates for the CLI progress display. Defaults to150 ms.
		/// </summary>
		public int ProgressUpdateIntervalMilliseconds { get; set; } = 150;
	}

	public class MetricsDisplayConfig
	{
		/// <summary>
		/// When true, the CLI will print per-client metrics after a request completes.
		/// </summary>
		public bool Enabled { get; set; } = true;

		/// <summary>
		/// Minimum total requests a client must have for metrics to be displayed.
		/// Set to0 to show metrics for all clients.
		/// </summary>
		public int MinimumTotalRequests { get; set; } = 0;
	}

	public class ApplicationSettingsRoot
	{
		public ApplicationConfiguration ApplicationSettings { get; set; } = new ApplicationConfiguration();
	}
}