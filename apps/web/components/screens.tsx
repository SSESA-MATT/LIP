"use client";

import Link from "next/link";
import { useRouter, useSearchParams } from "next/navigation";
import { useEffect, useRef, useState } from "react";
import { api, type SessionUser } from "@/lib/api";
import { DetailCard, EmptyState, Fields, FilterBar, KpiStrip, PrimaryButton, QueueCard, SecondaryButton, Segmented, StatusDot, WorkTable, fieldClass, initials, searchClass } from "@/components/cip/ui";

function useMe() {
  const [me, setMe] = useState<{ user: SessionUser; tenant: { name: string; mode: string; plan: string; branding: string } } | null>(null);
  useEffect(() => { api<NonNullable<typeof me>>("/api/v1/auth/me").then(setMe).catch(() => setMe(null)); }, []);
  return me;
}

type Metrics = {
  uniqueEnquiries: number; whatsappFirstTouch: number; webFirstTouch: number; visibleMatters: number;
  highRiskMatters: number; aiUnits: number; aiCost: number; collectedRevenue: number; invoicedAmount: number;
  regulatoryObligations: number; outcomeProbability: null; refreshedAt: string;
  definitions: { uniqueEnquiries: string; collectedRevenue: string };
  activity: Array<{ action: string; resource: string; outcome: string; at: string }>;
};

export function OverviewScreen() {
  const [data, setData] = useState<Metrics | null>(null);
  const [error, setError] = useState("");
  useEffect(() => { api<Metrics>("/api/v1/analytics/metrics").then(setData).catch((e) => setError(e.message)); }, []);
  if (error) return <EmptyState title="Overview unavailable" body={error} />;
  if (!data) return <p className="text-sm text-slate-500">Loading overview…</p>;
  return (
    <div className="space-y-4">
      <KpiStrip items={[
        { label: "Unique enquiries", value: String(data.uniqueEnquiries) },
        { label: "WhatsApp first touch", value: String(data.whatsappFirstTouch) },
        { label: "Visible matters", value: String(data.visibleMatters) },
        { label: "High risk", value: String(data.highRiskMatters) },
        { label: "AI units", value: String(data.aiUnits) },
        { label: "Collected revenue", value: Number(data.collectedRevenue).toLocaleString() },
        { label: "Invoiced", value: Number(data.invoicedAmount).toLocaleString() },
        { label: "Obligations", value: String(data.regulatoryObligations) },
      ]} />
      <p className="text-[12px] text-slate-500">{data.definitions.collectedRevenue} Outcome probability is withheld.</p>
      <QueueCard title="Recent activity">
        <ul className="divide-y divide-slate-50">
          {data.activity.map((row) => (
            <li key={row.resource + row.at} className="flex items-center justify-between px-4 py-3 text-[13px]">
              <span className="font-medium text-[#0B1640]">{row.action}</span>
              <span className="text-slate-500">{row.outcome}</span>
            </li>
          ))}
        </ul>
      </QueueCard>
    </div>
  );
}

type InboxRow = { id: string; enquiryId: string; channel: string; queue: string; ownerId: string; status: string; enquiry?: { summary: string; sourceChannel: string; stage: string }; messages: Array<{ id: string; author: string; body: string; kind: string; at?: string }> };
type Counsel = { id: string; name: string; role: string };

export function InboxScreen() {
  const me = useMe();
  const [rows, setRows] = useState<InboxRow[]>([]);
  const [people, setPeople] = useState<Counsel[]>([]);
  const [selected, setSelected] = useState("");
  const [owner, setOwner] = useState("");
  const [query, setQuery] = useState("");
  const [channel, setChannel] = useState("all");
  const [reply, setReply] = useState("");
  const [busy, setBusy] = useState(false);
  const load = () => api<InboxRow[]>("/api/v1/inbox").then((data) => {
    setRows(data);
    setSelected((current) => data.some((item) => item.id === current) ? current : data[0]?.id || "");
  });
  useEffect(() => { load(); }, []);
  useEffect(() => {
    api<{ people: Counsel[] }>("/api/v1/directory").then((directory) => {
      const counsel = directory.people.filter((person) => person.role !== "Client");
      setPeople(counsel);
      if (!me?.user.id) return;
      setOwner((current) => current && current !== me.user.id && counsel.some((person) => person.id === current)
        ? current
        : counsel.find((person) => person.id !== me.user.id)?.id || "");
    }).catch(() => setPeople([]));
  }, [me?.user.id]);
  const visible = rows.filter((item) => {
    const origin = (item.enquiry?.sourceChannel || item.channel).toLowerCase();
    if (channel !== "all" && origin !== channel) return false;
    return (item.enquiry?.summary || item.enquiryId).toLowerCase().includes(query.trim().toLowerCase());
  });
  const row = rows.find((item) => item.id === selected);
  async function sendReply() {
    const text = reply.trim();
    if (!text || !row || busy) return;
    setBusy(true);
    try {
      await api(`/api/v1/inbox/${row.id}/messages`, { method: "POST", body: JSON.stringify({ body: text }) });
      setReply("");
      await load();
    } finally { setBusy(false); }
  }
  async function handOver() {
    if (!row || !owner || busy) return;
    setBusy(true);
    try {
      await api(`/api/v1/inbox/${row.id}/handover`, { method: "POST", body: JSON.stringify({ ownerId: owner, reason: "Handed to counsel. Automated advice is paused." }) });
      await load();
    } finally { setBusy(false); }
  }
  return (
    <div className="cip-acq space-y-4">
      <KpiStrip items={[{ label: "Conversations", value: String(rows.length) }, { label: "WhatsApp", value: String(rows.filter((item) => (item.enquiry?.sourceChannel || item.channel) === "whatsapp").length) }, { label: "Escalated", value: String(rows.filter((item) => item.status === "escalated").length) }]} />
      <div className="flex flex-col gap-2 sm:flex-row sm:items-center">
        <input className={searchClass} placeholder="Search conversations" value={query} onChange={(event) => setQuery(event.target.value)} />
        <div className="flex gap-2">
          {[["all", "All"], ["whatsapp", "WhatsApp"], ["web", "Web"]].map(([id, label]) => (
            <button key={id} type="button" onClick={() => setChannel(id)} className={`rounded-full px-3 py-1.5 text-[12px] font-medium ${channel === id ? "bg-[#1769FF] text-white" : "bg-white text-slate-600 ring-1 ring-slate-200"}`}>{label}</button>
          ))}
        </div>
      </div>
      <div className="grid gap-4 lg:grid-cols-[minmax(0,1.1fr)_minmax(0,0.9fr)]">
        <QueueCard title="Unified inbox">
          {visible.length === 0 ? <div className="p-6"><EmptyState title="Inbox is clear" body="New web and WhatsApp conversations appear here." /></div> : (
            <>
              <div className="hidden lg:block">
                <WorkTable columns={visible.map((item) => [
                  { label: "Summary", cell: <button type="button" className={`max-w-[280px] truncate text-left ${selected === item.id ? "font-semibold text-[#1769FF]" : "font-medium text-[#0B1640]"}`} onClick={() => setSelected(item.id)}>{item.enquiry?.summary || item.enquiryId}</button> },
                  { label: "Origin", cell: item.enquiry?.sourceChannel || item.channel },
                  { label: "Queue", cell: item.queue },
                  { label: "Status", cell: <StatusDot status={item.status} /> },
                ])} />
              </div>
              <ul className="divide-y divide-slate-50 lg:hidden">
                {visible.map((item) => (
                  <li key={item.id}>
                    <button type="button" onClick={() => setSelected(item.id)} className={`flex w-full items-center gap-3 px-4 py-3 text-left hover:bg-slate-50/70 ${selected === item.id ? "bg-[#EEF4FF]" : ""}`}>
                      <span className="flex h-10 w-10 items-center justify-center rounded-full bg-slate-100 text-[12px] font-semibold text-[#0B1640]">{(item.enquiry?.sourceChannel || item.channel).slice(0, 2).toUpperCase()}</span>
                      <span className="min-w-0 flex-1">
                        <span className="block truncate text-[13px] font-semibold text-[#0B1640]">{item.enquiry?.summary || item.enquiryId}</span>
                        <span className="block text-[11px] text-slate-500">{item.enquiry?.sourceChannel || item.channel} · {item.queue}</span>
                      </span>
                      <StatusDot status={item.status} />
                    </button>
                  </li>
                ))}
              </ul>
            </>
          )}
        </QueueCard>
        <DetailCard title="Thread">
          {!row && <p className="text-[13px] text-slate-500">Select a conversation.</p>}
          {row && (
            <div className="space-y-3">
              <p className="text-[12px] text-slate-500">Origin {row.enquiry?.sourceChannel || row.channel}</p>
              {row.messages.map((message) => (
                <div key={message.id} className={`max-w-[90%] rounded-2xl px-3 py-2 text-[13px] ${message.author === "client" || message.author === "requester" ? "bg-[#1769FF] text-white" : "border border-slate-200 bg-white text-slate-800"}`}>
                  <div className="text-[10px] uppercase opacity-70">{message.author} · {message.kind}</div>
                  {message.body}
                </div>
              ))}
              <div className="space-y-2 border-t border-slate-100 pt-3">
                <div className="flex gap-2">
                  <input className="h-11 flex-1 rounded-xl border border-slate-200 px-3 text-[14px] outline-none focus:border-[#1769FF]" placeholder="Write a reply" value={reply} onChange={(event) => setReply(event.target.value)} onKeyDown={(event) => { if (event.key === "Enter") void sendReply(); }} />
                  <PrimaryButton onClick={() => void sendReply()} disabled={busy}>Reply</PrimaryButton>
                </div>
                <div className="flex flex-wrap items-center gap-2">
                  <select className={fieldClass} aria-label="Counsel" value={owner} onChange={(event) => setOwner(event.target.value)}>
                    {people.map((person) => <option key={person.id} value={person.id}>{person.name}</option>)}
                  </select>
                  <PrimaryButton onClick={() => void handOver()} disabled={busy || !owner}>Hand to counsel</PrimaryButton>
                </div>
              </div>
            </div>
          )}
        </DetailCard>
      </div>
    </div>
  );
}

