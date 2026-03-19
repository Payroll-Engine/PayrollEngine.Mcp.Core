using System;
using Microsoft.Extensions.Configuration;

namespace PayrollEngine.Mcp.Core.Isolation;

/// <summary>Permission levels per role for this MCP Server deployment.
/// Populated from McpServer:Permissions in appsettings.json or environment variables.
/// Default when the Permissions section is absent: Read for all roles.
/// The MCP Server is read-only by design — no write tools exist.</summary>
public sealed class McpPermissions
{
    /// <summary>Permission for HR tools (divisions, employees, case values).</summary>
    public McpPermission HR { get; init; } = McpPermission.Read;

    /// <summary>Permission for Payroll tools (payrolls, payruns, jobs, temporal queries).</summary>
    public McpPermission Payroll { get; init; } = McpPermission.Read;

    /// <summary>Permission for Report tools (payroll report execution).</summary>
    public McpPermission Report { get; init; } = McpPermission.Read;

    /// <summary>Permission for System tools (tenant and user queries).</summary>
    public McpPermission System { get; init; } = McpPermission.Read;

    /// <summary>Returns the permission granted for the given role.</summary>
    private McpPermission GetPermission(McpRole role) => role switch
    {
        McpRole.HR      => HR,
        McpRole.Payroll => Payroll,
        McpRole.Report  => Report,
        McpRole.System  => System,
        _ => throw new ArgumentOutOfRangeException(nameof(role), role, null)
    };

    /// <summary>Returns true if the given role is granted at least the required permission.</summary>
    public bool IsGranted(McpRole role, McpPermission required) =>
        GetPermission(role) >= required;

    /// <summary>Reads McpServer:Permissions from configuration.
    /// Returns a fully-read instance when the section is absent.</summary>
    public static McpPermissions FromConfiguration(IConfiguration configuration)
    {
        var section = configuration.GetSection("McpServer:Permissions");
        if (!section.Exists())
            return new McpPermissions();

        return new McpPermissions
        {
            HR      = ParsePermission(section["HR"]),
            Payroll = ParsePermission(section["Payroll"]),
            Report  = ParsePermission(section["Report"]),
            System  = ParsePermission(section["System"])
        };
    }

    private static McpPermission ParsePermission(string value) =>
        Enum.TryParse<McpPermission>(value, ignoreCase: true, out var result)
            ? result
            : McpPermission.None;
}
