namespace PayrollEngine.Mcp.Core.Isolation;

/// <summary>Data isolation level for the MCP server.
/// Controls which tenants, divisions, and employees are accessible within a session.</summary>
public enum IsolationLevel
{
    /// <summary>Full access across all tenants. No automatic scoping is applied.</summary>
    MultiTenant,

    /// <summary>Access restricted to a single tenant configured at startup via McpServer:TenantIdentifier.</summary>
    Tenant,

    /// <summary>Access restricted to a single division within a tenant.
    /// Requires McpServer:TenantIdentifier and McpServer:DivisionName.</summary>
    Division,

    /// <summary>Access restricted to a single employee — self-service scenarios.
    /// Requires McpServer:TenantIdentifier and McpServer:EmployeeIdentifier.</summary>
    Employee
}
