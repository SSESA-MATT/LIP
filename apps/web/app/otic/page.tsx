"use client";

import { useEffect, useState } from "react";
import { useRouter } from "next/navigation";
import { api } from "@/lib/api";
import { KpiStrip, Logo, QueueCard, StatusDot } from "@/components/cip/ui";

export default function OticPage() {
  const router = useRouter();
  const [tenants, setTenants] = useState<Array<{ id: string; name: string; mode: string; plan: string; status: string }>>([]);
  const [usage, setUsage] = useState<{ rows: Array<{ tenantId: string; units: number; cost: number }>; documentBodies: string[] } | null>(null);
  useEffect(() => {
    if (!localStorage.getItem("lip-token")) { router.replace("/login"); return; }
    Promise.all([
      api<{ tenants: typeof tenants; documents: unknown[] }>("/api/v1/admin/tenants"),
      api<NonNullable<typeof usage>>("/api/v1/admin/usage"),
    ]).then(([directory, meter]) => { setTenants(directory.tenants); setUsage(meter); }).catch(() => router.replace("/login"));
  }, [router]);
  return (
    <div className="dashboard-v2 min-h-screen">
      <header className="flex h-14 items-center justify-between border-b border-[var(--cip-v2-border)] bg-white px-5">
        <Logo />
        <button type="button" className="text-sm text-[#1769FF]" onClick={() => { localStorage.removeItem("lip-token"); router.replace("/login"); }}>Sign out</button>
      </header>
      <main className="mx-auto max-w-6xl space-y-4 px-4 py-6">
        <h1 className="text-lg font-semibold text-slate-900">Otic administration</h1>
        <p className="text-[12px] text-slate-500">Tenant and usage metadata. Document bodies are not returned.</p>
        <KpiStrip items={[{ label: "Tenants", value: String(tenants.length) }, { label: "Document bodies", value: String(usage?.documentBodies.length ?? 0) }]} />
        <QueueCard title="Tenants">
          <ul className="divide-y divide-slate-50">
            {tenants.map((tenant) => (
              <li key={tenant.id} className="flex items-center justify-between px-4 py-3 text-[13px]">
                <span><span className="font-semibold text-[#0B1640]">{tenant.name}</span><span className="block text-[11px] text-slate-500">{tenant.plan}</span></span>
                <StatusDot status={tenant.mode} />
              </li>
            ))}
          </ul>
        </QueueCard>
        <QueueCard title="Usage">
          <ul className="divide-y divide-slate-50">
            {(usage?.rows || []).map((row) => <li key={row.tenantId} className="flex justify-between px-4 py-3 text-[13px]"><span>{row.tenantId}</span><span>{row.units} units</span></li>)}
          </ul>
        </QueueCard>
      </main>
    </div>
  );
}
