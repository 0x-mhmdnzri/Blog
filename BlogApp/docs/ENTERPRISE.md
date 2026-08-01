# Enterprise features

Admin UI: `/AdminEnterprise` (SuperAdmin).

| Feature | Implementation |
|---------|----------------|
| Multi-tenant | `Tenant` + CRUD |
| Workspace isolation | `Workspace.IsIsolated` per tenant |
| Custom domains | `TenantDomain` + verification token |
| SSO OIDC/SAML | `SsoProviderConfig` stored; wire `AddOpenIdConnect` at deploy using saved values |
| Approval workflow | `ContentApprovalRequest` states Draft→Submitted→Approved/Rejected→Published |
| Content lifecycle | `ContentLifecycleRecord` Active/Review/Archive/Retire |
| Legal hold | Blocks GDPR erase & approval publish when active |
| GDPR export | JSON download of user posts/comments/consents |
| GDPR erase | Anonymize profile + redact comments (blocked by legal hold) |
| Backup & restore | Zip of SQLite + staging restore |
| DR runbook | `/AdminEnterprise/DrRunbook` |
| Localization management | Editorial `LocalizationEntry` key/lang/status |

Service: `IEnterpriseService` / `EnterpriseService`.
Schema: `SchemaBootstrap.EnsureEnterpriseTablesAsync`.
