// Operator UI istemci kodu. Saf vanilla JS, build adımı yok.
// API çağrıları aynı origin'e (servis) yapılır; X-Api-Key gerekirse localStorage'dan eklenir.

const API_KEY = localStorage.getItem("apiKey") || "";

function headers(extra = {}) {
  const h = { "Content-Type": "application/json", ...extra };
  if (API_KEY) h["X-Api-Key"] = API_KEY;
  return h;
}

async function api(path, init = {}) {
  const opts = { ...init, headers: headers(init.headers) };
  const r = await fetch(path, opts);
  if (!r.ok) {
    let detail = `${r.status} ${r.statusText}`;
    try { const j = await r.json(); detail = j.detail || j.title || detail; } catch {}
    throw new Error(detail);
  }
  return r.headers.get("content-type")?.includes("json") ? r.json() : r.text();
}

// ----- Banner -----
function showBanner(kind, title, detail) {
  const banner = document.getElementById("banner");
  const box = document.getElementById("banner-box");
  document.getElementById("banner-title").textContent = title;
  document.getElementById("banner-detail").textContent = detail;
  banner.classList.remove("hidden");

  box.className = "rounded border-l-4 p-4 shadow-sm";
  if (kind === "error")  box.classList.add("bg-rose-50", "border-rose-500", "text-rose-900");
  if (kind === "ok")     box.classList.add("bg-emerald-50", "border-emerald-500", "text-emerald-900");
  if (kind === "info")   box.classList.add("bg-blue-50", "border-blue-500", "text-blue-900");
}
function hideBanner() { document.getElementById("banner").classList.add("hidden"); }

// ----- Bağlantı -----
async function connect() {
  const mode = document.querySelector("input[name=mode]:checked").value;
  try {
    const r = await api("/connect", { method: "POST", body: JSON.stringify({ mode }) });
    document.getElementById("connect-result").textContent =
      r.connected ? `Bağlandı (${r.mode})` : `Bağlantı bekleniyor; reconnect aktif (${r.mode})`;
    showBanner("ok", "Bağlantı isteği gönderildi", r.message);
    refreshAll();
  } catch (e) {
    showBanner("error", "Bağlantı isteği başarısız", e.message);
  }
}

// ----- Print -----
async function printText() {
  const text = document.getElementById("text-input").value;
  if (!text) return showBanner("error", "Metin boş", "Metin alanı doldurulmalı.");
  try {
    const r = await api("/print/text", { method: "POST", body: JSON.stringify({ text }) });
    showBanner("ok", "Metin kuyruğa atıldı", `jobId=${r.jobId}`);
    refreshAll();
  } catch (e) { showBanner("error", "Basım hatası", e.message); }
}

async function printImage() {
  const file = document.getElementById("image-input").files[0];
  if (!file) return showBanner("error", "Görsel yok", "Dosya seçin.");
  const b64 = await fileToBase64(file);
  try {
    const r = await api("/print/image", { method: "POST", body: JSON.stringify({ imageBase64: b64 }) });
    showBanner("ok", "Görsel kuyruğa atıldı", `jobId=${r.jobId}`);
    refreshAll();
  } catch (e) { showBanner("error", "Görsel basım hatası", e.message); }
}

async function printQr() {
  const data = document.getElementById("qr-data").value;
  if (!data) return showBanner("error", "QR data boş", "URL/metin girin.");
  const moduleSize = parseInt(document.getElementById("qr-size").value, 10);
  const ecc = document.getElementById("qr-ecc").value;
  try {
    const r = await api("/print/qr", { method: "POST", body: JSON.stringify({ data, moduleSize, ecc }) });
    showBanner("ok", "QR kuyruğa atıldı", `jobId=${r.jobId}`);
    refreshAll();
  } catch (e) { showBanner("error", "QR basım hatası", e.message); }
}

async function printReceipt() {
  const txt = document.getElementById("receipt-input").value;
  let body;
  try { body = JSON.parse(txt); }
  catch { return showBanner("error", "Geçersiz JSON", "Receipt gövdesi geçerli JSON olmalı."); }
  try {
    const r = await api("/print/receipt", { method: "POST", body: JSON.stringify(body) });
    showBanner("ok", "Fiş kuyruğa atıldı", `jobId=${r.jobId}`);
    refreshAll();
  } catch (e) { showBanner("error", "Fiş basım hatası", e.message); }
}

async function reprint(jobId) {
  try {
    await api(`/reprint/${jobId}`, { method: "POST" });
    showBanner("ok", "Yeniden basım kuyruğa atıldı", jobId);
    refreshAll();
  } catch (e) { showBanner("error", "Reprint hatası", e.message); }
}

