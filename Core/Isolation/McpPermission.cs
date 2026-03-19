namespace PayrollEngine.Mcp.Core.Isolation;

/// <summary>Access level granted for a role in this MCP Server deployment.
/// Ordered: None &lt; Read — use &gt;= comparisons for minimum permission checks.
/// The MCP Server is read-only by design: no write tools are registered regardless of configuration.</summary>
public enum McpPermission
{
    /// <summary>Role tools are not registered — invisible to the AI agent.</summary>
    None,

    /// <summary>Read and query tools only. The MCP Server is read-only by design.</summary>
    Read
}
