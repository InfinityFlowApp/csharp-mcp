using System.Reflection;
using System.Runtime.Loader;
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
    
    static NuGetPackageResolver()
    {
        Directory.CreateDirectory(PackagesDirectory);
    }

    public static async Task<List<MetadataReference>> ResolvePackagesAsync(string scriptCode, CancellationToken cancellationToken = default)
    {
        var references = new List<MetadataReference>();
        var matches = NuGetDirectiveRegex.Matches(scriptCode);
        
        if (matches.Count == 0)
            return references;

        var logger = NullLogger.Instance;
        var cache = new SourceCacheContext();
        var settings = Settings.LoadDefaultSettings(null);
        var repository = Repository.Factory.GetCoreV3("https://api.nuget.org/v3/index.json");
        
        foreach (Match match in matches)
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
                throw new InvalidOperationException($"Failed to resolve NuGet package '{packageId}' version '{version}': {ex.Message}", ex);
            }
        }
        
        return references;
    }

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
        
        // Check if package is already downloaded
        var libPath = Path.Combine(packagePath, "lib");
        if (Directory.Exists(libPath))
        {
            return GetAssembliesFromPackage(packagePath);
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

        // Also extract dependencies if needed
        var dependencies = reader.GetPackageDependencies()
            .Where(x => DefaultCompatibilityProvider.Instance.IsCompatible(framework, x.TargetFramework))
            .SelectMany(x => x.Packages)
            .ToList();

        var assemblies = GetAssembliesFromPackage(packagePath);
        
        // Recursively download dependencies
        foreach (var dependency in dependencies)
        {
            if (dependency.Id != "NETStandard.Library" && !dependency.Id.StartsWith("System.") && !dependency.Id.StartsWith("Microsoft.NETCore."))
            {
                var depVersion = dependency.VersionRange.MinVersion?.ToString() ?? "latest";
                var depAssemblies = await DownloadPackageAsync(dependency.Id, depVersion, repository, cache, logger, cancellationToken);
                assemblies.AddRange(depAssemblies);
            }
        }

        return assemblies.Distinct().ToList();
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