const viewClass = "inline-flex h-8 items-center rounded-lg border border-slate-200 bg-white px-3 text-[12px] font-semibold text-slate-700";

export function ClientsScreen() {
  const [rows, setRows] = useState<Array<{ id: string; name: string; kind: string; consentStatus: string; verifiedChannels: string }>>([]);
  const [query, setQuery] = useState("");
  useEffect(() => { api<typeof rows>("/api/v1/clients").then(setRows); }, []);
  const visible = rows.filter((row) => row.name.toLowerCase().includes(query.toLowerCase()));
  return (
    <div className="cip-acq space-y-4">
      <input className={searchClass} placeholder="Search clients" value={query} onChange={(event) => setQuery(event.target.value)} />
      <QueueCard title="Clients and stakeholders">
        {visible.length === 0 ? <div className="p-6"><EmptyState title="No clients" body="Clients in this workspace appear here." /></div> : (
          <>
            <div className="hidden lg:block">
              <WorkTable columns={visible.map((row) => [
                { label: "Name", cell: row.name },
                { label: "Consent", cell: <StatusDot status={row.consentStatus} /> },
                { label: "Channels", cell: row.verifiedChannels },
                { label: "View", cell: <Link href={`/dashboard/clients/${row.id}`} className={viewClass}>View</Link> },
              ])} />
            </div>
            <ul className="divide-y divide-slate-50 lg:hidden">
              {visible.map((row) => (
                <li key={row.id} className="flex items-center justify-between gap-3 px-4 py-3 hover:bg-slate-50/70">
                  <span className="flex items-center gap-3">
                    <span className="flex h-10 w-10 items-center justify-center rounded-full bg-[#1769FF] text-[12px] font-semibold text-white">{initials(row.name)}</span>
                    <span>
                      <span className="block text-[13px] font-semibold text-[#0B1640]">{row.name}</span>
                      <span className="block text-[11px] text-slate-500">{row.kind} · {row.verifiedChannels}</span>
                    </span>
                  </span>
                  <span className="flex items-center gap-3">
                    <StatusDot status={row.consentStatus} />
                    <Link href={`/dashboard/clients/${row.id}`} className={viewClass}>View</Link>
                  </span>
                </li>
              ))}
            </ul>
          </>
        )}
      </QueueCard>
    </div>
  );
}

export function ClientDetail({ id }: { id: string }) {
  const [data, setData] = useState<{ client: { name: string; consentStatus: string; verifiedChannels: string; kind: string }; matters: Array<{ id: string; title: string; status: string }>; enquiries: Array<{ id: string; sourceChannel: string; stage: string; summary: string }>; timeline: Array<{ id: string; action: string; outcome: string; at: string }> } | null>(null);
  const [tab, setTab] = useState("profile");
  useEffect(() => { api<NonNullable<typeof data>>(`/api/v1/clients/${id}`).then(setData).catch(() => setData(null)); }, [id]);
  if (!data) return <EmptyState title="Client is outside your permission" body="A hidden matter is not listed, and the screen does not confirm that it exists." />;
  const tabs = [{ id: "profile", label: "Profile" }, { id: "matters", label: "Matters" }, { id: "enquiries", label: "Enquiries" }, { id: "timeline", label: "Timeline" }];
  return (
    <div className="mx-auto max-w-[1400px] space-y-4 text-[#0B1B4D]">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <div className="flex items-center gap-3">
          <span className="flex h-14 w-14 items-center justify-center rounded-full bg-[#1769FF] text-[18px] font-semibold text-white">{initials(data.client.name)}</span>
          <div>
            <h2 className="text-[22px] font-bold">{data.client.name}</h2>
            <p className="text-[12px] text-slate-500">Client 360 shows only matters you may see.</p>
          </div>
        </div>
        {data.matters[0] && <Link href={`/dashboard/matters/${data.matters[0].id}`} className="inline-flex h-9 items-center justify-center rounded-xl bg-[#1769FF] px-3.5 text-[13px] font-semibold text-white hover:bg-[#0F5AE6]">Open matter</Link>}
      </div>
      <div className="flex gap-1 border-b border-slate-200">
        {tabs.map((item) => (
          <button key={item.id} type="button" onClick={() => setTab(item.id)} className={`border-b-2 px-3 py-2.5 text-[13px] font-medium ${tab === item.id ? "border-[#1769FF] text-[#1769FF]" : "border-transparent text-slate-500"}`}>{item.label}</button>
        ))}
      </div>
      {tab === "profile" && <DetailCard title="Profile"><Fields rows={[{ label: "Kind", value: data.client.kind }, { label: "Consent", value: data.client.consentStatus }, { label: "Channels", value: data.client.verifiedChannels }]} /></DetailCard>}
      {tab === "matters" && <DetailCard title="Matters">{data.matters.map((matter) => <Link key={matter.id} href={`/dashboard/matters/${matter.id}`} className="block border-b border-slate-100 py-2 text-[13px] font-medium">{matter.title}</Link>)}{data.matters.length === 0 && <p className="text-[13px] text-slate-500">No visible matters.</p>}</DetailCard>}
      {tab === "enquiries" && <DetailCard title="Enquiries">{data.enquiries.map((enquiry) => <div key={enquiry.id} className="border-b border-slate-100 py-2 text-[13px]">{enquiry.summary}<span className="block text-[12px] text-slate-500">{enquiry.sourceChannel} · {enquiry.stage}</span></div>)}{data.enquiries.length === 0 && <p className="text-[13px] text-slate-500">No visible enquiries.</p>}</DetailCard>}
      {tab === "timeline" && <DetailCard title="Timeline">{data.timeline.map((row) => <div key={row.id} className="border-b border-slate-100 py-2 text-[13px]"><span className="font-medium text-[#0B1640]">{row.id}</span> · {row.action} · {row.outcome}</div>)}{data.timeline.length === 0 && <p className="text-[13px] text-slate-500">No audit rows for this client.</p>}</DetailCard>}
    </div>
  );
}

