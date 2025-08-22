using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using NuGet.Common;
using NuGet.Configuration;
using NuGet.Frameworks;
using NuGet.Packaging;
using NuGet.Packaging.Core;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;
using NuGet.Versioning;

namespace InfinityFlow.CSharp.Eval.Tools;

internal partial class NuGetPackageResolver
{
    private static readonly string PackagesDirectory = Path.Combine(Path.GetTempPath(), "csharp-mcp-packages");
    [GeneratedRegex(@"#r\s+""nuget:\s*([^,]+),\s*([^""]+)""", RegexOptions.IgnoreCase)]
    private static partial Regex NuGetDirectiveRegex();
    [GeneratedRegex(@"#r\s+""nuget:[^""]*""", RegexOptions.IgnoreCase)]
    private static partial Regex AnyNuGetDirectiveRegex();

    private const int MaxRecursionDepth = 10;
    private const string TargetFramework = "net9.0";
    private const string MicrosoftExtensionsStableVersion = "8.0.0";
    private static readonly TimeSpan NetworkOperationTimeout = GetNetworkTimeout();

    static NuGetPackageResolver()
    {
        Directory.CreateDirectory(PackagesDirectory);
    }

    private static TimeSpan GetNetworkTimeout()
    {
        // Allow test environments to override timeout for testing timeout scenarios
        if (Environment.GetEnvironmentVariable("NUGET_TIMEOUT_TEST") == "true")
        {
            return TimeSpan.FromMilliseconds(1); // Very short timeout for testing
        }
        return TimeSpan.FromSeconds(30); // Default production timeout
    }

