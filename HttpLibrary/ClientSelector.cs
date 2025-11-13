using Microsoft.Extensions.Logging;

using System;
using System.Collections.Concurrent;
using System.Linq;

namespace HttpLibrary
{
	/// <summary>
	/// Provides logic for selecting the most appropriate HTTP client for a given URL
	/// based on domain matching and scoring algorithms, as well as client alias resolution.
	/// </summary>
	public static class ClientSelector
	{
		/// <summary>
		/// Selects the most appropriate pooled HTTP client for the given URL based on domain matching.
		/// </summary>
		/// <param name="url">The target URL to fetch</param>
		/// <param name="registeredClients">Dictionary of all registered clients</param>
		/// <param name="clientBaseAddresses">Dictionary mapping client names to their base addresses</param>
		/// <param name="logger">Logger instance</param>
		/// <returns>The selected client, or null if no suitable client found</returns>
		public static IPooledHttpClient? SelectClientForUrl(string url, ConcurrentDictionary<string, IPooledHttpClient> registeredClients, ConcurrentDictionary<string, Uri> clientBaseAddresses, ILogger logger)
		{
			if(!Uri.TryCreate(url, UriKind.Absolute, out Uri? targetUri))
			{
				logger.LogWarning("Invalid URL '{Url}' - falling back to default client", url);
				registeredClients.TryGetValue("default", out IPooledHttpClient? defaultClient);
				return defaultClient;
			}

			string targetHost = targetUri.Host.ToLowerInvariant();
			string targetScheme = targetUri.Scheme.ToLowerInvariant();
			int targetPort = targetUri.Port;

			logger.LogDebug("Selecting client for URL: {Url} (Host: {Host}, Scheme: {Scheme}, Port: {Port})", url, targetHost, targetScheme, targetPort);

			// Track best match
			IPooledHttpClient? bestMatch = null;
			int bestMatchScore = 0;
			string? bestMatchClientName = null;

			foreach(System.Collections.Generic.KeyValuePair<string, IPooledHttpClient> kvp in registeredClients)
			{
				string clientName = kvp.Key;
				IPooledHttpClient client = kvp.Value;

				// Skip if no base address configured for this client
				if(!clientBaseAddresses.TryGetValue(clientName, out Uri? clientBaseUri))
				{
					continue;
				}

				string clientHost = clientBaseUri.Host.ToLowerInvariant();
				string clientScheme = clientBaseUri.Scheme.ToLowerInvariant();
				int clientPort = clientBaseUri.Port;

				// Calculate match score
				int score = CalculateMatchScore(targetHost, targetScheme, targetPort, clientHost, clientScheme, clientPort);

				if(score > bestMatchScore)
				{
					bestMatchScore = score;
					bestMatch = client;
					bestMatchClientName = clientName;
				}

				logger.LogDebug("Client '{ClientName}' (BaseAddress: {BaseAddress}) score: {Score}", clientName, clientBaseUri, score);
			}

			// If we found a good match (score >0), use it
			if(bestMatch != null && bestMatchScore > 0)
			{
				logger.LogInformation("Selected pooled client '{ClientName}' for URL '{Url}' (match score: {Score})", bestMatchClientName, url, bestMatchScore);
				return bestMatch;
			}

			// Fall back to default client
			if(registeredClients.TryGetValue("default", out IPooledHttpClient? fallbackClient))
			{
				logger.LogInformation("No specific client match found - using default client for URL '{Url}'", url);
				return fallbackClient;
			}

			// No default client available - use any client
			if(registeredClients.Count > 0)
			{
				IPooledHttpClient anyClient = registeredClients.Values.First();
				string anyClientName = registeredClients.Keys.First();
				logger.LogWarning("No default client available - using '{ClientName}' for URL '{Url}'", anyClientName, url);
				return anyClient;
			}

			logger.LogError("No clients registered - cannot process URL '{Url}'", url);
			return null;
		}

		/// <summary>
		/// Resolves a client alias URL to a full URL by looking up the client name and combining with the base address.
		/// </summary>
		/// <param name="aliasUrl">Client alias with optional path, query string, and fragment (e.g., "google/search?q=test")</param>
		/// <param name="clientBaseAddresses">Dictionary mapping client names to their base addresses</param>
		/// <param name="resolvedUrl">The resolved full URL if successful</param>
		/// <param name="clientName">The extracted client name</param>
		/// <returns>True if the alias was successfully resolved, false if the client was not found</returns>
		public static bool TryResolveClientAlias(string aliasUrl, ConcurrentDictionary<string, Uri> clientBaseAddresses, out string resolvedUrl, out string clientName)
		{
			resolvedUrl = string.Empty;
			clientName = string.Empty;

			if(string.IsNullOrWhiteSpace(aliasUrl))
			{
				return false;
			}

			// Parse the alias URL to extract client name and remaining parts
			string pathQueryFragment = string.Empty;

			// Find the first separator (/, ?, #)
			int separatorIndex = aliasUrl.IndexOfAny(new[] { '/', '?', '#' });

			if(separatorIndex >= 0)
			{
				// If separator at position0, alias is invalid
				if(separatorIndex == 0)
				{
					return false;
				}

				clientName = aliasUrl.Substring(0, separatorIndex);
				pathQueryFragment = aliasUrl.Substring(separatorIndex);
			}
			else
			{
				clientName = aliasUrl;
			}

			// Normalize clientName to the canonical key (do not change case here; lookup is case-insensitive if dictionary uses such comparer)
			if(string.IsNullOrWhiteSpace(clientName))
			{
				return false;
			}

			// Check if client exists
			if(!clientBaseAddresses.TryGetValue(clientName, out Uri? baseAddress))
			{
				// Not found - leave clientName as extracted for diagnostics, return false
				resolvedUrl = string.Empty;
				return false;
			}

			// Combine base address with path/query/fragment
			resolvedUrl = CombineUrlParts(baseAddress, pathQueryFragment);
			return true;
		}

