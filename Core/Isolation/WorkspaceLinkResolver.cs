using System;
using System.IO;

namespace PayrollEngine.Mcp.Core.Isolation;

/// <summary>Resolves symbolic links and junctions in every segment of a full path.
/// Segments that do not exist are appended unchanged.</summary>
internal static class WorkspaceLinkResolver
{
    /// <summary>Maximum number of links followed for one path (guards against link cycles)</summary>
    internal const int MaxLinkHops = 32;

    private const string ExtendedPrefix = @"\\?\";
    private const string ExtendedUncPrefix = @"\\?\UNC\";

    /// <summary>Returns the full path with all links resolved.</summary>
    /// <param name="fullPath">Normalized full path</param>
    internal static string GetRealPath(string fullPath)
    {
        var hops = 0;
        return Resolve(fullPath, ref hops);
    }

    private static string Resolve(string fullPath, ref int hops)
    {
        var pathRoot = Path.GetPathRoot(fullPath);
        if (string.IsNullOrEmpty(pathRoot))
        {
            throw new UnauthorizedAccessException($"Access denied: path '{fullPath}' is not fully qualified.");
        }

        var segments = fullPath[pathRoot.Length..].Split(
            Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries);
        var current = pathRoot;
        for (var i = 0; i < segments.Length; i++)
        {
            var next = Path.Combine(current, segments[i]);
            var linkTarget = GetLinkTarget(next);
            if (linkTarget != null)
            {
                if (++hops > MaxLinkHops)
                {
                    throw new UnauthorizedAccessException(
                        $"Access denied: too many links in path '{fullPath}'.");
                }
                current = Resolve(linkTarget, ref hops);
                continue;
            }

            if (!Directory.Exists(next) && !File.Exists(next))
            {
                // missing segments cannot contain links
                return Path.Combine([next, .. segments[(i + 1)..]]);
            }
            current = next;
        }
        return current;
    }

    /// <summary>Returns the full path of the immediate link target, or null when the path is no link.
    /// Dangling links return their target as well.</summary>
    private static string GetLinkTarget(string path)
    {
        string target;
        try
        {
            target = new FileInfo(path).LinkTarget;
        }
        catch (IOException)
        {
            return null;
        }
        if (string.IsNullOrEmpty(target))
        {
            return null;
        }

        if (target.StartsWith(ExtendedUncPrefix, StringComparison.Ordinal))
        {
            target = @"\\" + target[ExtendedUncPrefix.Length..];
        }
        else if (target.StartsWith(ExtendedPrefix, StringComparison.Ordinal))
        {
            target = target[ExtendedPrefix.Length..];
        }

        var directory = Path.GetDirectoryName(path) ?? Path.GetPathRoot(path);
        return Path.TrimEndingDirectorySeparator(Path.GetFullPath(target, directory!));
    }
}
