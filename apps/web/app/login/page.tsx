"use client";

import { useRouter } from "next/navigation";
import { api } from "@/lib/api";
import { Logo } from "@/components/cip/ui";

const PEOPLE = [
  { id: "usr-ada", label: "Ada Namukasa", detail: "Advocate · Kampala Advocates" },
  { id: "usr-ben", label: "Ben Okello", detail: "Advocate behind an ethical wall" },
  { id: "usr-paul", label: "Paul Ssemakula", detail: "Managing partner" },
  { id: "usr-tina", label: "Tina Sekitoleko", detail: "Tenant administrator" },
  { id: "usr-grace", label: "Grace Atim", detail: "General counsel · corporate mode" },
  { id: "usr-chris", label: "Chris Mugisha", detail: "Client portal" },
  { id: "usr-owen", label: "Owen Operator", detail: "Otic administration" },
];

export default function LoginPage() {
  const router = useRouter();
  async function enter(userId: string) {
    const result = await api<{ token: string; user: { role: string } }>("/api/v1/auth/demo", {
      method: "POST",
      body: JSON.stringify({ userId }),
    });
    localStorage.setItem("lip-token", result.token);
    if (result.user.role === "Client") router.push("/portal");
    else if (result.user.role === "OticAdministrator") router.push("/otic");
    else router.push("/dashboard");
  }

  return (
    <div className="login-page flex min-h-screen items-center justify-center px-4 py-10">
      <div className="login-signin-card w-full max-w-lg p-8">
        <div className="mb-6 flex justify-center"><Logo /></div>
        <p className="text-center text-[11px] font-bold uppercase tracking-[0.08em] text-[#0B1B4D]">Insights today. Sound counsel tomorrow.</p>
        <h1 className="mt-2 text-center text-2xl font-bold text-[#070B3F]">Sign in</h1>
        <p className="mt-2 text-center text-sm text-slate-500">Choose a demonstration identity. These accounts are LIP identities. CIP credentials are not accepted.</p>
        <div className="mt-6 space-y-2">
          {PEOPLE.map((person) => (
            <button key={person.id} type="button" onClick={() => enter(person.id)} className="flex w-full items-center justify-between rounded-xl border border-slate-200 px-3 py-3 text-left hover:bg-[#EEF4FF]">
              <span>
                <span className="block text-sm font-semibold text-[#0B1640]">{person.label}</span>
                <span className="block text-xs text-slate-500">{person.detail}</span>
              </span>
              <span className="text-xs font-semibold text-[#1769FF]">Continue</span>
            </button>
          ))}
        </div>
      </div>
    </div>
  );
}