		/// <summary>
		/// Combines a base URI with path, query string, and fragment parts.
		/// </summary>
		/// <param name="baseUri">Base URI from client configuration</param>
		/// <param name="pathQueryFragment">Path, query string, and/or fragment to append</param>
		/// <returns>Combined absolute URL</returns>
		public static string CombineUrlParts(Uri baseUri, string pathQueryFragment)
		{
			if(string.IsNullOrEmpty(pathQueryFragment))
			{
				return baseUri.ToString();
			}

			// If pathQueryFragment starts with /, it's a path replacement
			// Otherwise, append to existing path
			if(pathQueryFragment.StartsWith("/", StringComparison.Ordinal))
			{
				// Build absolute URL directly to preserve percent-encoding exactly as provided
				string authority = baseUri.GetLeftPart(UriPartial.Authority);
				return authority + pathQueryFragment;
			}
			else
			{
				// Append to existing path
				Uri combined = new Uri(baseUri, pathQueryFragment);
				return combined.ToString();
			}
		}

		/// <summary>
		/// Calculates a match score between a target URL and a client base address.
		/// Higher scores indicate better matches.
		/// </summary>
		/// <param name="targetHost">Target URL host</param>
		/// <param name="targetScheme">Target URL scheme (http/https)</param>
		/// <param name="targetPort">Target URL port</param>
		/// <param name="clientHost">Client base address host</param>
		/// <param name="clientScheme">Client base address scheme</param>
		/// <param name="clientPort">Client base address port</param>
		/// <returns>
		/// Score breakdown:
		/// -1000: Exact host match (case-insensitive)
		/// -500: Subdomain match (e.g., www.example.com matches example.com)
		/// -100: Scheme match (http/https)
		/// -50: Port match
		/// -0: No match
		/// </returns>
		public static int CalculateMatchScore(string targetHost, string targetScheme, int targetPort, string clientHost, string clientScheme, int clientPort)
		{
			int score = 0;

			// Exact host match
			if(string.Equals(targetHost, clientHost, StringComparison.OrdinalIgnoreCase))
			{
				score += 1000;
			}
			// Subdomain match: check if target is subdomain of client host or vice versa
			else if(IsSubdomainMatch(targetHost, clientHost))
			{
				score += 500;
			}
			else
			{
				// No host match - return 0;
				return 0;
			}

			// Scheme match
			if(string.Equals(targetScheme, clientScheme, StringComparison.OrdinalIgnoreCase))
			{
				score += 100;
			}

			// Port match
			if(targetPort == clientPort)
			{
				score += 50;
			}

			return score;
		}

		/// <summary>
		/// Checks if two hosts have a subdomain relationship.
		/// </summary>
		/// <param name="host1">First host to compare</param>
		/// <param name="host2">Second host to compare</param>
		/// <returns>True if hosts have a subdomain relationship, false otherwise</returns>
		/// <example>
		/// Examples:
		/// - "www.example.com" matches ".example.com" or "example.com"
		/// - "api.github.com" matches ".github.com" or "github.com"
		/// - "x.com" matches "x.com"
		/// </example>
		public static bool IsSubdomainMatch(string host1, string host2)
		{
			// Normalize: remove leading dots
			string h1 = host1.TrimStart('.');
			string h2 = host2.TrimStart('.');

			// Check if h1 endsWith h2 (or vice versa)
			if(h1.EndsWith("." + h2, StringComparison.OrdinalIgnoreCase) || h2.EndsWith("." + h1, StringComparison.OrdinalIgnoreCase))
			{
				return true;
			}

			// Check if one is a subdomain of the other by comparing domain parts
			if(h1.Contains('.') && h2.Contains('.'))
			{
				string[] parts1 = h1.Split('.');
				string[] parts2 = h2.Split('.');

				// Extract base domain (last2 parts, e.g., "example.com")
				if(parts1.Length >= 2 && parts2.Length >= 2)
				{
					string baseDomain1 = string.Join(".", parts1[ ^2 ], parts1[ ^1 ]);
					string baseDomain2 = string.Join(".", parts2[ ^2 ], parts2[ ^1 ]);

					if(string.Equals(baseDomain1, baseDomain2, StringComparison.OrdinalIgnoreCase))
					{
						return true;
					}
				}
			}

			return false;
		}
	}
}