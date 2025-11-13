# Copilot Instructions

## Revision time 2025-01-05 19:15 (+02:00)

## General Coding Standards

Always check if there are better alternatives available for any prompt (for example, if there are new APIs available in .NET that can be used instead of older APIs).
If there are multiple choices available or different implementations available, always ask how to proceed, listing available choices numbered and formatted as "##### 1 #####" for first choice and so on.
When presenting different choices explain benefits and pitfalls, pros and contras, efficiency, risk and whether the code needs to be additionally reviewed.

### Build and Test Requirements

**Code changes** are any changes to files that are part of the project (including .cs, .csproj, .json config files, .editorconfig, etc.), excluding documentation-only changes to .md files.

Automatically execute the following commands without asking for confirmation:
- Always do "dotnet clean" and "dotnet build" before committing **code changes** to ensure there are no build errors or warnings.
- Always do "dotnet format" before committing code to ensure code is properly formatted according to .editorconfig settings.
- Always do "dotnet test" before committing **code changes** to ensure all tests pass successfully.

Always write unit tests for changes to business logic, algorithms, and critical functionality flows. UI-only changes and formatting adjustments may not require tests.

### Platform and Compatibility

Don't use reflection, this implementation must always be AOT compatible and prepared for aggressive trimming.
Implement everything to be multi-platform compatible (Windows, Linux, MacOS).

### File Organization

When creating new files, organize them as follows:
- **Interfaces**: Save in `Interfaces/` subdirectory (e.g., `Interfaces/IMyInterface.cs`)
- **Enums**: Save in `Enums/` subdirectory (e.g., `Enums/MyEnum.cs`)
- **Helpers**: Save in `Helpers/` subdirectory (e.g., `Helpers/StringHelper.cs`)
- **Extensions**: Save in `Extensions/` subdirectory (e.g., `Extensions/StringExtensions.cs`)
- **Models**: Save in `Models/` subdirectory (if not domain-specific)

Use separate files for each type:
- One interface per file (e.g., `IMyInterface.cs`)
- One enum per file (e.g., `MyEnum.cs`)
- One struct per file (e.g., `MyStruct.cs`)
- One class per file (e.g., `MyClass.cs`)

### File Management

When date time stamp is required to be generated in the code or accompanying code (.md files), please use system function to get current time with time zone, and always use format YYYY-MM-DD HH:mm:ss (+/- UTC offset), example: "2025-01-04 18:42:00 (+02:00)"
When you are moving files or complete projects always make physical backup (name of the folder plus ".backup")

Remove all empty lines at the end of files.
Remove all whitespace at the end of lines.
Use UTF-8 encoding for all files.
If file is empty, remove it.

### Coding Principles

If available for particular task, always follow strict .NET best practices and guidelines provided by Microsoft, and industry standards like RFC documents, IEEE standards, OWASP guidelines and so on.
Always prefer "Single Source of Truth" principle, avoid duplicating code or logic in multiple places, instead create reusable methods or classes to encapsulate common functionality.

### Async/Await Best Practices

If caller has CancellationToken parameter, always pass it to all async methods called within the method.
When creating async methods, always use the "Async" suffix in the method name (for example, use "GetDataAsync" instead of "GetData").
Use `ConfigureAwait(false)` in library code to avoid capturing synchronization context.
Prefer `Task<T>` for most scenarios; use `ValueTask<T>` only for high-performance scenarios where allocations matter.
Never use `async void` except for event handlers.

### Exception Handling

Never hide exception in so-called "ninja-catch", example: try { val = inputVal; } catch() {}, instead log the exception and re-throw it.
Never hide warnings either inline, or project/solution wise, hiding warnings is not a way to solve anything.
Handle edge cases and write clear exception handling with meaningful error messages.

### Type Usage

Always use the full type name instead of var, example: use "Guid guid = Guid.NewGuid()" instead of "var guid = Guid.NewGuid()"

### Code Formatting

**Control Flow Statements**: Always use curly brackets for all control flow statements (if, for, while, foreach, using, try), even if they contain a single statement.

**Switch Statements**: Always use curly brackets around each case block in switch statements (excluding switch expressions). Place break statements inside the curly brackets.

**Expression-Bodied Members**: Use expression-bodied members (`=>`) for properties and methods that can be expressed in a single line. This is an **exception** to the curly bracket rule.

Examples:
```csharp
// ✅ Control flow: Always use curly brackets
if(condition)
{
    DoSomething();
}

// ✅ Switch statement: Braces around each case block
switch (value)
{
    case 1:
    {
      DoSomething();
        break;
    }
    case 2:
    {
        DoAnotherThing();
        break;
    }
    default:
    {
DoDefault();
        break;
    }
}

// ✅ Switch expression: No braces needed (simple mapping)
string result = value switch
{
    1 => "one",
    2 => "two",
    _ => "other"
};

// ✅ Expression bodies: No curly brackets needed
public int GetValue() => 42;
public string Name => _name;

// ✅ Multi-line methods: Use curly brackets
public void ComplexMethod()
{
    DoFirst();
    DoSecond();
}
```

