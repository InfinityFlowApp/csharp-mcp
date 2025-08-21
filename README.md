# InfinityFlow C# Eval MCP Server

An MCP (Model Context Protocol) server that evaluates and executes C# scripts using Roslyn. This tool allows AI assistants to run C# code dynamically, either from direct input or from .csx script files.

## Features

- 🚀 Execute C# scripts directly or from files
- 📦 Full Roslyn scripting support with common namespaces pre-imported
- 🔒 Console output capture (safe for MCP stdio protocol)
- ⚡ Comprehensive error handling for compilation and runtime errors
- 🐳 Available as both Docker container and dotnet tool
- ✅ Full test coverage with NUnit and FluentAssertions

## Installation

### As a dotnet tool

```bash
# Install globally
dotnet tool install -g InfinityFlow.CSharp.Eval

# Run the tool
infinityflow-csharp-eval
```

### Using Docker

```bash
# Pull from GitHub Container Registry
docker pull ghcr.io/infinityflowapp/csharp-mcp:latest

# Run interactively
docker run -it ghcr.io/infinityflowapp/csharp-mcp:latest

# Or use docker-compose
docker-compose run csharp-eval-mcp
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

Either `csxFile` or `csx` must be provided, but not both.

### Examples

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
```
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
```
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

Or if installed as a dotnet tool:

```json
{
  "mcpServers": {
    "csharp-eval": {
      "command": "infinityflow-csharp-eval",
      "env": {
        "CSX_ALLOWED_PATH": "${workspaceFolder}/scripts"
      }
    }
  }
}
```

### Claude Code

Add to your Claude Code configuration (`claude_desktop_config.json`):

- **macOS**: `~/Library/Application Support/Claude/claude_desktop_config.json`
- **Windows**: `%APPDATA%\Claude\claude_desktop_config.json`
- **Linux**: `~/.config/claude/claude_desktop_config.json`

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

Or if installed as a dotnet tool:

```json
{
  "mcpServers": {
    "csharp-eval": {
      "command": "infinityflow-csharp-eval",
      "env": {
        "CSX_ALLOWED_PATH": "/Users/your-username/scripts"
      }
    }
  }
}
```

### VS Code

Create `.vscode/mcp.json`:

```json
{
  "servers": {
    "csharp-eval": {
      "type": "stdio",
      "command": "infinityflow-csharp-eval"
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

# Pack as dotnet tool
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

```
csharp-mcp/
├── src/
│   └── InfinityFlow.CSharp.Eval/     # Main MCP server implementation
│       ├── Tools/
│       │   └── CSharpEvalTools.cs    # Roslyn script evaluation tool
│       ├── Program.cs                # MCP server entry point
│       └── .mcp/
│           └── server.json           # MCP server configuration
├── tests/
│   └── InfinityFlow.CSharp.Eval.Tests/  # Unit tests
│       └── CSharpEvalToolsTests.cs      # Comprehensive test suite
├── Directory.Packages.props          # Central package management
├── Dockerfile                        # Docker containerization
├── docker-compose.yml               # Docker compose configuration
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
