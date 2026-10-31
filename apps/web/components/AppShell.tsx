"use client";

import Link from "next/link";
import { usePathname, useRouter, useSearchParams } from "next/navigation";
import { useEffect, useState } from "react";
import {
  BarChart3, Briefcase, Calendar, FileSearch, Gavel, Inbox, Menu,
  PanelLeftClose, PanelLeftOpen, Scale, Settings, Shield, Sparkles, Users, X,
} from "lucide-react";
import { api, type SessionUser } from "@/lib/api";
import { Logo } from "@/components/cip/ui";

const NAV = [
  { href: "/dashboard", label: "AI Command Centre", icon: Sparkles },
  { href: "/dashboard/inbox", label: "Inbox", icon: Inbox },
  { href: "/dashboard/clients", label: "Clients & Stakeholders", icon: Users },
  { href: "/dashboard/matters", label: "Matters", icon: Briefcase },
  { href: "/dashboard/workbench", label: "Legal Workbench", icon: Scale },
  { href: "/dashboard/contracts", label: "Contracts", icon: FileSearch },
  { href: "/dashboard/litigation", label: "Litigation", icon: Gavel },
  { href: "/dashboard/compliance", label: "Compliance", icon: Shield },
  { href: "/dashboard/calendar", label: "Calendar", icon: Calendar },
  { href: "/dashboard/acquisition", label: "Acquisition & Conversion", icon: BarChart3 },
  { href: "/dashboard/reports", label: "Reports", icon: BarChart3 },
  { href: "/dashboard/settings", label: "Settings", icon: Settings },
];

const TITLES: Record<string, string> = {
  "/dashboard": "AI Command Centre",
  "/dashboard/inbox": "Inbox",
  "/dashboard/clients": "Clients & Stakeholders",
  "/dashboard/matters": "Matters",
  "/dashboard/workbench": "Legal Workbench",
  "/dashboard/contracts": "Contracts",
  "/dashboard/litigation": "Litigation",
  "/dashboard/compliance": "Compliance",
  "/dashboard/calendar": "Calendar",
  "/dashboard/acquisition": "Acquisition & Conversion",
  "/dashboard/reports": "Reports",
  "/dashboard/settings": "Settings",
};

function formatHeaderDate(date = new Date()) {
  return date.toLocaleDateString(undefined, { weekday: "long", day: "numeric", month: "short", year: "numeric" });
}

