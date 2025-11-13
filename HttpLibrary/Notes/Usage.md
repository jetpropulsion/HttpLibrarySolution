# Usage

This library provides a reusable, configurable HTTP client foundation and a small CLI that demonstrates common scenarios.

Last updated:2025-11-1100:00:00 (+00:00)

## Quick start (library)

1. Initialize services and obtain named clients

```csharp
using Microsoft.Extensions.DependencyInjection;
using HttpLibrary;
using System.Collections.Concurrent;

ConcurrentDictionary<string, IPooledHttpClient> clients;
ConcurrentDictionary<string, Uri> baseAddresses;
ServiceProvider serviceProvider = ServiceConfiguration.InitializeServices(
 out clients,
 out baseAddresses
);

// Use a named client
IPooledHttpClient client = clients["default"];
```

2. Make requests

```csharp
HttpResponseMessage resp = await client.GetAsync("https://httpbin.org/get", CancellationToken.None);
string body = await resp.Content.ReadAsStringAsync();
```

3. Common features

- Cookie handling: enable per-client using `UseCookies` and optional persistence (`CookiePersistenceEnabled`).
- Alias resolution: use the `alias://` scheme and configure aliases in application configuration to map logical names to base urls.
- Proxy and TLS: configure per-client via JSON configuration (see examples and `clients.json` in CLI project).
- Progress, redirects and metrics: clients expose callbacks and metrics on the `IPooledHttpClient` implementation.

## Configuration

- Primary configuration is loaded from `HttpLibraryConfiguration.json` and optional application-specific JSON (`application.json`, `clients.json`) used by the CLI.
- Typical client settings: `Name`, `Uri`, `UseCookies`, `CookiePersistenceEnabled`, `HttpProxy`, `HttpsProxy`, `DisableSslValidation`, `ClientCertificatePath`.

---

## Configuration files (JSON)

This section explains the JSON files used by the library and CLI, their purpose, expected shapes, and how they participate in building the runtime client set.

Files covered:
- `defaults.json` (often shipped with the CLI as `defaults.json`) — base/default `HttpClientConfig` values used when loading client definitions.
- `clients.json` — client-specific configuration entries. Can be a file with a `Clients` array or a plain array of client objects.
- `application.json` — application-level settings such as `Aliases` used by the `alias://` scheme and other app-specific flags.
- `HttpLibraryConfiguration.json` — library primary configuration used when no application/CLI files are present.
- `cookies.json` — persisted cookie store used by cookie persistence implementation (CLI-level output).

### How files are used together (precedence)

1. `HttpLibraryConfiguration.json` is treated as the library's primary/packaged configuration. It provides baseline behavior and settings used by library consumers when no application-specific files are supplied.
2. `defaults.json` provides a single `HttpClientConfig` instance that acts as the default values for all clients. When `clients.json` is loaded, each client config is merged with `defaults.json` using `HttpClientConfig.PatchFrom` so clients inherit unspecified properties from the defaults.
3. `clients.json` defines zero or more named clients. Each entry is merged with the defaults, validated, and then used to create a named `IPooledHttpClient`.
4. `application.json` contains application-level settings (for example alias mappings). It does not define clients but augments runtime behavior (e.g., `AliasResolutionHandler` uses `ApplicationSettings.Aliases`).
5. `cookies.json` is produced/consumed by cookie persistence. It is not required for client construction but is used when `CookiePersistenceEnabled` is true on a client to reload/save cookies across runs.

When running the CLI, the common pattern is to have all three files present in the working directory: `defaults.json`, `clients.json`, and `application.json`. The library attempts to be tolerant: missing optional files are logged and treated as empty.

### `defaults.json` (shape and purpose)

- Purpose: provide a template `HttpClientConfig` containing default settings to apply to every client defined in `clients.json`.
- Expected shape: a single `HttpClientConfig` JSON object. It must include a `ConfigVersion` property (the loader validates versioning).
- Typical values set here: `TimeoutSeconds`, `UseCookies`, `CookiePersistenceEnabled`, `DefaultRequestHeaders`, `DisableSslValidation`, proxy defaults, etc.

Example `defaults.json`:

```json
{
 "ConfigVersion":1,
 "TimeoutSeconds":60,
 "UseCookies": true,
 "CookiePersistenceEnabled": true,
 "DefaultRequestHeaders": {
 "Accept": "application/json"
 }
}
```

Notes:
- The library uses `ConfigurationLoader.LoadDefaultClientConfig` to deserialize and validate this file using source-generated JSON context for AOT/trimming safety.
- If a property is omitted in a client entry, the value from `defaults.json` (if present) will be used after merging.

### `clients.json` (shape and purpose)

- Purpose: enumerate named HTTP clients the application wants to use. Each client becomes a named entry in the `ConcurrentDictionary<string, IPooledHttpClient>` returned by `ServiceConfiguration.InitializeServices`.
- Accepted shapes:
 - An object with a `Clients` property (the `ClientsFile` shape):
 {
 "Clients": [ { /* HttpClientConfig */ }, { /* ... */ } ]
 }
 - A bare JSON array of `HttpClientConfig` objects. The loader will accept both forms.