// ----- Quick helpers -----
async function quickPrintText() {
  document.getElementById("text-input").value = "Test fişi\nMerhaba ACO\nReward: 3.00 ₺\n";
  showTab("text");
  await printText();
}

function fillSampleQr() {
  document.getElementById("qr-data").value = "https://aco.test/r/ACO-TEST-0001-0001";
}

function fillSampleReceipt() {
  const sample = sampleReceipt();
  document.getElementById("receipt-input").value = JSON.stringify(sample, null, 2);
  renderPreview();
}

function sampleReceipt() {
  return {
    elements: [
      { type: "text", text: "ACO RECYCLING", alignment: "Center", bold: true, width: 2, height: 2 },
      { type: "text", text: `MachineID: ACO-TEST-0001-0001`, alignment: "Center" },
      { type: "text", text: new Date().toISOString().replace("T", " ").slice(0, 19) + " UTC", alignment: "Center" },
      { type: "text", text: "Reward: 3.00 ₺", alignment: "Center", bold: true, width: 2, height: 2 },
      { type: "separator", character: "-" },
      {
        type: "table",
        headers: ["Product", "Qty", "Reward"],
        rows: [["Glass", "0", "0"], ["Plastic", "2", "2"], ["Metal", "1", "1"], ["Tetrapak", "0", "0"]]
      },
      { type: "feed", lines: 2 },
      { type: "qr", qrData: "https://aco.test/r/ACO-TEST-0001-0001", qrModuleSize: 6 },
      { type: "feed", lines: 3 },
      { type: "cut", feedDots: 30 }
    ]
  };
}

async function quickPrintReceipt() {
  fillSampleReceipt();
  showTab("receipt");
  await printReceipt();
}

// ----- Refresh status + logs -----
async function refreshAll() {
  try {
    const status = await api("/status");
    renderStatus(status);
  } catch (e) { /* sessiz: badge kalır */ }
  try {
    const logs = await api("/logs");
    renderLogs(logs);
    renderFailedFromLogs(logs);
  } catch (e) { /* sessiz */ }
}

function renderStatus(s) {
  const conn = document.getElementById("badge-conn");
  conn.textContent = s.connected ? `${s.mode} bağlı` : (s.mode ? `${s.mode} bağlanıyor...` : "bağlı değil");
  let connColor = "bg-slate-600";
  if (s.connected) connColor = (s.mode === "Mock") ? "bg-violet-500" : "bg-emerald-500";
  conn.className = "px-3 py-1 rounded-full text-xs font-semibold " + connColor;

  const state = document.getElementById("badge-state");
  state.textContent = s.state;
  const color = {
    Ready: "bg-emerald-500", Disconnected: "bg-slate-600",
    PaperOut: "bg-rose-500", PaperJam: "bg-rose-500",
    CoverOpen: "bg-amber-500", Overheat: "bg-orange-500",
    CommError: "bg-rose-700", UnknownCommand: "bg-rose-500"
  }[s.state] || "bg-slate-500";
  state.className = "px-3 py-1 rounded-full text-xs font-semibold " + color;

  document.getElementById("q-pending").textContent = s.queue.pending;
  document.getElementById("q-inflight").textContent = s.queue.inFlight;
  document.getElementById("q-succeeded").textContent = s.queue.succeeded ?? 0;
  document.getElementById("q-failed").textContent = s.queue.failed;

  document.getElementById("eta").textContent =
    (s.queue.etaSeconds != null) ? `${s.queue.etaSeconds.toFixed(1)} sn` : "—";

  document.getElementById("last-job").textContent = s.lastJob
    ? `${s.lastJob.kind} • ${s.lastJob.status}${s.lastJob.errorCode ? " • " + s.lastJob.errorCode : ""}`
    : "yok";

  if (s.paper) renderPaper(s.paper);
}

function renderPaper(p) {
  const bar = document.getElementById("paper-bar");
  bar.style.width = p.remainingPercent + "%";
  bar.className = "h-3 transition-all " +
    (p.remainingPercent > 30 ? "bg-emerald-500"
      : p.remainingPercent > 10 ? "bg-amber-500"
      : "bg-rose-500");

  document.getElementById("paper-remaining").textContent = p.remainingMm.toLocaleString();
  document.getElementById("paper-percent").textContent = p.remainingPercent.toFixed(2) + "%";
  document.getElementById("paper-used").textContent = p.usedMm.toLocaleString();
  document.getElementById("paper-jobs").textContent = p.estimatedJobsRemaining ?? "—";
  document.getElementById("paper-avg").textContent = p.averageJobMm ?? "—";
  document.getElementById("paper-printed-count").textContent = p.printedJobCount;
}

