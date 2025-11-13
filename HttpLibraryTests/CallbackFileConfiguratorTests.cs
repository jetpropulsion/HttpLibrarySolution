using HttpLibrary;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace HttpLibraryTests
{
	[TestClass]
	public class CallbackConfiguratorTests
	{
		private string _tempFile = null!;

		[TestInitialize]
		public void Init()
		{
			_tempFile = Path.Combine(Path.GetTempPath(), "callbacks_test.json");
			if(File.Exists(_tempFile))
				File.Delete(_tempFile);
			CallbackRegistry.Clear();
		}

		[TestCleanup]
		public void Cleanup()
		{
			try
			{ if(File.Exists(_tempFile)) File.Delete(_tempFile); }
			catch { }
			CallbackRegistry.Clear();
		}

		[TestMethod]
		public void ConfigureFromApplicationSettings_MergesWithExistingHandlers()
		{
			// Arrange: register a programmatic handler first
			SocketCallbackHandlers programHandlers = new SocketCallbackHandlers();
			programHandlers.PlaintextStreamFilter = (context, token) => new System.Threading.Tasks.ValueTask<System.IO.Stream>(context.PlaintextStream);
			CallbackRegistry.RegisterHandlers("default", programHandlers);

			// Create application configuration that only pins certificates for 'default'
			ApplicationConfiguration cfg = new ApplicationConfiguration();
			cfg.Clients = new System.Collections.Generic.Dictionary<string, ClientCallbackSettings>(StringComparer.OrdinalIgnoreCase);
			ClientCallbackSettings cbs = new ClientCallbackSettings();
			cbs.EnableCertificatePinning = true;
			cbs.CertificatePinning = new CertificatePinningConfig();
			cbs.CertificatePinning.PinnedThumbprints.Add("DEADBEEF");
			cfg.Clients[ "default" ] = cbs;

			// Act
			CallbackConfigurator.ConfigureFromApplicationSettings("default", cfg, NullLogger.Instance);

			// Assert: existing plaintext handler should still be present, and server cert handler should be set
			SocketCallbackHandlers? merged = CallbackRegistry.GetHandlers("default");
			Assert.IsNotNull(merged, "Handlers should be registered");
			Assert.IsNotNull(merged.PlaintextStreamFilter, "Existing PlaintextStreamFilter should be preserved");
			Assert.IsNotNull(merged.ServerCertificateCustomValidationCallback, "Pinned cert handler should be registered");
		}

		[TestMethod]
		public void ConfigureFromApplicationSettings_SkipsInvalidEntries()
		{
			// Empty name entries are not possible via the API; ensure no registration occurs for missing client
			ApplicationConfiguration cfg = new ApplicationConfiguration();
			cfg.Clients = new System.Collections.Generic.Dictionary<string, ClientCallbackSettings>(StringComparer.OrdinalIgnoreCase);
			// do not add any client entries

			// Act - no exception
			CallbackConfigurator.ConfigureFromApplicationSettings("", cfg, NullLogger.Instance);

			// No handlers should be registered for empty name
			SocketCallbackHandlers? h = CallbackRegistry.GetHandlers("");
			Assert.IsNull(h, "Empty name should not register handlers");
		}

		[TestMethod]
		public void ConfigureFromApplicationSettings_SetsConnectCallback()
		{
			ApplicationConfiguration cfg = new ApplicationConfiguration();
			cfg.Clients = new System.Collections.Generic.Dictionary<string, ClientCallbackSettings>(StringComparer.OrdinalIgnoreCase);
			ClientCallbackSettings cbs = new ClientCallbackSettings();
			cbs.EnableCustomDns = true;
			cbs.CustomDns = new CustomDnsConfig { SocketOptions = new SocketOptions { NoDelay = true } };
			cfg.Clients[ "conn" ] = cbs;

			// Act
			CallbackConfigurator.ConfigureFromApplicationSettings("conn", cfg, NullLogger.Instance);

			SocketCallbackHandlers? handlers = CallbackRegistry.GetHandlers("conn");
			Assert.IsNotNull(handlers, "Handlers should be registered for 'conn'");
			Assert.IsNotNull(handlers.ConnectCallback, "ConnectCallback should be populated from application configuration");
		}

		[TestMethod]
		public void ConfigureFromApplicationSettings_RegistersMutualTlsHandler()
		{
			ApplicationConfiguration cfg = new ApplicationConfiguration();
			cfg.Clients = new System.Collections.Generic.Dictionary<string, ClientCallbackSettings>(StringComparer.OrdinalIgnoreCase);
			ClientCallbackSettings cbs = new ClientCallbackSettings();
			cbs.EnableMutualTls = true;
			cbs.MutualTls = new MutualTlsConfig { CertificateSubjectName = "DoesNotExist" };
			cfg.Clients[ "mtls" ] = cbs;

			// Act
			CallbackConfigurator.ConfigureFromApplicationSettings("mtls", cfg, NullLogger.Instance);

			SocketCallbackHandlers? handlers = CallbackRegistry.GetHandlers("mtls");
			Assert.IsNotNull(handlers, "Handlers should be registered for 'mtls'");
			Assert.IsNotNull(handlers.LocalCertificateSelectionCallback, "LocalCertificateSelectionCallback should be set when EnableMutualTls=true");
		}
	}
}