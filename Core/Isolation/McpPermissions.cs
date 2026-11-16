using System;
using Microsoft.Extensions.Configuration;

namespace PayrollEngine.Mcp.Core.Isolation;

/// <summary>Permission levels per role for this MCP Server deployment.
/// Populated from McpServer:Permissions in appsettings.json or environment variables.
/// Default when the Permissions section is absent: Read for the backend roles and None for Regulator.</summary>
public sealed class McpPermissions
{
    /// <summary>Permission for HR tools (divisions, employees, case values).</summary>
    // ReSharper disable once InconsistentNaming
    public McpPermission HR { get; init; } = McpPermission.Read;

    /// <summary>Permission for Payroll tools (payrolls, payruns, jobs, temporal queries).</summary>
    public McpPermission Payroll { get; init; } = McpPermission.Read;

    /// <summary>Permission for Report tools (payroll report execution).</summary>
    public McpPermission Report { get; init; } = McpPermission.Read;

    /// <summary>Permission for System tools (tenant and user queries).</summary>
    public McpPermission System { get; init; } = McpPermission.Read;

    /// <summary>Permission for Regulator tools (file based regulation authoring).
    /// Defaults to None; must be granted explicitly via McpServer:Permissions:Regulator.</summary>
    public McpPermission Regulator { get; init; } = McpPermission.None;

    /// <summary>Returns the permission granted for the given role.</summary>
    private McpPermission GetPermission(McpRole role) => role switch
    {
        McpRole.HR      => HR,
        McpRole.Payroll => Payroll,
        McpRole.Report  => Report,
        McpRole.System  => System,
        McpRole.Regulator => Regulator,
        _ => throw new ArgumentOutOfRangeException(nameof(role), role, null)
    };

    /// <summary>Returns true if the given role is granted at least the required permission.</summary>
    public bool IsGranted(McpRole role, McpPermission required) =>
        GetPermission(role) >= required;

    /// <summary>Reads McpServer:Permissions from configuration.
    /// Returns the default instance when the section is absent. A missing or empty key in an existing section is None.</summary>
    /// <exception cref="InvalidOperationException">A key holds a value that is not a permission name (None, Read, Write)</exception>
    public static McpPermissions FromConfiguration(IConfiguration configuration)
    {
        var section = configuration.GetSection("McpServer:Permissions");
        if (!section.Exists())
            return new McpPermissions();

        return new McpPermissions
        {
            HR      = ParsePermission(section, "HR"),
            Payroll = ParsePermission(section, "Payroll"),
            Report  = ParsePermission(section, "Report"),
            System  = ParsePermission(section, "System"),
            Regulator = ParsePermission(section, "Regulator")
        };
    }

    private static McpPermission ParsePermission(IConfigurationSection section, string key)
    {
        var value = section[key];
        if (string.IsNullOrWhiteSpace(value))
        {
            return McpPermission.None;
        }

        // compare against the names: Enum.TryParse also accepts numbers and comma lists
        foreach (var name in Enum.GetNames<McpPermission>())
        {
            if (string.Equals(name, value.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                return Enum.Parse<McpPermission>(name);
            }
        }

        throw new InvalidOperationException(
            $"{section.Path}:{key} has the invalid value '{value}'. " +
            $"Allowed values: {string.Join(", ", Enum.GetNames<McpPermission>())}.");
    }
}
