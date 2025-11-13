using System;
using System.Collections.Generic;
using System.Text.Json;

namespace HttpLibrary
{
	internal static class ConfigurationLoader
	{
		public static HttpClientConfig[] LoadClientConfigs(string json, string filePath, HttpClientConfig defaults)
		{
			if(string.IsNullOrWhiteSpace(json))
			{
				LoggerBridge.LogWarning("{File}: Empty clients configuration", filePath);
				return Array.Empty<HttpClientConfig>();
			}

			try
			{
				// Try source-gen deserialization into the expected ClientsFile shape
				ClientsFile? clientsFile = null;
				try
				{
					clientsFile = JsonSerializer.Deserialize<ClientsFile>(json, ApplicationJsonContext.Default.ClientsFile);
				}
				catch(JsonException)
				{
					// swallow and try alternative shapes below
				}

				// Fallback: if root is an array of HttpClientConfig, accept that as well
				if(clientsFile == null || clientsFile.Clients == null)
				{
					try
					{
						using JsonDocument doc = JsonDocument.Parse(json);
						if(doc.RootElement.ValueKind == JsonValueKind.Array)
						{
							clientsFile = new ClientsFile();
							foreach(JsonElement el in doc.RootElement.EnumerateArray())
							{
								// Deserialize each element using source-gen context for HttpClientConfig
								try
								{
									HttpClientConfig? cfg = JsonSerializer.Deserialize<HttpClientConfig>(el.GetRawText(), ApplicationJsonContext.Default.HttpClientConfig);
									if(cfg != null)
										clientsFile.Clients.Add(cfg);
								}
								catch(JsonException)
								{
									// ignore malformed element
								}
							}
						}
					}
					catch(JsonException)
					{
						// ignore
					}
				}

				if(clientsFile == null || clientsFile.Clients == null)
				{
					LoggerBridge.LogWarning("{File}: No client configurations found", filePath);
					return Array.Empty<HttpClientConfig>();
				}

				List<HttpClientConfig> result = new List<HttpClientConfig>();
				foreach(HttpClientConfig cfg in clientsFile.Clients)
				{
					// Merge defaults with client-specific values so DefaultRequestHeaders and other defaults are applied
					HttpClientConfig merged = defaults.PatchFrom(cfg);

					// Ensure required fields are present (fall back to defaults or sane defaults)
					if(string.IsNullOrWhiteSpace(merged.Name))
						merged.Name = cfg.Name ?? defaults.Name ?? "default";
					if(string.IsNullOrWhiteSpace(merged.Uri))
						merged.Uri = cfg.Uri ?? defaults.Uri ?? Constants.DefaultClientBaseUri;
					if(merged.Timeout == default)
						merged.Timeout = defaults.Timeout == default ? TimeSpan.FromMinutes(1) : defaults.Timeout;

					result.Add(merged);
				}

				LoggerBridge.LogInformation("{File}: Successfully loaded {Count} client configuration(s)", filePath, result.Count);
				return result.ToArray();
			}
			catch(JsonException ex)
			{
				LoggerBridge.LogError(ex, "{File}: Failed to deserialize clients configuration", filePath);
				return Array.Empty<HttpClientConfig>();
			}
			catch(Exception ex)
			{
				LoggerBridge.LogError(ex, "{File}: Failed to load clients configuration", filePath);
				return Array.Empty<HttpClientConfig>();
			}
		}

		public static HttpClientConfig LoadDefaultClientConfig(string json, string filePath)
		{
			if(string.IsNullOrWhiteSpace(json))
			{
				LoggerBridge.LogWarning("{File}: Defaults configuration is empty", filePath);
				return new HttpClientConfig();
			}

			try
			{
				HttpClientConfig? def = JsonSerializer.Deserialize<HttpClientConfig>(json, ApplicationJsonContext.Default.HttpClientConfig);
				if(def == null)
				{
					LoggerBridge.LogWarning("{File}: Defaults configuration deserialized to null", filePath);
					return new HttpClientConfig();
				}

				LoggerBridge.LogInformation("{File}: Loaded defaults configuration", filePath);
				return def;
			}
			catch(JsonException ex)
			{
				LoggerBridge.LogError(ex, "{File}: Invalid defaults configuration format", filePath);
				return new HttpClientConfig();
			}
			catch(Exception ex)
			{
				LoggerBridge.LogError(ex, "{File}: Failed to load defaults configuration", filePath);
				return new HttpClientConfig();
			}
		}
	}
}