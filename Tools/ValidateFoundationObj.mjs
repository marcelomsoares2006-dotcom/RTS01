#!/usr/bin/env node
/** Validate generated OBJ/MTL foundation sources without requiring Unity. */
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');
const assets = [
  'Assets/Resources/Imports/Units/Buildings/town-center/TownCenter_authoring.obj',
  'Assets/Resources/Imports/Units/Buildings/resource-dropoff/ResourceDropoff_authoring.obj',
  'Assets/Resources/Imports/Units/Buildings/lumber-camp/LumberCamp_authoring.obj',
];

function fail(message) {
  console.error(`OBJ validation failed: ${message}`);
  process.exitCode = 1;
}

for (const relative of assets) {
  const file = path.join(root, relative);
  if (!fs.existsSync(file)) { fail(`${relative} is missing`); continue; }
  const lines = fs.readFileSync(file, 'utf8').split(/\r?\n/);
  const vertices = [];
  const normals = [];
  const materials = new Set();
  const objects = new Set();
  let mtllib = null;
  let triangles = 0;
  for (const line of lines) {
    const trimmed = line.trim();
    if (!trimmed || trimmed.startsWith('#')) continue;
    const [kind, ...tokens] = trimmed.split(/\s+/);
    if (kind === 'mtllib') mtllib = tokens[0];
    else if (kind === 'o') objects.add(tokens.join(' '));
    else if (kind === 'usemtl') materials.add(tokens.join(' '));
    else if (kind === 'v' || kind === 'vn') {
      if (tokens.length !== 3 || tokens.some(value => !Number.isFinite(Number(value))))
        fail(`${relative} contains an invalid ${kind} record`);
      (kind === 'v' ? vertices : normals).push(tokens);
    } else if (kind === 'f') {
      if (tokens.length !== 3) fail(`${relative} contains a non-triangulated face`);
      for (const token of tokens) {
        const match = /^(\d+)\/\/(\d+)$/.exec(token);
        if (!match || Number(match[1]) < 1 || Number(match[1]) > vertices.length
          || Number(match[2]) < 1 || Number(match[2]) > normals.length)
          fail(`${relative} contains an out-of-range face reference: ${token}`);
      }
      triangles++;
    }
  }
  const mtl = mtllib && path.join(path.dirname(file), mtllib);
  if (!mtl || !fs.existsSync(mtl)) fail(`${relative} references missing MTL ${mtllib ?? '<none>'}`);
  else {
    const declared = new Set([...fs.readFileSync(mtl, 'utf8').matchAll(/^newmtl\s+(.+)$/gm)].map(match => match[1].trim()));
    for (const material of materials) if (!declared.has(material)) fail(`${relative} uses undeclared material ${material}`);
  }
  if (objects.size === 0 || vertices.length === 0 || normals.length === 0 || triangles === 0)
    fail(`${relative} has incomplete geometry`);
  else console.log(`${relative}: ${objects.size} objects, ${vertices.length} vertices, ${triangles} triangles, ${materials.size} materials`);
}

if (process.exitCode) process.exit(process.exitCode);
