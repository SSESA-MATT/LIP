"use client";

import { redirect, useParams, useSearchParams } from "next/navigation";
import { AcquisitionScreen, CalendarScreen, ClientsScreen, ComplianceScreen, ContractsScreen, InboxScreen, LitigationScreen, MattersScreen, ReportsScreen, SettingsScreen, WorkbenchScreen } from "@/components/screens";

export default function SectionPage() {
  const section = useParams<{ section: string }>().section;
  const search = useSearchParams();
  if (section === "command") {
    const conversationId = search.get("conversationId");
    redirect(conversationId ? `/dashboard?conversationId=${encodeURIComponent(conversationId)}` : "/dashboard");
  }
  if (section === "inbox") return <InboxScreen />;
  if (section === "clients") return <ClientsScreen />;
  if (section === "matters") return <MattersScreen />;
  if (section === "workbench") return <WorkbenchScreen />;
  if (section === "contracts") return <ContractsScreen />;
  if (section === "litigation") return <LitigationScreen />;
  if (section === "calendar") return <CalendarScreen />;
  if (section === "compliance") return <ComplianceScreen />;
  if (section === "acquisition") return <AcquisitionScreen />;
  if (section === "reports") return <ReportsScreen />;
  if (section === "connectors") return <SettingsScreen />;
  if (section === "settings") return <SettingsScreen />;
  return <p className="text-sm text-slate-500">That section is not part of the workspace.</p>;
}
