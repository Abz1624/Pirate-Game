#!/usr/bin/env node
/** Local visual browser for piratenation-art reference assets. */

import http from "node:http";
import fs from "node:fs";
import fsp from "node:fs/promises";
import path from "node:path";
import { fileURLToPath } from "node:url";
import { exec } from "node:child_process";

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const ROOT = path.resolve(__dirname, "..", "..");
const STATIC = path.join(__dirname, "static");
const HOST = "127.0.0.1";
const PORT = 8765;

/**
 * Prefer curated Pirate Assets under references/, then full art clone,
 * then WSL clone, then partial Windows checkout.
 */
function resolveAssetsRoot() {
  const candidates = [
    path.join(ROOT, "references", "Pirate Assets"),
    path.join(ROOT, "references", "piratenation-art-full"),
    "\\\\wsl$\\Ubuntu\\home\\kshitij\\piratenation-refs\\piratenation-art",
    path.join(ROOT, "references", "piratenation-art"),
  ];
  for (const candidate of candidates) {
    const ships = path.join(candidate, "Voxel Game Assets", "ships");
    if (fs.existsSync(ships)) return candidate;
  }
  for (const candidate of candidates) {
    if (fs.existsSync(candidate)) return candidate;
  }
  return candidates[0];
}

const ASSETS = resolveAssetsRoot();

const VIEWABLE = new Set([
  ".png",
  ".jpg",
  ".jpeg",
  ".webp",
  ".gif",
  ".svg",
  ".mp4",
  ".webm",
  ".gltf",
]);
const SKIP_DIRS = new Set([".git", "node_modules", "__pycache__"]);

let index = [];
let indexReady = false;
let indexError = null;

function categoryFor(rel) {
  const parts = rel.split("/");
  const top = parts[0] || "Other";
  if (top === "Combat Cards" || top === "Logos") return top;
  if (top.startsWith("Founder")) {
    return parts[1] ? `Founders / ${parts[1]}` : "Founders";
  }
  if (top === "Voxel Game Assets" && parts[1]) {
    return `Voxel / ${parts[1].trim()}`;
  }
  return top;
}

async function walk(dir, out) {
  let entries;
  try {
    entries = await fsp.readdir(dir, { withFileTypes: true });
  } catch {
    return;
  }
  for (const entry of entries) {
    if (entry.isDirectory()) {
      if (SKIP_DIRS.has(entry.name)) continue;
      await walk(path.join(dir, entry.name), out);
      continue;
    }
    if (!entry.isFile()) continue;
    const ext = path.extname(entry.name).toLowerCase();
    if (!VIEWABLE.has(ext)) continue;
    const full = path.join(dir, entry.name);
    const rel = path.relative(ASSETS, full).split(path.sep).join("/");
    const item = {
      path: rel,
      name: entry.name,
      ext: ext.slice(1),
      category: categoryFor(rel),
      kind:
        ext === ".mp4" || ext === ".webm"
          ? "video"
          : ext === ".gltf"
            ? "model"
            : "image",
    };

    if (item.kind === "model") {
      // Founders 3D models pair with numbered voxel PNG previews when present.
      if (/Founder's Pirate NFT\/model3d\/\d+\.gltf$/i.test(rel)) {
        const id = path.basename(entry.name, ".gltf");
        const previewRel = `Founder's Pirate NFT/voxel/${id}.png`;
        if (fs.existsSync(path.join(ASSETS, previewRel))) {
          item.preview = previewRel;
        }
      }
      // Unity import layout: .../<asset>/GLTF/model.gltf + .../<asset>/Images/thumbnail.png
      if (!item.preview && /\/GLTF\/[^/]+\.gltf$/i.test(rel)) {
        const assetDir = path.posix.dirname(path.posix.dirname(rel));
        for (const name of ["thumbnail.png", "thumbnail.jpg", "preview.png"]) {
          const previewRel = `${assetDir}/Images/${name}`;
          if (fs.existsSync(path.join(ASSETS, previewRel))) {
            item.preview = previewRel;
            break;
          }
        }
      }
    }

    out.push(item);
  }
}

async function buildIndex() {
  const started = Date.now();
  try {
    if (!fs.existsSync(ASSETS)) {
      throw new Error(`Assets folder not found: ${ASSETS}`);
    }
    const items = [];
    await walk(ASSETS, items);
    items.sort((a, b) => a.path.localeCompare(b.path, undefined, { sensitivity: "base" }));
    index = items;
    console.log(`Indexed ${items.length.toLocaleString()} viewable assets in ${((Date.now() - started) / 1000).toFixed(1)}s`);
  } catch (err) {
    indexError = err.message || String(err);
    console.error("Index failed:", indexError);
  } finally {
    indexReady = true;
  }
}

function sendJson(res, data, status = 200) {
  const body = JSON.stringify(data);
  res.writeHead(status, {
    "Content-Type": "application/json",
    "Content-Length": Buffer.byteLength(body),
    "Cache-Control": "no-store",
  });
  res.end(body);
}

