using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using System;
using System.Collections.Concurrent;
using System.Net.Http;

namespace HttpLibrary
{
	public sealed class NamedPooledHttpClientProvider : INamedPooledHttpClientProvider
	{
		readonly IHttpClientFactory factory;
		readonly IOptionsMonitor<PooledHttpClientOptions> options;
		readonly ILoggerFactory loggerFactory;
		readonly ConcurrentDictionary<string, IPooledHttpClient> clients = new ConcurrentDictionary<string, IPooledHttpClient>();

		public NamedPooledHttpClientProvider(IHttpClientFactory factory, IOptionsMonitor<PooledHttpClientOptions> options, ILoggerFactory loggerFactory)
		{
			this.factory = factory ?? throw new ArgumentNullException(nameof(factory));
			this.options = options ?? throw new ArgumentNullException(nameof(options));
			this.loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
		}

		public IPooledHttpClient GetClient(string name)
		{
			if(string.IsNullOrWhiteSpace(name))
			{
				throw new ArgumentException("name is required", nameof(name));
			}
			return clients.GetOrAdd(name, n =>
			{
				HttpClient httpClient = factory.CreateClient(n);
				PooledHttpClientOptions opts = options.Get(n);
				ILogger<PooledHttpClient> log = loggerFactory.CreateLogger<PooledHttpClient>();
				return new PooledHttpClient(httpClient, Options.Create(opts), log);
			});
		}
	}
}