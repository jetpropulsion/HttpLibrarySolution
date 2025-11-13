# Fetch Tests

## Status
Test project using MSTest framework.

## Test Framework
MSTest (Microsoft.VisualStudio.TestTools.UnitTesting)

## Running Tests
```bash
dotnet test Tests/Fetch.Tests.csproj
```

## Test Coverage
The test project includes:
- **CookiePersistenceTests.cs**: 11 tests for cookie loading, saving, persistence, expiration, and client management
- **PooledHttpClientTests.cs**: 17 tests for HTTP client pooling, metrics tracking, configuration, and named client providers

## Test Structure
- `[TestClass]` - Marks a class containing tests
- `[TestMethod]` - Marks individual test methods
- `[TestInitialize]` - Runs before each test
- `[TestCleanup]` - Runs after each test

## Assertions
MSTest uses:
- `Assert.AreEqual()` - Value equality
- `Assert.AreSame()` - Reference equality
- `Assert.IsTrue()` / `Assert.IsFalse()` - Boolean assertions
- `Assert.IsNull()` / `Assert.IsNotNull()` - Null checks
- `Assert.ThrowsException<T>()` - Exception testing
- `CollectionAssert.Contains()` - Collection membership
