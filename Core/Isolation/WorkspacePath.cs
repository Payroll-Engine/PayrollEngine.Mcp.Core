namespace PayrollEngine.Mcp.Core.Isolation;

/// <summary>A path argument resolved below a workspace root.
/// Created by <see cref="WorkspaceContext.ResolvePath"/> only.</summary>
public sealed class WorkspacePath
{
    /// <summary>The workspace root containing the path (full path, no trailing separator)</summary>
    public string Root { get; }

    /// <summary>The full path below the root, with platform separators</summary>
    public string FullPath { get; }

    /// <summary>The full path with all links resolved; equals <see cref="FullPath"/> without links.
    /// Lies below a workspace root as well.</summary>
    public string RealPath { get; }

    /// <summary>The path relative to the root; empty for the root itself</summary>
    public string RelativePath { get; }

    /// <summary>The repository folder name (first segment below the root); null for the root itself</summary>
    public string Repository { get; }

    /// <summary>True when the path is the workspace root itself</summary>
    public bool IsRoot => RelativePath.Length == 0;

    internal WorkspacePath(string root, string fullPath, string realPath, string relativePath, string repository)
    {
        Root = root;
        FullPath = fullPath;
        RealPath = realPath;
        RelativePath = relativePath;
        Repository = repository;
    }

    /// <inheritdoc />
    public override string ToString() => FullPath;
}
