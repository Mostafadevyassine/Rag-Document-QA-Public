import { useState } from "react";
import Uploader from "./components/Uploader/Uploader";
import Chat from "./components/Chat/Chat";
import "./App.css";

export default function App() {
  const [ready, setReady] = useState(false);

  return (
    <div className="app">
      <header className="topbar">
        <div className="brand">
          <span className="brand-mark" aria-hidden="true">
            <svg width="20" height="20" viewBox="0 0 24 24" fill="none">
              <path
                d="M5 4.5A2.5 2.5 0 0 1 7.5 2H15l4 4v11.5A2.5 2.5 0 0 1 16.5 20h-9A2.5 2.5 0 0 1 5 17.5v-13Z"
                fill="currentColor"
                opacity="0.95"
              />
              <circle cx="12" cy="13" r="2.4" fill="#fff" />
              <path d="M13.7 14.7l2 2" stroke="#fff" strokeWidth="1.6" strokeLinecap="round" />
            </svg>
          </span>
          <span className="brand-text">
            <span className="brand-title">DocQA</span>
            <span className="brand-sub">Grounded answers from your PDF</span>
          </span>
        </div>

        <span className={`doc-status ${ready ? "is-ready" : ""}`}>
          <span className="dot" />
          {ready ? "Document indexed" : "No document"}
        </span>
      </header>

      <main className="workspace">
        <Uploader onIndexed={() => setReady(true)} />
        <Chat ready={ready} />
      </main>

      <footer className="footer">
        Retrieval-Augmented Generation · React + .NET · OpenAI
      </footer>
    </div>
  );
}
