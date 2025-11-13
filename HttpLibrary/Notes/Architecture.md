# Architecture

This document outlines the high-level architecture of the HttpLibrary project, its main components, and extension points.

Last updated:2025-11-1100:00:00 (+00:00)

## Goals

- Provide a configurable, reusable HTTP client abstraction for applications and CLI tools.
- Support named clients with independent configuration (base address, proxy, TLS, cookies).
- Expose metrics, progress callbacks, and redirect policies for observability and control.
- Keep the core library AOT-friendly and cross-platform.

## Major Components

- `ServiceConfiguration` — bootstraps Dependency Injection, loads configuration files, and registers named `IPooledHttpClient` instances.
- `PooledHttpClient` — the main client implementation implementing `IPooledHttpClient` providing convenience methods (`GetAsync`, `PostAsync`, `GetBytesAsync`, metrics, callbacks).
- `CookiePersistenceImpl` — persisting cookies to a JSON file and providing runtime `CookieContainer` instances.
- `DelegatingHandler` chain — request/response pipeline that composes functionality via handlers:
 - `AliasResolutionHandler` — resolves `alias://` URIs to concrete URIs.
 - `CookieCaptureHandler` — captures `Set-Cookie` headers and forwards them to `ICookiePersistence`.
 - `LoggingHandler` — logs request/response events and integrates with `LoggerBridge`.
 - Other handlers registered via `AliasMessageHandlerBuilderFilter`.
- `HttpRequestExecutor` — responsible for sending requests and performing retries, redirect handling and progress reporting.

## Request pipeline

1. Caller invokes a high-level method on `PooledHttpClient`.
2. `PooledHttpClient` constructs an `HttpRequestMessage` and sends it through an `HttpClient` instance configured with the handler pipeline.
3. Handlers are executed in order; each `DelegatingHandler` may inspect or rewrite the request and/or response.
4. `CookieCaptureHandler` inspects `Set-Cookie` headers on responses and delegates persistence to `ICookiePersistence`.
5. Responses are returned to the caller with metrics and callbacks populated.

## Cookie persistence

- Cookie persistence is implemented by `CookiePersistenceImpl` which serializes cookies to a JSON file (`cookies.json`) and restores them at startup.
- Runtime `CookieContainer` objects are registered per-client so cookies are available for immediate subsequent requests.
- `CookieCaptureHandler` performs best-effort parsing of `Set-Cookie` headers and calls `ICookiePersistence.AddCookieFromHeader`.
- Persistence operations are designed to be resilient: failures during header processing are suppressed to avoid impacting normal request flow.

## Extension points

- Register additional `DelegatingHandler` instances in `ServiceConfiguration` to add cross-cutting behavior (metrics, custom auth, header injection).
- Replace or extend `ICookiePersistence` to provide alternative storage (encrypted store, platform-specific keystore).
- Provide custom `HttpMessageHandler` implementations for specialized transport (e.g., platform-specific sockets, testing harness).

## Testing and Diagnostics

- Unit tests exercise handlers and `ServiceConfiguration` using the `HttpLibraryTests` project.
- `HttpHandlerDiagnosticSubscriber` emits structured events for diagnostics and integration with logging backends.

## Design considerations

- Avoids reflection and dynamic code paths to remain AOT and trimming friendly.
- Uses `ConfigureAwait(false)` in library code to prevent synchronization-context captures.
- Emphasizes best-effort, non-throwing behavior for non-critical operations (cookie persistence, metrics reporting) to avoid surprising callers.

## File map (primary)

- `HttpLibrary/ServiceConfiguration.cs`
- `HttpLibrary/PooledHttpClient.cs`
- `HttpLibrary/CookiePersistenceImpl.cs`
- `HttpLibrary/Handlers/*.cs` (various delegating handlers)
- `HttpLibrary/HttpRequestExecutor.cs`