type Matter = { id: string; title: string; status: string; clearanceState: string; risk: string; practiceArea: string; ownerId: string; clientId: string };

export function MattersScreen() {
  const me = useMe();
  const [rows, setRows] = useState<Matter[]>([]);
  const [enquiries, setEnquiries] = useState<Array<{ id: string; summary: string }>>([]);
  const [people, setPeople] = useState<Array<{ id: string; name: string; role: string }>>([]);
  const [search, setSearch] = useState("");
  const [status, setStatus] = useState("");
  const [risk, setRisk] = useState("");
  const [page, setPage] = useState(0);
  const [title, setTitle] = useState("");
  const [enquiryId, setEnquiryId] = useState("");
  const [ownerId, setOwnerId] = useState("");
  const [note, setNote] = useState("");
  const load = () => api<Matter[]>("/api/v1/matters").then(setRows);
  useEffect(() => {
    load();
    api<Array<{ id: string; summary: string }>>("/api/v1/enquiries").then((items) => { setEnquiries(items); setEnquiryId((current) => current || items[0]?.id || ""); });
    api<{ people: Array<{ id: string; name: string; role: string }> }>("/api/v1/directory").then((directory) => setPeople(directory.people.filter((person) => person.role !== "Client"))).catch(() => setPeople([]));
  }, []);
  useEffect(() => { if (me?.user.id) setOwnerId((current) => current || me.user.id); }, [me?.user.id]);
  useEffect(() => { setPage(0); }, [search, status, risk]);
  const filtered = rows.filter((row) => row.title.toLowerCase().includes(search.toLowerCase()) && (!status || row.status === status) && (!risk || row.risk === risk));
  const pageSize = 8;
  const pageCount = Math.max(1, Math.ceil(filtered.length / pageSize));
  const currentPage = Math.min(page, pageCount - 1);
  const visible = filtered.slice(currentPage * pageSize, currentPage * pageSize + pageSize);
  const ownerName = (id: string) => people.find((person) => person.id === id)?.name || id;
  return (
    <div className="space-y-3">
      <div className="flex flex-wrap items-end justify-between gap-3">
        <div>
          <h2 className="text-lg font-semibold text-slate-900">Matters</h2>
          <p className="text-[12px] text-slate-500">A matter stays out of the active state until conflict clearance exists.</p>
        </div>
      </div>
      <FilterBar>
        <input className={fieldClass} placeholder="Search" value={search} onChange={(event) => setSearch(event.target.value)} />
        <select className={fieldClass} aria-label="Status" value={status} onChange={(event) => setStatus(event.target.value)}>
          <option value="">All statuses</option>
          <option value="active">Active</option>
          <option value="conflict_review">Conflict review</option>
          <option value="proposed">Proposed</option>
        </select>
        <select className={fieldClass} aria-label="Risk" value={risk} onChange={(event) => setRisk(event.target.value)}>
          <option value="">All risks</option>
          <option value="low">Low</option>
          <option value="medium">Medium</option>
          <option value="high">High</option>
        </select>
      </FilterBar>
      <form className="flex flex-wrap items-center gap-2" onSubmit={async (event) => {
        event.preventDefault();
        try {
          await api("/api/v1/matters", { method: "POST", body: JSON.stringify({ enquiryId, title, ownerId, practiceArea: "Commercial" }) });
          setTitle(""); setNote("Matter opened outside the active state."); await load();
        } catch (error) { setNote(error instanceof Error ? error.message : "Could not open the matter."); }
      }}>
        <select className={fieldClass} aria-label="Enquiry" value={enquiryId} onChange={(event) => setEnquiryId(event.target.value)}>{enquiries.map((enquiry) => <option key={enquiry.id} value={enquiry.id}>{enquiry.summary}</option>)}</select>
        <input className={fieldClass} placeholder="Matter title" value={title} onChange={(event) => setTitle(event.target.value)} required />
        <select className={fieldClass} aria-label="Owner" value={ownerId} onChange={(event) => setOwnerId(event.target.value)}>{people.map((person) => <option key={person.id} value={person.id}>{person.name}</option>)}</select>
        <button type="submit" className="inline-flex h-8 items-center rounded border border-slate-200 bg-white px-3 text-[12px] font-medium text-slate-700">Open matter</button>
        {note && <span className="text-[12px] text-slate-500">{note}</span>}
      </form>
      <WorkTable empty="No visible matters." columns={visible.map((row) => [
        { label: "Matter", cell: <Link href={`/dashboard/matters/${row.id}`} className="font-semibold text-[#0B1640]">{row.title}</Link> },
        { label: "Practice", cell: row.practiceArea },
        { label: "Status", cell: <StatusDot status={row.status} /> },
        { label: "Clearance", cell: <StatusDot status={row.clearanceState} /> },
        { label: "Risk", cell: <StatusDot status={row.risk} /> },
        { label: "Owner", cell: ownerName(row.ownerId) },
      ])} />
      <div className="flex items-center justify-between text-[12px] text-slate-500">
        <span>{filtered.length === 0 ? "0" : `${currentPage * pageSize + 1}–${Math.min(filtered.length, (currentPage + 1) * pageSize)}`} of {filtered.length}</span>
        <span className="flex gap-2">
          <button type="button" className="rounded border border-slate-200 px-2 py-1 disabled:opacity-40" disabled={currentPage === 0} onClick={() => setPage(currentPage - 1)}>Previous</button>
          <button type="button" className="rounded border border-slate-200 px-2 py-1 disabled:opacity-40" disabled={currentPage >= pageCount - 1} onClick={() => setPage(currentPage + 1)}>Next</button>
        </span>
      </div>
    </div>
  );
}

