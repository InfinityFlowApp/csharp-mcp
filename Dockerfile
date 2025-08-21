# Build stage
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /source

# Copy solution and project files
COPY csharp-mcp.sln .
COPY Directory.Packages.props .
COPY src/InfinityFlow.CSharp.Eval/*.csproj ./src/InfinityFlow.CSharp.Eval/
COPY tests/InfinityFlow.CSharp.Eval.Tests/*.csproj ./tests/InfinityFlow.CSharp.Eval.Tests/

# Restore dependencies
RUN dotnet restore

# Copy source code
COPY src/ ./src/
COPY tests/ ./tests/

# Build and test
RUN dotnet build -c Release --no-restore
RUN dotnet test -c Release --no-build --verbosity normal

# Publish
RUN dotnet publish src/InfinityFlow.CSharp.Eval/InfinityFlow.CSharp.Eval.csproj -c Release -o /app/publish --no-restore

# Runtime stage
FROM mcr.microsoft.com/dotnet/runtime:9.0 AS runtime
WORKDIR /app

# Install required dependencies for Roslyn scripting
RUN apt-get update && apt-get install -y \
    libicu-dev \
    && rm -rf /var/lib/apt/lists/*

# Copy published app
COPY --from=build /app/publish .

# Create non-root user
RUN useradd -m -s /bin/bash mcpuser && \
    chown -R mcpuser:mcpuser /app

USER mcpuser

# The MCP server uses stdio for communication
ENTRYPOINT ["dotnet", "InfinityFlow.CSharp.Eval.dll"]