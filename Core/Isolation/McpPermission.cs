namespace PayrollEngine.Mcp.Core.Isolation;

/// <summary>Access level granted for a role in this MCP Server deployment.
/// Ordered: None &lt; Read &lt; Write — use &gt;= comparisons for minimum permission checks.</summary>
public enum McpPermission
{
    /// <summary>Role tools are not registered — invisible to the AI agent.</summary>
    None,

    /// <summary>Read and query tools only.</summary>
    Read,

    /// <summary>Read and write tools. Includes all Read tools plus mutation operations
    /// (case changes, payrun execution, employee mutations).
    /// Available in PayrollEngine.Mcp.Server.Pro only.</summary>
    Write
}