    public static async Task<(List<MetadataReference> References, List<string> Errors)> ResolvePackagesAsync(string scriptCode, CancellationToken cancellationToken = default)
    {
        var references = new List<MetadataReference>();
        var errors = new List<string>();

        // Note: ResolvedPackages is now a persistent cache for the session

        // First, find all #r "nuget:..." directives
        var allNuGetDirectives = AnyNuGetDirectiveRegex().Matches(scriptCode);

        // Then find properly formatted ones
        var validMatches = NuGetDirectiveRegex().Matches(scriptCode);

        // Check for malformed directives
        foreach (Match directive in allNuGetDirectives)
        {
            bool isValid = false;
            foreach (Match valid in validMatches)
            {
                if (valid.Value == directive.Value)
                {
                    isValid = true;
                    break;
                }
            }

            if (!isValid)
            {
                errors.Add($"Invalid NuGet directive syntax: {directive.Value}. Expected format: #r \"nuget: PackageName, Version\"");
            }
        }

        if (validMatches.Count == 0 && errors.Count == 0)
            return (references, errors);

        try
        {
            var logger = NullLogger.Instance;
            var cache = new SourceCacheContext();
            var settings = Settings.LoadDefaultSettings(null);
            var repository = Repository.Factory.GetCoreV3("https://api.nuget.org/v3/index.json");

            // Apply timeout to cancellation token for network operations
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(NetworkOperationTimeout);
            var timeoutToken = timeoutCts.Token;

            foreach (Match match in validMatches)
            {
                var packageId = match.Groups[1].Value.Trim();
                var version = match.Groups[2].Value.Trim();

                try
                {
                    var assemblies = await DownloadPackageAsync(packageId, version, repository, cache, logger, timeoutToken);
                    foreach (var assembly in assemblies)
                    {
                        references.Add(MetadataReference.CreateFromFile(assembly));
                    }
                }
                catch (OperationCanceledException) when (timeoutToken.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
                {
                    errors.Add($"Failed to resolve NuGet package '{packageId}' version '{version}': Network operation timed out after {NetworkOperationTimeout.TotalSeconds} seconds");
                }
                catch (Exception ex)
                {
                    errors.Add($"Failed to resolve NuGet package '{packageId}' version '{version}': {ex.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            errors.Add($"NuGet initialization failed: {ex.Message}");
        }

        return (references, errors);
    }

    private static readonly ConcurrentDictionary<string, bool> ResolvedPackages = new();
    private static readonly ConcurrentDictionary<string, IEnumerable<PackageDependency>> PackageDependencies = new();

    private static async Task<List<string>> DownloadPackageAsync(
        string packageId,
        string versionString,
        SourceRepository repository,
        SourceCacheContext cache,
        ILogger logger,
        CancellationToken cancellationToken,
        int depth = 0)
    {
        var resource = await repository.GetResourceAsync<FindPackageByIdResource>(cancellationToken);
        var version = NuGetVersion.Parse(versionString);
        var packageIdentity = new PackageIdentity(packageId, version);
        var packagePath = Path.Combine(PackagesDirectory, $"{packageId}.{version}");
        var packageKey = $"{packageId}.{version}";

        // Check if package is already resolved in this session
        if (ResolvedPackages.ContainsKey(packageKey))
        {
            return GetAssembliesFromPackage(packagePath);
        }

        // Check if package is already downloaded
        var libPath = Path.Combine(packagePath, "lib");
        if (Directory.Exists(libPath))
        {
            ResolvedPackages.TryAdd(packageKey, true);
            // For cached packages, we need to read dependencies from the package file
            var cachedDependencies = await GetCachedPackageDependencies(packagePath, packageId, version, repository, cache, logger, cancellationToken);
            return await ResolveTransitiveDependenciesAsync(packagePath, packageId, version, cachedDependencies, repository, cache, logger, cancellationToken);
        }

        // Download package and extract both assemblies and dependencies in one pass
        using var packageStream = new MemoryStream();
        var downloaded = await resource.CopyNupkgToStreamAsync(
            packageId,
            version,
            packageStream,
            cache,
            logger,
            cancellationToken);

        if (!downloaded)
        {
            throw new InvalidOperationException($"Package '{packageId}' version '{version}' not found");
        }

        // Extract package and read dependencies in single operation
        packageStream.Seek(0, SeekOrigin.Begin);
        using var reader = new PackageArchiveReader(packageStream);

        var framework = NuGetFramework.Parse(TargetFramework);
        var items = reader.GetLibItems().ToList();
        var compatible = items.Where(x => DefaultCompatibilityProvider.Instance.IsCompatible(framework, x.TargetFramework))
                              .OrderByDescending(x => x.TargetFramework.Version)
                              .FirstOrDefault();

        if (compatible == null)
        {
            // Try to find any .NET Standard or .NET Core compatible version
            compatible = items.Where(x => x.TargetFramework.Framework == ".NETStandard" ||
                                         x.TargetFramework.Framework == ".NETCoreApp")
                              .OrderByDescending(x => x.TargetFramework.Version)
                              .FirstOrDefault();
        }

        if (compatible == null)
        {
            throw new InvalidOperationException($"No compatible framework found for package '{packageId}'");
        }

        // Extract files
        Directory.CreateDirectory(packagePath);
        foreach (var file in compatible.Items)
        {
            if (file.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
            {
                var targetPath = Path.Combine(packagePath, file);
                Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);

                using var fileStream = reader.GetStream(file);
                using var targetStream = File.Create(targetPath);
                await fileStream.CopyToAsync(targetStream, cancellationToken);
            }
        }

        // Read dependencies from the same package reader (avoid re-downloading)
        var dependencies = reader.GetPackageDependencies()
            .Where(x => DefaultCompatibilityProvider.Instance.IsCompatible(framework, x.TargetFramework))
            .SelectMany(x => x.Packages)
            .ToList();

        // Cache dependencies for future use
        PackageDependencies.TryAdd(packageKey, dependencies);
        ResolvedPackages.TryAdd(packageKey, true);
        return await ResolveTransitiveDependenciesAsync(packagePath, packageId, version, dependencies, repository, cache, logger, cancellationToken, depth);
    }

    private static async Task<List<string>> ResolveTransitiveDependenciesAsync(
        string packagePath,
        string packageId,
        NuGetVersion version,
        IEnumerable<PackageDependency> dependencies,
        SourceRepository repository,
        SourceCacheContext cache,
        ILogger logger,
        CancellationToken cancellationToken,
        int depth = 0)
    {
        // Prevent infinite recursion
        if (depth >= MaxRecursionDepth)
        {
            logger.LogWarning($"Maximum recursion depth ({MaxRecursionDepth}) reached for package {packageId}. Stopping transitive resolution.");
            return GetAssembliesFromPackage(packagePath);
        }

        var assemblies = GetAssembliesFromPackage(packagePath);
        var unresolvedDependencies = new List<string>();

        // Resolve dependencies with improved error tracking
        foreach (var dependency in dependencies)
        {
            if (ShouldResolveDependency(dependency.Id))
            {
                try
                {
                    var depVersion = GetBestVersionForDependency(dependency);
                    var depAssemblies = await DownloadPackageAsync(dependency.Id, depVersion, repository, cache, logger, cancellationToken, depth + 1);
                    assemblies.AddRange(depAssemblies);
                }
                catch (Exception ex)
                {
                    // Classify errors to determine if they should be ignored or logged
                    if (IsExpectedDependencyFailure(ex, dependency.Id))
                    {
                        // These are expected failures for built-in .NET packages - log at debug level
                        logger.LogDebug($"Skipping built-in dependency '{dependency.Id}': {ex.Message}");
                    }
                    else
                    {
                        // Unexpected failures should be tracked and logged as warnings
                        unresolvedDependencies.Add($"{dependency.Id}: {ex.Message}");
                        logger.LogWarning($"Failed to resolve dependency '{dependency.Id}' for package '{packageId}': {ex.Message}");
                    }
                }
            }
        }

        // Log summary of unresolved dependencies if any
        if (unresolvedDependencies.Count > 0)
        {
            logger.LogInformation($"Package '{packageId}' has {unresolvedDependencies.Count} unresolved dependencies (may impact functionality): {string.Join(", ", unresolvedDependencies.Take(3))}{(unresolvedDependencies.Count > 3 ? "..." : "")}");
        }

        // Remove duplicates and prefer newer versions
        var uniqueAssemblies = new Dictionary<string, string>();

        foreach (var assembly in assemblies)
        {
            var fileName = Path.GetFileNameWithoutExtension(assembly);
            if (!uniqueAssemblies.ContainsKey(fileName))
            {
                uniqueAssemblies[fileName] = assembly;
            }
            else
            {
                // Prefer assemblies from more specific framework versions (longer paths usually = more specific)
                if (assembly.Length > uniqueAssemblies[fileName].Length)
                {
                    uniqueAssemblies[fileName] = assembly;
                }
            }
        }

        return uniqueAssemblies.Values.ToList();
    }

    private static bool ShouldResolveDependency(string packageId)
    {
        // Skip framework reference packages and packages that are already in .NET runtime
        var skipPrefixes = new[]
        {
            "Microsoft.NETCore.App",
            "Microsoft.AspNetCore.App",
            "Microsoft.WindowsDesktop.App"
        };

        var skipExact = new[]
        {
            "NETStandard.Library"
        };

        // Skip very basic System packages that cause version conflicts
        var systemSkipPrefixes = new[]
        {
            "System.Runtime",
            "System.Collections",
            "System.Linq",
            "System.Threading.Tasks",
            "System.IO",
            "System.Text.",
            "System.Globalization",
            "System.Resources",
            "System.Diagnostics.Debug",
            "System.Diagnostics.Tools",
            "System.Reflection",
            "System.ComponentModel",
            "System.Xml"
        };

        // Allow important Microsoft.Extensions packages that are commonly needed
        if (packageId.StartsWith("Microsoft.Extensions.", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return !skipPrefixes.Any(prefix => packageId.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) &&
               !skipExact.Any(exact => packageId.Equals(exact, StringComparison.OrdinalIgnoreCase)) &&
               !systemSkipPrefixes.Any(prefix => packageId.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
    }

    private static string GetBestVersionForDependency(PackageDependency dependency)
    {
        // For Microsoft.Extensions packages, use specific known good versions
        if (dependency.Id.StartsWith("Microsoft.Extensions.", StringComparison.OrdinalIgnoreCase))
        {
            // Use a stable version that works well with .NET 8/9
            return MicrosoftExtensionsStableVersion;
        }

        // Use the minimum version if available, otherwise try to resolve latest compatible
        if (dependency.VersionRange.MinVersion != null)
        {
            return dependency.VersionRange.MinVersion.ToString();
        }

        if (dependency.VersionRange.MaxVersion != null && dependency.VersionRange.IsMaxInclusive)
        {
            return dependency.VersionRange.MaxVersion.ToString();
        }

        // Try to extract a reasonable version from the range
        if (!string.IsNullOrEmpty(dependency.VersionRange.OriginalString))
        {
            var versionString = dependency.VersionRange.OriginalString;
            // Handle ranges like "[1.0.0,)" - use the minimum
            if (versionString.StartsWith("[") && dependency.VersionRange.MinVersion != null)
            {
                return dependency.VersionRange.MinVersion.ToString();
            }
        }

        // Default to a reasonable version range
        return dependency.VersionRange.OriginalString ?? "latest";
    }

    private static List<string> GetAssembliesFromPackage(string packagePath)
    {
        var assemblies = new List<string>();
        var directories = new[] { "lib/net10.0", "lib/net9.0", "lib/net8.0", "lib/net7.0", "lib/net6.0", "lib/net5.0", "lib/netstandard2.1", "lib/netstandard2.0" };

        foreach (var dir in directories)
        {
            var fullPath = Path.Combine(packagePath, dir);
            if (Directory.Exists(fullPath))
            {
                assemblies.AddRange(Directory.GetFiles(fullPath, "*.dll"));
                break;
            }
        }

        // If no specific framework folder found, look in lib directly
        if (assemblies.Count == 0)
        {
            var libPath = Path.Combine(packagePath, "lib");
            if (Directory.Exists(libPath))
            {
                // Get the first available framework folder
                var frameworkDir = Directory.GetDirectories(libPath).FirstOrDefault();
                if (frameworkDir != null)
                {
                    assemblies.AddRange(Directory.GetFiles(frameworkDir, "*.dll"));
                }
            }
        }

        return assemblies;
    }

    private static async Task<IEnumerable<PackageDependency>> GetCachedPackageDependencies(
        string packagePath,
        string packageId,
        NuGetVersion version,
        SourceRepository repository,
        SourceCacheContext cache,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        var packageKey = $"{packageId}.{version}";
        
        // First check if dependencies are already cached
        if (PackageDependencies.TryGetValue(packageKey, out var cachedDependencies))
        {
            return cachedDependencies;
        }

        try
        {
            // Only download if dependencies not cached yet
            using var packageStream = new MemoryStream();
            var resource = await repository.GetResourceAsync<FindPackageByIdResource>(cancellationToken);
            var downloaded = await resource.CopyNupkgToStreamAsync(
                packageId,
                version,
                packageStream,
                cache,
                logger,
                cancellationToken);

            if (!downloaded)
            {
                var emptyDeps = Enumerable.Empty<PackageDependency>();
                PackageDependencies.TryAdd(packageKey, emptyDeps);
                return emptyDeps;
            }

            packageStream.Seek(0, SeekOrigin.Begin);
            using var reader = new PackageArchiveReader(packageStream);
            var framework = NuGetFramework.Parse(TargetFramework);

            var dependencies = reader.GetPackageDependencies()
                .Where(x => DefaultCompatibilityProvider.Instance.IsCompatible(framework, x.TargetFramework))
                .SelectMany(x => x.Packages)
                .ToList();

            // Cache for future use
            PackageDependencies.TryAdd(packageKey, dependencies);
            return dependencies;
        }
        catch (Exception ex)
        {
            logger.LogWarning($"Failed to read dependencies for cached package '{packageId}': {ex.Message}");
            var emptyDeps = Enumerable.Empty<PackageDependency>();
            PackageDependencies.TryAdd(packageKey, emptyDeps);
            return emptyDeps;
        }
    }

    private static bool IsExpectedDependencyFailure(Exception ex, string dependencyId)
    {
        // These are expected failures for packages that are built into .NET runtime
        var expectedFailureMessages = new[]
        {
            "not found",
            "No compatible framework",
            "Package does not exist",
            "404 (Not Found)"
        };

        var isExpectedMessage = expectedFailureMessages.Any(msg => 
            ex.Message.Contains(msg, StringComparison.OrdinalIgnoreCase));

        // Also check if it's a known built-in package that commonly fails
        var builtInPrefixes = new[]
        {
            "System.Runtime",
            "System.Collections",
            "System.Linq",
            "System.Threading.Tasks",
            "System.IO",
            "System.Text.Encoding",
            "System.Globalization",
            "System.Resources.ResourceManager",
            "Microsoft.NETCore",
            "Microsoft.CSharp"
        };

        var isBuiltInPackage = builtInPrefixes.Any(prefix => 
            dependencyId.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));

        return isExpectedMessage && isBuiltInPackage;
    }
}
