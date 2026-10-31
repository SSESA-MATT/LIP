"use client";
import { useParams } from "next/navigation";
import { ClientDetail } from "@/components/screens";
export default function Page() {
  const { id } = useParams<{ id: string }>();
  return <ClientDetail id={id} />;
}
