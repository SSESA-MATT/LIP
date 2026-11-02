import Link from "next/link";
import { Logo } from "@/components/cip/ui";

export default function HomePage() {
  return (
    <div className="lip-page min-h-screen text-[#0B1640]">
      <header className="sticky top-0 z-20 flex h-16 items-center justify-between border-b border-transparent bg-white/90 px-6 backdrop-blur-md">
        <Logo />
        <Link href="/login" className="rounded-xl lip-primary px-4 py-2 text-sm font-semibold text-white hover:bg-[#163f79]">Sign in</Link>
      </header>
      <section className="mx-auto grid max-w-6xl gap-10 px-6 py-16 md:grid-cols-2">
        <div>
          <p className="text-[11px] font-bold uppercase tracking-[0.08em] text-[#1769FF]">Uganda-first · Firms and corporate legal teams</p>
          <h1 className="mt-3 text-4xl font-bold tracking-tight">Legal work with provenance, review, and a named approver.</h1>
          <p className="mt-4 text-lg text-slate-500">Intake, matters, grounded research, contract review, and the AI Legal Command Centre stay in one governed workspace. Outputs remain drafts until a qualified person approves them.</p>
          <div className="mt-6 flex flex-wrap gap-3">
            <Link href="/intake" className="rounded-xl lip-primary px-4 py-2 text-sm font-semibold text-white">Start an enquiry</Link>
            <Link href="/login" className="rounded-xl border border-slate-200 px-4 py-2 text-sm font-semibold text-slate-700">Open the workspace</Link>
          </div>
        </div>
        <div className="mkt-glow-card p-6">
          <h2 className="text-lg font-semibold">Four environments, one record</h2>
          <ul className="mt-4 space-y-3 text-sm text-slate-600">
            <li>Customer channels keep consent and an immutable first touch.</li>
            <li>The internal workspace runs inbox, Client 360, and matters.</li>
            <li>Command Centre separates Ask, Prepare, and Act.</li>
            <li>Otic administration sees tenants and usage, not document bodies.</li>
          </ul>
        </div>
      </section>
    </div>
  );
}