Example `clients.json` (array form):

```json
[
 {
 "Name": "default",
 "Uri": "https://httpbin.org",
 "UseCookies": true
 },
 {
 "Name": "api",
 "Uri": "https://api.example.com",
 "DefaultRequestHeaders": {
 "Accept": "application/json"
 }
 }
]
```

Notes:
- The loader (`ConfigurationLoader.LoadClientConfigs`) will attempt source-generated deserialization into `ClientsFile` first and fall back to parsing a JSON array if needed.
- Each client config is merged with the defaults (see above) and then validated. Missing `Name` or `Uri` may be filled from defaults or sensible fallback values.

### `application.json` (shape and purpose)

- Purpose: application-level settings that change runtime behavior but are not per-client settings.
- Common content:
 - `ApplicationSettings.Aliases` — a mapping used by `AliasResolutionHandler` when requests use the `alias://` scheme.
 - Other app-level flags that the CLI or host may use.

Example `application.json`:

```json
{
 "ApplicationSettings": {
 "Aliases": {
 "billing": "https://billing.api.example",
 "search": "https://search.example"
 }
 }
}
```

Notes:
- `AliasResolutionHandler` looks up aliases at runtime from the loaded `ApplicationConfiguration` (populated from `application.json`) and rewrites `alias://{alias}/...` requests to the resolved base URI.
- If an unknown alias is used, the library will throw an `HttpRequestException` when attempting to send the request.

### `cookies.json` (cookie persistence)

- Purpose: persist cookies when clients have `CookiePersistenceEnabled` set to true so cookies survive process restarts.
- Format: The library supports both a legacy flat dictionary format and a newer hierarchical format. Serialization/deserialization uses the source-generated JSON context types to remain trimming-safe.
- The CLI includes a `cookies.json` file next to other configuration files and the `CookiePersistenceImpl` is responsible for loading and saving this file. Failures to persist are best-effort and do not throw during request processing.

### Runtime flow (what the library does)

1. `ServiceConfiguration.InitializeServices` is the entry point used by consumers and the CLI. It loads configuration files (library primary config + optional `defaults.json`, `clients.json`, `application.json`) and composes a set of `HttpClientConfig` instances.
2. `ConfigurationLoader` deserializes files using source-generated contexts and applies merging rules: each client from `clients.json` is patched with `defaults.json` via `PatchFrom`.
3. For each final `HttpClientConfig`, `ServiceConfiguration` registers and constructs an `HttpMessageInvoker` pipeline and a `PooledHttpClient` instance. Options like cookie handling, proxy, SSL validation, and client certificates are applied per-client based on merged config values.
4. `AliasResolutionHandler` (if enabled by presence of `ApplicationSettings.Aliases`) inspects outgoing requests and rewrites `alias://` URIs to resolved absolute URIs using the `application.json` aliases mapping.
5. If `CookiePersistenceEnabled` is true for a client, `CookieCaptureHandler` and `CookiePersistenceImpl` participate to capture `Set-Cookie` headers and persist cookies to `cookies.json`.

### Tips for users

- Keep `defaults.json` small and focused on common settings like timeouts and cookie defaults so individual clients only need to override what differs.
- Use `clients.json` to define logical client names; call those names via the `clients` dictionary returned by `ServiceConfiguration.InitializeServices`.
- Use `application.json` for environment-specific alias mappings and non-client settings. This lets the same `clients.json` work across environments while `application.json` adapts alias targets.
- `clients.json` entries may omit properties present in `defaults.json` — they will be filled during merge.

## CLI

The CLI demonstrates library usage and provides utility commands.

Examples:

```bash
# Simple GET
dotnet run --project HttpLibraryCLI -- GET https://httpbin.org/get

# POST JSON body
dotnet run --project HttpLibraryCLI -- POST https://httpbin.org/post --body '{"name":"john"}'

# Cookies: list saved cookies
dotnet run --project HttpLibraryCLI -- cookies ls

# Prune expired cookies
dotnet run --project HttpLibraryCLI -- cookies prune-all
```

## Files of interest

- `HttpLibrary/ServiceConfiguration.cs` — DI and client bootstrap
- `HttpLibrary/PooledHttpClient.cs` — client implementation and public API
- `HttpLibrary/CookiePersistenceImpl.cs` — cookie persistence implementation
- `HttpLibraryCLI/*` — CLI project and sample configuration files

## Notes

- Library code uses `ConfigureAwait(false)` where appropriate and is designed to be cross-platform and AOT-friendly.
- Cookie persistence is best-effort: failures to persist do not throw during request processing.
- For advanced usage (custom handlers or modified pipeline) register your own `DelegatingHandler` instances through `ServiceConfiguration`.
