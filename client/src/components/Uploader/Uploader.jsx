import { useRef, useState } from "react";
import { uploadPdf } from "../../api/api";
import "./Uploader.css";

export default function Uploader({ onIndexed }) {
  const [state, setState] = useState("idle"); // idle | uploading | done | error
  const [message, setMessage] = useState("");
  const [fileName, setFileName] = useState("");
  const [dragging, setDragging] = useState(false);
  const inputRef = useRef(null);

  async function upload(file) {
    if (!file) return;
    const isPdf =
      file.type === "application/pdf" || file.name.toLowerCase().endsWith(".pdf");
    if (!isPdf) {
      setState("error");
      setFileName(file.name);
      setMessage("That isn't a PDF. Please choose a PDF file.");
      return;
    }
    setFileName(file.name);
    setState("uploading");
    setMessage("Reading and indexing…");
    try {
      const { chunks } = await uploadPdf(file);
      setState("done");
      setMessage(`Indexed ${chunks} chunks · ready to ask`);
      onIndexed?.();
    } catch (err) {
      setState("error");
      setMessage(err.message || "Upload failed");
    }
  }

  function onDrop(e) {
    e.preventDefault();
    setDragging(false);
    upload(e.dataTransfer.files?.[0]);
  }

  const busy = state === "uploading";

  return (
    <div
      className={`dropzone state-${state} ${dragging ? "is-dragging" : ""} ${
        busy ? "is-busy" : ""
      }`}
      onDragOver={(e) => {
        e.preventDefault();
        if (!busy) setDragging(true);
      }}
      onDragLeave={() => setDragging(false)}
      onDrop={(e) => !busy && onDrop(e)}
      onClick={() => !busy && inputRef.current?.click()}
      onKeyDown={(e) =>
        (e.key === "Enter" || e.key === " ") && !busy && inputRef.current?.click()
      }
      role="button"
      tabIndex={0}
      aria-label="Upload a PDF"
    >
      <input
        ref={inputRef}
        type="file"
        accept="application/pdf"
        onChange={(e) => upload(e.target.files[0])}
        hidden
      />

      <span className="dz-icon" aria-hidden="true">
        {state === "uploading" ? (
          <span className="spinner" />
        ) : state === "done" ? (
          <svg width="22" height="22" viewBox="0 0 24 24" fill="none">
            <path
              d="M5 13l4 4L19 7"
              stroke="currentColor"
              strokeWidth="2.2"
              strokeLinecap="round"
              strokeLinejoin="round"
            />
          </svg>
        ) : (
          <svg width="22" height="22" viewBox="0 0 24 24" fill="none">
            <path
              d="M12 16V4m0 0L7.5 8.5M12 4l4.5 4.5"
              stroke="currentColor"
              strokeWidth="2"
              strokeLinecap="round"
              strokeLinejoin="round"
            />
            <path
              d="M4 15v2.5A2.5 2.5 0 0 0 6.5 20h11a2.5 2.5 0 0 0 2.5-2.5V15"
              stroke="currentColor"
              strokeWidth="2"
              strokeLinecap="round"
            />
          </svg>
        )}
      </span>

      <div className="dz-body">
        {state === "idle" ? (
          <>
            <p className="dz-title">
              Drop a PDF here, or <span className="dz-link">browse</span>
            </p>
            <p className="dz-sub">One document at a time · PDF only</p>
          </>
        ) : (
          <>
            <p className="dz-title">{fileName}</p>
            <p className={`dz-sub status-${state}`}>{message}</p>
          </>
        )}
      </div>

      {state !== "idle" && !busy && (
        <span className="dz-replace">Replace</span>
      )}
    </div>
  );
}
