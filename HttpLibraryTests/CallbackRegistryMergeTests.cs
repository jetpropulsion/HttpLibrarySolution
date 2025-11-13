using HttpLibrary;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using System;
using System.IO;
using System.Net.Http;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;

namespace HttpLibraryTests
{
	[TestClass]
	public class CallbackRegistryTests
	{
		[TestMethod]
		public void CallbackRegistry_RegisterHandlers_MergesCallbacks()
		{
			CallbackRegistry.Clear();
			string clientName = "merge-client";

			SocketCallbackHandlers first = new SocketCallbackHandlers
			{
				ConnectCallback = (context, ct) => new ValueTask<Stream>(Stream.Null)
			};

			CallbackRegistry.RegisterHandlers(clientName, first);

			SocketCallbackHandlers second = new SocketCallbackHandlers
			{
				PlaintextStreamFilter = (context, ct) => new ValueTask<Stream>(Stream.Null)
			};

			CallbackRegistry.RegisterHandlers(clientName, second);

			SocketCallbackHandlers? merged = CallbackRegistry.GetHandlers(clientName);

			Assert.IsNotNull(merged, "Merged handlers should not be null");
			Assert.IsNotNull(merged!.ConnectCallback, "ConnectCallback should be preserved from first registration");
			Assert.IsNotNull(merged.PlaintextStreamFilter, "PlaintextStreamFilter should be present from second registration");
		}

		[TestMethod]
		public void CallbackRegistry_RegisterHandlers_OverridesNonNullProperties()
		{
			CallbackRegistry.Clear();
			string clientName = "override-client";

			SocketCallbackHandlers original = new SocketCallbackHandlers
			{
				ServerCertificateCustomValidationCallback = (req, cert, chain, errors) => false
			};

			CallbackRegistry.RegisterHandlers(clientName, original);

			SocketCallbackHandlers replacement = new SocketCallbackHandlers
			{
				ServerCertificateCustomValidationCallback = (req, cert, chain, errors) => true
			};

			CallbackRegistry.RegisterHandlers(clientName, replacement);

			SocketCallbackHandlers? result = CallbackRegistry.GetHandlers(clientName);
			Assert.IsNotNull(result, "Resulting handlers should not be null");
			Assert.IsNotNull(result!.ServerCertificateCustomValidationCallback, "ServerCertificateCustomValidationCallback should be set");

			// Call the callback using placeholders to satisfy nullable reference types
			HttpRequestMessage dummyReq = new HttpRequestMessage(HttpMethod.Get, "http://test");
			X509Certificate2? dummyCert = null;
			X509Chain? dummyChain = null;
			SslPolicyErrors errors = SslPolicyErrors.None;

			bool accepted = result.ServerCertificateCustomValidationCallback(dummyReq, dummyCert, dummyChain, errors);
			Assert.IsTrue(accepted, "The replacement validation callback should be in effect and return true");
		}

		[TestMethod]
		public void CallbackRegistry_RegisterHandlers_DoesNotOverwriteWithNull()
		{
			CallbackRegistry.Clear();
			string clientName = "nooverwrite-client";

			SocketCallbackHandlers original = new SocketCallbackHandlers
			{
				ConnectCallback = (context, ct) => new ValueTask<Stream>(Stream.Null)
			};

			CallbackRegistry.RegisterHandlers(clientName, original);

			// Register using configure action that does not set ConnectCallback
			CallbackRegistry.RegisterHandlers(clientName, handlers =>
			{
				handlers.PlaintextStreamFilter = (context, ct) => new ValueTask<Stream>(Stream.Null);
			});

			SocketCallbackHandlers? merged = CallbackRegistry.GetHandlers(clientName);
			Assert.IsNotNull(merged, "Merged handlers should not be null");
			Assert.IsNotNull(merged!.ConnectCallback, "ConnectCallback should still be present after second registration");
			Assert.IsNotNull(merged.PlaintextStreamFilter, "PlaintextStreamFilter should be present from second registration");
		}
	}
}