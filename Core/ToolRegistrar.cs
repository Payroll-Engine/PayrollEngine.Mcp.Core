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
/// Tool classes without [ToolRole] are always registered.</summary>
public static class ToolRegistrar
{
    /// <summary>Returns all tool types from the given assembly that pass the permission
    /// and isolation-level compatibility filter.</summary>
    public static IEnumerable<Type> GetPermittedTypes(
        Assembly assembly,
        McpPermissions permissions,
        IsolationLevel isolationLevel)
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

            // exact isolation level required (overrides the compatibility matrix)
            if (roleAttr.RequiredIsolationLevel.HasValue)
            {
                if (isolationLevel != roleAttr.RequiredIsolationLevel.Value)
                    continue;
            }
            else
            {
                // Role x IsolationLevel compatibility check (README matrix)
                if (!IsRoleCompatibleWithIsolationLevel(roleAttr.Role, isolationLevel))
                    continue;
            }

            if (permissions.IsGranted(roleAttr.Role, roleAttr.Required))
                yield return type;
        }
    }

    /// <summary>Returns true when the given role is applicable for the given isolation level.
    /// Based on the Role x IsolationLevel matrix defined in README.md:
    /// HR: all levels; Payroll: all except Employee;
    /// Report and System: MultiTenant and Tenant only.</summary>
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

            _ => true
        };
}
