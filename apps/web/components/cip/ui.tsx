import Link from "next/link";
import type { ReactNode } from "react";

export function Logo({ compact = false, wordmark = true }: { compact?: boolean; wordmark?: boolean }) {
  return (
    <span className="flex min-w-0 items-center gap-2">
      <span className={`flex flex-shrink-0 items-center justify-center p-1 ${compact ? "h-8 w-10" : "h-11 w-16"}`}>
        <img src="/brand/lip-mark.png" alt="" className="h-full w-full object-contain" />
      </span>
      {wordmark && (
        <span className="min-w-0 leading-tight">
          <span className="block truncate text-[20px] font-bold tracking-tight text-[#080F49]">LIP</span>
          {!compact && <span className="block truncate text-[11px] font-medium text-slate-500">Legal Intelligence Platform</span>}
        </span>
      )}
    </span>
  );
}

export function StatusDot({ status }: { status: string }) {
  const key = status.toLowerCase();
  const tone = key.includes("active") || key.includes("clear") || key.includes("approv") || key.includes("complete") || key === "cited"
    ? "bg-emerald-50 text-emerald-800 ring-emerald-200"
    : key.includes("high") || key.includes("conflict") || key.includes("pending") || key.includes("review")
      ? "bg-amber-50 text-amber-900 ring-amber-200"
      : key.includes("block") || key.includes("refus")
        ? "bg-rose-50 text-rose-800 ring-rose-200"
        : "bg-slate-50 text-slate-700 ring-slate-200";
  return (
    <span className={`inline-flex items-center gap-2 whitespace-nowrap rounded-full px-3 py-1 text-[13px] font-medium leading-5 ring-1 ring-inset ${tone}`}>
      <span aria-hidden="true" className="h-1.5 w-1.5 shrink-0 rounded-full bg-current" />
      {status.replace(/_/g, " ")}
    </span>
  );
}

export function EmptyState({ title, body }: { title: string; body: string }) {
  return (
    <div className="rounded-2xl border border-dashed border-slate-300 bg-white/80 p-10 text-center">
      <div className="text-sm font-semibold text-slate-700">{title}</div>
      <p className="mt-1 text-[13px] text-slate-500">{body}</p>
    </div>
  );
}

export function KpiStrip({ items }: { items: { label: string; value: string }[] }) {
  return (
    <div className="grid gap-4 sm:grid-cols-2 xl:grid-cols-4">
      {items.map((item) => (
        <div key={item.label} className="lip-kpi rounded-2xl border border-slate-200 px-5 py-4">
          <div className="text-[13px] font-medium text-slate-500">{item.label}</div>
          <div className="mt-2 text-[26px] font-semibold tabular-nums text-[#0B1640]">{item.value}</div>
        </div>
      ))}
    </div>
  );
}

export function Segmented<T extends string>({ tabs, value, onChange }: { tabs: { id: T; label: string }[]; value: T; onChange: (id: T) => void }) {
  return (
    <div className="inline-flex max-w-full flex-wrap gap-1 rounded-xl border border-slate-200 bg-white/90 p-1" role="tablist">
      {tabs.map((tab) => (
        <button key={tab.id} type="button" role="tab" aria-selected={value === tab.id} onClick={() => onChange(tab.id)}
          className={`rounded-lg px-4 py-2 text-[13px] font-medium ${value === tab.id ? "bg-[#1769FF]/12 text-[#1769FF] shadow-[0_0_0_1px_rgba(23,105,255,0.25)]" : "text-slate-600 hover:bg-slate-50"}`}>
          {tab.label}
        </button>
      ))}
    </div>
  );
}

export function FilterBar({ children }: { children: ReactNode }) {
  return <div className="flex flex-wrap items-center gap-3 rounded-2xl border border-slate-200 bg-white/90 p-3">{children}</div>;
}

export const fieldClass = "min-h-10 max-w-full rounded-xl border border-slate-200 bg-white px-3 text-[13px] text-slate-800 outline-none focus:border-[#1769FF] focus:ring-1 focus:ring-[#1769FF]/20";
export const searchClass = "h-11 min-w-0 w-full rounded-xl border border-slate-200 bg-white pl-3 pr-3 text-[13px] text-[#0B1640] outline-none focus:border-[#1769FF]/40";