export function AppShell({ children }: { children: React.ReactNode }) {
  const pathname = usePathname();
  const router = useRouter();
  const search = useSearchParams().toString();
  const activeConversation = new URLSearchParams(search).get("conversationId");
  const [open, setOpen] = useState(true);
  const [mobile, setMobile] = useState(false);
  const [user, setUser] = useState<SessionUser | null>(null);
  const [tenant, setTenant] = useState<{ name: string; mode: string } | null>(null);
  const [chats, setChats] = useState<Array<{ id: string; query: string }>>([]);

  useEffect(() => {
    const token = localStorage.getItem("lip-token");
    if (!token) { router.replace("/login"); return; }
    api<{ user: SessionUser; tenant: { name: string; mode: string } }>("/api/v1/auth/me")
      .then((me) => {
        setUser(me.user);
        setTenant(me.tenant);
        if (me.user.role === "Client") router.replace("/portal");
        if (me.user.role === "OticAdministrator") router.replace("/otic");
      })
      .catch(() => router.replace("/login"));
    api<Array<{ id: string; query: string }>>("/api/v1/commands/runs").then(setChats).catch(() => setChats([]));
  }, [router, pathname, search]);

  const visible = NAV.filter((item) => {
    if (tenant?.mode === "Corporate" && item.href === "/dashboard/acquisition") return false;
    if (user?.role === "Advocate" && item.href === "/dashboard/settings") return false;
    return true;
  });
  const title = TITLES[pathname] || (pathname.startsWith("/dashboard/matters/") ? "Matter" : pathname.startsWith("/dashboard/clients/") ? "Client" : "Workspace");
  const onCommandHome = pathname === "/dashboard";
  const selfTitled = onCommandHome || pathname === "/dashboard/acquisition";
  const showLabels = open || !mobile;

  return (
    <div className="dashboard-v2 flex h-[100dvh] overflow-hidden">
      {mobile && (
        <button className="fixed inset-0 z-40 bg-slate-900/30 backdrop-blur-[1px] lg:hidden" aria-label="Close navigation" onClick={() => setMobile(false)} />
      )}
      <aside className={`fixed inset-y-0 left-0 z-50 flex h-full w-[min(18rem,calc(100vw-3rem))] flex-col border-r border-[var(--cip-v2-border)] bg-white lg:static ${mobile ? "translate-x-0" : "-translate-x-full"} lg:translate-x-0 ${open ? "lg:w-[var(--cip-v2-sidebar-expanded)]" : "lg:w-[var(--cip-v2-sidebar-collapsed)]"}`}>
        <div className="flex h-14 items-center justify-between border-b border-[var(--cip-v2-border)] px-3">
          <Link href="/dashboard" className="min-w-0" onClick={() => setMobile(false)}>
            <Logo compact={!open} wordmark={open} />
          </Link>
          <button type="button" className="rounded-md p-1.5 text-gray-400 hover:bg-gray-100" aria-label="Toggle sidebar" onClick={() => { setOpen(!open); setMobile(false); }}>
            <X className="h-4 w-4 lg:hidden" />
            <span className="hidden lg:inline">{open ? <PanelLeftClose className="h-4 w-4" /> : <PanelLeftOpen className="h-4 w-4" />}</span>
          </button>
        </div>
        <nav className="flex-1 space-y-0.5 overflow-y-auto px-2 py-3">
          {visible.map((item) => {
            const active = pathname === item.href;
            const Icon = item.icon;
            return (
              <Link key={item.href} href={item.href} title={item.label} onClick={() => setMobile(false)}
                className={`flex items-center gap-3 rounded-md px-2.5 py-2 text-sm font-medium ${active ? "v2-nav-active" : "text-gray-600 hover:bg-gray-100"}`}>
                <Icon className="h-4 w-4 flex-shrink-0" />
                {showLabels && <span className="truncate">{item.label}</span>}
              </Link>
            );
          })}
          {open && (
            <div className="mt-4 border-t border-gray-100 pt-3">
              <div className="flex items-center justify-between px-2.5 pb-1">
                <div className="text-[10px] font-semibold uppercase tracking-wide text-slate-400">Recent chats</div>
                <Link href="/dashboard" className="text-[11px] font-semibold text-[#1769FF]" onClick={() => setMobile(false)}>New chat</Link>
              </div>
              {chats.slice(0, 5).map((chat) => (
                <Link key={chat.id} href={`/dashboard?conversationId=${encodeURIComponent(chat.id)}`} className={`block truncate rounded-md px-2.5 py-1.5 text-[12px] hover:bg-gray-100 ${activeConversation === chat.id ? "bg-[#eff6ff] font-medium text-[#1d4ed8]" : "text-slate-600"}`} onClick={() => setMobile(false)}>{chat.query}</Link>
              ))}
              {chats.length === 0 && <div className="px-2.5 text-[12px] text-slate-400">No command runs yet</div>}
            </div>
          )}
        </nav>
        <div className="border-t border-[var(--cip-v2-border)] p-2">
          <button type="button" className="flex w-full items-center gap-2 rounded-md px-2 py-2 text-left hover:bg-gray-100" onClick={() => { localStorage.removeItem("lip-token"); router.replace("/login"); }}>
            <span className="flex h-8 w-8 items-center justify-center rounded-full bg-[#1769FF] text-[11px] font-semibold text-white">{(user?.name || "L").slice(0, 1)}</span>
            {open && (
              <span className="min-w-0">
                <span className="block truncate text-[13px] font-semibold text-[#0B1640]">{user?.name}</span>
                <span className="block truncate text-[11px] text-slate-500">{user?.role} · Sign out</span>
              </span>
            )}
          </button>
        </div>
      </aside>
      <div className="flex min-w-0 flex-1 flex-col">
        <header className="z-30 flex h-14 flex-shrink-0 items-center justify-between border-b border-[var(--cip-v2-border)] bg-white/95 px-3 backdrop-blur-md sm:px-5">
          <div className="flex min-w-0 items-center gap-2 lg:hidden">
            <button type="button" className="rounded-md p-2 text-gray-600 hover:bg-gray-100" aria-label="Open navigation" onClick={() => setMobile(true)}><Menu className="h-5 w-5" /></button>
            <Logo compact />
          </div>
          <h1 className="hidden truncate text-sm font-medium text-[var(--cip-v2-text)] lg:block">{title}</h1>
          <div className="text-right">
            {onCommandHome ? <p className="text-sm font-medium text-[var(--cip-v2-text)]">{formatHeaderDate()}</p> : <div className="text-sm font-medium text-[#0B1640]">{tenant?.name}</div>}
            <div className="text-[11px] text-slate-400">{onCommandHome ? tenant?.name : tenant?.mode}</div>
          </div>
        </header>
        <main className="min-h-0 flex-1 overflow-y-auto px-3 py-4 sm:px-4 md:px-6">
          {!selfTitled && <h2 className="mb-4 text-xl font-semibold tracking-tight text-[#0B1640] lg:hidden">{title}</h2>}
          {children}
        </main>
      </div>
    </div>
  );
}
