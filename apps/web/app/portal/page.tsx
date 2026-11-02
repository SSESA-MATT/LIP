"use client";

import { useEffect, useState } from "react";
import { useRouter } from "next/navigation";
import { api } from "@/lib/api";
import { DetailCard, Logo, StatusDot } from "@/components/cip/ui";

export default function PortalPage() {
  const router = useRouter();
  const [rows, setRows] = useState<Array<{ id: string; title: string; milestone: string; practiceArea: string }>>([]);
  useEffect(() => {
    if (!localStorage.getItem("lip-token")) { router.replace("/login"); return; }
    api<typeof rows>("/api/v1/portal/matters").then(setRows).catch(() => router.replace("/login"));
  }, [router]);
  return (
    <div className="lip-page min-h-screen px-4 py-8">
      <div className="mx-auto max-w-3xl">
        <div className="mb-6 flex items-center justify-between">
          <Logo />
          <button type="button" className="text-sm text-[#1769FF]" onClick={() => { localStorage.removeItem("lip-token"); router.replace("/login"); }}>Sign out</button>
        </div>
        <h1 className="lip-page-title text-[#0B1B4D]">Your matters</h1>
        <p className="mb-4 text-[13px] text-slate-500">Status only. Internal notes stay in the workspace.</p>
        <div className="space-y-3">
          {rows.map((row) => (
            <DetailCard key={row.id} title={row.title}>
              <div className="flex items-center justify-between text-[13px]">
                <span>{row.practiceArea}</span>
                <StatusDot status={row.milestone} />
              </div>
            </DetailCard>
          ))}
        </div>
      </div>
    </div>
  );
}