export function MatterScreen({ id }: { id: string }) {
  const me = useMe();
  const [tab, setTab] = useState("summary");
  const [note, setNote] = useState("");
  const [approvalIds, setApprovalIds] = useState<Record<string, string>>({});
  const [data, setData] = useState<{
    matter: Matter & { exposureNote: string; recordedExposure: number | null; jurisdiction: string };
    tasks: Array<{ id: string; title: string; dateVerified: boolean; timeZone: string; status: string }>;
    documents: Array<{ id: string; title: string; approvalState: string }>;
    hearings: Array<{ id: string; caseReference: string; forum: string; verifiedByCounsel: boolean; when: string }>;
    approvals: Array<{ id: string; decision: string; requestorId: string; subjectId: string }>;
    conflicts: Array<{ counterparty: string; disposition: string; notes: string }>;
    outcomeProbability: null;
  } | null>(null);
  const load = () => api<NonNullable<typeof data>>(`/api/v1/matters/${id}`).then(setData).catch(() => setData(null));
  useEffect(() => { load(); }, [id]);
  if (!data) return <EmptyState title="This matter is not available" body="If it is behind an ethical wall, the response does not confirm that it exists." />;
  const canClear = me && ["ManagingPartner", "GeneralCounsel", "TenantAdministrator"].includes(me.user.role) && me.user.id !== data.matter.ownerId;
  const tabs = [{ id: "summary", label: "Summary" }, { id: "tasks", label: "Tasks" }, { id: "documents", label: "Documents" }, { id: "approvals", label: "Approvals" }, { id: "issue", label: "Issue" }, { id: "timeline", label: "Timeline" }];
  return (
    <div className="mx-auto max-w-[1400px] space-y-4">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <div className="flex items-center gap-3">
          <span className="flex h-14 w-14 items-center justify-center rounded-full bg-[#1769FF] text-white">{initials(data.matter.title)}</span>
          <div>
            <h2 className="text-[22px] font-bold text-[#0B1B4D]">{data.matter.title}</h2>
            <p className="text-[12px] text-slate-500">{data.matter.practiceArea} · {data.matter.jurisdiction}</p>
          </div>
        </div>
        <div className="flex gap-2">
          {canClear && data.matter.clearanceState === "pending" && <PrimaryButton onClick={async () => { await api(`/api/v1/matters/${id}/clearance`, { method: "POST", body: JSON.stringify({ decision: "cleared" }) }); setNote("Cleared by a different person."); await load(); }}>Clear conflict</PrimaryButton>}
          {data.matter.status !== "active" && <PrimaryButton onClick={async () => { try { await api(`/api/v1/matters/${id}/activate`, { method: "POST" }); setNote("Matter is active."); await load(); } catch (error) { setNote(error instanceof Error ? error.message : "Activation blocked."); } }}>Activate</PrimaryButton>}
        </div>
      </div>
      {note && <p className="text-[12px] text-slate-500">{note}</p>}
      <div className="flex gap-1 overflow-x-auto border-b border-slate-200">
        {tabs.map((item) => (
          <button key={item.id} type="button" onClick={() => setTab(item.id)} className={`border-b-2 px-3 py-2.5 text-[13px] font-medium ${tab === item.id ? "border-[#1769FF] text-[#1769FF]" : "border-transparent text-slate-500"}`}>{item.label}</button>
        ))}
      </div>
      {tab === "summary" && <DetailCard title="Summary"><Fields rows={[{ label: "Status", value: <StatusDot status={data.matter.status} /> }, { label: "Clearance", value: <StatusDot status={data.matter.clearanceState} /> }, { label: "Risk", value: <StatusDot status={data.matter.risk} /> }, { label: "Exposure", value: data.matter.exposureNote }, { label: "Jurisdiction", value: data.matter.jurisdiction }, { label: "Outcome probability", value: "Withheld" }]} /></DetailCard>}
      {tab === "tasks" && <DetailCard title="Tasks">{data.tasks.map((task) => <div key={task.id} className="flex items-center justify-between border-b border-slate-100 py-2 text-[13px]"><span>{task.title} · {task.timeZone}</span>{task.dateVerified ? <StatusDot status="verified" /> : <SecondaryButton onClick={async () => { await api(`/api/v1/tasks/${task.id}/verify`, { method: "POST" }); await load(); }}>Verify date</SecondaryButton>}</div>)}</DetailCard>}
      {tab === "documents" && <DetailCard title="Documents">{data.documents.map((doc) => <div key={doc.id} className="flex flex-wrap items-center justify-between gap-2 border-b border-slate-100 py-2 text-[13px]"><span>{doc.title}{approvalIds[doc.id] && <span className="mt-1 block text-[12px] text-slate-500">Approval {approvalIds[doc.id]}</span>}</span><span className="flex items-center gap-2"><StatusDot status={doc.approvalState} />{doc.approvalState !== "issued" && doc.approvalState !== "approved" && <SecondaryButton onClick={async () => { const approval = await api<{ id: string }>(`/api/v1/documents/${doc.id}/request-approval`, { method: "POST" }); setApprovalIds((current) => ({ ...current, [doc.id]: approval.id })); setNote(`Approval ${approval.id} is waiting for a different person.`); await load(); }}>Request approval</SecondaryButton>}</span></div>)}{data.documents.length === 0 && <p className="text-[13px] text-slate-500">No documents yet.</p>}</DetailCard>}
      {tab === "approvals" && <DetailCard title="Approvals">{data.approvals.map((approval) => <div key={approval.id} className="flex items-center justify-between border-b border-slate-100 py-2 text-[13px]"><span>{approval.id} · requestor {approval.requestorId}</span>{approval.decision === "pending" ? <PrimaryButton onClick={async () => { try { await api(`/api/v1/approvals/${approval.id}/decisions`, { method: "POST", body: JSON.stringify({ decision: "approved" }) }); setNote("Approved by a different person."); await load(); } catch (error) { setNote(error instanceof Error ? error.message : "Approval refused."); } }}>Approve</PrimaryButton> : <StatusDot status={approval.decision} />}</div>)}{data.approvals.length === 0 && <p className="text-[13px] text-slate-500">No approvals on this matter.</p>}</DetailCard>}
      {tab === "issue" && <DetailCard title="Issue">{data.documents.map((doc) => <div key={doc.id} className="flex items-center justify-between border-b border-slate-100 py-2 text-[13px]"><span>{doc.title}</span><span className="flex items-center gap-2"><StatusDot status={doc.approvalState} />{doc.approvalState === "approved" && <PrimaryButton onClick={async () => { try { await api(`/api/v1/documents/${doc.id}/issue`, { method: "POST" }); setNote(`${doc.title} was issued.`); await load(); } catch (error) { setNote(error instanceof Error ? error.message : "Issue is waiting for approval."); } }}>Issue</PrimaryButton>}</span></div>)}{data.documents.length === 0 && <p className="text-[13px] text-slate-500">No documents yet.</p>}</DetailCard>}
      {tab === "timeline" && <DetailCard title="Timeline">{data.conflicts.map((item) => <div key={item.counterparty} className="border-b border-slate-100 py-2 text-[13px]">Conflict {item.counterparty}: {item.disposition}. {item.notes}</div>)}{data.hearings.map((hearing) => <div key={hearing.caseReference} className="border-b border-slate-100 py-2 text-[13px]">{hearing.forum} · {hearing.caseReference} · {hearing.verifiedByCounsel ? "counsel verified" : "date unverified"}</div>)}</DetailCard>}
    </div>
  );
}

type ResearchResult = { evidence?: string; body?: string; reviewState?: string; citations?: Array<{ citation: string; licenceStatus: string }> };
type ResearchNoteRow = { id: string; question: string; body: string; reviewState: string };

