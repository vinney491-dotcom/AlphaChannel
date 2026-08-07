const STORAGE_KEY = "mythcase.sheets.v1";

const listEl = document.getElementById("sheet-list");
const emptyEl = document.getElementById("empty");
const countEl = document.getElementById("sheet-count");
const addDialog = document.getElementById("add-dialog");
const hintDialog = document.getElementById("hint-dialog");
const addForm = document.getElementById("add-form");
const titleInput = document.getElementById("title-input");
const urlInput = document.getElementById("url-input");
const formError = document.getElementById("form-error");

function loadSheets() {
  try {
    const raw = localStorage.getItem(STORAGE_KEY);
    if (!raw) return [];
    const parsed = JSON.parse(raw);
    return Array.isArray(parsed) ? parsed : [];
  } catch {
    return [];
  }
}

function saveSheets(sheets) {
  localStorage.setItem(STORAGE_KEY, JSON.stringify(sheets));
}

function parseMythWeaversURL(raw) {
  const trimmed = String(raw || "").trim();
  if (!trimmed) return null;

  let candidate = trimmed;
  if (!candidate.includes("://")) candidate = `https://${candidate}`;

  let url;
  try {
    url = new URL(candidate);
  } catch {
    return null;
  }

  const host = url.hostname.toLowerCase();
  if (!host.includes("myth-weavers.com")) return null;

  if (host === "myth-weavers.com" || host === "og.myth-weavers.com") {
    url.hostname = "www.myth-weavers.com";
  }

  const id = extractId(url);
  if (id) {
    url = new URL(`https://www.myth-weavers.com/sheets/?id=${encodeURIComponent(id)}`);
  }

  return { id: id || crypto.randomUUID(), url: url.toString() };
}

function extractId(url) {
  for (const key of ["id", "sheet", "sheetid"]) {
    const value = url.searchParams.get(key);
    if (value) return value;
  }

  if (url.hash) {
    const fragment = url.hash.replace(/^#/, "");
    for (const part of fragment.split("&")) {
      const [k, v] = part.split("=");
      if (k && k.toLowerCase() === "id" && v) return decodeURIComponent(v);
    }
  }

  const segments = url.pathname.split("/").filter(Boolean);
  const last = segments[segments.length - 1];
  if (last && /^\d+$/.test(last)) return last;

  return null;
}

function render() {
  const sheets = loadSheets().sort(
    (a, b) => (b.lastOpened || b.addedAt) - (a.lastOpened || a.addedAt)
  );

  countEl.textContent = String(sheets.length);
  listEl.innerHTML = "";

  if (!sheets.length) {
    emptyEl.hidden = false;
    listEl.hidden = true;
    return;
  }

  emptyEl.hidden = true;
  listEl.hidden = false;

  for (const sheet of sheets) {
    const li = document.createElement("li");
    li.className = "sheet-item";

    const openBtn = document.createElement("button");
    openBtn.type = "button";
    openBtn.className = "open";
    openBtn.innerHTML = `<div class="title"></div><div class="meta"></div>`;
    openBtn.querySelector(".title").textContent = sheet.title;
    openBtn.querySelector(".meta").textContent = sheet.id.match(/^\d+$/)
      ? `ID ${sheet.id}`
      : "Public Myth-Weavers link";
    openBtn.addEventListener("click", () => openSheet(sheet.id));

    const delBtn = document.createElement("button");
    delBtn.type = "button";
    delBtn.className = "delete";
    delBtn.setAttribute("aria-label", `Remove ${sheet.title}`);
    delBtn.textContent = "Remove";
    delBtn.addEventListener("click", () => removeSheet(sheet.id));

    li.append(openBtn, delBtn);
    listEl.append(li);
  }
}

function openSheet(id) {
  const sheets = loadSheets();
  const index = sheets.findIndex((s) => s.id === id);
  if (index < 0) return;
  sheets[index].lastOpened = Date.now();
  saveSheets(sheets);
  // Full Myth-Weavers viewer (they block embedding, so open the real sheet page).
  window.location.assign(sheets[index].url);
}

function removeSheet(id) {
  const next = loadSheets().filter((s) => s.id !== id);
  saveSheets(next);
  render();
}

function addSheet(title, rawUrl) {
  const parsed = parseMythWeaversURL(rawUrl);
  if (!parsed) {
    return { ok: false, error: "That doesn’t look like a Myth-Weavers URL." };
  }

  const sheets = loadSheets();
  const existing = sheets.findIndex((s) => s.id === parsed.id || s.url === parsed.url);
  const cleanedTitle = title.trim() || (parsed.id.match(/^\d+$/) ? `Sheet #${parsed.id}` : "Myth-Weavers Sheet");

  if (existing >= 0) {
    sheets[existing].url = parsed.url;
    sheets[existing].title = title.trim() || sheets[existing].title;
    sheets[existing].lastOpened = Date.now();
  } else {
    sheets.unshift({
      id: parsed.id,
      title: cleanedTitle,
      url: parsed.url,
      addedAt: Date.now(),
      lastOpened: Date.now(),
    });
  }

  saveSheets(sheets);
  return { ok: true };
}

document.getElementById("open-add").addEventListener("click", () => {
  formError.hidden = true;
  titleInput.value = "";
  urlInput.value = "";
  addDialog.showModal();
  urlInput.focus();
});

document.getElementById("install-hint-btn").addEventListener("click", () => {
  hintDialog.showModal();
});

document.getElementById("close-hint").addEventListener("click", () => {
  hintDialog.close();
});

addForm.addEventListener("submit", (event) => {
  // dialog form method=dialog closes automatically; intercept the add path
  const submitter = event.submitter;
  if (!submitter || submitter.value !== "add") return;

  event.preventDefault();
  const result = addSheet(titleInput.value, urlInput.value);
  if (!result.ok) {
    formError.textContent = result.error;
    formError.hidden = false;
    return;
  }
  addDialog.close();
  render();
});

if ("serviceWorker" in navigator) {
  window.addEventListener("load", () => {
    navigator.serviceWorker.register("./sw.js").catch(() => {
      /* offline cache is optional */
    });
  });
}

render();
