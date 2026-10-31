# Azure isolation

Subscription: Microsoft Azure Sponsorship `5f5765ae-985f-42e4-a443-8e2447be33ca`.

Resource group: `rg-lip-web-prod` in `westus2`.

Nothing in this group is shared with `rg-cip-web-prod`:

- App Service plan `asp-lip-web`
- Web `app-lip-web`
- API `app-lip-web-api`
- SQL server `sql-lip-web-lip01` (`sql-lip-web-lip01.database.windows.net`) and database `db-lip-web`
- Key Vault `kvlipweblip01`
- Storage `stlipweblip01` with public blob access disabled
- Log Analytics `log-lip-web` and Application Insights `appi-lip-web`

The API uses a system-assigned managed identity with Key Vault Secrets User on the LIP vault only. SQL allows Azure-hosted services to connect and requires TLS 1.2. The web and API accept HTTPS only.

The plan SKU is S1 so the app can scale out. The database SKU is Basic for this demonstration and is a parameter, not a contractual capacity claim. A second region and Front Door are not deployed.

Public sites: `https://app-lip-web.azurewebsites.net` and `https://app-lip-web-api.azurewebsites.net`.

Demo authentication signs tokens with `DemoAuth` on the LIP API. It does not read CIP Entra app registrations. The SQL administrator password and the demo signing key live only in the API application settings.
