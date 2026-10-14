#!/usr/bin/env node
/** Validate authored military OBJ/MTL sources and their deterministic metrics. */
import fs from 'node:fs';
import path from 'node:path';

const root = process.cwd();
const metricsPath = path.join(root, 'Assets/Resources/Imports/Units/Military/military-unit-asset-metrics.json');
const metrics = JSON.parse(fs.readFileSync(metricsPath, 'utf8'));
let failures = 0;
for (const unit of metrics.units ?? []) {
  const objPath = path.join(root, unit.authoredSource);
  const mtlPath = path.join(root, unit.materialSource);
  if (!fs.existsSync(objPath) || !fs.existsSync(mtlPath)) { console.error(`FAIL ${unit.code}: source missing`); failures++; continue; }
  const obj = fs.readFileSync(objPath, 'utf8').split(/\r?\n/);
  const declared = new Set([...fs.readFileSync(mtlPath, 'utf8').matchAll(/^newmtl\s+(.+)$/gm)].map(m => m[1].trim()));
  let vertices = 0; let faces = 0; const used = new Set();
  for (const line of obj) {
    const value = line.trim(); if (!value || value.startsWith('#')) continue;
    const [kind, ...tokens] = value.split(/\s+/);
    if (kind === 'v') { if (tokens.length !== 3 || tokens.some(n => !Number.isFinite(Number(n)))) failures++; vertices++; }
    if (kind === 'usemtl' && !declared.has(tokens.join(' '))) { console.error(`FAIL ${unit.code}: undeclared material ${tokens.join(' ')}`); failures++; }
    if (kind === 'f') {
      if (tokens.length !== 3 || tokens.some(t => !/^\d+$/.test(t) || Number(t) < 1 || Number(t) > vertices)) failures++;
      faces++;
    }
  }
  const budget = unit.budget?.maxTriangles ?? Number.MAX_SAFE_INTEGER;
  if (faces !== unit.triangleCount || vertices !== unit.vertexCount || faces > budget || unit.materialCount > (unit.budget?.maxMaterials ?? Number.MAX_SAFE_INTEGER)) {
    console.error(`FAIL ${unit.code}: metrics mismatch or budget exceeded`); failures++;
  } else console.log(`PASS ${unit.code}: ${vertices} vertices, ${faces} triangles, ${unit.materialCount} materials`);
}
if (failures) process.exit(1);
