# InfinityFlow C# Eval MCP Server

An MCP (Model Context Protocol) server that evaluates and executes C# scripts using Roslyn. This tool allows AI assistants to run C# code dynamically, either from direct input or from .csx script files.

## Features

- 🚀 Execute C# scripts directly or from files
- 📦 Full Roslyn scripting support with common namespaces pre-imported
- 📚 NuGet package support via `#r "nuget: PackageName, Version"` directives
- 🔒 Console output capture (safe for MCP stdio protocol)
- ⚡ Comprehensive error handling for compilation and runtime errors
- 🐳 Available as both Docker container and dnx package with volume mounting support
- ✅ Full test coverage with NUnit and FluentAssertions

## Installation

### Using dnx

```bash
# Install via dnx
dnx InfinityFlow.CSharp.Eval --version 1.0.0 --yes

# Run the tool
dnx run InfinityFlow.CSharp.Eval
```

### Using Docker

```bash
# Pull from GitHub Container Registry
docker pull ghcr.io/infinityflowapp/csharp-mcp:latest

# Run interactively
docker run -it ghcr.io/infinityflowapp/csharp-mcp:latest
```

### From source

```bash
# Clone the repository
git clone https://github.com/InfinityFlowApp/csharp-mcp.git
cd csharp-mcp

# Build and run
dotnet build
dotnet run --project src/InfinityFlow.CSharp.Eval
```

## Usage

The MCP server exposes a single tool: `EvalCSharp`

### Parameters

- `csxFile` (optional): Full path to a .csx file to execute
- `csx` (optional): C# script code to execute directly
- `timeoutSeconds` (optional): Maximum execution time in seconds (default: 30)

Either `csxFile` or `csx` must be provided, but not both.

### Examples

For comprehensive examples, see the [examples directory](examples/):

- [Basic Execution](examples/basic-execution/) - Simple C# script execution
- [Fibonacci Sequence](examples/fibonacci-sequence/) - Generating number sequences
- [Data Processing](examples/data-processing/) - LINQ and data manipulation
- [NuGet Packages](examples/nuget-packages/) - Using external NuGet packages
- [NUnit Testing](examples/nunit-testing/) - Running tests programmatically

#### Direct code execution

```json
{
  "tool": "EvalCSharp",
  "parameters": {
    "csx": "Console.WriteLine(\"Hello World!\"); 2 + 2"
  }
}
```

Output:

```text
Hello World!
Result: 4
```

#### Execute from file

```json
{
  "tool": "EvalCSharp",
  "parameters": {
    "csxFile": "/scripts/example.csx"
  }
}
```

#### Complex example with LINQ

```csharp
var numbers = Enumerable.Range(1, 10);
var evenSum = numbers.Where(n => n % 2 == 0).Sum();
Console.WriteLine($"Sum of even numbers: {evenSum}");
evenSum * 2
```

Output:

```text
Sum of even numbers: 30
Result: 60
```

### Pre-imported namespaces

The following namespaces are automatically available:

- `System`
- `System.IO`
- `System.Linq`
- `System.Text`
- `System.Collections.Generic`
- `System.Threading.Tasks`
- `System.Net.Http`
- `System.Text.Json`
- `System.Text.RegularExpressions`

### NuGet Package Support

You can reference NuGet packages directly in your scripts using the `#r` directive:

```csharp
#r "nuget: Newtonsoft.Json, 13.0.3"
#r "nuget: Humanizer, 2.14.1"

using Newtonsoft.Json;
using Humanizer;

var json = JsonConvert.SerializeObject(new { Message = "Hello World" });
Console.WriteLine(json);
Console.WriteLine("5 days".Humanize());
```

The tool will automatically:

- Download the specified packages from NuGet.org
- Resolve and download dependencies
- Cache packages for faster subsequent runs
- Provide detailed error messages for invalid package specifications

## MCP Configuration

### Cursor

Add to your Cursor settings (`.cursor/mcp_settings.json` or via Settings UI):

```json
{
  "mcpServers": {
    "csharp-eval": {
      "command": "docker",
      "args": ["run", "-i", "--rm", "ghcr.io/infinityflowapp/csharp-mcp:latest"],
      "env": {
        "CSX_ALLOWED_PATH": "/scripts"
      }
    }
  }
}
```

Or if installed via dnx:

```json
{
  "mcpServers": {
    "csharp-eval": {
      "command": "dnx",
      "args": ["run", "InfinityFlow.CSharp.Eval"],
      "env": {
        "CSX_ALLOWED_PATH": "${workspaceFolder}/scripts"
      }
    }
  }
}
```

### Claude Code

Add the MCP server using the CLI:

**Using Docker:**

*Basic setup:*

```bash
claude mcp add csharp-eval docker -- run -i --rm ghcr.io/infinityflowapp/csharp-mcp:latest
```

*With file system access:*

```bash
claude mcp add csharp-eval docker -- run -i --rm --pull=always -v "${HOME}:${HOME}" -w "${PWD}" ghcr.io/infinityflowapp/csharp-mcp:latest
```

*With restricted script directory:*

```bash
claude mcp add csharp-eval -e CSX_ALLOWED_PATH="/scripts" docker -- run -i --rm -v "${HOME}/scripts:/scripts:ro" ghcr.io/infinityflowapp/csharp-mcp:latest
```

