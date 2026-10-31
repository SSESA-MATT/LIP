import type { Metadata } from "next";
import "./styles.css";

export const metadata: Metadata = {
  title: "Legal Intelligence Platform",
  description: "Multi-tenant legal operations and intelligence for law firms and corporate legal departments.",
};

export default function RootLayout({ children }: { children: React.ReactNode }) {
  return (
    <html lang="en">
      <body>{children}</body>
    </html>
  );
}
