# Look versus flow

LIP looks like the CIP web application. The journeys are the ones in the LIP BRD v2.1, FSD v1.1, and TSD v1.1.

## Copied from CIP

- Dashboard chrome: light page `#f7f8fc`, white sidebar, blue active nav `#eff6ff` / `#1d4ed8`, Inter, 12px cards, collapsed rail, top bar.
- Tailwind tokens from CIP (`navy`, `primary.blue`, surfaces, shadows).
- Hosting shape: Linux App Service plan, Next.js standalone web app, .NET API, Azure SQL, Key Vault, storage with public blob access off, Application Insights, Log Analytics.
- Sign-in gate before the workspace, and a separate operator console.

The logo is the LIP mark. CIP client IDs, resource groups, Key Vaults, databases, and storage accounts are not used.

## Defined by the LIP documents

- Four environments: customer interaction, internal dashboard, AI Legal Command Centre, Otic administration.
- Navigation: Overview, Inbox, Clients and Stakeholders, Matters, Legal Workbench, Contracts, Litigation, Compliance, Calendar, Acquisition and Conversion, Reports, AI Command Centre, Settings. Corporate mode hides the sales-style acquisition funnel.
- Screens UI-C01, UI-C02, UI-I01 through UI-I06, UI-A01, UI-O01.
- Workflows WF-01 to WF-08 and the WhatsApp and corporate intake paths.
- State control: enquiry origin is immutable, a matter with a potential conflict stays in `conflict_review`, a document is issued only after a different person approves it, an Act command waits in `AWAITING_APPROVAL`.
- Research abstains when the demonstration corpus has no authority. The seeded Employment Act passage is labelled demonstration material, not a licensed corpus.
- Ethical walls remove a matter from lists, Client 360, and Command Centre retrieval.
- Otic administration returns tenant and usage metadata and an empty document list.

## Later releases

Microsoft 365, Teams, finance, and court e-filing are visible as connector records. They use LIP demo adapters. They do not call CIP.