**Using dnx:**

```bash
claude mcp add csharp-eval -e CSX_ALLOWED_PATH="/Users/your-username/scripts" dnx -- run InfinityFlow.CSharp.Eval
```

The volume mounting (`-v ${HOME}:${HOME}`) allows the tool to access .csx files from your filesystem.

### VS Code

Create `.vscode/mcp.json`:

```json
{
  "servers": {
    "csharp-eval": {
      "type": "stdio",
      "command": "dnx",
      "args": ["run", "InfinityFlow.CSharp.Eval"]
    }
  }
}
```

### Visual Studio

Create `.mcp.json` in solution directory:

```json
{
  "servers": {
    "csharp-eval": {
      "type": "stdio",
      "command": "dnx",
      "args": [
        "InfinityFlow.CSharp.Eval",
        "--version",
        "1.0.0",
        "--yes"
      ]
    }
  }
}
```

## Development

### Prerequisites

- .NET 9.0 SDK or later
- Docker (optional, for containerization)

### Building

```bash
# Restore dependencies
dotnet restore

# Build the project
dotnet build

# Run tests
dotnet test

# Pack as MCP package
dotnet pack -c Release
```

### Testing

The project includes comprehensive unit tests using NUnit and FluentAssertions:

```bash
dotnet test
```

### Docker Build

```bash
# Build the Docker image
docker build -t infinityflow/csharp-eval-mcp .

# Run the container
docker run -it infinityflow/csharp-eval-mcp
```

## Project Structure

```text
csharp-mcp/
├── src/
│   └── InfinityFlow.CSharp.Eval/     # Main MCP server implementation
│       ├── Tools/
│       │   ├── CSharpEvalTools.cs    # Roslyn script evaluation tool
│       │   └── NuGetPackageResolver.cs # NuGet package resolution
│       ├── Program.cs                # MCP server entry point
│       └── .mcp/
│           └── server.json           # MCP server configuration
├── tests/
│   └── InfinityFlow.CSharp.Eval.Tests/  # Unit tests
│       ├── CSharpEvalToolsTests.cs      # Core functionality tests
│       └── ExamplesTests.cs             # Example validation tests
├── examples/                         # Example scripts with documentation
│   ├── basic-execution/             # Simple C# script examples
│   ├── fibonacci-sequence/          # Algorithm demonstrations
│   ├── data-processing/             # LINQ and data manipulation
│   ├── nuget-packages/              # External package usage
│   └── nunit-testing/               # Programmatic test execution
├── Directory.Packages.props          # Central package management
├── Dockerfile                        # Docker containerization
└── .github/
    └── workflows/                   # GitHub Actions CI/CD
        ├── ci-cd.yml               # Main CI/CD pipeline
        ├── validate-pr.yml         # PR validation
        └── release-drafter.yml     # Automated release notes
```

## CI/CD

The project uses GitHub Actions for continuous integration and deployment:

- **CI/CD Pipeline**: Automated testing, Docker builds, and NuGet publishing
- **PR Validation**: Build and test validation for all pull requests
- **Release Drafter**: Automated release notes generation
- **Dependabot**: Automated dependency updates

### Releases

Releases are automatically published when a version tag is pushed:

```bash
git tag v1.0.0
git push origin v1.0.0
```

This will:

1. Build and test the project
2. Publish Docker image to GitHub Container Registry
3. Publish NuGet package to NuGet.org
4. Create a GitHub release with auto-generated notes

## Security Considerations

- ⚠️ Scripts run in the same process context as the MCP server
- 🔐 Console output is captured to prevent interference with MCP stdio protocol
- 🐳 Docker container runs as non-root user for additional security
- 🛡️ Use appropriate sandboxing when running untrusted scripts
- 📁 File access can be restricted via `CSX_ALLOWED_PATH` environment variable
- 🔒 Only .csx files are allowed for execution
- ⏱️ Scripts have a configurable timeout (default 30 seconds)

### File Access Restrictions

The `CSX_ALLOWED_PATH` environment variable restricts which directories can be accessed when executing .csx files:

```bash
# Restrict to specific directory
export CSX_ALLOWED_PATH=/path/to/allowed/scripts

# Multiple paths (colon-separated on Linux/Mac, semicolon on Windows)
export CSX_ALLOWED_PATH=/path/one:/path/two:/path/three
```

**Important Notes:**

- Path restrictions are **disabled inside Docker containers** (when `DOTNET_RUNNING_IN_CONTAINER=true`)
- This is because Docker already provides isolation via volume mounts
- If not set, file access is unrestricted (use with caution)
- Paths are checked recursively - subdirectories are allowed

## Contributing

Contributions are welcome! Please feel free to submit a Pull Request.

1. Fork the repository
2. Create your feature branch (`git checkout -b feature/amazing-feature`)
3. Commit your changes (`git commit -m 'Add some amazing feature'`)
4. Push to the branch (`git push origin feature/amazing-feature`)
5. Open a Pull Request

## License

MIT

## Links

- [Model Context Protocol](https://modelcontextprotocol.io/)
- [MCP C# SDK](https://www.nuget.org/packages/ModelContextProtocol)
- [Roslyn Scripting](https://github.com/dotnet/roslyn/wiki/Scripting-API-Samples)
