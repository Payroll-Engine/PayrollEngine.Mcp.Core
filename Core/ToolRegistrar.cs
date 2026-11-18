using System;
using System.Collections.Generic;
using System.Reflection;
using ModelContextProtocol.Server;
using PayrollEngine.Mcp.Core.Isolation;

namespace PayrollEngine.Mcp.Core;

/// <summary>Filters and registers MCP tool classes based on the active McpPermissions
/// and the active IsolationLevel.
/// Tool classes tagged with [ToolRole] are only registered when:
///   (a) the deployment grants at least the required permission for that role, AND
///   (b) the role is applicable for the active isolation level (per Role x IsolationLevel matrix), AND
///   (c) when RequiredIsolationLevel is set: the active isolation level matches exactly.
/// Tool classes without [ToolRole] are always registered.
/// Roles defined by the server (<see cref="McpRoleDefinition"/>) use their own isolation levels instead of the matrix.</summary>
public static class ToolRegistrar
{
    /// <summary>Returns all tool types from the given assembly that pass the permission
    /// and isolation-level compatibility filter.</summary>
    public static IEnumerable<Type> GetPermittedTypes(
        Assembly assembly,
        McpPermissions permissions,
        IsolationLevel isolationLevel) =>
        GetPermittedTypes(assembly, permissions, isolationLevel, []);

    /// <summary>Returns all tool types from the given assembly that pass the permission
    /// and isolation-level compatibility filter, including tool classes of the roles defined by the server.</summary>
    /// <param name="assembly">The tool assembly</param>
    /// <param name="permissions">The role permissions</param>
    /// <param name="isolationLevel">The active isolation level</param>
    /// <param name="roles">The roles defined by the server</param>
    /// <remarks>A tool class whose role is neither built-in nor defined is not registered.</remarks>
    /// <exception cref="ArgumentException">A role is defined more than once</exception>
    public static IEnumerable<Type> GetPermittedTypes(
        Assembly assembly,
        McpPermissions permissions,
        IsolationLevel isolationLevel,
        IEnumerable<McpRoleDefinition> roles)
    {
        var definitions = McpPermissions.ToRoleMap(roles);
        return FilterTypes(assembly, permissions, isolationLevel, definitions);
    }

    private static IEnumerable<Type> FilterTypes(
        Assembly assembly,
        McpPermissions permissions,
        IsolationLevel isolationLevel,
        Dictionary<string, McpRoleDefinition> definitions)
    {
        foreach (var type in assembly.GetTypes())
        {
            // must be a concrete MCP tool class
            if (!type.IsClass || type.IsAbstract)
                continue;
            if (type.GetCustomAttribute<McpServerToolTypeAttribute>() == null)
                continue;

            var roleAttr = type.GetCustomAttribute<ToolRoleAttribute>();
            if (roleAttr == null)
            {
                // no role restriction — always register
                yield return type;
                continue;
            }

            // a role the server does not define is never granted
            McpRoleDefinition definition = null;
            if (!roleAttr.IsBuiltInRole && !definitions.TryGetValue(roleAttr.RoleName, out definition))
                continue;

            // exact isolation level required (overrides the compatibility matrix)
            if (roleAttr.RequiredIsolationLevel.HasValue)
            {
                if (isolationLevel != roleAttr.RequiredIsolationLevel.Value)
                    continue;
            }
            else if (definition != null)
            {
                // role defined by the server: its own isolation levels
                if (!definition.IsApplicable(isolationLevel))
                    continue;
            }
            else
            {
                // Role x IsolationLevel compatibility check (README matrix)
                if (!IsRoleCompatibleWithIsolationLevel(roleAttr.Role, isolationLevel))
                    continue;
            }

            if (permissions.IsGranted(roleAttr.RoleName, roleAttr.Required))
                yield return type;
        }
    }

    /// <summary>Returns true when the given role is applicable for the given isolation level.
    /// Based on the Role x IsolationLevel matrix defined in README.md:
    /// HR: all levels; Payroll: all except Employee;
    /// Report, System and Regulator: MultiTenant and Tenant only.</summary>
    private static bool IsRoleCompatibleWithIsolationLevel(McpRole role, IsolationLevel level) =>
        (role, level) switch
        {
            // HR is applicable at all isolation levels
            (McpRole.HR, _) => true,

            // Payroll: not applicable at Employee level
            (McpRole.Payroll, IsolationLevel.Employee) => false,
            (McpRole.Payroll, _) => true,

            // Report: only applicable at MultiTenant and Tenant
            (McpRole.Report, IsolationLevel.MultiTenant) => true,
            (McpRole.Report, IsolationLevel.Tenant)      => true,
            (McpRole.Report, _) => false,

            // System: only applicable at MultiTenant and Tenant
            (McpRole.System, IsolationLevel.MultiTenant) => true,
            (McpRole.System, IsolationLevel.Tenant)      => true,
            (McpRole.System, _) => false,

            // Regulator: file based tools, valid without data isolation to a single division or employee
            (McpRole.Regulator, IsolationLevel.MultiTenant) => true,
            (McpRole.Regulator, IsolationLevel.Tenant)      => true,
            (McpRole.Regulator, _) => false,

            _ => true
        };
}
