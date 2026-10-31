# LIP page implementation plan

Each page is built on its own, to the CIP visual pattern named below, until a person can finish the legal workflow on demonstration data. Commit, push, and deploy happen only in the last phase.

The dashboard home is the AI Legal Command Centre. There is no Overview page. The figures that used to sit on Overview move to Reports.

CIP credit features stay out: scorecards, memos, PD/LGD, borrower 360, model training, MFA wizards, billing seats, and live Microsoft, Google, or Meta connections.

## How a page is finished

A page is not finished when it renders seed rows. It is finished when all of these are true:

- The layout matches the CIP interior named for that page: spacing, type, table or chat frame, buttons, empty state, and loading state.
- Every control calls the LIP API and the screen updates from the response.
- Records the signed-in person must not see are absent. A hidden matter returns the same not-found result as a missing one.
- Demonstration copy is labelled. The Employment Act passage is not presented as a licensed corpus.
- The page is checked in the browser as that page’s primary user, then as a user who must be excluded.

Shared kit lives in `apps/web/components/cip/`. Page screens leave `apps/web/components/screens.tsx` and become one folder per page under `apps/web/components/pages/`. API changes stay in `apps/lip-api/Endpoints/LipEndpoints.cs` and `apps/lip-api/Data/DemoSeed.cs`.

## Navigation after phase 0

| Route | Label | CIP interior |
|---|---|---|
| `/dashboard` | AI Command Centre | Command centre home |
| `/dashboard/inbox` | Inbox | Queue list |
| `/dashboard/clients` | Clients & Stakeholders | Queue list |
| `/dashboard/clients/[id]` | Client | Record detail |
| `/dashboard/matters` | Matters | Dense work table |
| `/dashboard/matters/[id]` | Matter | Record detail |
| `/dashboard/workbench` | Legal Workbench | Research workspace inside the work-table chrome |
| `/dashboard/contracts` | Contracts | Dense work table |
| `/dashboard/litigation` | Litigation | Dense work table |
| `/dashboard/compliance` | Compliance | Dense work table |
| `/dashboard/calendar` | Calendar | Dense work table |
| `/dashboard/acquisition` | Acquisition & Conversion | Queue list with segmented tabs. Hidden in corporate mode |
| `/dashboard/reports` | Reports | Work table plus the KPI strip that used to be Overview |
| `/dashboard/settings` | Settings | Settings overlay. Hidden from advocates |
| `/login`, `/`, `/intake` | Sign-in, marketing, intake | CIP login and marketing |
| `/portal` | Client portal | Record cards, client-safe fields only |
| `/otic` | Otic administration | Ops shell plus queue cards |

`/dashboard/command` redirects to `/dashboard`. `/dashboard` no longer renders `OverviewScreen`.

Shell files: `apps/web/components/AppShell.tsx`, copied from CIP `DashboardV2Shell`, `V2Sidebar`, `V2TopBar`, `V2UserMenu`, and `V2RecentChats`. The home date and tagline appear only on `/dashboard`. Recent chats are persisted command threads, not a static list of runs. The LIP mark stays on a white plate.

## Phase 0 — Shell

Remove the Overview item and the separate Command Centre item. Point the first nav item at `/dashboard` with the label AI Command Centre. Corporate mode still hides Acquisition. An advocate still does not see Settings.

Recent chats in the sidebar call `GET /api/v1/commands/runs` and open `/dashboard?conversationId=`. A new chat clears the query string.

Done when the home URL is the command centre, no nav item says Overview, and `/dashboard/command` redirects.

## Phase 1 — Command Centre (`/dashboard`)

CIP source: `CommandCenterView` and the idle layout of `CommandCenterPanel`. Root class `cip-cc`. Idle screen is centered, max width `3xl`, serif greeting, first name in `#1769FF`, composer under the greeting. No KPI strip and no table on the idle screen. After a send: blue user bubble, white assistant bubble, citation row, working indicator.

Function:

- Ask returns an informational answer. A corpus hit cites the demonstration Employment Act passage and says it is not a licensed corpus. A miss says no citation was created.
- Prepare writes a draft document on the selected matter and does not issue it. The bubble names the draft.
- Act creates an approval and stays `AWAITING_APPROVAL`. Nothing is published. The bubble names the approval id.
- The matter picker lists only matters `Access.CanReadMatter` allows. It may be empty. It never sends a hardcoded matter id.
- A Kabanda question from a walled user returns the same not-found result as a missing matter.
- The thread is stored. Reload and the sidebar recent-chat link restore it.
- Approving that Act run is done on the matter or in Settings by a different person. The requestor cannot approve it.

API: persist turns on `CommandRun` with a conversation id, and add `GET /api/v1/commands/runs/{id}` for the thread. Seed one restored Ask thread for Ada.

Done when Ada can Ask, Prepare, and Act on the employment matter, leave and return to the same thread, and Ben cannot retrieve Kabanda.