export function PrimaryButton({ children, onClick, type = "button", disabled }: { children: ReactNode; onClick?: () => void; type?: "button" | "submit"; disabled?: boolean }) {
  return <button type={type} disabled={disabled} onClick={onClick} className="lip-primary inline-flex min-h-11 shrink-0 items-center justify-center gap-2 rounded-xl px-5 text-[13px] font-semibold text-white disabled:opacity-50">{children}</button>;
}

export function SecondaryButton({ children, onClick }: { children: ReactNode; onClick?: () => void }) {
  return <button type="button" onClick={onClick} className="inline-flex min-h-10 shrink-0 items-center justify-center rounded-xl border border-slate-200 bg-white px-4 text-[13px] font-medium text-slate-700 hover:bg-slate-50">{children}</button>;
}

export function WorkTable({ columns, rows, empty }: { columns: { label: string; cell: ReactNode }[][]; rows?: never; empty?: string }) {
  void rows;
  if (columns.length === 0) return <EmptyState title="Nothing to show" body={empty || "Records appear here when the signed-in person can see them."} />;
  const head = columns[0].map((column) => column.label);
  return (
    <div className="lip-table overflow-hidden rounded-2xl border border-slate-200 bg-white">
      <div className="max-h-[70vh] overflow-auto">
        <table className="w-full min-w-[720px] border-separate border-spacing-0 text-sm leading-6">
          <thead>
            <tr>{head.map((label) => <th key={label} className="sticky top-0 z-10 whitespace-nowrap border-b border-slate-200 bg-slate-50 px-4 py-3 text-left text-[13px] font-semibold text-slate-700">{label}</th>)}</tr>
          </thead>
          <tbody>
            {columns.map((row, index) => (
              <tr key={index} className="hover:bg-slate-50/70">
                {row.map((column, cell) => <td key={cell} className="whitespace-nowrap border-b border-slate-100 px-4 py-3.5 align-middle">{column.cell}</td>)}
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </div>
  );
}

export function QueueCard({ title, children, action }: { title: string; children: ReactNode; action?: ReactNode }) {
  return (
    <section className="lip-card min-w-0 overflow-hidden rounded-2xl border border-slate-200 bg-white">
      <header className="flex flex-wrap items-center justify-between gap-3 border-b border-slate-100 px-5 py-4">
        <h2 className="text-base font-semibold text-[#0B1640]">{title}</h2>
        {action}
      </header>
      {children}
    </section>
  );
}

export function DetailTabs({ tabs, active }: { tabs: { id: string; label: string; href: string }[]; active: string }) {
  return (
    <nav className="-mx-1 flex gap-1 overflow-x-auto border-b border-slate-200 px-1">
      {tabs.map((tab) => (
        <Link key={tab.id} href={tab.href} className={`shrink-0 border-b-2 px-3 py-2.5 text-[13px] font-medium ${active === tab.id ? "border-[#1769FF] text-[#1769FF]" : "border-transparent text-slate-500"}`}>{tab.label}</Link>
      ))}
    </nav>
  );
}

export function DetailCard({ title, children }: { title: string; children: ReactNode }) {
  return (
    <section className="lip-card min-w-0 rounded-2xl border border-slate-200 bg-white p-5">
      <h3 className="mb-3 text-base font-semibold text-[#0B1B4D]">{title}</h3>
      {children}
    </section>
  );
}

export function Fields({ rows }: { rows: { label: string; value: ReactNode }[] }) {
  return (
    <dl>
      {rows.map((row) => (
        <div key={row.label} className="grid grid-cols-1 gap-1 border-b border-slate-100 py-3 sm:grid-cols-[minmax(7rem,0.42fr)_minmax(0,1fr)] sm:gap-4">
          <dt className="text-[13px] text-slate-500">{row.label}</dt>
          <dd className="break-words text-sm leading-6 font-medium text-[#0B1B4D]">{row.value}</dd>
        </div>
      ))}
    </dl>
  );
}

export function initials(name: string) {
  return name.split(" ").slice(0, 2).map((part) => part[0] || "").join("").toUpperCase();
}