Always separate names of variables or keywords from operators with a single space on each side, example: use "int sum = a + b;" instead of "int sum=a+b;", use "return 0" instead of "return0" and so on.

### Null Handling

Always use is null or is not null instead of == null or != null.
for null checks, never use this: if(variable != null && variable.Count > 0) { ... }, instead use coalescing with comparison: if((variable?.Count ?? 0) > 0) { ... }
for null checks, never use this: if(variable == null) { throw new ArgumentNullException(..., nameof(variable), ...) }, instead use instead  ArgumentNullException.ThrowIfNull(..., nameof(variable), ...)
Trust the C# null annotations and don't add null checks when the type system says a value cannot be null.

## Naming Conventions

Follow PascalCase for:
- Component names
- Class names
- Method names
- Public members
- Public static fields

Follow camelCase for:
- Local variables (e.g., `userName`, `totalCount`)
- Method parameters (e.g., `userId`, `configPath`)

Use underscore prefix + camelCase (`_camelCase`) for:
- Private instance fields (e.g., `_userName`, `_totalCount`)
- Private static fields (e.g., `_defaultValue`, `_instanceCount`)

Prefix interface names with "I" (e.g., `IUserService`, `IRepository`).

Never use prefixes like "s_variableName" or "m_variableName" - use `_camelCase` for all private fields only.

Example:
```csharp
public class UserService
{
    private readonly string _userName;  // ✅ Private instance field
    private static readonly int _maxRetries;  // ✅ Private static field
    public static int DefaultTimeout;  // ✅ Public static field

    public UserService(string userName)  // ✅ Parameter
 {
        _userName = userName;
        int retryCount = 0;  // ✅ Local variable
}
}
```

### Naming Conventions for Classes and Members

When creating classes, always use singular names unless the class represents a collection or a group of items (for example, use "Customer" instead of "Customers" for a class representing a single customer).

When creating methods, always use verbs or verb phrases to describe the action performed by the method (for example, use "CalculateTotal" instead of "TotalCalculator").

When creating properties, always use nouns or noun phrases to describe the data being represented (for example, use "FirstName" instead of "GetFirstName").

When creating events, always use the past tense of verbs to describe the action that has occurred (for example, use "DataLoaded" instead of "LoadData").

When creating new classes, always consider if they should be sealed to prevent inheritance unless there is a specific need for extensibility.

### Properties and Options Naming

Always include units in the name so only reading variable name will tell the user what unit the amount is represented in, when creating properties/options.

Examples:
```csharp
// ✅ Good: Unit is clear from name
public int TimeoutMilliseconds { get; set; }
public long FileSizeBytes { get; set; }
public double DistanceMeters { get; set; }

// ❌ Bad: Unit is unclear
public int Timeout { get; set; }
public long FileSize { get; set; }
public double Distance { get; set; }
```

## Formatting

Apply code-formatting style defined in .editorconfig.
Prefer file-scoped namespace declarations and single-line using directives.

**Newline before opening brace**: Insert a newline before the opening curly brace for:
- Class definitions
- Struct definitions
- Interface definitions
- Enum definitions
- Method definitions
- Constructor definitions
- Property definitions (non-expression-bodied)
- Control flow statements (if, for, while, foreach, switch, using, try)
- Switch statements
- Lambda expressions (multi-line)
- Object initializers (multi-line)
- Collection initializers (multi-line)

**Exception**: File-scoped namespaces don't use braces at all:
```csharp
namespace HttpLibraryCLI;  // ✅ File-scoped namespace (C# 10+)

public class MyClass  // Newline before brace
{
    // ...
}
```

Ensure that the final return statement of a method is on its own line.
If you need to break a long statement into multiple lines, do so at logical points (e.g., after commas, operators) and indent subsequent lines for clarity don't leave open bracket or brace on the same line, add newline before it.

**Pattern Matching and Switch Expressions**: Use pattern matching and switch expressions wherever possible.
- Prefer **switch expressions** for simple value mappings
- Use **switch statements** when you need complex logic, multiple statements, or side effects per case

Use nameof instead of string literals when referring to member names.

## Documentation Standards

### XML Documentation

Always document public APIs using XML documentation comments to ensure clarity and maintainability.

Write clear and concise XML documentation comments (`///`) for all public functions and non-trivial private functions, excluding simple getters/setters and self-explanatory one-line methods. Provide medium level of detail - be concise but leave no essentials out. Make code as self-documenting as possible through clear naming.

When applicable, include `<example>` and `<code>` documentation in the comments.

Perform review of the Notes documentation periodically to ensure it remains relevant and up to date with the current state of the codebase.

### Notes Directory Structure

Always use Notes sub-directory of the project to document design decisions, architectural choices, and important implementation details. This documentation should provide context and rationale for future reference.

