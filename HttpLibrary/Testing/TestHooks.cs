using System;
using System.Net.Http;

namespace HttpLibrary.Testing
{
	public static class TestHooks
	{
		public static void SetPrimaryHandlerFactory(Func<HttpMessageHandler>? factory)
		{
			HttpLibrary.ServiceConfiguration.PrimaryHandlerFactory = factory;
		}
	}
}