## Phase 2 — Inbox (`/dashboard/inbox`)

CIP source: acquisition customer queue. KPI strip, rounded search, filter chips, table card on desktop, avatar rows on mobile, thread in the detail frame rather than a second plain card.

Function:

- List conversations the person may see. Hide any conversation whose enquiry is tied only to a walled or non-member matter.
- Open a row to show every message in order, not only the first.
- Origin channel is read-only.
- Reply posts a message on that conversation.
- Hand to counsel picks an active user in the same tenant, sets the owner, moves the queue to assigned, and appends a handover message that pauses automated advice.
- A new web enquiry from Intake appears here with its summary as the first message.

API: `POST /api/v1/inbox/{id}/messages`. Handover already exists; stop hardcoding `usr-ada` in the client. Seed a message on every conversation, including the corporate one.

Done when Ada can reply and hand a visible thread to Paul, and Ben’s list has no Kabanda enquiry.

## Phase 3 — Clients (`/dashboard/clients` and `/dashboard/clients/[id]`)

CIP source: the same queue for the list. The detail page uses the lead-style frame: initials avatar, name, underline tabs, field rows, `#1769FF` primary button.

List function: search by name, show consent and verified channels, open View.

Detail tabs:

- Profile: kind, consent, channels. Consent is display-only in this pass.
- Matters: only visible matters, each opening the matter page.
- Enquiries: only enquiries that pass the same visibility rule as the inbox. Stage and immutable source channel are shown.
- Timeline: audit rows for that client.

Done when Ada’s Client 360 for Chris Mugisha lists the employment matter and omits Kabanda, and Ben’s client page also omits it.

## Phase 4 — Matters (`/dashboard/matters` and `/dashboard/matters/[id]`)

CIP source: credit-files work table for the list. Header, 12px description, filter bar, sticky single-line grid, status dot, pagination, dashed empty state. Detail uses the record frame.

List function:

- Search, status, and risk filters.
- Columns: matter, practice, status, clearance, risk, owner.
- Open matter from an enquiry the user can see. New matters start as `proposed`, or `conflict_review` when a potential conflict exists. They are not created already active.
- The owner is the signed-in user, or a chosen colleague in the tenant. It is not hardcoded.

Detail tabs:

- Summary: status, clearance, risk, exposure note, jurisdiction. Outcome probability is the word Withheld.
- Tasks: verify a date. Verified stays verified.
- Documents: request approval. The returned approval id stays on screen.
- Approvals: a managing partner, general counsel, or tenant admin who is not the requestor can approve. The requestor sees a refusal if they try.
- Issue: available only after that other person’s approval. Issue sets the document to `issued`.
- Timeline: conflict checks and hearings.

Clearance: Paul can clear `mat-conflict`. Ada, the owner, cannot. After clearance, Ada can activate it. Until clearance, activate returns conflict pending.

Done when that clearance, approval, and issue path works in the browser as Ada and then Paul, and Ben has an empty matter list.

## Phase 5 — Legal Workbench (`/dashboard/workbench`)

CIP source: the command composer’s field styling inside a work-table header, plus a result card in the detail frame. This is not a second command centre.

Function:

- Question, jurisdiction, and a matter the user can read, or no matter.
- A question containing the employment or notice demonstration terms returns the labelled passage and review state `pending`.
- Any other question returns evidence `none` and does not invent a citation.
- The page lists that user’s earlier research notes for the selected matter.
- It does not issue a document.

Done when Ada’s employment question cites the demonstration passage, a Mars question does not, and Grace researches her own matter instead of receiving a not-found for the firm matter.

## Phase 6 — Contracts (`/dashboard/contracts`)

CIP source: dense work table.

Columns: contract, parties, rule, location, severity, extracted dates, disposition.

Function:

- Accept, mitigate, or escalate. The reason is typed by counsel, not a fixed string.
- Dates stay unvalidated until counsel checks “dates confirmed”.
- Saving a disposition does not change the contract text.
- Rows are limited to visible matters.

Done when Ada records a disposition and a date confirmation on the Mugisha contract, and Grace sees only the Nile processor terms.

## Phase 7 — Litigation (`/dashboard/litigation`)

CIP source: dense work table.

Columns: reference, forum, when, timezone, verification, source note.

Function:

- Only hearings on visible matters.
- An unverified date shows the unverified dot. Counsel can mark it verified.
- No column and no detail offers an outcome probability.

Seed keeps the unverified Industrial Court date on the employment matter so Ada’s page is not empty, and the verified Kabanda hearing so only Paul sees it.

Done when Ada sees the employment hearing and not Kabanda, and can verify the date.

## Phase 8 — Compliance (`/dashboard/compliance`)

CIP source: dense work table.

Columns: obligation, source, owner, due date, status.

