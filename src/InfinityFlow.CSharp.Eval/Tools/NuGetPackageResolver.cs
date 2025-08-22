using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using NuGet.Common;
using NuGet.Configuration;
using NuGet.Frameworks;
using NuGet.Packaging;
using NuGet.Packaging.Core;
using NuGet.Packaging.Signing;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;
using NuGet.Resolver;
using NuGet.Versioning;

namespace InfinityFlow.CSharp.Eval.Tools;

internal class NuGetPackageResolver
{
    private static readonly string PackagesDirectory = Path.Combine(Path.GetTempPath(), "csharp-mcp-packages");
    private static readonly Regex NuGetDirectiveRegex = new(@"#r\s+""nuget:\s*([^,]+),\s*([^""]+)""", RegexOptions.Compiled);
    private static readonly Regex AnyNuGetDirectiveRegex = new(@"#r\s+""nuget:[^""]*""", RegexOptions.Compiled);

    static NuGetPackageResolver()
    {
        Directory.CreateDirectory(PackagesDirectory);
    }

    public static async Task<(List<MetadataReference> References, List<string> Errors)> ResolvePackagesAsync(string scriptCode, CancellationToken cancellationToken = default)
    {
        var references = new List<MetadataReference>();
        var errors = new List<string>();

        // Note: ResolvedPackages is now a persistent cache for the session

        // First, find all #r "nuget:..." directives
        var allNuGetDirectives = AnyNuGetDirectiveRegex.Matches(scriptCode);

        // Then find properly formatted ones
        var validMatches = NuGetDirectiveRegex.Matches(scriptCode);

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

            foreach (Match match in validMatches)
            {
                var packageId = match.Groups[1].Value.Trim();
                var version = match.Groups[2].Value.Trim();

                try
                {
                    var assemblies = await DownloadPackageAsync(packageId, version, repository, cache, logger, cancellationToken);
                    foreach (var assembly in assemblies)
                    {
                        references.Add(MetadataReference.CreateFromFile(assembly));
                    }
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

    private static async Task<List<string>> DownloadPackageAsync(
        string packageId,
        string versionString,
        SourceRepository repository,
        SourceCacheContext cache,
        ILogger logger,
        CancellationToken cancellationToken)
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
            return await ResolveTransitiveDependenciesAsync(packagePath, packageId, version, repository, cache, logger, cancellationToken);
        }

        // Download package
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

        // Extract package
        packageStream.Seek(0, SeekOrigin.Begin);
        using var reader = new PackageArchiveReader(packageStream);

        var framework = NuGetFramework.Parse("net9.0");
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

        ResolvedPackages.TryAdd(packageKey, true);
        return await ResolveTransitiveDependenciesAsync(packagePath, packageId, version, repository, cache, logger, cancellationToken);
    }

    private static async Task<List<string>> ResolveTransitiveDependenciesAsync(
        string packagePath,
        string packageId,
        NuGetVersion version,
        SourceRepository repository,
        SourceCacheContext cache,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        var assemblies = GetAssembliesFromPackage(packagePath);

        // Get package dependencies for transitive resolution
        try
        {
            using var packageStream = new MemoryStream();
            var resource = await repository.GetResourceAsync<FindPackageByIdResource>(cancellationToken);
            var downloaded = await resource.CopyNupkgToStreamAsync(
                packageId,
                version,
                packageStream,
                cache,
                logger,
                cancellationToken);

            if (downloaded)
            {
                packageStream.Seek(0, SeekOrigin.Begin);
                using var reader = new PackageArchiveReader(packageStream);

                var framework = NuGetFramework.Parse("net9.0");
                var dependencies = reader.GetPackageDependencies()
                    .Where(x => DefaultCompatibilityProvider.Instance.IsCompatible(framework, x.TargetFramework))
                    .SelectMany(x => x.Packages)
                    .ToList();

                // Resolve dependencies with better filtering
                foreach (var dependency in dependencies)
                {
                    if (ShouldResolveDependency(dependency.Id))
                    {
                        try
                        {
                            var depVersion = GetBestVersionForDependency(dependency);
                            var depAssemblies = await DownloadPackageAsync(dependency.Id, depVersion, repository, cache, logger, cancellationToken);
                            assemblies.AddRange(depAssemblies);
                        }
                        catch (Exception ex)
                        {
                            // Skip dependencies that can't be resolved (likely built into .NET runtime)
                            if (ex.Message.Contains("not found") || ex.Message.Contains("No compatible framework"))
                            {
                                // Silently skip built-in .NET packages
                                continue;
                            }
                            // Log other dependency resolution failures but continue
                            Console.WriteLine($"Warning: Failed to resolve dependency '{dependency.Id}': {ex.Message}");
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Warning: Failed to resolve transitive dependencies for '{packageId}': {ex.Message}");
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
            return "8.0.0";
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
        var directories = new[] { "lib/net9.0", "lib/net8.0", "lib/net7.0", "lib/net6.0", "lib/net5.0", "lib/netstandard2.1", "lib/netstandard2.0" };

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
}
