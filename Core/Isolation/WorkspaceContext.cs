using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Enumeration;
using System.Linq;
using Microsoft.Extensions.Configuration;

namespace PayrollEngine.Mcp.Core.Isolation;

/// <summary>File workspace of the file based tools: permitted root folders, repository mask,
/// human identity and backend access.
/// Populated from McpServer:Regulator in appsettings.json or environment variables.
/// Every path argument of a file based tool is resolved below a root with <see cref="ResolvePath"/>.
/// Default when the section is absent: no roots, no backend access.</summary>
public sealed class WorkspaceContext
{
    /// <summary>The configuration section of the workspace</summary>
    public const string SectionName = "McpServer:Regulator";

    /// <summary>Permitted root folders (fully qualified). Empty: no file access.</summary>
    public IReadOnlyList<string> Roots { get; init; } = [];

    /// <summary>Wildcard mask (<c>*</c>, <c>?</c>) for the repository folders directly below a root.
    /// Null or empty: every folder below a root.</summary>
    public string RepoMask { get; init; }

    /// <summary>Human identity used for records that require a person</summary>
    public string Identity { get; init; }

    /// <summary>Backend access of the file based tools: None (offline), Read or Write. Default: None.</summary>
    public McpPermission Backend { get; init; } = McpPermission.None;

    /// <summary>Allowlist of tenants for backend writes. Empty: no backend writes.</summary>
    public IReadOnlyList<string> Tenants { get; init; } = [];

    /// <summary>True when at least one root is configured</summary>
    public bool IsConfigured => Roots.Count > 0;

    private static readonly StringComparison PathComparison =
        OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

    private static readonly string[] WindowsDeviceNames =
    [
        // ReSharper disable StringLiteralTypo
        "CON", "PRN", "AUX", "NUL", "CONIN$", "CONOUT$",
        // ReSharper restore StringLiteralTypo
        "COM0", "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
        "COM\u00B9", "COM\u00B2", "COM\u00B3",
        "LPT0", "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9",
        "LPT\u00B9", "LPT\u00B2", "LPT\u00B3"
    ];

    private readonly Lazy<List<WorkspaceRoot>> resolvedRoots;

    /// <summary>Creates an empty workspace; the roots are resolved on first use</summary>
    public WorkspaceContext()
    {
        resolvedRoots = new(() => Roots.Select(ResolveRoot).ToList());
    }

    #region Configuration

    /// <summary>Reads McpServer:Regulator from configuration.
    /// Roots and Tenants accept an array or a semicolon separated string.
    /// Returns an empty workspace when the section is absent.</summary>
    /// <param name="configuration">The application configuration</param>
    public static WorkspaceContext FromConfiguration(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        var section = configuration.GetSection(SectionName);
        if (!section.Exists())
        {
            return new WorkspaceContext();
        }

        return new WorkspaceContext
        {
            Roots = ReadList(section.GetSection(nameof(Roots))),
            RepoMask = TrimToNull(section[nameof(RepoMask)]),
            Identity = TrimToNull(section[nameof(Identity)]),
            Backend = ParseBackend(section[nameof(Backend)]),
            Tenants = ReadList(section.GetSection(nameof(Tenants)))
        };
    }

    private static List<string> ReadList(IConfigurationSection section)
    {
        var values = section.Value?.Split(';') ?? section.GetChildren().Select(child => child.Value);
        return values
            .Select(TrimToNull)
            .Where(value => value != null)
            .ToList();
    }

    private static string TrimToNull(string value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static McpPermission ParseBackend(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return McpPermission.None;
        }
        if (!Enum.TryParse<McpPermission>(value.Trim(), ignoreCase: true, out var backend) ||
            !Enum.IsDefined(backend))
        {
            throw new InvalidOperationException(
                $"{SectionName}:Backend has the invalid value '{value}' (allowed: None, Read, Write).");
        }
        return backend;
    }

    #endregion

    #region Validation

    /// <summary>Validates the workspace at startup.
    /// Throws if a root is not a fully qualified existing folder, the repository mask is invalid,
    /// or workspace settings are present without roots.</summary>
    public void Validate()
    {
        if (!IsConfigured)
        {
            if (RepoMask != null || Identity != null || Backend != McpPermission.None || Tenants.Count > 0)
            {
                throw new InvalidOperationException(
                    $"{SectionName}:Roots is required when other {SectionName} settings are present.");
            }
            return;
        }

        ValidateRepoMask(RepoMask);
        if (Tenants.Any(string.IsNullOrWhiteSpace))
        {
            throw new InvalidOperationException($"{SectionName}:Tenants contains an empty entry.");
        }
        GetResolvedRoots();
    }