export function WorkbenchScreen() {
  const [matters, setMatters] = useState<Matter[]>([]);
  const [matterId, setMatterId] = useState("");
  const [jurisdiction, setJurisdiction] = useState("Uganda");
  const [question, setQuestion] = useState("");
  const [result, setResult] = useState<ResearchResult | null>(null);
  const [notes, setNotes] = useState<ResearchNoteRow[]>([]);
  const [busy, setBusy] = useState(false);
  const composer = "rounded-xl border border-slate-200 px-3 text-[14px] outline-none focus:border-[#1769FF]";
  useEffect(() => { api<Matter[]>("/api/v1/matters").then(setMatters).catch(() => setMatters([])); }, []);
  useEffect(() => {
    const query = matterId ? `?matterId=${encodeURIComponent(matterId)}` : "";
    api<ResearchNoteRow[]>(`/api/v1/research${query}`).then(setNotes).catch(() => setNotes([]));
  }, [matterId]);
  async function research() {
    const text = question.trim();
    if (!text || busy) return;
    setBusy(true);
    try {
      const next = await api<ResearchResult>("/api/v1/research", { method: "POST", body: JSON.stringify({ question: text, jurisdiction, matterId: matterId || null }) });
      setResult(next);
      const query = matterId ? `?matterId=${encodeURIComponent(matterId)}` : "";
      setNotes(await api<ResearchNoteRow[]>(`/api/v1/research${query}`));
    } finally { setBusy(false); }
  }
  return (
    <div className="space-y-4">
      <div>
        <h2 className="text-lg font-semibold text-slate-900">Legal workbench</h2>
        <p className="text-[12px] text-slate-500">A hit cites the demonstration extract. A miss creates no citation. This page does not issue a document.</p>
      </div>
      <div className="space-y-2 rounded-xl border border-slate-200 bg-white p-3">
        <textarea className={`min-h-28 w-full py-2 ${composer}`} placeholder="Question" aria-label="Question" value={question} onChange={(event) => setQuestion(event.target.value)} />
        <div className="flex flex-wrap items-center gap-2">
          <input className={`h-11 w-full max-w-[12rem] ${composer}`} aria-label="Jurisdiction" value={jurisdiction} onChange={(event) => setJurisdiction(event.target.value)} />
          <select className={`h-11 w-full max-w-md ${composer}`} aria-label="Matter" value={matterId} onChange={(event) => { setMatterId(event.target.value); setResult(null); }}>
            <option value="">No matter</option>
            {matters.map((matter) => <option key={matter.id} value={matter.id}>{matter.title}</option>)}
          </select>
          <PrimaryButton onClick={() => void research()} disabled={busy}>Research</PrimaryButton>
        </div>
      </div>
      {result && (
        <DetailCard title={result.evidence === "none" ? "No verified authority" : "Citation"}>
          <p className="text-[13px] leading-relaxed">{result.body}</p>
          <p className="mt-2 text-[12px] text-slate-500">Review state {result.reviewState}</p>
          {result.citations?.map((citation) => <p key={citation.citation} className="mt-2 text-[12px] text-slate-500">{citation.citation} · {citation.licenceStatus}</p>)}
        </DetailCard>
      )}
      <DetailCard title="Earlier notes">
        {notes.length === 0 && <p className="text-[13px] text-slate-500">No research notes for this matter.</p>}
        {notes.map((note) => (
          <div key={note.id} className="border-b border-slate-100 py-2 text-[13px]">
            <p className="font-medium text-[#0B1640]">{note.question}</p>
            <p className="mt-1 text-slate-600">{note.body}</p>
            <p className="mt-1 text-[12px] text-slate-500">{note.reviewState}</p>
          </div>
        ))}
      </DetailCard>
    </div>
  );
}

type ContractRow = { id: string; title: string; parties: string; deviationRule: string; deviationLocation: string; severity: string; disposition: string; dispositionReason: string; extractedDates: string; datesValidated: boolean };

export function ContractsScreen() {
  const [rows, setRows] = useState<ContractRow[]>([]);
  const [drafts, setDrafts] = useState<Record<string, { reason: string; datesConfirmed: boolean }>>({});
  const [note, setNote] = useState("");
  const load = () => api<ContractRow[]>("/api/v1/contracts").then(setRows);
  useEffect(() => { load(); }, []);
  return (
    <div className="space-y-3">
      <h2 className="text-lg font-semibold text-slate-900">Contracts</h2>
      <p className="text-[12px] text-slate-500">Dates stay unvalidated until counsel confirms them. A disposition does not amend the contract.</p>
      {note && <p className="text-[12px] text-slate-500">{note}</p>}
      <WorkTable empty="No contract reviews for the matters you can see." columns={rows.map((row) => {
        const draft = drafts[row.id] ?? { reason: "", datesConfirmed: false };
        return [
          { label: "Contract", cell: row.title },
          { label: "Parties", cell: row.parties },
          { label: "Rule", cell: row.deviationRule },
          { label: "Location", cell: row.deviationLocation },
          { label: "Severity", cell: <StatusDot status={row.severity} /> },
          { label: "Dates", cell: <span className="inline-flex items-center gap-2">{row.extractedDates}{row.datesValidated && <span className="inline-flex items-center gap-1 text-[12px] text-emerald-700"><span className="h-1.5 w-1.5 rounded-full bg-emerald-500" />confirmed</span>}</span> },
          { label: "Disposition", cell: row.disposition ? <div className="whitespace-normal"><StatusDot status={row.disposition} /><p className="mt-1 max-w-xs text-[12px] text-slate-500">{row.dispositionReason}</p></div> : <div className="flex flex-col items-start gap-1 whitespace-normal"><input className={`${fieldClass} w-56`} aria-label="Reason" value={draft.reason} onChange={(event) => setDrafts((current) => ({ ...current, [row.id]: { ...draft, reason: event.target.value } }))} /><label className="flex items-center gap-2 text-[12px] text-slate-600"><input type="checkbox" checked={draft.datesConfirmed} onChange={(event) => setDrafts((current) => ({ ...current, [row.id]: { ...draft, datesConfirmed: event.target.checked } }))} />dates confirmed</label><span className="flex gap-1">{["accepted", "mitigated", "escalated"].map((disposition) => <button key={disposition} type="button" disabled={!draft.reason.trim()} className="rounded border border-slate-200 px-2 py-1 text-[11px] disabled:opacity-40" onClick={async () => { await api(`/api/v1/contracts/${row.id}/dispositions`, { method: "POST", body: JSON.stringify({ disposition, reason: draft.reason.trim(), datesValidated: draft.datesConfirmed }) }); setNote("Disposition stored. The contract was not amended."); await load(); }}>{disposition}</button>)}</span></div> },
        ];
      })} />
    </div>
  );
}

type HearingRow = { id: string; caseReference: string; forum: string; verifiedByCounsel: boolean; when: string; sourceNote: string };

export function LitigationScreen() {
  const [rows, setRows] = useState<HearingRow[]>([]);
  const load = () => api<{ hearings: HearingRow[] }>("/api/v1/calendar").then((data) => setRows(data.hearings));
  useEffect(() => { load(); }, []);
  return (
    <div className="space-y-3">
      <h2 className="text-lg font-semibold text-slate-900">Litigation</h2>
      <p className="text-[12px] text-slate-500">Hearing dates show whether counsel has verified them. No outcome probability is stored.</p>
      <WorkTable empty="No hearings on matters you can see." columns={rows.map((row) => [
        { label: "Reference", cell: row.caseReference },
        { label: "Forum", cell: row.forum },
        { label: "When", cell: new Date(row.when).toLocaleString("en-GB", { timeZone: "Africa/Kampala" }) },
        { label: "Timezone", cell: "Africa/Kampala" },
        { label: "Verification", cell: row.verifiedByCounsel ? <StatusDot status="verified" /> : <span className="inline-flex items-center gap-2"><StatusDot status="unverified" /><SecondaryButton onClick={async () => { await api(`/api/v1/hearings/${row.id}/verify`, { method: "POST" }); await load(); }}>Verify date</SecondaryButton></span> },
        { label: "Source note", cell: row.sourceNote },
      ])} />
    </div>
  );
}