async function resetPaper() {
  if (!confirm("Yeni rulo takıldı mı? Sayaç sıfırlanacak.")) return;
  try {
    await api("/paper/reset", { method: "POST" });
    showBanner("ok", "Rulo sayacı sıfırlandı", "");
    refreshAll();
  } catch (e) { showBanner("error", "Sıfırlama hatası", e.message); }
}

function renderLogs(logs) {
  const tbody = document.getElementById("logs-body");
  const last = logs.slice(-50).reverse();
  tbody.innerHTML = last.map(l => `
    <tr class="border-t hover:bg-slate-50">
      <td class="p-2">${new Date(l.ts).toLocaleTimeString()}</td>
      <td class="p-2">${l.op}</td>
      <td class="p-2">${l.conn || ""}</td>
      <td class="p-2 truncate max-w-xs">${l.jobId || ""}</td>
      <td class="p-2"><span class="${l.status === "ok" ? "text-emerald-600" : (l.status === "failed" ? "text-rose-600" : "text-slate-600")}">${l.status}</span></td>
      <td class="p-2 text-rose-600">${l.error ? (l.error.code + " — " + l.error.detail) : ""}</td>
    </tr>`).join("");
}

function renderFailedFromLogs(logs) {
  const seen = new Set();
  const failed = [];
  for (const l of logs.slice().reverse()) {
    if (l.status === "failed" && l.jobId && !seen.has(l.jobId)) {
      seen.add(l.jobId);
      failed.push(l);
      if (failed.length >= 10) break;
    }
  }
  const root = document.getElementById("failed-list");
  if (failed.length === 0) { root.textContent = "yok"; return; }
  root.innerHTML = failed.map(f => `
    <div class="flex items-center justify-between border-b py-1">
      <div class="flex-1">
        <div class="text-xs font-mono text-slate-500">${f.jobId.slice(0, 8)}...</div>
        <div class="text-xs">${f.op} • <span class="text-rose-600">${f.error ? f.error.code : "ERR"}</span></div>
      </div>
      <button onclick="reprint('${f.jobId}')" class="text-xs bg-slate-900 text-white px-3 py-1 rounded hover:bg-slate-700">
        Tekrar Bastır
      </button>
    </div>`).join("");
}

function showTab(name) {
  document.querySelectorAll(".tab-panel").forEach(p => p.classList.add("hidden"));
  document.getElementById("tab-" + name).classList.remove("hidden");
  document.querySelectorAll(".tab-btn").forEach(b => {
    b.classList.remove("border-b-2", "border-slate-900");
    b.classList.add("text-slate-500");
    if (b.dataset.tab === name) {
      b.classList.add("border-b-2", "border-slate-900");
      b.classList.remove("text-slate-500");
    }
  });
}

function fileToBase64(file) {
  return new Promise((resolve, reject) => {
    const r = new FileReader();
    r.onload = () => resolve(r.result.split(",")[1]);
    r.onerror = reject;
    r.readAsDataURL(file);
  });
}

// Image preview (orijinal)
document.addEventListener("DOMContentLoaded", () => {
  const input = document.getElementById("image-input");
  if (input) {
    input.addEventListener("change", e => {
      const f = e.target.files[0];
      if (!f) return;
      const reader = new FileReader();
      reader.onload = ev => {
        document.getElementById("image-preview").src = ev.target.result;
        document.getElementById("image-preview-wrap").classList.remove("hidden");
      };
      reader.readAsDataURL(f);
    });
  }
});

// ===========================================================
// FIS ONIZLEME — receipt JSON'i HTML olarak render eder
// ===========================================================
function renderPreview(forceShow = false) {
  const txt = document.getElementById("receipt-input").value.trim();
  const root = document.getElementById("preview-paper");
  if (!txt) {
    if (forceShow) {
      root.innerHTML = '<div class="text-center text-rose-500 text-xs py-10">Önce JSON girin.</div>';
    } else {
      root.innerHTML = '<div class="text-center text-slate-400 text-xs py-10">Önizleme için "Fiş (JSON)" sekmesini doldur.</div>';
    }
    return;
  }
  let doc;
  try { doc = JSON.parse(txt); }
  catch {
    root.innerHTML = '<div class="text-center text-rose-500 text-xs py-4">Geçersiz JSON</div>';
    return;
  }
  if (!doc.elements || !Array.isArray(doc.elements)) {
    root.innerHTML = '<div class="text-center text-rose-500 text-xs py-4">elements alanı gerekli</div>';
    return;
  }

  const parts = [];
  for (const el of doc.elements) {
    parts.push(renderElement(el));
  }
  root.innerHTML = parts.join("");
}