function contentType(filePath) {
  const ext = path.extname(filePath).toLowerCase();
  return (
    {
      ".html": "text/html; charset=utf-8",
      ".js": "text/javascript; charset=utf-8",
      ".css": "text/css; charset=utf-8",
      ".json": "application/json",
      ".png": "image/png",
      ".jpg": "image/jpeg",
      ".jpeg": "image/jpeg",
      ".webp": "image/webp",
      ".gif": "image/gif",
      ".svg": "image/svg+xml",
      ".mp4": "video/mp4",
      ".webm": "video/webm",
      ".gltf": "model/gltf+json",
      ".bin": "application/octet-stream",
    }[ext] || "application/octet-stream"
  );
}

function isLfsPointer(buf) {
  if (buf.length > 300) return false;
  const text = buf.toString("utf8");
  return text.startsWith("version https://git-lfs.github.com/spec/v1");
}

async function sendFile(res, filePath, cache = false) {
  try {
    const data = await fsp.readFile(filePath);
    if (isLfsPointer(data)) {
      const msg =
        "Git LFS pointer only — real asset not downloaded yet. Run git lfs pull in the art repo, then re-sync to references/piratenation-art-full.";
      const body = Buffer.from(msg, "utf8");
      res.writeHead(424, {
        "Content-Type": "text/plain; charset=utf-8",
        "Content-Length": body.length,
        "X-Git-LFS-Pointer": "1",
        "Cache-Control": "no-store",
      });
      res.end(body);
      return;
    }
    res.writeHead(200, {
      "Content-Type": contentType(filePath),
      "Content-Length": data.length,
      "Cache-Control": cache ? "public, max-age=3600" : "no-store",
    });
    res.end(data);
  } catch {
    res.writeHead(404);
    res.end("Not found");
  }
}

function openBrowser(url) {
  const cmd =
    process.platform === "win32"
      ? `cmd /c start "" "${url}"`
      : process.platform === "darwin"
        ? `open "${url}"`
        : `xdg-open "${url}"`;
  exec(cmd);
}

const server = http.createServer(async (req, res) => {
  const url = new URL(req.url || "/", `http://${HOST}:${PORT}`);
  const { pathname, searchParams } = url;

  if (pathname === "/api/status") {
    return sendJson(res, {
      ready: indexReady,
      count: index.length,
      error: indexError,
      assetsRoot: ASSETS,
    });
  }

  if (pathname === "/api/categories") {
    while (!indexReady) await new Promise((r) => setTimeout(r, 100));
    const counts = new Map();
    for (const item of index) {
      counts.set(item.category, (counts.get(item.category) || 0) + 1);
    }
    const categories = [...counts.entries()]
      .map(([name, count]) => ({ name, count }))
      .sort((a, b) => b.count - a.count || a.name.localeCompare(b.name));
    return sendJson(res, { categories, total: index.length });
  }

  if (pathname === "/api/assets") {
    while (!indexReady) await new Promise((r) => setTimeout(r, 100));
    const category = searchParams.get("category") || "";
    const kind = searchParams.get("kind") || "";
    const q = (searchParams.get("q") || "").toLowerCase().trim();
    const offset = Math.max(0, Number.parseInt(searchParams.get("offset") || "0", 10) || 0);
    const limit = Math.min(200, Math.max(1, Number.parseInt(searchParams.get("limit") || "60", 10) || 60));

    let filtered = index;
    if (category) filtered = filtered.filter((i) => i.category === category);
    if (kind) filtered = filtered.filter((i) => i.kind === kind);
    if (q) filtered = filtered.filter((i) => i.path.toLowerCase().includes(q));

    return sendJson(res, {
      total: filtered.length,
      offset,
      limit,
      items: filtered.slice(offset, offset + limit),
    });
  }

  if (pathname.startsWith("/files/")) {
    // pathname is already percent-decoded by URL(), except encoded slashes.
    const rel = decodeURIComponent(pathname.slice("/files/".length));
    const root = path.resolve(ASSETS);
    const target = path.resolve(root, rel);
    const relCheck = path.relative(root, target);
    if (relCheck.startsWith("..") || path.isAbsolute(relCheck)) {
      res.writeHead(403);
      return res.end("Forbidden");
    }
    return sendFile(res, target, true);
  }

  if (pathname === "/" || pathname === "/index.html") {
    return sendFile(res, path.join(STATIC, "index.html"));
  }

  const staticPath = path.resolve(STATIC, "." + pathname);
  if (!staticPath.startsWith(STATIC + path.sep)) {
    res.writeHead(403);
    return res.end("Forbidden");
  }
  return sendFile(res, staticPath);
});

if (!fs.existsSync(ASSETS)) {
  console.error(`Missing assets at ${ASSETS}`);
  process.exit(1);
}

buildIndex();
server.listen(PORT, HOST, () => {
  const url = `http://${HOST}:${PORT}`;
  console.log(`Asset browser → ${url}`);
  console.log(`Serving assets from ${ASSETS}`);
  setTimeout(() => openBrowser(url), 600);
});