type ObligationRow = { id: string; title: string; status: string; reviewDue: string; sourceId: string; ownerId: string };

export function ComplianceScreen() {
  const [rows, setRows] = useState<ObligationRow[]>([]);
  const [names, setNames] = useState<Record<string, string>>({});
  const load = () => api<ObligationRow[]>("/api/v1/compliance").then(setRows);
  useEffect(() => {
    load();
    api<{ people: Array<{ id: string; name: string }> }>("/api/v1/directory").then((directory) => setNames(Object.fromEntries(directory.people.map((person) => [person.id, person.name])))).catch(() => setNames({}));
  }, []);
  return (
    <div className="space-y-3">
      <h2 className="text-lg font-semibold text-slate-900">Compliance</h2>
      <p className="text-[12px] text-slate-500">A status change leaves the source id attached.</p>
      <WorkTable empty="No obligations for this tenant." columns={rows.map((row) => [
        { label: "Obligation", cell: row.title },
        { label: "Source", cell: row.sourceId },
        { label: "Owner", cell: names[row.ownerId] || row.ownerId },
        { label: "Due date", cell: new Date(row.reviewDue).toLocaleDateString("en-GB", { timeZone: "Africa/Kampala" }) },
        { label: "Status", cell: <select aria-label="Status" className={fieldClass} value={row.status} onChange={async (event) => { await api(`/api/v1/obligations/${row.id}/status`, { method: "POST", body: JSON.stringify({ status: event.target.value }) }); await load(); }}><option value="open">open</option><option value="in_review">in review</option><option value="complete">complete</option></select> },
      ])} />
    </div>
  );
}

type CalendarTask = { id: string; title: string; dueAt: string; timeZone: string; dateVerified: boolean; matterId: string };
type CalendarHearing = { id: string; caseReference: string; when: string; verifiedByCounsel: boolean; matterId: string };

export function CalendarScreen() {
  const [data, setData] = useState<{ tasks: CalendarTask[]; hearings: CalendarHearing[] } | null>(null);
  const [matters, setMatters] = useState<Record<string, string>>({});
  const load = () => api<{ tasks: CalendarTask[]; hearings: CalendarHearing[] }>("/api/v1/calendar").then(setData);
  useEffect(() => {
    load();
    api<Array<{ id: string; title: string }>>("/api/v1/matters").then((rows) => setMatters(Object.fromEntries(rows.map((matter) => [matter.id, matter.title])))).catch(() => setMatters({}));
  }, []);
  const rows = [
    ...(data?.tasks || []).map((task) => ({ when: task.dueAt, item: task.title, kind: "task" as const, matterId: task.matterId, zone: task.timeZone || "Africa/Kampala", verified: task.dateVerified, taskId: task.id })),
    ...(data?.hearings || []).map((hearing) => ({ when: hearing.when, item: hearing.caseReference, kind: "hearing" as const, matterId: hearing.matterId, zone: "Africa/Kampala", verified: hearing.verifiedByCounsel, taskId: "" })),
  ].sort((left, right) => new Date(left.when).getTime() - new Date(right.when).getTime());
  return (
    <div className="space-y-3">
      <h2 className="text-lg font-semibold text-slate-900">Calendar</h2>
      <p className="text-[12px] text-slate-500">Deadlines stay linked to matters you can see. Unverified dates stay marked.</p>
      <WorkTable empty="No deadlines." columns={rows.map((row) => [
        { label: "When", cell: new Date(row.when).toLocaleString("en-GB", { timeZone: row.zone }) },
        { label: "Item", cell: row.item },
        { label: "Kind", cell: row.kind },
        { label: "Matter", cell: matters[row.matterId] || row.matterId },
        { label: "Timezone", cell: row.zone },
        { label: "Verified", cell: row.kind === "task" && !row.verified ? <span className="inline-flex items-center gap-2"><StatusDot status="unverified" /><SecondaryButton onClick={async () => { await api(`/api/v1/tasks/${row.taskId}/verify`, { method: "POST" }); await load(); }}>Verify date</SecondaryButton></span> : <StatusDot status={row.verified ? "verified" : "unverified"} /> },
      ])} />
    </div>
  );
}

type EnquiryRow = { id: string; summary: string; sourceChannel: string; latestChannel: string; stage: string; category: string };

export function AcquisitionScreen() {
  const me = useMe();
  const router = useRouter();
  const [tab, setTab] = useState<"enquiries" | "conversion" | "channels">("enquiries");
  const [enquiries, setEnquiries] = useState<EnquiryRow[]>([]);
  const [connectors, setConnectors] = useState<Array<{ name: string; status: string; detail: string }>>([]);
  const [openId, setOpenId] = useState("");
  useEffect(() => { if (me?.tenant.mode === "Corporate") router.replace("/dashboard"); }, [me, router]);
  useEffect(() => {
    if (!me || me.tenant.mode === "Corporate") return;
    api<EnquiryRow[]>("/api/v1/enquiries").then(setEnquiries);
    api<typeof connectors>("/api/v1/connectors").then(setConnectors);
  }, [me]);
  if (!me || me.tenant.mode === "Corporate") return null;
  const open = enquiries.find((row) => row.id === openId) ?? null;
  const whatsapp = enquiries.filter((row) => row.sourceChannel === "whatsapp").length;
  const web = enquiries.filter((row) => row.sourceChannel === "web").length;
  return (
    <div className="cip-acq space-y-4">
      <Segmented tabs={[{ id: "enquiries", label: "Enquiries" }, { id: "conversion", label: "Conversion" }, { id: "channels", label: "Channels" }]} value={tab} onChange={setTab} />
      {tab === "enquiries" && (
        <QueueCard title="Enquiries">
          <ul className="divide-y divide-slate-50">
            {enquiries.map((row) => (
              <li key={row.id}>
                <button type="button" className="flex w-full items-center justify-between px-4 py-3 text-left text-[13px] hover:bg-slate-50" onClick={() => setOpenId(row.id)}>
                  <span><span className="font-semibold text-[#0B1640]">{row.summary}</span><span className="block text-[11px] text-slate-500">{row.category}</span></span>
                  <span className="text-[12px] text-slate-500">{row.sourceChannel} · {row.stage}</span>
                </button>
              </li>
            ))}
          </ul>
          {open && (
            <div className="border-t border-slate-100 px-4 py-3 text-[13px]">
              <p className="font-semibold text-[#0B1640]">{open.summary}</p>
              <p className="mt-1 text-[12px] text-slate-500">First touch {open.sourceChannel}. Stage {open.stage}.</p>
              {open.latestChannel !== open.sourceChannel && <p className="mt-1 text-[12px] text-slate-500">A later {open.latestChannel} visit did not change the first touch.</p>}
            </div>
          )}
        </QueueCard>
      )}
      {tab === "conversion" && <KpiStrip items={[{ label: "WhatsApp first touch", value: String(whatsapp) }, { label: "Web first touch", value: String(web) }, { label: "Unique enquiries", value: String(enquiries.length) }]} />}
      {tab === "channels" && <WorkTable empty="No connectors for this tenant." columns={connectors.map((row) => [{ label: "Connector", cell: row.name }, { label: "Status", cell: <StatusDot status={row.status} /> }, { label: "Detail", cell: row.detail }])} />}
    </div>
  );
}

