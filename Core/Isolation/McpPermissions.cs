using System;
using System.Collections.Generic;
using Microsoft.Extensions.Configuration;

namespace PayrollEngine.Mcp.Core.Isolation;

/// <summary>Permission levels per role for this MCP Server deployment.
/// Populated from McpServer:Permissions in appsettings.json or environment variables.
/// Default when the Permissions section is absent: Read for the backend roles and None for Regulator
/// and for the roles defined by the server (<see cref="McpRoleDefinition"/>).</summary>
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

    /// <summary>Permissions of the roles defined by the server, by role name (ignoring case).
    /// A defined role without entry is None.</summary>
    public IReadOnlyDictionary<string, McpPermission> CustomRoles { get; init; } =
        new Dictionary<string, McpPermission>(StringComparer.OrdinalIgnoreCase);

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

    /// <summary>Returns the permission granted for the given role name: a built-in role or a role defined by the server.
    /// An unknown name is None.</summary>
    /// <param name="roleName">The role name (ignoring case)</param>
    public McpPermission GetPermission(string roleName)
    {
        if (string.IsNullOrWhiteSpace(roleName))
        {
            throw new ArgumentException("The role name is empty.", nameof(roleName));
        }

        if (McpRoleDefinition.TryGetBuiltIn(roleName, out var builtIn))
        {
            return GetPermission(builtIn);
        }

        foreach (var customRole in CustomRoles)
        {
            if (string.Equals(customRole.Key, roleName.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                return customRole.Value;
            }
        }
        return McpPermission.None;
    }

    /// <summary>Returns true if the given role is granted at least the required permission.</summary>
    public bool IsGranted(McpRole role, McpPermission required) =>
        GetPermission(role) >= required;

    /// <summary>Returns true if the given role (built-in or defined by the server) is granted at least the required permission.</summary>
    /// <param name="roleName">The role name (ignoring case)</param>
    /// <param name="required">The minimum required permission</param>
    public bool IsGranted(string roleName, McpPermission required) =>
        GetPermission(roleName) >= required;

    /// <summary>Reads McpServer:Permissions from configuration.
    /// Returns the default instance when the section is absent. A missing or empty key in an existing section is None.</summary>
    /// <exception cref="InvalidOperationException">A key holds a value that is not a permission name (None, Read, Write)</exception>
    public static McpPermissions FromConfiguration(IConfiguration configuration) =>
        FromConfiguration(configuration, []);

    /// <summary>Reads McpServer:Permissions from configuration, including the permissions of the roles defined by the server.
    /// Returns the default instance when the section is absent. A missing or empty key in an existing section is None.</summary>
    /// <param name="configuration">The application configuration</param>
    /// <param name="roles">The roles defined by the server</param>
    /// <exception cref="ArgumentException">A role is defined more than once</exception>
    /// <exception cref="InvalidOperationException">A key holds a value that is not a permission name (None, Read, Write)</exception>
    public static McpPermissions FromConfiguration(IConfiguration configuration, IEnumerable<McpRoleDefinition> roles)
    {
        var definitions = ToRoleMap(roles);
        var section = configuration.GetSection("McpServer:Permissions");
        if (!section.Exists())
            return new McpPermissions();

        var customRoles = new Dictionary<string, McpPermission>(StringComparer.OrdinalIgnoreCase);
        foreach (var definition in definitions.Values)
        {
            customRoles[definition.Name] = ParsePermission(section, definition.Name);
        }

        return new McpPermissions
        {
            HR      = ParsePermission(section, "HR"),
            Payroll = ParsePermission(section, "Payroll"),
            Report  = ParsePermission(section, "Report"),
            System  = ParsePermission(section, "System"),
            Regulator = ParsePermission(section, "Regulator"),
            CustomRoles = customRoles
        };
    }

    /// <summary>Maps the role definitions by name (ignoring case)</summary>
    /// <param name="roles">The roles defined by the server, may be null</param>
    /// <exception cref="ArgumentException">A role is defined more than once</exception>
    internal static Dictionary<string, McpRoleDefinition> ToRoleMap(IEnumerable<McpRoleDefinition> roles)
    {
        var map = new Dictionary<string, McpRoleDefinition>(StringComparer.OrdinalIgnoreCase);
        if (roles == null)
        {
            return map;
        }
        foreach (var role in roles)
        {
            if (role == null)
            {
                throw new ArgumentException("A role definition is null.", nameof(roles));
            }
            if (!map.TryAdd(role.Name, role))
            {
                throw new ArgumentException($"The role '{role.Name}' is defined more than once.", nameof(roles));
            }
        }
        return map;
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
