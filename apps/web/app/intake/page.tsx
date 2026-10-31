"use client";

import { useState } from "react";
import Link from "next/link";
import { api } from "@/lib/api";
import { Logo, PrimaryButton } from "@/components/cip/ui";

export default function IntakePage() {
  const [summary, setSummary] = useState("I need a review of an employment contract in Uganda.");
  const [consent, setConsent] = useState(true);
  const [result, setResult] = useState("");
  return (
    <div className="login-page min-h-screen px-4 py-10">
      <div className="mx-auto max-w-xl">
        <Link href="/" className="text-sm text-[#1769FF]">Back</Link>
        <div className="login-signin-card mt-4 p-6">
          <Logo />
          <h1 className="mt-4 text-2xl font-bold text-[#0B1640]">Start an enquiry</h1>
          <p className="mt-2 text-sm text-slate-500">Consent is required. The first channel stays the origin of the enquiry.</p>
          <label className="mt-4 flex items-center gap-2 text-sm"><input type="checkbox" checked={consent} onChange={(event) => setConsent(event.target.checked)} /> I agree to the demonstration privacy notice.</label>
          <textarea className="mt-3 min-h-28 w-full rounded-xl border border-slate-200 p-3 text-sm" value={summary} onChange={(event) => setSummary(event.target.value)} />
          <div className="mt-4">
            <PrimaryButton onClick={async () => {
              try {
                if (!localStorage.getItem("lip-token")) {
                  const login = await api<{ token: string }>("/api/v1/auth/demo", { method: "POST", body: JSON.stringify({ userId: "usr-tina" }) });
                  localStorage.setItem("lip-token", login.token);
                }
                const created = await api<{ id: string }>("/api/v1/enquiries", { method: "POST", body: JSON.stringify({ channel: "web", category: "Employment", summary, clientId: "cli-chris", consentStatus: consent ? "granted" : "refused" }) });
                setResult(`Enquiry ${created.id} is recorded.`);
              } catch (error) {
                setResult(error instanceof Error ? error.message : "The enquiry was not created.");
              }
            }}>Submit</PrimaryButton>
          </div>
          {result && <p className="mt-4 text-sm text-slate-600">{result}</p>}
        </div>
      </div>
    </div>
  );
}