export function ReportsScreen() {
  const [data, setData] = useState<Metrics | null>(null);
  useEffect(() => { api<Metrics>("/api/v1/analytics/metrics").then(setData); }, []);
  const money = (value: number) => Number(value).toLocaleString("en-GB");
  const rows = data ? [
    ["Unique enquiries", String(data.uniqueEnquiries), data.definitions.uniqueEnquiries],
    ["WhatsApp first touch", String(data.whatsappFirstTouch), "Immutable source channel."],
    ["Web first touch", String(data.webFirstTouch), "Immutable source channel."],
    ["Visible matters", String(data.visibleMatters), "Matters the signed-in person may see."],
    ["High-risk matters", String(data.highRiskMatters), "Matters marked high risk among those the signed-in person may see."],
    ["AI units", String(data.aiUnits), "Metered assistant usage. Cash receipts and invoices are not usage."],
    ["Collected revenue", money(data.collectedRevenue), data.definitions.collectedRevenue],
    ["Invoiced amount", money(data.invoicedAmount), "Separate from collected cash."],
    ["Obligations", String(data.regulatoryObligations), "Obligations recorded for this tenant."],
    ["Outcome probability", "Withheld", "Not calculated."],
  ] : [];
  return (
    <div className="space-y-3">
      <div className="flex items-center justify-between">
        <h2 className="text-lg font-semibold text-slate-900">Reports</h2>
        <SecondaryButton onClick={() => {
          const csv = ["Metric,Value,Definition", ...rows.map((row) => row.map((cell) => `"${cell.replace(/"/g, '""')}"`).join(","))].join("\n");
          const blob = new Blob([csv], { type: "text/csv" });
          const url = URL.createObjectURL(blob);
          const link = document.createElement("a");
          link.href = url; link.download = "lip-report.csv"; link.click();
          URL.revokeObjectURL(url);
        }}>Export</SecondaryButton>
      </div>
      <p className="text-[12px] text-slate-500">Collected revenue is reconciled cash. It is separate from invoices.</p>
      {data && <KpiStrip items={[
        { label: "Unique enquiries", value: String(data.uniqueEnquiries) },
        { label: "WhatsApp first touch", value: String(data.whatsappFirstTouch) },
        { label: "Web first touch", value: String(data.webFirstTouch) },
        { label: "Visible matters", value: String(data.visibleMatters) },
        { label: "High-risk matters", value: String(data.highRiskMatters) },
        { label: "AI units", value: String(data.aiUnits) },
        { label: "Collected revenue", value: money(data.collectedRevenue) },
        { label: "Invoiced amount", value: money(data.invoicedAmount) },
        { label: "Obligations", value: String(data.regulatoryObligations) },
      ]} />}
      <WorkTable columns={rows.map((row) => [{ label: "Metric", cell: row[0] }, { label: "Value", cell: row[1] }, { label: "Definition", cell: row[2] }])} />
      {data && (
        <QueueCard title="Activity">
          <ul className="divide-y divide-slate-50">
            {data.activity.map((row) => (
              <li key={`${row.action}-${row.resource}-${row.at}`} className="flex items-center justify-between gap-3 px-4 py-3 text-[13px]">
                <span className="font-medium text-[#0B1640]">{row.action}</span>
                <span className="text-slate-500">{row.outcome}</span>
              </li>
            ))}
          </ul>
        </QueueCard>
      )}
    </div>
  );
}

type Run = {
  id: string;
  conversationId: string;
  state: string;
  mode: string;
  query: string;
  reviewState: string;
  matterId?: string;
  approvalId?: string | null;
  draftDocumentId?: string | null;
  result?: { body?: string; citation?: string; message?: string; label?: string; autoPublished?: boolean };
};

type ChatMessage = { role: "user" | "ai"; text: string; citation?: string };

function commandFailure(error: unknown) {
  const status = typeof error === "object" && error && "status" in error ? Number((error as { status?: number }).status) : 0;
  if (status === 404) return "That matter or command was not found.";
  return error instanceof Error ? error.message : "The command could not be run.";
}

function assistantCopy(turn: Run) {
  let text = turn.result?.body || turn.result?.message || "";
  if (turn.draftDocumentId && !text.includes(turn.draftDocumentId))
    text = `${text}${text ? " " : ""}Draft ${turn.draftDocumentId} was saved and not issued.`;
  if (turn.state === "AWAITING_APPROVAL" && turn.approvalId && !text.includes(turn.approvalId))
    text = `${text}${text ? " " : ""}Waiting for approval ${turn.approvalId}. Nothing was published.`;
  return text || turn.reviewState;
}

function messagesFromTurns(turns: Run[]): ChatMessage[] {
  return turns.flatMap((turn) => [
    { role: "user" as const, text: turn.query },
    { role: "ai" as const, text: assistantCopy(turn), citation: turn.result?.citation },
  ]);
}

export function CommandScreen() {
  const me = useMe();
  const router = useRouter();
  const conversationId = useSearchParams().get("conversationId");
  const conversationRef = useRef<string | null>(conversationId);
  const skipReload = useRef<string | null>(null);
  const [matterId, setMatterId] = useState("");
  const [query, setQuery] = useState("");
  const [busy, setBusy] = useState(false);
  const [loadingThread, setLoadingThread] = useState(Boolean(conversationId));
  const [messages, setMessages] = useState<ChatMessage[]>([]);
  const first = me?.user.name.split(" ")[0] || "";

  useEffect(() => { conversationRef.current = conversationId; }, [conversationId]);
  useEffect(() => {
    if (skipReload.current && skipReload.current === conversationId) {
      skipReload.current = null;
      setLoadingThread(false);
      return;
    }
    if (!conversationId) {
      setMessages([]);
      setLoadingThread(false);
      return;
    }
    let cancelled = false;
    setLoadingThread(true);
    api<{ matterId?: string; turns: Run[] }>(`/api/v1/commands/runs/${encodeURIComponent(conversationId)}`)
      .then((thread) => {
        if (cancelled) return;
        setMessages(messagesFromTurns(thread.turns));
        if (thread.matterId) setMatterId(thread.matterId);
      })
      .catch((error) => { if (!cancelled) setMessages([{ role: "ai", text: commandFailure(error) }]); })
      .finally(() => { if (!cancelled) setLoadingThread(false); });
    return () => { cancelled = true; };
  }, [conversationId]);

  async function send() {
    const text = query.trim();
    if (!text || busy) return;
    const currentId = conversationRef.current;
    setQuery("");
    setMessages((current) => [...current, { role: "user", text }]);
    setBusy(true);
    try {
      const run = await api<Run>("/api/v1/commands/runs", {
        method: "POST",
        body: JSON.stringify({ mode: "ask", query: text, matterId: matterId || null, conversationId: currentId }),
      });
      conversationRef.current = run.conversationId;
      setMessages((current) => [...current, { role: "ai", text: assistantCopy(run), citation: run.result?.citation }]);
      if (currentId !== run.conversationId) {
        skipReload.current = run.conversationId;
        router.replace(`/dashboard?conversationId=${encodeURIComponent(run.conversationId)}`);
      }
    } catch (error) {
      setMessages((current) => [...current, { role: "ai", text: commandFailure(error) }]);
    } finally { setBusy(false); }
  }

  const idle = messages.length === 0 && !busy && !loadingThread;
  return (
    <div className="cip-cc -mx-3 -my-4 flex min-h-[calc(100dvh-3.5rem)] flex-col px-4 sm:-mx-4 sm:px-6 md:-mx-6">
      <div className={`mx-auto flex w-full max-w-3xl flex-1 flex-col ${idle ? "justify-center" : "pt-8"}`}>
        {idle && (
          <header className="mb-6 text-center">
            <h2 className="cip-cc-serif text-[1.85rem] font-semibold text-[#0B1640] sm:text-[2.35rem]">{greeting()}{first ? <>, <span className="text-[#1769FF]">{first}</span></> : null}</h2>
            <p className="cip-cc-serif mx-auto mt-3 max-w-lg text-[15px] text-slate-500">Ask a legal question and get an answer with its citation.</p>
          </header>
        )}
        {!idle && (
          <div className="mb-28 space-y-4">
            {messages.map((message, index) => (
              <div key={index} className={`flex ${message.role === "user" ? "justify-end" : "justify-start"}`}>
                <div className="max-w-[85%]">
                  <div className={`rounded-2xl px-4 py-3 text-[14px] leading-relaxed ${message.role === "user" ? "bg-[#1769FF] text-white" : "border border-slate-200 bg-white text-slate-800"}`}>
                    {message.text}
                  </div>
                  {message.citation && <p className="mt-1 px-1 text-[11px] leading-snug text-slate-500">{message.citation}</p>}
                </div>
              </div>
            ))}
            {busy && <div className="text-[12px] text-slate-400">Working…</div>}
          </div>
        )}
        <div className={`${idle ? "" : "sticky bottom-4"} rounded-2xl border border-slate-200 bg-white p-3 shadow-[0_8px_30px_rgba(15,23,42,0.06)]`}>
          <div className="flex gap-2">
            <input className="h-11 flex-1 rounded-xl border border-slate-200 px-3 text-[14px] outline-none focus:border-[#1769FF]" placeholder="Ask about a matter you can see" value={query} onChange={(event) => setQuery(event.target.value)} onKeyDown={(event) => { if (event.key === "Enter") void send(); }} />
            <PrimaryButton onClick={() => void send()} disabled={busy}>Send</PrimaryButton>
          </div>
        </div>
      </div>
    </div>
  );
}

