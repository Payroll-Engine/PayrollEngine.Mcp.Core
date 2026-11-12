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
- **Workspace isolation** — file paths confined below configured root folders
- **Permission system** — role-based tool visibility controlled at startup
- **`IsolationToolBase`** — abstract base class for all MCP tool classes, without backend client; provides isolation, permission checks, workspace path resolution, uniform error handling, and integrity signing
- **`ToolBase`** — abstract base class for backend tool classes; adds typed service factories, identifier resolvers, and isolation-aware query helpers
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

## Workspace Isolation

Controls **which files** file based tools may access. The workspace is configured in `McpServer:Regulator`; without this section no file access is possible.

```json
{
  "McpServer": {
    "Regulator": {
      "Roots": [ "C:/Repos" ],
      "RepoMask": "Repo.*",
      "Identity": "Jane Doe",
      "AgentIdentities": [ "Example Agent" ],
      "Backend": "Read",
      "Tenants": [ "XX.Dev" ]
    }
  }
}
```

| Setting | Description |
|:--------|:------------|
| `Roots` | Permitted root folders, fully qualified and existing (array or semicolon separated string) |
| `RepoMask` | Wildcard mask (`*`, `?`) for the repository folders directly below a root; empty: every folder |
| `Identity` | Human identity for records that require a person |
| `AgentIdentities` | Names of automated agents (array or semicolon separated string). A tool can refuse a record whose person is one of these names; empty: none configured |
| `Backend` | Backend access of the file based tools: `None` (default, offline), `Read`, `Write` |
| `Tenants` | Allowlist of tenants for backend writes; empty: no backend writes. In `Tenant`, `Division` and `Employee` isolation only the isolated tenant is permitted |

`WorkspaceContext.ResolvePath` resolves every path argument below a root and rejects:

- parent segments (`..`) and segments of dots and spaces only
- absolute paths outside the roots, including folders whose name only starts with a root name
- links (symbolic links, junctions) whose target leaves the roots or the repository mask
- network, device and extended paths (`\\server\share`, `\\?\`, `\\.\`), drive-relative paths (`C:folder`) and stream names (`file:stream`)
- on Windows: rooted paths without drive (`\folder`, `/folder`), device names (`NUL`, `CON.txt`) and segments ending with a dot or space
- repository folders not matching `RepoMask`

`\` and `/` are both accepted as separators on every platform. Relative paths resolve against the first root containing the path, otherwise against the first root.

### Known path validation limits

Path checks run when a tool resolves the path argument. They do not run again when the tool opens files:

- A symbolic link or junction created **after** a path was accepted can point outside the workspace; another process with write access to the workspace could exploit that race.
- On Windows, paths may use **8.3 short names** (for example `REPO~1`). The resolver does not normalize short names to their long form; `RepoMask` matches the repository folder name as given in the path.

Deployments should avoid writable links inside the workspace roots and should configure stable long folder names below the roots.

---

## Roles and Permissions

When the `McpServer:Permissions` section is absent, `HR`, `Payroll`, `Report` and `System` default to `Read` while `Regulator` defaults to `None` (no file tools).

Controls **which tools** are registered at startup. Each tool class is tagged with a `[ToolRole]` attribute declaring its role and minimum permission. A tool whose role is not granted is never registered — invisible to the AI agent.

| Role | Domain |
|:-----|:-------|
| `HR` | Employee master data and case values |
| `Payroll` | Payroll execution and results |
| `Report` | Report execution |
| `System` | Tenant and user management |
| `Regulator` | File based regulation authoring (default permission `None`, granted via `McpServer:Permissions:Regulator`) |

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
| Regulator | ✓ | ✓ | ✗ | ✗ |

---

## Architecture

### `IsolationContext`

Singleton populated at startup from `appsettings.json` or environment variables. Holds the active `IsolationLevel`, tenant/division/employee identifiers, `McpPermissions`, and the `WorkspaceContext`. Injected into every tool class via the constructor. `Validate()` checks the isolation level and the workspace at startup.

### `WorkspaceContext`

Workspace isolation, read with `WorkspaceContext.FromConfiguration` from `McpServer:Regulator`. Provides `ResolvePath` (returns a `WorkspacePath` with root, full path, real path, relative path and repository), `IsRepositoryName` and `IsTenantAllowed`.

### `IsolationToolBase`

Abstract base class for all MCP tool classes, without backend client. Constructor injection: `IsolationContext`.

| Category | Members |
|:---------|:--------|
| Isolation | `Isolation`, `Workspace` |
| Permissions | `IsGranted` |
| Workspace | `ResolveWorkspacePath` |
| Error handling | `Error(Exception)` — serializes exceptions as structured JSON for MCP clients |
| Integrity | `SignWithIntegrity` — HMAC-SHA256 signed envelope when `IntegrityKey` is configured |

### `ToolBase`

Abstract base class for backend tool classes, derived from `IsolationToolBase`. Constructor injection: `PayrollHttpClient` + `IsolationContext`.

Key capabilities:

| Category | Members |
|:---------|:--------|
| Query helpers | `ActiveQuery`, `IsolatedTenantQuery`, `IsolatedEmployeeQuery`, `IsolatedPayrollQueryAsync`, `IsolatedDivisionQueryAsync` |
| Isolation guards | `FilterEmployeesByIsolation`, `AssertEmployeeInDivision` |
| Resolvers | `ResolveTenantAsync`, `ResolveEmployeeAsync`, `ResolveDivisionAsync`, `ResolvePayrollContextAsync`, `ResolveLookupContextAsync`, `ResolveReportAsync` |
| Service factories | `TenantService`, `EmployeeService`, `PayrollService`, `PayrunJobService`, `ReportService`, and more |

### Tool base hierarchy

| Base class | Backend client | Typical tools |
|:-----------|:---------------|:--------------|
| `IsolationToolBase` | No | File based tools (`Regulator`), `get_server_info` |
| `ToolBase` | Yes (`PayrollHttpClient`) | `HR`, `Payroll`, `Report`, `System` query tools |

File based tool classes take only `IsolationContext`, call `ResolveWorkspacePath` for every path argument, and never need a payroll backend for file I/O.

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
