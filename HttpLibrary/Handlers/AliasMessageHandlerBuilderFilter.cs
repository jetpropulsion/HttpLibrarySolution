using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http;

using System;
using System.Net.Http;

namespace HttpLibrary.Handlers
{
	/// <summary>
	/// IHttpMessageHandlerBuilderFilter that injects AliasResolutionHandler as the outermost delegating handler
	/// so alias:// requests are rewritten before other framework-provided handlers (e.g. logging) inspect the request.
	/// </summary>
	public sealed class AliasMessageHandlerBuilderFilter : IHttpMessageHandlerBuilderFilter
	{
		public Action<HttpMessageHandlerBuilder> Create(Action<HttpMessageHandlerBuilder> next)
		{
			return builder =>
			{
				next(builder);

				try
				{
					if(ServiceConfiguration.AppConfig?.Aliases != null)
					{
						IServiceProvider services = (IServiceProvider)builder.Services;
						AliasResolutionHandler? aliasHandler = services.GetService<AliasResolutionHandler>();
						if(aliasHandler != null)
						{
							// Insert at index0 so it runs before other additional handlers
							builder.AdditionalHandlers.Insert(0, aliasHandler);
						}
					}
				}
				catch
				{
					// best-effort
				}
			};
		}

		// Some versions of the library expect Configure instead of Create; delegate to Create for compatibility.
		public Action<HttpMessageHandlerBuilder> Configure(Action<HttpMessageHandlerBuilder> next) => Create(next);
	}
}