    private static void ValidateRepoMask(string repoMask)
    {
        if (string.IsNullOrEmpty(repoMask))
        {
            return;
        }
        if (repoMask.IndexOfAny(['/', '\\', ':']) >= 0 ||
            repoMask.Trim('.').Length == 0 ||
            repoMask.Any(char.IsControl))
        {
            throw new InvalidOperationException(
                $"{SectionName}:RepoMask '{repoMask}' must be a folder name mask without separators.");
        }
    }

    private List<WorkspaceRoot> GetResolvedRoots() => resolvedRoots.Value;

    private static WorkspaceRoot ResolveRoot(string root)
    {
        if (string.IsNullOrWhiteSpace(root))
        {
            throw new InvalidOperationException($"{SectionName}:Roots contains an empty entry.");
        }

        var normalized = NormalizeSeparators(root.Trim());
        if (!Path.IsPathFullyQualified(normalized) || IsDevicePath(normalized))
        {
            throw new InvalidOperationException(
                $"{SectionName}:Roots entry '{root}' must be a fully qualified local folder path.");
        }

        var fullPath = Path.TrimEndingDirectorySeparator(Path.GetFullPath(normalized));
        if (!Directory.Exists(fullPath))
        {
            throw new InvalidOperationException(
                $"{SectionName}:Roots entry '{root}' does not exist.");
        }

        var realPath = Path.TrimEndingDirectorySeparator(WorkspaceLinkResolver.GetRealPath(fullPath));
        return new WorkspaceRoot(fullPath, realPath);
    }

    #endregion

    #region Repository and tenant checks

