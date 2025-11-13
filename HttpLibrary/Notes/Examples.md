# HttpLibrary Examples

**Version**: 2.0  
**Last Updated**: 2025-11-11 00:00:00 (+00:00)  
**Status**: [OK] Current

---

## Table of Contents

1. [Basic HTTP Requests](#basic-http-requests)
2. [Authentication](#authentication)
3. [Cookie Management](#cookie-management)
4. [Proxy Configuration](#proxy-configuration)
5. [Client Certificates](#client-certificates)
6. [Advanced Features](#advanced-features)
7. [CLI Examples](#cli-examples)
8. [Alias Scheme (alias://) Usage](#alias-scheme-alias-usage)
9. [Additional Code Samples](#additional-code-samples)

---

## Basic HTTP Requests

### Example 1: Simple GET Request

```csharp
using HttpLibrary;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.Concurrent;

// Initialize services
ConcurrentDictionary<string, IPooledHttpClient> clients;
ConcurrentDictionary<string, Uri> baseAddresses;
ServiceProvider serviceProvider = ServiceConfiguration.InitializeServices(
    out clients,
    out baseAddresses
);

// Get default client
IPooledHttpClient client = clients["default"];

// Make GET request
HttpResponseMessage response = await client.GetAsync(
    "https://httpbin.org/get",
  CancellationToken.None
);

if(response.IsSuccessStatusCode)
{
    string content = await response.Content.ReadAsStringAsync();
    Console.WriteLine(content);
}
```

### Example 2: GET with Query Parameters

```csharp
string baseUrl = "https://api.example.com/search";
string query = "?q=dotnet&limit=10&offset=0";
string url = baseUrl + query;

string jsonResponse = await client.GetStringAsync(url, CancellationToken.None);
Console.WriteLine(jsonResponse);
```

### Example 3: POST JSON Data

```csharp
using System.Text;
using System.Text.Json;

// Create JSON object
object data = new
{
  name = "John Doe",
    email = "john@example.com",
age = 30
};

string jsonString = JsonSerializer.Serialize(data);

// Create HTTP content
StringContent jsonContent = new StringContent(
    jsonString,
    Encoding.UTF8,
    "application/json"
);

// POST request
HttpResponseMessage response = await client.PostAsync(
    "https://httpbin.org/post",
    jsonContent,
    CancellationToken.None
);

string responseJson = await response.Content.ReadAsStringAsync();
Console.WriteLine(responseJson);
```

---

## Authentication

### Example 4: Bearer Token Authentication

**Configuration (clients.json):**
```json
{
  "Name": "authenticated-api",
  "Uri": "https://api.example.com",
  "DefaultRequestHeaders": {
    "Authorization": "Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
    "Accept": "application/json"
  }
}
```

**Code:**
```csharp
IPooledHttpClient apiClient = clients["authenticated-api"];

HttpResponseMessage response = await apiClient.GetAsync(
    "https://api.example.com/protected/resource",
    CancellationToken.None
);
```

---

## Cookie Management

### Example 5: Automatic Cookie Handling

**Configuration:**
```json
{
  "Name": "cookie-client",
  "Uri": "https://example.com",
  "UseCookies": true,
  "CookiePersistenceEnabled": true
}
```

**Code:**
```csharp
IPooledHttpClient cookieClient = clients["cookie-client"];

// First request - server sets cookies
HttpResponseMessage loginResponse = await cookieClient.PostAsync(
    "https://example.com/login",
    new StringContent("{\"username\":\"user\",\"password\":\"pass\"}", Encoding.UTF8, "application/json"),
    CancellationToken.None
);

// Second request - cookies automatically sent
HttpResponseMessage profileResponse = await cookieClient.GetAsync(
  "https://example.com/profile",
    CancellationToken.None
);

// Cookies are automatically saved to cookies.json by the persistence implementation
```

Notes:
- The library attempts to parse `Set-Cookie` response headers and persist cookies to the configured cookie file when `CookiePersistenceEnabled` is true.
- The `CookieCaptureHandler` is responsible for extracting `Set-Cookie` headers and forwarding them to `ICookiePersistence`.

---

## Proxy Configuration

### Example 6: Corporate Proxy with Authentication

**Configuration:**
```json
{
  "Name": "corporate-proxy",
  "Uri": "https://external-api.com",
  "UseProxy": true,
  "HttpProxy": "http://proxy.company.com:8080",
  "HttpsProxy": "http://proxy.company.com:8080",
  "ProxyUsername": "proxyuser",
  "ProxyPassword": "proxypassword",
  "ProxyBypassList": ["localhost", "*.internal.company.com", "192.168.*"]
}
```

---

## Client Certificates

### Example 7: Mutual TLS (mTLS)

**Configuration:**
```json
{
  "Name": "mtls-client",
  "Uri": "https://secure.api.example.com",
  "ClientCertificatePath": "certificates/client.pfx",
  "ClientCertificatePassword": "cert-password-here",
  "DisableSslValidation": false
}
```

**Code:**
```csharp
IPooledHttpClient mtlsClient = clients["mtls-client"];

HttpResponseMessage response = await mtlsClient.GetAsync(
    "https://secure.api.example.com/protected",
    CancellationToken.None
);
```

---

## Advanced Features

### Example 8: Redirect Handling

```csharp
// Setup redirect callback
client.RedirectCallback = (redirectInfo) =>
{
    Console.WriteLine($"Redirect: {redirectInfo.StatusCode} -> {redirectInfo.RedirectUrl}");
  return RedirectAction.Follow;
};

HttpResponseMessage response = await client.GetAsync(
    "https://httpbin.org/redirect/3",
    CancellationToken.None
);
```

### Example 9: Progress Tracking

```csharp
// Setup progress callback
client.ProgressCallback = (progressInfo) =>
{
    Console.Write($"\r{progressInfo.Stage}: {progressInfo.ProgressPercentage:F1}%");
};

// Download with progress
byte[] data = await client.GetBytesAsync(
    "https://httpbin.org/bytes/10485760",
    CancellationToken.None
);
```

### Example 10: Client Metrics

```csharp
PooledHttpClientMetrics metrics = client.Metrics;

Console.WriteLine($"Total Requests: {metrics.TotalRequests}");
Console.WriteLine($"Successful: {metrics.SuccessfulRequests}");
Console.WriteLine($"Failed: {metrics.FailedRequests}");
Console.WriteLine($"Average Time: {metrics.AverageRequestMs:F2}ms");
```

---

## CLI Examples

### Example 11: Basic CLI Usage

```bash
# GET request
dotnet run --project HttpLibraryCLI -- GET https://httpbin.org/get

# POST JSON data
dotnet run --project HttpLibraryCLI -- POST https://httpbin.org/post --body '{"name":"John"}'

# List cookies
dotnet run --project HttpLibraryCLI -- cookies ls

# Prune expired cookies
dotnet run --project HttpLibraryCLI -- cookies prune-all
```

---

## Alias Scheme (alias://) Usage

The library supports an optional `alias://` URI scheme that allows callers to reference a logical alias instead of a full absolute URL. When the `alias://` scheme is used the `AliasResolutionHandler` will resolve the alias to a configured base URL and rewrite the request to the final absolute URI before sending. This is opt-in and only applies when the request URI's scheme is exactly `alias`.

Example usage (code):

```csharp
// Given application.json contains an Aliases mapping (see configuration example below)
// Initialize services as usual
ConcurrentDictionary<string, IPooledHttpClient> clients;
ConcurrentDictionary<string, Uri> baseAddresses;
ServiceProvider sp = ServiceConfiguration.InitializeServices(out clients, out baseAddresses);

// Use alias:// scheme for requests
IPooledHttpClient client = clients["default"]; // named clients remain available
HttpResponseMessage resp = await client.GetAsync("alias://billing/v1/invoices/123", CancellationToken.None);
```

Sample `application.json` fragment to enable alias resolution:

```json
{
 "ApplicationSettings": {
 "Aliases": {
 "billing": "https://billing.api.example",
 "google": "https://google.com"
 }
 }
}
```

Notes:
- The alias name is the authority portion of the alias URI: `alias://{alias}/{path}`.
- Path and query from the alias URI are preserved and combined with the configured base URL.
- If no `Aliases` mapping is present, the `AliasResolutionHandler` is a no-op and the library behavior is unchanged.
- Unknown aliases result in an `HttpRequestException` when the request is sent.

---

## Additional Code Samples

### Deserialize JSON to a typed object

```csharp
using System.Text.Json;

public class UserProfile
{
 public string Id { get; set; }
 public string Name { get; set; }
 public string Email { get; set; }
}

// Request and deserialize
HttpResponseMessage resp = await client.GetAsync("https://api.example.com/users/123", CancellationToken.None);
resp.EnsureSuccessStatusCode();
string json = await resp.Content.ReadAsStringAsync();
UserProfile profile = JsonSerializer.Deserialize<UserProfile>(json)!;
Console.WriteLine(profile.Name);
```

### Post object and get typed response

```csharp
UserProfile newUser = new UserProfile { Name = "Alice", Email = "alice@example.com" };
string payload = JsonSerializer.Serialize(newUser);
StringContent content = new StringContent(payload, System.Text.Encoding.UTF8, "application/json");

HttpResponseMessage createResp = await client.PostAsync("https://api.example.com/users", content, CancellationToken.None);
createResp.EnsureSuccessStatusCode();
string createdJson = await createResp.Content.ReadAsStringAsync();
UserProfile created = JsonSerializer.Deserialize<UserProfile>(createdJson)!;
```

### Use CancellationToken to cancel long requests

```csharp
using System.Threading;

CancellationTokenSource cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
try
{
 HttpResponseMessage longResp = await client.GetAsync("https://httpbin.org/delay/30", cts.Token);
}
catch (OperationCanceledException)
{
 Console.WriteLine("Request was canceled");
}
```

### Implement a simple retry loop (client-level)

```csharp
public static async Task<HttpResponseMessage> GetWithRetriesAsync(IPooledHttpClient client, string uri, int maxRetries, CancellationToken ct)
{
 int attempt =0;
 while(true)
 {
 attempt++;
 try
 {
 HttpResponseMessage r = await client.GetAsync(uri, ct);
 if(r.IsSuccessStatusCode)
 {
 return r;
 }
 if(attempt >= maxRetries)
 {
 return r; // give up and return last response
 }
 }
 catch (HttpRequestException) when (attempt < maxRetries)
 {
 // brief backoff
 await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, attempt)), ct).ConfigureAwait(false);
 }
 }
}
```

### Registering a custom DelegatingHandler in DI

```csharp
public sealed class TimingHandler : DelegatingHandler
{
 protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
 {
 System.Diagnostics.Stopwatch sw = System.Diagnostics.Stopwatch.StartNew();
 HttpResponseMessage resp = await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
 sw.Stop();
 Console.WriteLine($"Request to {request.RequestUri} took {sw.ElapsedMilliseconds}ms");
 return resp;
 }
}

// Register in ServiceConfiguration or DI composition
// ServiceConfiguration.InitializeServices has an extension point for additional handlers; when composing manually:
ServiceCollection services = new ServiceCollection();
services.AddSingleton<TimingHandler>();
// When creating HttpClientFactory pipeline ensure TimingHandler is added to the client handlers chain
```

### Adding custom headers per request

```csharp
HttpRequestMessage req = new HttpRequestMessage(HttpMethod.Get, "https://api.example.com/data");
req.Headers.Add("X-Correlation-Id", Guid.NewGuid().ToString());
HttpResponseMessage r = await client.SendAsync(req, CancellationToken.None);
```

---

**Last Updated**: 2025-11-11 00:00:00 (+00:00)  
**Version**: 2.0  
**Status**: [OK] Current
