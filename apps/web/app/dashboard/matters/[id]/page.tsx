"use client";
import { useParams } from "next/navigation";
import { MatterScreen } from "@/components/screens";
export default function Page() {
  const { id } = useParams<{ id: string }>();
  return <MatterScreen id={id} />;
}
