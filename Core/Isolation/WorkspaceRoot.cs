namespace PayrollEngine.Mcp.Core.Isolation;

/// <summary>A configured workspace root with its full path and its real path (links resolved)</summary>
/// <param name="FullPath">Full path without trailing separator</param>
/// <param name="RealPath">Full path with all links resolved</param>
internal sealed record WorkspaceRoot(string FullPath, string RealPath);