Function: move a row among `open`, `in_review`, and `complete`. The source id stays attached. Firm users do not see Nile’s obligation. Grace does.

Done when Grace changes the data-protection filing to in review and Ada’s grid is empty.

## Phase 9 — Calendar (`/dashboard/calendar`)

CIP source: dense work table, one combined schedule.

Columns: when, item, kind, matter, timezone, verified.

Rows are tasks and hearings on visible matters. Verifying a task here is the same action as on the matter tab. Unverified dates stay marked.

Done when Ada sees the employment task and hearing, and does not see the Kabanda hearing.

## Phase 10 — Acquisition (`/dashboard/acquisition`)

CIP source: `AcquisitionView` pill tabs plus the customer queue.

Tabs: Enquiries, Conversion, Channels. Hidden when the tenant mode is Corporate.

- Enquiries: the visible enquiry queue. Stage and immutable first-touch channel. Opening a row does not create a second enquiry.
- Conversion: counts of distinct enquiry ids by first-touch channel. A later web visit does not change WhatsApp origin.
- Channels: this tenant’s connector records only. Status is `demo-adapter` or `unavailable`. There is no connect button that calls Meta, Microsoft, or a court.

Done when Ada sees WhatsApp and web first-touch counts that match the enquiry rows, and Grace has no Acquisition item.

## Phase 11 — Reports (`/dashboard/reports`)

This page holds the overview. There is still no Overview route.

CIP source: work-table header and grid, with the acquisition KPI strip above it.

The strip shows unique enquiries, WhatsApp first touch, web first touch, visible matters, high-risk matters, AI units, collected revenue, invoiced amount, and obligations. Collected revenue is reconciled cash. It is not computed from invoices. Outcome probability is Withheld.

The grid repeats those figures with the definition text. Export downloads that grid as CSV for rows the user can see. A short activity list under the grid uses the same audit feed.

Otic administration does not use this page. It never receives document bodies.

Done when Ada’s collected figure is the seeded cash amount, the invoiced figure is different, and the export contains only her visible metrics.

## Phase 12 — Settings (`/dashboard/settings`)

CIP source: `V2SettingsOverlay`. Left rail on desktop, pills on mobile. Sections: Profile, Organization, People, Ethical walls, Connectors, Usage, Audit.

- Profile shows the signed-in person.
- Organization shows mode, plan, and branding. Corporate mode is what hides Acquisition.
- People lists users in the tenant.
- Ethical walls list walls on matters the viewer may know about. Paul sees Ben’s wall on Kabanda. Ben is an advocate and does not open Settings.
- Connectors are the tenant-scoped records from Acquisition’s Channels tab.
- Usage repeats the report figures for this tenant.
- Audit lists the latest events for roles allowed to read them.

An advocate does not see the nav item. A direct URL returns the access-denied state.

Done when Tina opens Settings, sees the wall and the audit, and Ada does not.

## Phase 13 — Outside the workspace

### Marketing `/`

CIP marketing header: transparent bar, logo on white, one glow card. Links to intake and sign-in. No navy plate behind the mark.

### Sign-in `/login`

CIP login: mist background, white card, seven identities including Tina Sekitoleko. Client goes to `/portal`. Otic operator goes to `/otic`. Everyone else goes to `/dashboard`, which is the command centre.

### Intake `/intake`

Consent refused does not create an enquiry. Consent granted creates one, stores the summary as the first message, and shows the enquiry id. The channel sent by this form is `web` and stays the origin.

### Portal `/portal`

Client-safe cards in the detail frame. Title, practice, milestone. No internal notes, no Kabanda title, no document bodies. The logo uses the white plate.

### Otic `/otic`

Ops shell, max width `6xl`. Tenant queue and usage queue. Document-body count is zero. No matter content.

## Phase 14 — Prove, then commit, push, and deploy

Run the API tests, then walk the browser in this order:

1. Ada: command centre Ask, Prepare, and Act; inbox reply; matter clearance wait; document approval request.
2. Paul: clear the conflict matter; approve Ada’s document; issue remains available to Ada only after that approval.
3. Ben: matters, inbox, and command centre do not reveal Kabanda.
4. Grace: corporate workspace has her matter and obligation, and no Acquisition item.
5. Chris: portal shows the employment matter only.
6. Owen: tenants and usage, empty document bodies.
7. Confirm `/dashboard` is the command centre and no Overview page remains.

Then commit, push `main` to https://github.com/Otic-Group/LIP, rebuild the web with `NEXT_PUBLIC_API_BASE_URL=https://app-lip-web-api.azurewebsites.net`, and zip-deploy the API and web to the existing apps in `rg-lip-web-prod`. CIP resources stay untouched.

## Out of this plan

Live WhatsApp, Microsoft 365, Teams, finance posting, court e-filing, and a licensed statute corpus. Those stay labelled demonstration adapters and demonstration text.
