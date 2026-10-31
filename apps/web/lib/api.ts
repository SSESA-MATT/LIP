export const API_BASE = process.env.NEXT_PUBLIC_API_BASE_URL || "http://localhost:5080";

export type SessionUser = {
  id: string;
  name: string;
  email: string;
  role: string;
  tenantId: string;
};

export async function api<T>(path: string, init?: RequestInit): Promise<T> {
  const token = typeof window === "undefined" ? "" : localStorage.getItem("lip-token") || "";
  const response = await fetch(`${API_BASE}${path}`, {
    ...init,
    headers: {
      "Content-Type": "application/json",
      ...(token ? { Authorization: `Demo ${token}` } : {}),
      ...(init?.headers || {}),
    },
  });
  const text = await response.text();
  const body = text ? JSON.parse(text) : null;
  if (!response.ok) {
    const error = new Error(body?.message || body?.code || response.statusText) as Error & { status?: number; body?: unknown };
    error.status = response.status;
    error.body = body;
    throw error;
  }
  return body as T;
}
