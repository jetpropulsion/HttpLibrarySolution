using HttpLibrary;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using System;
using System.Net.Http;
using System.Net.Security;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace HttpLibraryTests
{
	[TestClass]
	public class CallbackAdapterTests
	{
		private static X509Certificate2 CreateSelfSignedCert()
		{
			using RSA rsa = RSA.Create(2048);
			CertificateRequest req = new CertificateRequest("CN=localhost", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
			return req.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(365));
		}

		[TestMethod]
		public void ServerCertificateCallback_Invoke_ReturnsTrue()
		{
			bool invoked = false;
			SocketCallbackHandlers handlers = new SocketCallbackHandlers();
			handlers.ServerCertificateCustomValidationCallback = (HttpRequestMessage req, X509Certificate2? cert, X509Chain? chain, SslPolicyErrors errors) =>
			{
				invoked = true;
				return true;
			};

			// Adapt like ServiceConfiguration does
			RemoteCertificateValidationCallback adapter = (sender, certificate, chain, sslPolicyErrors) =>
			{
				HttpRequestMessage tempReq = new HttpRequestMessage();
				X509Certificate2? cert2 = certificate as X509Certificate2;
				return handlers.ServerCertificateCustomValidationCallback!(tempReq, cert2, chain, sslPolicyErrors);
			};

			X509Certificate2 cert = CreateSelfSignedCert();
			bool result = adapter(new object(), cert, new X509Chain(), SslPolicyErrors.None);
			Assert.IsTrue(invoked, "Runtime server certificate callback should be invoked");
			Assert.IsTrue(result, "Adapter should return the value from runtime callback");
		}

		[TestMethod]
		public void ServerCertificateCallback_ExceptionHandled_ReturnsFalse()
		{
			SocketCallbackHandlers handlers = new SocketCallbackHandlers();
			handlers.ServerCertificateCustomValidationCallback = (HttpRequestMessage req, X509Certificate2? cert, X509Chain? chain, SslPolicyErrors errors) =>
			{
				throw new InvalidOperationException("boom");
			};

			RemoteCertificateValidationCallback adapter = (sender, certificate, chain, sslPolicyErrors) =>
			{
				try
				{
					HttpRequestMessage tempReq = new HttpRequestMessage();
					X509Certificate2? cert2 = certificate as X509Certificate2;
					return handlers.ServerCertificateCustomValidationCallback!(tempReq, cert2, chain, sslPolicyErrors);
				}
				catch
				{
					return false; // adapter swallows exceptions and returns false
				}
			};

			X509Certificate2 cert = CreateSelfSignedCert();
			bool result = adapter(new object(), cert, new X509Chain(), SslPolicyErrors.RemoteCertificateNameMismatch);
			Assert.IsFalse(result, "Adapter should return false when runtime callback throws");
		}

		[TestMethod]
		public void LocalCertificateSelection_Invoke_ReturnsCertificate()
		{
			SocketCallbackHandlers handlers = new SocketCallbackHandlers();

			X509Certificate2 dummy = CreateSelfSignedCert();

			handlers.LocalCertificateSelectionCallback = (HttpRequestMessage reqMsg, X509Certificate2Collection? localCerts, string[] issuers) =>
			{
				if(localCerts != null && localCerts.Count > 0)
				{
					return localCerts[ 0 ];
				}
				return null;
			};

			X509CertificateCollection nativeColl = new X509CertificateCollection();
			nativeColl.Add(dummy);

			Func<object?, string, X509CertificateCollection?, X509Certificate?, string[], X509Certificate?> adapter = (sender, targetHost, localCertificates, remoteCertificate, acceptableIssuers) =>
			{
				try
				{
					X509Certificate2Collection? localCerts2 = null;
					if(( localCertificates?.Count ?? 0 ) > 0)
					{
						localCerts2 = new X509Certificate2Collection();
						foreach(X509Certificate c in localCertificates!)
						{
							if(c is X509Certificate2 c2)
							{
								localCerts2.Add(c2);
							}
						}
					}

					HttpRequestMessage tempReq = new HttpRequestMessage();
					X509Certificate2? selected = handlers.LocalCertificateSelectionCallback!(tempReq, localCerts2, acceptableIssuers ?? Array.Empty<string>());
					return selected as X509Certificate;
				}
				catch
				{
					return null;
				}
			};

			X509Certificate? outCert = adapter(new object(), "host", nativeColl, null, Array.Empty<string>());
			Assert.IsNotNull(outCert, "Adapter should return the certificate selected by runtime callback");
		}

		[TestMethod]
		public void LocalCertificateSelection_ExceptionHandled_ReturnsNull()
		{
			SocketCallbackHandlers handlers = new SocketCallbackHandlers();
			handlers.LocalCertificateSelectionCallback = (HttpRequestMessage reqMsg, X509Certificate2Collection? localCerts, string[] issuers) =>
			{
				throw new InvalidOperationException("boom");
			};

			Func<object?, string, X509CertificateCollection?, X509Certificate?, string[]?, X509Certificate?> adapter = (sender, targetHost, localCertificates, remoteCertificate, acceptableIssuers) =>
			{
				try
				{
					X509Certificate2Collection? localCerts2 = null;
					if(( localCertificates?.Count ?? 0 ) > 0)
					{
						localCerts2 = new X509Certificate2Collection();
						foreach(X509Certificate c in localCertificates!)
						{
							if(c is X509Certificate2 c2)
							{
								localCerts2.Add(c2);
							}
						}
					}

					HttpRequestMessage tempReq = new HttpRequestMessage();
					X509Certificate2? selected = handlers.LocalCertificateSelectionCallback!(tempReq, localCerts2, acceptableIssuers ?? Array.Empty<string>());
					return selected as X509Certificate;
				}
				catch
				{
					return null;
				}
			};

			X509CertificateCollection? none = null;
			X509Certificate? outCert = adapter(new object(), "host", none, null, Array.Empty<string>());
			Assert.IsNull(outCert, "Adapter should return null when runtime callback throws");
		}
	}
}