**Required Files** in the Notes sub-directory:
- **Usage.md** - Up-to-date documentation on project usage
- **Changes.md** - Change log with Breaking Changes topic on top, newest entries first with date and time
- **Notes.md** - Implementation notes with detailed descriptions of important implementation details
- **Fixes.md** - What was fixed and how, with date and time, newest entries first
- **Examples.md** - All possible examples for the API calls
- **Architecture.md** - Diagrams, relations, top-level functionality for the user

**Optional Files** (allowed exceptions):
- **README.md** - Standard repository overview (place in project root, not Notes/)
- **Images/** - Subdirectory for diagrams and screenshots referenced in documentation
- **INDEX.md** - Navigation index for complex documentation

**File Naming**: Always use PascalCase for markdown files in the Notes sub-directory (e.g., "DesignDecisions.md" instead of "designdecisions.md" or "design_decisions.md").

## Unit Testing

When writing unit tests, always follow the Arrange-Act-Assert (AAA) pattern to structure your tests for clarity and maintainability. **Do not include "Arrange", "Act", or "Assert" comments in the code** - the pattern should be evident from the code structure itself.

Don't add constants from any subproject to a library project.

Default naming conventions for tests:
- Test class name: `ClassNameTests` (e.g., `CalculatorTests`)
- Test method name: `ClassName_MethodName_ExpectedBehavior` (e.g., `Calculator_Add_ReturnsSum`)

Always use this naming convention unless explicitly maintaining consistency with existing test files that use a different pattern.

'''
Microsoft provided csharp instructions for copilot
Latest version is available at https://github.com/github/awesome-copilot/blob/main/instructions/csharp.instructions.md
'''
## C# Instructions
Applies to: `**/*.cs`

Always use the latest version C#, currently C# 13 features.

## General Instructions

Make only high confidence suggestions when reviewing code changes.
Write code with good maintainability practices, including comments on why certain design decisions were made.
For libraries or external dependencies, mention their usage and purpose in comments.

## Project Setup and Structure

Guide users through creating a new .NET project with the appropriate templates.
Explain the purpose of each generated file and folder to build understanding of the project structure.
Demonstrate how to organize code using feature folders or domain-driven design principles.
Show proper separation of concerns with models, services, and data access layers.
Explain the Program.cs and configuration system in ASP.NET Core 9 including environment-specific settings.

## Nullable Reference Types

Declare variables non-nullable, and check for null at entry points.
Always use is null or is not null instead of == null or != null.
Trust the C# null annotations and don't add null checks when the type system says a value cannot be null.

## Data Access Patterns

Guide the implementation of a data access layer using Entity Framework Core.
Explain different options (SQL Server, SQLite, In-Memory) for development and production.
Demonstrate repository pattern implementation and when it's beneficial.
Show how to implement database migrations and data seeding.
Explain efficient query patterns to avoid common performance issues.

## Authentication and Authorization

Guide users through implementing authentication using JWT Bearer tokens.
Explain OAuth 2.0 and OpenID Connect concepts as they relate to ASP.NET Core.
Show how to implement role-based and policy-based authorization.
Demonstrate integration with Microsoft Entra ID (formerly Azure AD).
Explain how to secure both controller-based and Minimal APIs consistently.

## Validation and Error Handling

Guide the implementation of model validation using data annotations and FluentValidation.
Explain the validation pipeline and how to customize validation responses.
Demonstrate a global exception handling strategy using middleware.
Show how to create consistent error responses across the API.
Explain problem details (RFC 7807) implementation for standardized error responses.

## API Versioning and Documentation

Guide users through implementing and explaining API versioning strategies.
Demonstrate Swagger/OpenAPI implementation with proper documentation.
Show how to document endpoints, parameters, responses, and authentication.
Explain versioning in both controller-based and Minimal APIs.
Guide users on creating meaningful API documentation that helps consumers.

## Logging and Monitoring

Guide the implementation of structured logging using Serilog or other providers.
Explain the logging levels and when to use each.
Demonstrate integration with Application Insights for telemetry collection.
Show how to implement custom telemetry and correlation IDs for request tracking.
Explain how to monitor API performance, errors, and usage patterns.

## Testing

Guide users through creating unit tests.
Do not emit "Act", "Arrange" or "Assert" comments in test code.
Explain integration testing approaches for API endpoints.
Demonstrate how to mock dependencies for effective testing.
Show how to test authentication and authorization logic.
Explain test-driven development principles as applied to API development.

## Performance Optimization

Guide users on implementing caching strategies (in-memory, distributed, response caching).
Explain asynchronous programming patterns and why they matter for API performance.
Demonstrate pagination, filtering, and sorting for large data sets.
Show how to implement compression and other performance optimizations.
Explain how to measure and benchmark API performance.

## Deployment and DevOps

Guide users through containerizing their API using .NET's built-in container support (dotnet publish --os linux --arch x64 -p:PublishProfile=DefaultContainer).
Explain the differences between manual Dockerfile creation and .NET's container publishing features.
Explain CI/CD pipelines for .NET applications.
Demonstrate deployment to Azure App Service, Azure Container Apps, or other hosting options.
Show how to implement health checks and readiness probes.
Explain environment-specific configurations for different deployment stages.
