using System;

namespace PayrollEngine.Mcp.Core.Isolation;

/// <summary>Holds the active data isolation and permission configuration for the MCP server session.
/// Registered as a singleton; populated from McpServer appsettings on startup.</summary>
public sealed class IsolationContext
{
    /// <summary>Active isolation level. Defaults to MultiTenant.</summary>
    public IsolationLevel Level { get; init; } = IsolationLevel.MultiTenant;

    /// <summary>Tenant identifier used when Level is Tenant, Division, or Employee.
    /// All tool calls are automatically scoped to this tenant.</summary>
    public string TenantIdentifier { get; init; }

    /// <summary>Division name used when Level is Division.
    /// All tool calls are automatically scoped to this division within the configured tenant.</summary>
    public string DivisionName { get; init; }

    /// <summary>Employee identifier used when Level is Employee.
    /// All tool calls are automatically scoped to this employee within the configured tenant.</summary>
    public string EmployeeIdentifier { get; init; }

    /// <summary>User identifier used as the service account for payrun preview and report execution.
    /// Required when the get_employee_pay_preview or execute_payroll_report tool is used.
    /// The user must exist in the target tenant.</summary>
    public string PreviewUserIdentifier { get; init; }

    /// <summary>Role permission levels for this deployment.
    /// Defaults to Full for all roles when absent from configuration.</summary>
    public McpPermissions Permissions { get; init; } = new();

    /// <summary>Validates the isolation context at startup.
    /// Throws if configuration is incomplete or uses an invalid combination.</summary>
    public void Validate()
    {
        if (Level == IsolationLevel.Tenant && string.IsNullOrWhiteSpace(TenantIdentifier))
        {
            throw new InvalidOperationException(
                "McpServer:TenantIdentifier is required when IsolationLevel is Tenant.");
        }

        if (Level == IsolationLevel.Division)
        {
            if (string.IsNullOrWhiteSpace(TenantIdentifier))
            {
                throw new InvalidOperationException(
                    "McpServer:TenantIdentifier is required when IsolationLevel is Division.");
            }
            if (string.IsNullOrWhiteSpace(DivisionName))
            {
                throw new InvalidOperationException(
                    "McpServer:DivisionName is required when IsolationLevel is Division.");
            }
        }

        if (Level == IsolationLevel.Employee)
        {
            if (string.IsNullOrWhiteSpace(TenantIdentifier))
            {
                throw new InvalidOperationException(
                    "McpServer:TenantIdentifier is required when IsolationLevel is Employee.");
            }
            if (string.IsNullOrWhiteSpace(EmployeeIdentifier))
            {
                throw new InvalidOperationException(
                    "McpServer:EmployeeIdentifier is required when IsolationLevel is Employee.");
            }
        }
    }
}
