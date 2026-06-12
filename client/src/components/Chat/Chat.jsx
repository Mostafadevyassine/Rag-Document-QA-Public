import { useState, useRef, useEffect } from "react";
import ReactMarkdown from "react-markdown";
import { askStream } from "../../api/api";
import "./Chat.css";

export default function Chat({ ready }) {
  const [messages, setMessages] = useState([]); // { role, text, sources? }
  const [input, setInput] = useState("");
  const [loading, setLoading] = useState(false);
  const endRef = useRef(null);

  // Auto-scroll to the latest message as it streams in.
  useEffect(() => {
    endRef.current?.scrollIntoView({ behavior: "smooth", block: "end" });
  }, [messages]);

  async function send() {
    const q = input.trim();
    if (!q || loading) return;
    setInput("");

    // Conversation so far (before this question) → backend, capped to recent turns.
    const history = messages
      .filter((m) => m.text)
      .slice(-10)
      .map((m) => ({ role: m.role, content: m.text }));

    // Add the user message + an empty assistant message we'll fill as tokens stream in.
    setMessages((m) => [
      ...m,
      { role: "user", text: q },
      { role: "assistant", text: "", sources: [] },
    ]);
    setLoading(true);

    // Update only the last message (the streaming assistant one).
    const patchLast = (patch) =>
      setMessages((m) => {
        const copy = [...m];
        const last = copy[copy.length - 1];
        copy[copy.length - 1] = { ...last, ...patch(last) };
        return copy;
      });

    try {
      await askStream(q, history, {
        onSources: (sources) => patchLast(() => ({ sources })),
        onToken: (text) => patchLast((last) => ({ text: last.text + text })),
      });
    } catch (err) {
      patchLast(() => ({ text: `Something went wrong: ${err.message}`, sources: [] }));
    } finally {
      setLoading(false);
    }
  }

  return (
    <section className="chat">
      <div className="messages">
        {messages.length === 0 ? (
          <div className="empty">
            <div className="empty-icon" aria-hidden="true">
              <svg width="30" height="30" viewBox="0 0 24 24" fill="none">
                <path
                  d="M4 5.5A2.5 2.5 0 0 1 6.5 3h11A2.5 2.5 0 0 1 20 5.5v8A2.5 2.5 0 0 1 17.5 16H9l-4 4v-4H6.5"
                  stroke="currentColor"
                  strokeWidth="1.7"
                  strokeLinejoin="round"
                  fill="none"
                />
                <path
                  d="M8 8h8M8 11h5"
                  stroke="currentColor"
                  strokeWidth="1.7"
                  strokeLinecap="round"
                />
              </svg>
            </div>
            <p className="empty-title">
              {ready ? "Ask anything about your document" : "Upload a PDF to begin"}
            </p>
            <p className="empty-sub">
              Answers are grounded in your document — each one shows the exact
              passages it used, with a relevance score.
            </p>
          </div>
        ) : (
          messages.map((m, i) => {
            const isLast = i === messages.length - 1;
            const streaming = loading && isLast && m.role === "assistant";
            return (
              <div key={i} className={`row row-${m.role}`}>
                {m.role === "assistant" && (
                  <span className="avatar avatar-ai" aria-hidden="true">
                    <svg width="16" height="16" viewBox="0 0 24 24" fill="none">
                      <path
                        d="M12 3l1.8 4.2L18 9l-4.2 1.8L12 15l-1.8-4.2L6 9l4.2-1.8L12 3Z"
                        fill="currentColor"
                      />
                      <circle cx="18" cy="17" r="1.6" fill="currentColor" />
                    </svg>
                  </span>
                )}

                <div className="bubble-wrap">
                  <div className="bubble">
                    {m.text === "" && streaming ? (
                      <span className="typing" aria-label="Thinking">
                        <span />
                        <span />
                        <span />
                      </span>
                    ) : m.role === "assistant" ? (
                      <div className="md">
                        <ReactMarkdown>{m.text}</ReactMarkdown>
                        {streaming && <span className="caret" />}
                      </div>
                    ) : (
                      <span className="bubble-text">{m.text}</span>
                    )}
                  </div>

                  {m.role === "assistant" && m.sources?.length > 0 && (
                    <details className="sources">
                      <summary>
                        <svg
                          className="src-chevron"
                          width="13"
                          height="13"
                          viewBox="0 0 24 24"
                          fill="none"
                        >
                          <path
                            d="M9 6l6 6-6 6"
                            stroke="currentColor"
                            strokeWidth="2.2"
                            strokeLinecap="round"
                            strokeLinejoin="round"
                          />
                        </svg>
                        {m.sources.length} source{m.sources.length > 1 ? "s" : ""} used
                      </summary>
                      <div className="source-list">
                        {m.sources.map((s, j) => (
                          <div key={j} className="source">
                            <div className="source-head">
                              <span className="source-rank">#{j + 1}</span>
                              <span
                                className={`source-tag ${j === 0 ? "is-best" : ""}`}
                                title={`${Math.round(s.score * 100)}% similarity`}
                              >
                                {j === 0 ? "Best match" : "Related"}
                              </span>
                            </div>
                            <p className="source-text">{s.text.trim()}</p>
                          </div>
                        ))}
                      </div>
                    </details>
                  )}
                </div>

                {m.role === "user" && (
                  <span className="avatar avatar-you" aria-hidden="true">
                    You
                  </span>
                )}
              </div>
            );
          })
        )}
        <div ref={endRef} />
      </div>

      <div className="composer">
        <input
          value={input}
          placeholder={ready ? "Ask a question…" : "Upload a PDF first"}
          disabled={!ready}
          onChange={(e) => setInput(e.target.value)}
          onKeyDown={(e) => e.key === "Enter" && send()}
        />
        <button
          className="send-btn"
          onClick={send}
          disabled={!ready || loading || !input.trim()}
          aria-label="Send"
        >
          <svg width="18" height="18" viewBox="0 0 24 24" fill="none">
            <path
              d="M4 12l16-8-6 16-2.5-6.5L4 12Z"
              fill="currentColor"
              stroke="currentColor"
              strokeWidth="1.4"
              strokeLinejoin="round"
            />
          </svg>
        </button>
      </div>
    </section>
  );
}
