/**
 * Unity scene YAML: collect all document local IDs in the subtree under a root GameObject.
 * Documents use "--- !u!TYPE &LOCALID"
 */
import fs from "fs";

function splitDocuments(text) {
  const parts = text.split(/^--- /m);
  const docs = [];
  for (let i = 1; i < parts.length; i++) {
    const body = parts[i];
    const firstLine = body.split(/\r?\n/)[0];
    const m = firstLine.match(/^!u!(\d+) &(-?\d+)/);
    if (!m) continue;
    docs.push({ type: parseInt(m[1], 10), id: parseInt(m[2], 10), raw: "--- " + body });
  }
  return docs;
}

function buildMap(docs) {
  const byId = new Map();
  for (const d of docs) byId.set(d.id, d);
  return byId;
}

function parseYamlList(block, key) {
  const re = new RegExp(`^\\s*${key}:\\s*$`, "m");
  const idx = block.search(re);
  if (idx < 0) return [];
  const after = block.slice(idx);
  const lines = after.split(/\r?\n/);
  const out = [];
  let inList = false;
  for (const line of lines) {
    if (line.match(new RegExp(`^${key}:\\s*$`))) {
      inList = true;
      continue;
    }
    if (inList) {
      let lm = line.match(/^\s*-\s*\{fileID:\s*(-?\d+)\}/);
      if (lm) {
        out.push(parseInt(lm[1], 10));
        continue;
      }
      lm = line.match(/^\s*-\s*component:\s*\{fileID:\s*(-?\d+)\}/);
      if (lm) {
        out.push(parseInt(lm[1], 10));
        continue;
      }
      if (line.match(/^\S/) && !line.startsWith("-")) break;
    }
  }
  return out;
}

function parseYamlScalar(block, key) {
  const m = block.match(new RegExp(`^\\s*${key}:\\s*\\{fileID:\\s*(-?\\d+)\\}`, "m"));
  if (!m) return null;
  const v = parseInt(m[1], 10);
  return v === 0 ? null : v;
}

function collectSubtree(byId, rootGoId) {
  const all = new Set();
  const goQueue = [rootGoId];

  while (goQueue.length) {
    const goId = goQueue.pop();
    if (all.has(goId)) continue;
    all.add(goId);

    const goDoc = byId.get(goId);
    if (!goDoc || goDoc.type !== 1) continue;

    const comps = parseYamlList(goDoc.raw, "m_Component");
    for (const cid of comps) {
      if (cid === 0 || all.has(cid)) continue;
      all.add(cid);
      const cDoc = byId.get(cid);
      if (!cDoc) continue;

      const children = parseYamlList(cDoc.raw, "m_Children");
      for (const childTid of children) {
        if (childTid === 0) continue;
        const tDoc = byId.get(childTid);
        if (!tDoc) continue;
        all.add(childTid);
        const childGo = parseYamlScalar(tDoc.raw, "m_GameObject");
        if (childGo != null && !all.has(childGo)) goQueue.push(childGo);
      }
    }
  }
  return all;
}

const rootId = 1836535431; // ConfigCanvas GameObject
import path from "path";
import { fileURLToPath } from "url";
const __dirname = path.dirname(fileURLToPath(import.meta.url));

const headPath = path.join(process.env.TEMP || process.env.TMPDIR || "/tmp", "title_scene_HEAD.unity");
const workPath = path.join(__dirname, "..", "Assets", "Scenes", "title_scene.unity");

const headText = fs.readFileSync(headPath, "utf8");
console.log("headPath", headPath, "len", headText.length);
const workText = fs.readFileSync(workPath, "utf8");

const headDocs = splitDocuments(headText);
const workDocs = splitDocuments(workText);
const headBy = buildMap(headDocs);
const workBy = buildMap(workDocs);

console.log("head docs", headDocs.length, "has root", headBy.has(rootId));
const rootDoc = headBy.get(rootId);
if (rootDoc) console.log("root comps", parseYamlList(rootDoc.raw, "m_Component"));

const headSet = collectSubtree(headBy, rootId);
const workSet = collectSubtree(workBy, rootId);

const onlyWork = [...workSet].filter((id) => !headSet.has(id)).sort((a, b) => a - b);
const onlyHead = [...headSet].filter((id) => !workSet.has(id)).sort((a, b) => a - b);

console.log("HEAD subtree size", headSet.size);
console.log("WORK subtree size", workSet.size);
console.log("Only in WORK (extra)", onlyWork.length, onlyWork.slice(0, 50));
console.log("Only in HEAD (missing in work)", onlyHead.length, onlyHead.slice(0, 50));
