const BASE = import.meta.env.VITE_API_URL ?? "http://localhost:5000";

export async function uploadPdf(file) {
  const form = new FormData();
  form.append("file", file); // name must match the .NET endpoint param "file"
  // Do NOT set Content-Type here — the browser sets the multipart boundary.
  const res = await fetch(`${BASE}/upload`, { method: "POST", body: form });
  if (!res.ok) throw new Error(await res.text());
  return res.json(); // { chunks }
}

// Streams the answer as Server-Sent Events. `history` is the prior conversation
// ([{ role, content }]) so follow-up questions keep context. Calls
// onSources([{ text, score }]) once with the retrieved citations, then
// onToken(string) for each token as it arrives. Resolves when done.
export async function askStream(question, history = [], { onSources, onToken } = {}) {
  const res = await fetch(`${BASE}/ask`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ question, history }),
  });
  if (!res.ok) throw new Error(await res.text());

  const reader = res.body.getReader();
  const decoder = new TextDecoder();
  let buffer = "";

  while (true) {
    const { value, done } = await reader.read();
    if (done) break;
    buffer += decoder.decode(value, { stream: true });

    // SSE events are separated by a blank line.
    const events = buffer.split("\n\n");
    buffer = events.pop(); // keep the last, possibly-incomplete event

    for (const event of events) {
      const line = event.trim();
      if (!line.startsWith("data:")) continue;
      const evt = JSON.parse(line.slice(5).trim());
      if (evt.type === "sources") onSources?.(evt.sources);
      else if (evt.type === "token") onToken?.(evt.text);
      else if (evt.type === "error") throw new Error(evt.message);
    }
  }
}