    /// <summary>Returns true when the folder name matches the repository mask.</summary>
    /// <param name="name">Folder name directly below a root</param>
    public bool IsRepositoryName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return false;
        }
        return string.IsNullOrEmpty(RepoMask) ||
               FileSystemName.MatchesSimpleExpression(RepoMask, name, ignoreCase: OperatingSystem.IsWindows());
    }

    /// <summary>Returns true when backend writes are granted for the tenant
    /// (Backend is Write and the tenant is on the allowlist).</summary>
    /// <param name="tenantIdentifier">The tenant identifier</param>
    public bool IsTenantAllowed(string tenantIdentifier) =>
        Backend >= McpPermission.Write &&
        !string.IsNullOrWhiteSpace(tenantIdentifier) &&
        Tenants.Any(tenant => string.Equals(tenant, tenantIdentifier.Trim(), StringComparison.OrdinalIgnoreCase));

    #endregion

    #region Path resolution

    /// <summary>Resolves a path argument below the workspace roots.
    /// Accepts relative paths (resolved against the first root containing the path,
    /// otherwise the first root) and absolute paths below a root; <c>\</c> and <c>/</c> are both separators.
    /// Rejects parent segments (<c>..</c>), absolute paths outside the roots, links leading outside the roots,
    /// device, UNC, drive-relative and stream paths, and repository folders not matching the mask.</summary>
    /// <param name="path">Relative or absolute path provided by the caller</param>
    /// <returns>The resolved workspace path</returns>
    /// <exception cref="ArgumentException">The path is empty</exception>
    /// <exception cref="InvalidOperationException">No workspace roots are configured</exception>
    /// <exception cref="UnauthorizedAccessException">The path leaves the workspace</exception>
    public WorkspacePath ResolvePath(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        if (!IsConfigured)
        {
            throw new InvalidOperationException($"No workspace roots configured ({SectionName}:Roots).");
        }
        var roots = GetResolvedRoots();

        var normalized = path.Trim().Replace('\\', '/');
        if (normalized.Any(char.IsControl))
        {
            throw Denied(path, "contains control characters");
        }
        if (normalized.StartsWith("//", StringComparison.Ordinal))
        {
            throw Denied(path, "network and device paths are not permitted");
        }

        // absolute prefix
        string pathRoot = null;
        var relative = normalized;
        if (OperatingSystem.IsWindows())
        {
            if (normalized.Length >= 2 && char.IsAsciiLetter(normalized[0]) && normalized[1] == ':')
            {
                if (normalized.Length == 2 || normalized[2] != '/')
                {
                    throw Denied(path, "drive-relative paths are not permitted");
                }
                pathRoot = normalized[..3];
                relative = normalized[3..];
            }
            else if (normalized.StartsWith('/'))
            {
                throw Denied(path, "rooted paths without drive are not permitted");
            }
        }
        else if (normalized.StartsWith('/'))
        {
            pathRoot = "/";
            relative = normalized[1..];
        }

        var segments = GetSegments(path, relative);
        var relativePath = string.Join(Path.DirectorySeparatorChar, segments);

        WorkspaceRoot root;
        string fullPath;
        if (pathRoot != null)
        {
            fullPath = Path.TrimEndingDirectorySeparator(
                Path.GetFullPath(NormalizeSeparators(pathRoot) + relativePath));
            root = roots.FirstOrDefault(candidate => IsWithin(fullPath, candidate.FullPath));
            if (root == null)
            {
                throw Denied(path, "outside the workspace roots");
            }
        }
        else
        {
            root = roots.FirstOrDefault(candidate => PathExists(Combine(candidate.FullPath, relativePath)))
                   ?? roots[0];
            fullPath = Combine(root.FullPath, relativePath);
        }

        var lexicalRelative = GetRelative(root.FullPath, fullPath);
        var repository = GetRepository(path, lexicalRelative);

        // links: the real path must stay below a root and inside a repository matching the mask
        var realPath = Path.TrimEndingDirectorySeparator(WorkspaceLinkResolver.GetRealPath(fullPath));
        var realRoot = roots.FirstOrDefault(candidate => IsWithin(realPath, candidate.RealPath));
        if (realRoot == null)
        {
            throw Denied(path, "a link leads outside the workspace roots");
        }
        GetRepository(path, GetRelative(realRoot.RealPath, realPath));

        return new WorkspacePath(root.FullPath, fullPath, realPath, lexicalRelative, repository);
    }

    private static List<string> GetSegments(string path, string relative)
    {
        var segments = new List<string>();
        var invalidChars = Path.GetInvalidFileNameChars();
        foreach (var segment in relative.Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            if (segment == ".")
            {
                continue;
            }
            if (segment == "..")
            {
                throw Denied(path, "parent segments are not permitted");
            }
            if (segment.Trim('.', ' ').Length == 0)
            {
                throw Denied(path, $"segment '{segment}' is not permitted");
            }
            if (segment.IndexOfAny(['*', '?', ':']) >= 0 || segment.IndexOfAny(invalidChars) >= 0)
            {
                throw Denied(path, $"segment '{segment}' contains invalid characters");
            }
            if (OperatingSystem.IsWindows())
            {
                if (segment.EndsWith('.') || segment.EndsWith(' '))
                {
                    throw Denied(path, $"segment '{segment}' ends with a dot or space");
                }
                if (IsWindowsDeviceName(segment))
                {
                    throw Denied(path, $"segment '{segment}' is a device name");
                }
            }
            segments.Add(segment);
        }
        return segments;
    }

    private string GetRepository(string path, string relative)
    {
        if (relative.Length == 0)
        {
            return null;
        }
        var separator = relative.IndexOf(Path.DirectorySeparatorChar);
        var repository = separator < 0 ? relative : relative[..separator];
        if (!IsRepositoryName(repository))
        {
            throw Denied(path, $"folder '{repository}' does not match the repository mask");
        }
        return repository;
    }

    private static bool IsWindowsDeviceName(string segment)
    {
        var dot = segment.IndexOf('.');
        var name = (dot < 0 ? segment : segment[..dot]).TrimEnd(' ');
        return WindowsDeviceNames.Contains(name, StringComparer.OrdinalIgnoreCase);
    }

    private static bool IsDevicePath(string path) =>
        path.StartsWith(@"\\", StringComparison.Ordinal) || path.StartsWith("//", StringComparison.Ordinal);

    private static string NormalizeSeparators(string path) =>
        path.Replace('\\', Path.DirectorySeparatorChar).Replace('/', Path.DirectorySeparatorChar);

    private static string Combine(string root, string relativePath) =>
        relativePath.Length == 0 ? root : Path.Combine(root, relativePath);

    private static bool PathExists(string path) =>
        Directory.Exists(path) || File.Exists(path);

    private static bool IsWithin(string path, string root)
    {
        if (string.Equals(path, root, PathComparison))
        {
            return true;
        }
        var prefix = Path.EndsInDirectorySeparator(root) ? root : root + Path.DirectorySeparatorChar;
        return path.StartsWith(prefix, PathComparison);
    }

    private static string GetRelative(string root, string path)
    {
        if (string.Equals(path, root, PathComparison))
        {
            return string.Empty;
        }
        return path[root.Length..].TrimStart(Path.DirectorySeparatorChar);
    }

    private static UnauthorizedAccessException Denied(string path, string reason) =>
        new($"Access denied: path '{path}': {reason}.");

    #endregion
}
