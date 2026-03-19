# Payroll Engine MCP Core

> Part of the [Payroll Engine](https://github.com/Payroll-Engine/PayrollEngine) open-source payroll automation framework.
> Full documentation at [payrollengine.org](https://payrollengine.org).

Core infrastructure for building Payroll Engine MCP servers. Provides the base classes, isolation model, and permission system shared across all MCP server implementations — both read-only and write-enabled.

---

## NuGet Package

Available as a private GitHub Package:

```sh
dotnet add package PayrollEngine.Mcp.Core
```

---

## Overview

`PayrollEngine.Mcp.Core` is the foundation for any MCP server that exposes Payroll Engine data to AI agents. It defines:

- **Isolation model** — tenant, division, and employee scoping enforced at the infrastructure level
- **Permission system** — role-based tool visibility controlled at startup
- **`ToolBase`** — abstract base class for all MCP tool classes; provides typed service factories, identifier resolvers, isolation-aware query helpers, and uniform error handling
- **`ToolRegistrar`** — filters tool classes by role permission and isolation level compatibility at registration time

---

## Isolation Level

Controls **which records** are returned at runtime. Enforcement is server-side and cannot be bypassed by the AI agent.

| Value | Description |
|:------|:------------|
| `MultiTenant` | Full access across all tenants (default) |
| `Tenant` | All tool calls scoped to a single configured tenant |
| `Division` | Scoped to a single division within a tenant. Requires `TenantIdentifier` and `DivisionName`. |
| `Employee` | Self-service — single employee access. Requires `TenantIdentifier` and `EmployeeIdentifier`. |

---

## Roles and Permissions

Controls **which tools** are registered at startup. Each tool class is tagged with a `[ToolRole]` attribute declaring its role and minimum permission. A tool whose role is not granted is never registered — invisible to the AI agent.

| Role | Domain |
|:-----|:-------|
| `HR` | Employee master data and case values |
| `Payroll` | Payroll execution and results |
| `Report` | Report execution |
| `System` | Tenant and user management |

| Permission | Description |
|:-----------|:------------|
| `None` | Tools not registered |
| `Read` | Query tools registered (default) |

### Role × Isolation Level Compatibility

`✓` = role applicable  `✗` = not applicable at this isolation level

| Role | MultiTenant | Tenant | Division | Employee |
|:-----|:-----------:|:------:|:--------:|:--------:|
| HR | ✓ | ✓ | ✓ | ✓ |
| Payroll | ✓ | ✓ | ✓ | ✗ |
| Report | ✓ | ✓ | ✗ | ✗ |
| System | ✓ | ✓ | ✗ | ✗ |

---

## Architecture

### `IsolationContext`

Singleton populated at startup from `appsettings.json` or environment variables. Holds the active `IsolationLevel`, tenant/division/employee identifiers, and `McpPermissions`. Injected into every tool class via the constructor.

### `ToolBase`

Abstract base class for all MCP tool classes. Constructor injection: `PayrollHttpClient` + `IsolationContext`.

Key capabilities:

| Category | Members |
|:---------|:--------|
| Query helpers | `ActiveQuery`, `IsolatedTenantQuery`, `IsolatedEmployeeQuery`, `IsolatedPayrollQueryAsync`, `IsolatedDivisionQueryAsync` |
| Isolation guards | `FilterEmployeesByIsolation`, `AssertEmployeeInDivision` |
| Resolvers | `ResolveTenantAsync`, `ResolveEmployeeAsync`, `ResolveDivisionAsync`, `ResolvePayrollContextAsync`, `ResolveLookupContextAsync`, `ResolveReportAsync` |
| Service factories | `TenantService`, `EmployeeService`, `PayrollService`, `PayrunJobService`, `ReportService`, and more |
| Error handling | `Error(Exception)` — serializes exceptions as structured JSON for MCP clients |

### `ToolRegistrar`

Scans an assembly for classes decorated with `[McpServerToolType]` and filters them by:

1. `[ToolRole]` attribute — role and minimum permission check
2. Role × IsolationLevel compatibility matrix

Classes without `[ToolRole]` are always registered (e.g. `ServerInfoTools`).

### `ToolRoleAttribute`

Applied to MCP tool classes to declare their role and minimum required permission:

```csharp
[McpServerToolType]
[ToolRole(McpRole.HR, McpPermission.Read)]
public sealed class EmployeeQueryTools(PayrollHttpClient http, IsolationContext iso)
    : ToolBase(http, iso) { ... }
```

---

## Extending: Building a Custom MCP Server

Any MCP server — read-only or write-enabled — inherits the full infrastructure by referencing this package:

```xml
<PackageReference Include="PayrollEngine.Mcp.Core" Version="0.10.0-beta.3" />
```

A write tool follows the same pattern as a read tool:

```csharp
[McpServerToolType]
[ToolRole(McpRole.Payroll, McpPermission.Read)]
public sealed class CaseChangeTools(PayrollHttpClient http, IsolationContext iso)
    : ToolBase(http, iso)
{
    [McpServerTool(Name = "create_case_change")]
    public async Task<string> CreateCaseChangeAsync(...)
    {
        try
        {
            var (context, employee) = await ResolveEmployeeAsync(...);
            // write logic
        }
        catch (Exception ex) { return Error(ex); }
    }
}
```

All isolation enforcement, resolver logic, and error handling are inherited automatically.

---

## Related Repositories

| Repository | Description |
|:-----------|:------------|
| [PayrollEngine.Mcp.Tools](https://github.com/Payroll-Engine/PayrollEngine.Mcp.Tools) | Read-only MCP tools built on this library |
| [PayrollEngine.Mcp.Server](https://github.com/Payroll-Engine/PayrollEngine.Mcp.Server) | Hosted MCP server using Core and Tools |
| [PayrollEngine.Client.Core](https://github.com/Payroll-Engine/PayrollEngine.Client.Core) | Payroll Engine REST API client |

---

## License

[MIT License](LICENSE) — free for personal and commercial use.