function renderElement(el) {
  const type = (el.type || "").toLowerCase();
  switch (type) {
    case "text": return renderText(el);
    case "image": return renderImage(el);
    case "qr": return renderQr(el);
    case "table": return renderTable(el);
    case "feed": return renderFeed(el);
    case "separator": return renderSeparator(el);
    case "cut": return renderCut();
    default: return `<div class="text-rose-500 text-xs">[bilinmeyen element: ${escapeHtml(el.type || "?")}]</div>`;
  }
}

function renderText(el) {
  const align = (el.alignment || "Left").toLowerCase();
  const w = el.width || 1, h = el.height || 1;
  const size = Math.min(Math.max(w, h), 4); // 1-4 görsel skala
  const fontSize = 12 + (size - 1) * 4; // 12, 16, 20, 24px
  const style = [
    `text-align:${align}`,
    `font-weight:${el.bold ? "bold" : "normal"}`,
    `text-decoration:${el.underline ? "underline" : "none"}`,
    `font-size:${fontSize}px`,
    `line-height:1.25`
  ].join(";");
  // ₺ -> TL fallback (server bunu yapıyor; preview de tutarlı olsun)
  const safeText = (el.text || "").replace(/₺/g, "TL").replace(/€/g, "EUR");
  return `<div style="${style}">${escapeHtml(safeText)}</div>`;
}

function renderImage(el) {
  if (!el.imageBase64) return '<div class="text-xs text-slate-400 text-center">[görsel yok]</div>';
  const align = (el.alignment || "Center").toLowerCase();
  // mime sniff: PNG header? Default png.
  const src = `data:image/png;base64,${el.imageBase64}`;
  return `<div style="text-align:${align}"><img src="${src}" style="max-width:90%; filter:grayscale(100%) contrast(1.4); border:1px dashed #ccc; padding:2px" alt="receipt image"/></div>`;
}

function renderQr(el) {
  if (!el.qrData) return '<div class="text-xs text-slate-400 text-center">[QR data yok]</div>';
  const align = (el.alignment || "Center").toLowerCase();
  const size = el.qrModuleSize || 6;
  const cssSize = Math.min(180, size * 24); // görsel boyut
  // qrcode-generator: typeNumber=0 auto, ecc M
  const ecc = { L: "L", M: "M", Q: "Q", H: "H" }[(el.qrEcc || "M").toUpperCase()] || "M";
  let svg = "";
  try {
    const q = qrcode(0, ecc);
    q.addData(el.qrData);
    q.make();
    svg = q.createSvgTag({ cellSize: 4, margin: 1 });
  } catch (e) {
    return `<div class="text-xs text-rose-500 text-center">[QR render hatasi: ${escapeHtml(e.message || "")}]</div>`;
  }
  return `<div style="text-align:${align}"><div style="display:inline-block; width:${cssSize}px; height:${cssSize}px; line-height:0">${svg.replace("<svg", `<svg style="width:100%;height:100%"`)}</div></div>`;
}

function renderTable(el) {
  if (!el.headers || !Array.isArray(el.headers)) return '';
  const rows = el.rows || [];
  const colCount = el.headers.length;
  const colWidth = `${100 / colCount}%`;
  const ths = el.headers.map(h =>
    `<th style="text-align:left; padding:1px 4px; border-bottom:1px solid #000; font-size:11px; width:${colWidth}; font-weight:${el.headerBold !== false ? "bold" : "normal"}">${escapeHtml(h || "")}</th>`
  ).join("");
  const trs = rows.map(r =>
    `<tr>${r.map(c => `<td style="padding:1px 4px; font-size:11px">${escapeHtml(c || "")}</td>`).join("")}</tr>`
  ).join("");
  return `<table style="width:100%; border-collapse:collapse; font-family:'Courier New', monospace; margin:4px 0"><thead><tr>${ths}</tr></thead><tbody>${trs}</tbody></table>`;
}

function renderFeed(el) {
  const lines = el.lines || 1;
  return `<div style="height:${lines * 8}px"></div>`;
}

function renderSeparator(el) {
  const ch = (el.character || "-").charAt(0);
  // Yaklaşık 32 karakter genişlik (KP-300 standart 12-dot font)
  const line = ch.repeat(32);
  return `<div style="font-family:'Courier New', monospace; font-size:11px">${escapeHtml(line)}</div>`;
}

function renderCut() {
  return `<div style="border-top:1px dashed #888; margin-top:8px; padding-top:4px; text-align:center; color:#888; font-size:9px">— ✂ kesim —</div>`;
}

function escapeHtml(s) {
  return String(s).replace(/[&<>"']/g, c =>
    ({ "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;", "'": "&#039;" })[c]);
}

// İlk render + periyodik yenileme
refreshAll();
setInterval(refreshAll, 2000);