function greeting() {
  const hour = new Date().getHours();
  if (hour < 12) return "Good morning";
  if (hour < 17) return "Good afternoon";
  return "Good evening";
}

const settingsSections = [
  { id: "profile", label: "Profile" },
  { id: "organization", label: "Organization" },
  { id: "people", label: "People" },
  { id: "walls", label: "Ethical walls" },
  { id: "connectors", label: "Connectors" },
  { id: "usage", label: "Usage" },
  { id: "audit", label: "Audit" },
] as const;

export function SettingsScreen() {
  const me = useMe();
  const [section, setSection] = useState<(typeof settingsSections)[number]["id"]>("profile");
  const [directory, setDirectory] = useState<{ people: Array<{ id: string; name: string; email: string; role: string }>; walls: Array<{ id: string; userId: string; matterTitle: string; reason: string }> } | null>(null);
  const [connectors, setConnectors] = useState<Array<{ name: string; release: string; status: string; detail: string }>>([]);
  const [metrics, setMetrics] = useState<Metrics | null>(null);
  const [audit, setAudit] = useState<Array<{ action: string; resource: string; outcome: string; at: string }>>([]);
  const allowed = !!me && me.user.role !== "Advocate";
  useEffect(() => {
    if (!allowed) return;
    api<NonNullable<typeof directory>>("/api/v1/directory").then(setDirectory).catch(() => setDirectory(null));
    api<typeof connectors>("/api/v1/connectors").then(setConnectors);
    api<Metrics>("/api/v1/analytics/metrics").then(setMetrics);
    api<typeof audit>("/api/v1/audit").then(setAudit).catch(() => setAudit([]));
  }, [allowed]);
  if (!me) return null;
  if (me.user.role === "Advocate") return <EmptyState title="Access denied" body="Settings is not available for this role." />;
  const money = (value: number) => Number(value).toLocaleString("en-GB");
  return (
    <div className="mx-auto flex max-w-[920px] overflow-hidden rounded-2xl border border-slate-200 bg-white shadow-2xl lg:min-h-[560px]">
      <aside className="hidden w-52 flex-shrink-0 border-r border-gray-100 bg-gray-50 p-3 lg:block">
        {settingsSections.map((item) => <button key={item.id} type="button" onClick={() => setSection(item.id)} className={`mb-1 block w-full rounded-lg px-3 py-2 text-left text-sm ${section === item.id ? "bg-blue-50 text-blue-700" : "text-slate-600"}`}>{item.label}</button>)}
      </aside>
      <div className="min-w-0 flex-1 p-5">
        <div className="mb-4 flex gap-2 overflow-x-auto lg:hidden">
          {settingsSections.map((item) => <button key={item.id} type="button" onClick={() => setSection(item.id)} className={`shrink-0 rounded-full px-3 py-1.5 text-[12px] ${section === item.id ? "bg-blue-600 text-white" : "bg-white ring-1 ring-gray-200"}`}>{item.label}</button>)}
        </div>
        {section === "profile" && <Fields rows={[{ label: "Name", value: me.user.name }, { label: "Email", value: me.user.email }, { label: "Role", value: me.user.role }]} />}
        {section === "organization" && (
          <div className="space-y-2">
            <Fields rows={[{ label: "Tenant", value: me.tenant.name }, { label: "Mode", value: me.tenant.mode }, { label: "Plan", value: me.tenant.plan }, { label: "Branding", value: me.tenant.branding }]} />
            <p className="text-[12px] text-slate-500">Corporate mode hides Acquisition.</p>
          </div>
        )}
        {section === "people" && directory?.people.map((person) => <div key={person.id} className="border-b border-slate-100 py-2 text-[13px]"><span className="font-semibold text-[#0B1640]">{person.name}</span> · {person.role}</div>)}
        {section === "walls" && (directory?.walls || []).map((wall) => {
          const name = directory?.people.find((person) => person.id === wall.userId)?.name ?? wall.userId;
          return <div key={wall.id} className="border-b border-slate-100 py-2 text-[13px]">{name} cannot see {wall.matterTitle}. {wall.reason}</div>;
        })}
        {section === "connectors" && connectors.map((row) => <div key={row.name} className="border-b border-slate-100 py-2 text-[13px]"><span className="font-semibold">{row.name}</span> · <StatusDot status={row.status} /><div className="text-[12px] text-slate-500">{row.detail}</div></div>)}
        {section === "usage" && metrics && <Fields rows={[
          { label: "Unique enquiries", value: String(metrics.uniqueEnquiries) },
          { label: "WhatsApp first touch", value: String(metrics.whatsappFirstTouch) },
          { label: "Web first touch", value: String(metrics.webFirstTouch) },
          { label: "Visible matters", value: String(metrics.visibleMatters) },
          { label: "High-risk matters", value: String(metrics.highRiskMatters) },
          { label: "AI units", value: String(metrics.aiUnits) },
          { label: "Collected revenue", value: money(metrics.collectedRevenue) },
          { label: "Invoiced amount", value: money(metrics.invoicedAmount) },
          { label: "Obligations", value: String(metrics.regulatoryObligations) },
        ]} />}
        {section === "audit" && audit.map((row) => <div key={`${row.at}-${row.resource}-${row.action}`} className="border-b border-slate-100 py-2 text-[13px]">{row.action} · {row.outcome}</div>)}
      </div>
    </div>
  );
}
