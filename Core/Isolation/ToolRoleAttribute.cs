using System;

namespace PayrollEngine.Mcp.Core.Isolation;

/// <summary>Declares the role and minimum permission required to register a tool class.
/// Applied to classes decorated with [McpServerToolType].
/// Tool classes without this attribute are always registered regardless of permissions.</summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class ToolRoleAttribute : Attribute
{
    /// <summary>The built-in functional domain this tool class belongs to.
    /// Only meaningful when <see cref="IsBuiltInRole"/> is true; otherwise use <see cref="RoleName"/>.</summary>
    public McpRole Role { get; }

    /// <summary>The role name: the name of a built-in role or of a role defined by the server (<see cref="McpRoleDefinition"/>).</summary>
    public string RoleName { get; }

    /// <summary>True when the role is a built-in role of <see cref="McpRole"/>.</summary>
    public bool IsBuiltInRole { get; }

    /// <summary>Minimum permission level required to register this tool class.
    /// Defaults to Read.</summary>
    public McpPermission Required { get; }

    /// <summary>When set, the tool class is only registered at exactly this isolation level.
    /// Null means the role × isolation-level compatibility matrix applies (default behaviour).</summary>
    public IsolationLevel? RequiredIsolationLevel { get; }

    /// <param name="role">The role this tool class belongs to.</param>
    /// <param name="required">Minimum permission to register. Default: Read.</param>
    public ToolRoleAttribute(McpRole role, McpPermission required = McpPermission.Read)
    {
        Role = role;
        RoleName = role.ToString();
        IsBuiltInRole = true;
        Required = required;
        RequiredIsolationLevel = null;
    }

    /// <param name="role">The role this tool class belongs to.</param>
    /// <param name="required">Minimum permission to register.</param>
    /// <param name="requiredIsolationLevel">Exact isolation level required.
    /// Use this for tools that are only meaningful at a specific isolation level
    /// (e.g. consolidation tools require MultiTenant).</param>
    public ToolRoleAttribute(McpRole role, McpPermission required, IsolationLevel requiredIsolationLevel)
    {
        Role = role;
        RoleName = role.ToString();
        IsBuiltInRole = true;
        Required = required;
        RequiredIsolationLevel = requiredIsolationLevel;
    }

    /// <param name="roleName">The name of a role defined by the server; a built-in role name selects the built-in role.</param>
    /// <param name="required">Minimum permission to register. Default: Read.</param>
    public ToolRoleAttribute(string roleName, McpPermission required = McpPermission.Read)
    {
        (Role, RoleName, IsBuiltInRole) = ResolveRole(roleName);
        Required = required;
        RequiredIsolationLevel = null;
    }

    /// <param name="roleName">The name of a role defined by the server; a built-in role name selects the built-in role.</param>
    /// <param name="required">Minimum permission to register.</param>
    /// <param name="requiredIsolationLevel">Exact isolation level required.</param>
    public ToolRoleAttribute(string roleName, McpPermission required, IsolationLevel requiredIsolationLevel)
    {
        (Role, RoleName, IsBuiltInRole) = ResolveRole(roleName);
        Required = required;
        RequiredIsolationLevel = requiredIsolationLevel;
    }

    private static (McpRole Role, string RoleName, bool IsBuiltInRole) ResolveRole(string roleName)
    {
        if (string.IsNullOrWhiteSpace(roleName))
        {
            throw new ArgumentException("The role name is empty.", nameof(roleName));
        }
        return McpRoleDefinition.TryGetBuiltIn(roleName, out var builtIn)
            ? (builtIn, builtIn.ToString(), true)
            : (default, roleName.Trim(), false);
    }
}
