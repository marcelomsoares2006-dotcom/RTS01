#!/usr/bin/env node
/**
 * Reproducible, dependency-free foundation asset generator for RTS01.
 *
 * Output units are metres, Y-up, right-handed glTF 2.0.  The generated GLBs
 * intentionally contain authored low-poly geometry, named parts, normals and
 * PBR materials. Unity can import them later and create the production prefabs
 * when an editor is available; this script does not touch catalog or prefab
 * files.
 */
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');
const outputRoot = path.join(root, 'Assets', 'Resources', 'Imports', 'Units', 'Buildings');

const materials = [
  { name: 'Stone warm', color: [0.42, 0.34, 0.25, 1], roughness: 0.88 },
  { name: 'Stone light', color: [0.61, 0.52, 0.39, 1], roughness: 0.84 },
  { name: 'Timber dark', color: [0.20, 0.10, 0.045, 1], roughness: 0.78 },
  { name: 'Roof terracotta', color: [0.44, 0.13, 0.07, 1], roughness: 0.92 },
  { name: 'Metal blue', color: [0.055, 0.18, 0.31, 1], roughness: 0.5, metallic: 0.18 },
  { name: 'Banner gold', color: [0.90, 0.56, 0.08, 1], roughness: 0.64 },
];

const deg = Math.PI / 180;
function v3(a, b, c) { return [a, b, c]; }
function sub(a, b) { return [a[0] - b[0], a[1] - b[1], a[2] - b[2]]; }
function cross(a, b) { return [a[1] * b[2] - a[2] * b[1], a[2] * b[0] - a[0] * b[2], a[0] * b[1] - a[1] * b[0]]; }
function norm(n) { const l = Math.hypot(...n) || 1; return [n[0] / l, n[1] / l, n[2] / l]; }
function add(a, b) { return [a[0] + b[0], a[1] + b[1], a[2] + b[2]]; }
function rotateY(p, angle) { const c = Math.cos(angle), s = Math.sin(angle); return [p[0] * c - p[2] * s, p[1], p[0] * s + p[2] * c]; }

function part(name, material, vertices, faces) {
  return { name, material, vertices, faces };
}

function box(name, cx, cy, cz, sx, sy, sz, material, angle = 0) {
  const x = sx / 2, y = sy / 2, z = sz / 2;
  const raw = [[-x,-y,-z],[x,-y,-z],[x,-y,z],[-x,-y,z],[-x,y,-z],[x,y,-z],[x,y,z],[-x,y,z]];
  const vertices = raw.map(p => add(rotateY(p, angle), [cx, cy, cz]));
  return part(name, material, vertices, [[0,3,2,1],[4,5,6,7],[0,1,5,4],[1,2,6,5],[2,3,7,6],[3,0,4,7]]);
}

function pyramid(name, cx, cy, cz, sx, sz, height, material, angle = 0) {
  const x = sx / 2, z = sz / 2;
  const raw = [[-x,0,-z],[x,0,-z],[x,0,z],[-x,0,z],[0,height,0]];
  const vertices = raw.map(p => add(rotateY(p, angle), [cx, cy, cz]));
  return part(name, material, vertices, [[0,3,2,1],[0,1,4],[1,2,4],[2,3,4],[3,0,4]]);
}

function cylinder(name, cx, cy, cz, radius, height, sides, material, angle = 0) {
  const vertices = [];
  for (let y of [0, height]) for (let i = 0; i < sides; i++) {
    const a = 2 * Math.PI * i / sides;
    vertices.push(add(rotateY([radius * Math.cos(a), y, radius * Math.sin(a)], angle), [cx, cy, cz]));
  }
  const faces = [];
  faces.push([...Array(sides).keys()].reverse());
  faces.push([...Array(sides).keys()].map(i => sides + i));
  for (let i = 0; i < sides; i++) { const j = (i + 1) % sides; faces.push([i, j, sides + j, sides + i]); }
  return part(name, material, vertices, faces);
}

function buildTownCenter() {
  const p = [];
  p.push(box('Foundation', 0, 0.25, 0, 9.2, 0.5, 7.2, 0));
  p.push(box('KeepWalls', 0, 1.95, 0, 8.3, 3.0, 6.3, 1));
  // Corner towers give a readable silhouette and defendable footprint.
  for (const [x, z] of [[-3.55,-2.55],[3.55,-2.55],[-3.55,2.55],[3.55,2.55]]) {
    p.push(cylinder('CornerTower', x, 0.5, z, 0.72, 3.8, 8, 0));
    p.push(pyramid('TowerCap', x, 4.3, z, 1.8, 1.8, 0.95, 3, Math.PI / 4));
  }
  p.push(box('CentralKeep', 0, 4.0, 0, 3.2, 3.8, 3.2, 0));
  // CentralKeep spans y=2.1..5.9; seat the roof directly on its top face.
  p.push(pyramid('CentralRoof', 0, 5.9, 0, 4.15, 4.15, 1.8, 3, Math.PI / 4));
  // Entrance surround and gate are intentionally separate parts for later Unity animation.
  p.push(box('EntranceFrame', 0, 1.65, -3.23, 2.2, 2.7, 0.35, 2));
  p.push(box('Gate', 0, 1.25, -3.43, 1.35, 1.9, 0.12, 4));
  p.push(box('GateLintel', 0, 2.72, -3.43, 2.45, 0.35, 0.35, 2));
  p.push(box('BannerPole', 0, 3.85, -3.62, 0.08, 2.0, 0.08, 2));
  p.push(box('Banner', 0.34, 4.25, -3.62, 0.62, 0.9, 0.05, 4));
  return p;
}

function buildResourceDropoff() {
  const p = [];
  p.push(box('Foundation', 0, 0.2, 0, 4.8, 0.4, 3.8, 0));
  p.push(box('StorehouseWalls', 0, 1.25, 0, 4.25, 1.7, 3.25, 1));
  // Timber braces make the building read as a deliberate production asset.
  for (const x of [-1.75, 1.75]) p.push(box('TimberPost', x, 1.35, -1.69, 0.22, 2.3, 0.22, 2));
  p.push(box('TimberBeam', 0, 2.05, -1.71, 4.0, 0.25, 0.25, 2));
  p.push(box('LoadingPlatform', 0, 0.58, -2.0, 2.6, 0.18, 0.9, 2));
  p.push(pyramid('Roof', 0, 2.1, 0, 4.75, 3.8, 1.7, 3));
  p.push(box('Chimney', 1.25, 3.1, 0.25, 0.55, 1.2, 0.55, 0));
  p.push(pyramid('ChimneyCap', 1.25, 3.7, 0.25, 0.72, 0.72, 0.18, 3));
  p.push(box('Sign', 0, 2.35, -1.9, 1.4, 0.45, 0.08, 4));
  p.push(box('SignMark', 0, 2.35, -1.96, 0.32, 0.08, 0.04, 5));
  return p;
}

function buildLumberCamp() {
  const p = [];
  // A compact forest outpost: the open loading face and stacked timber make
  // the wood role legible at gameplay distance while keeping the footprint
  // inside the existing dropoff placement family.
  p.push(box('Foundation', 0, 0.2, 0, 5.4, 0.4, 4.2, 0));
  p.push(box('StorehouseWalls', 0, 1.35, 0.1, 4.7, 2.2, 3.35, 1));
  for (const [x, z] of [[-2.0,-1.45],[2.0,-1.45],[-2.0,1.45],[2.0,1.45]]) {
    p.push(box('TimberPost', x, 1.45, z, 0.24, 2.5, 0.24, 2));
  }
  p.push(box('TimberBeam', 0, 2.35, -1.63, 4.5, 0.24, 0.24, 2));
  p.push(pyramid('Roof', 0, 2.55, 0.1, 5.25, 3.95, 1.55, 3));
  p.push(box('LoadingPlatform', 0, 0.58, -2.2, 2.8, 0.18, 1.0, 2));
  // Three separate logs preserve readable named parts for later harvesting VFX.
  p.push(box('LogPileLower', -1.15, 0.82, 2.05, 2.25, 0.42, 0.48, 2, -0.08));
  p.push(box('LogPileUpper', -1.0, 1.25, 2.03, 1.9, 0.38, 0.44, 2, 0.08));
  p.push(box('LogPileBrace', 0.35, 0.92, 2.04, 0.22, 0.9, 0.22, 2));
  p.push(box('Sign', 1.35, 2.25, -1.82, 1.45, 0.48, 0.08, 4));
  p.push(box('SignMark', 1.35, 2.25, -1.88, 0.48, 0.08, 0.04, 5));
  p.push(box('AxeRack', 2.05, 1.0, -0.55, 0.16, 1.35, 0.3, 2));
  return p;
}

function buildMiningCamp() {
  const p = [];
  // A stone-and-timber extraction outpost: the marked shaft, ore bins and
  // hoist make the gold/stone role readable at gameplay distance.
  p.push(box('Foundation', 0, 0.2, 0, 5.0, 0.4, 4.6, 0));
  p.push(box('StorehouseWalls', 0, 1.25, 0.2, 4.25, 1.8, 3.15, 1));
  for (const [x, z] of [[-1.8,-1.35],[1.8,-1.35],[-1.8,1.7],[1.8,1.7]]) {
    p.push(box('TimberPost', x, 1.45, z, 0.22, 2.5, 0.22, 2));
  }
  p.push(box('TimberBeam', 0, 2.25, -1.52, 4.1, 0.24, 0.24, 2));
  p.push(pyramid('Roof', 0, 2.25, 0.2, 4.65, 3.6, 1.65, 3));
  // The shaft rim and shaft are separate so the later Unity prefab can attach
  // resource VFX and worker interaction sockets without remodelling the mesh.
  p.push(cylinder('MineShaftRim', 0, 0.45, 1.0, 1.05, 0.3, 10, 1));
  p.push(cylinder('MineShaft', 0, 0.7, 1.0, 0.78, 0.18, 10, 4));
  p.push(box('OreBinGold', -1.55, 0.72, 2.05, 1.1, 0.65, 0.9, 4, -0.12));
  p.push(box('OreBinStone', 1.15, 0.72, 2.05, 1.2, 0.65, 0.9, 1, 0.12));
  p.push(box('HoistPost', 2.02, 1.2, 0.65, 0.2, 2.25, 0.2, 2));
  p.push(box('HoistBeam', 1.15, 2.18, 0.65, 1.95, 0.2, 0.2, 2));
  p.push(cylinder('HoistWheel', 0.78, 1.25, 0.65, 0.38, 0.16, 10, 4, Math.PI / 2));
  p.push(box('Sign', -1.25, 2.2, -1.72, 1.55, 0.48, 0.08, 4));
  p.push(box('SignMark', -1.25, 2.2, -1.78, 0.5, 0.08, 0.04, 5));
  return p;
}

function makeBinaryGLB(parts, assetName) {
  const positions = [], normals = [], indices = [], primitiveInfo = [];
  for (const item of parts) {
    const base = positions.length / 3;
    const indexStart = indices.length;
    for (const face of item.faces) {
      const a = item.vertices[face[0]], b = item.vertices[face[1]], c = item.vertices[face[2]];
      const n = norm(cross(sub(b, a), sub(c, a)));
      for (const ix of face) { positions.push(...item.vertices[ix]); normals.push(...n); }
      // Each primitive exposes a local POSITION accessor beginning at `base`.
      // Keep its indices local too; using the packed global vertex offset makes
      // the primitive sample another part's vertices in glTF viewers/importers.
      const start = (positions.length / 3) - base - face.length;
      for (let i = 1; i < face.length - 1; i++) indices.push(start, start + i, start + i + 1);
    }
    const end = positions.length / 3;
    primitiveInfo.push({ name: item.name, material: item.material, first: base, count: end - base, indexStart, indexCount: indices.length - indexStart });
  }
  const buffer = Buffer.alloc((positions.length * 4 * 2) + (indices.length * 4));
  let off = 0;
  const posOff = off; for (const n of positions) { buffer.writeFloatLE(n, off); off += 4; }
  const norOff = off; for (const n of normals) { buffer.writeFloatLE(n, off); off += 4; }
  const idxOff = off; for (const n of indices) { buffer.writeUInt32LE(n, off); off += 4; }
  const min = [Infinity, Infinity, Infinity], max = [-Infinity, -Infinity, -Infinity];
  for (let i = 0; i < positions.length; i += 3) for (let a = 0; a < 3; a++) { min[a] = Math.min(min[a], positions[i+a]); max[a] = Math.max(max[a], positions[i+a]); }
  const gltf = {
    asset: { version: '2.0', generator: 'RTS01 GenerateFoundationAssets.mjs' },
    scene: 0,
    scenes: [{ name: assetName + '_Scene', nodes: [0] }],
    nodes: [{ name: assetName, mesh: 0 }],
    meshes: [{ name: assetName + '_Mesh', primitives: primitiveInfo.map((p, i) => ({
      attributes: { POSITION: i * 2, NORMAL: i * 2 + 1 }, indices: primitiveInfo.length * 2, material: p.material,
    })) }],
    materials: materials.map(m => ({ name: m.name, pbrMetallicRoughness: { baseColorFactor: m.color, metallicFactor: m.metallic ?? 0, roughnessFactor: m.roughness } })),
    buffers: [{ byteLength: buffer.length }],
    bufferViews: [], accessors: [],
  };
  // Each part has a compact pair of accessors over its own vertex range. This keeps
  // semantic names in the metrics and makes later Unity material assignment simple.
  let accessorIndex = 0, viewIndex = 0, byteCursor = 0;
  const partVertexRanges = [];
  let vertexCursor = 0;
  for (const item of parts) {
    const count = item.faces.reduce((sum, f) => sum + f.length, 0);
    const vStart = vertexCursor; vertexCursor += count;
    const pv = vStart * 3 * 4, nv = positions.length * 4 + vStart * 3 * 4;
    const pView = { buffer: 0, byteOffset: pv, byteLength: count * 12, target: 34962 };
    const nView = { buffer: 0, byteOffset: nv, byteLength: count * 12, target: 34962 };
    gltf.bufferViews.push(pView, nView);
    const pos = positions.slice(vStart * 3, (vStart + count) * 3);
    gltf.accessors.push({ bufferView: viewIndex++, componentType: 5126, count, type: 'VEC3', min: [Math.min(...pos.filter((_,i)=>i%3===0)), Math.min(...pos.filter((_,i)=>i%3===1)), Math.min(...pos.filter((_,i)=>i%3===2))], max: [Math.max(...pos.filter((_,i)=>i%3===0)), Math.max(...pos.filter((_,i)=>i%3===1)), Math.max(...pos.filter((_,i)=>i%3===2))] });
    gltf.accessors.push({ bufferView: viewIndex++, componentType: 5126, count, type: 'VEC3' });
    partVertexRanges.push({ pos: accessorIndex++, nor: accessorIndex++, count });
  }
  const partIndexAccessors = primitiveInfo.map((info) => {
    const view = gltf.bufferViews.length;
    gltf.bufferViews.push({ buffer: 0, byteOffset: idxOff + info.indexStart * 4, byteLength: info.indexCount * 4, target: 34963 });
    const accessor = gltf.accessors.length;
    const values = indices.slice(info.indexStart, info.indexStart + info.indexCount);
    gltf.accessors.push({ bufferView: view, componentType: 5125, count: info.indexCount, type: 'SCALAR', min: [Math.min(...values)], max: [Math.max(...values)] });
    return accessor;
  });
  gltf.meshes[0].primitives = parts.map((item, i) => ({ attributes: { POSITION: partVertexRanges[i].pos, NORMAL: partVertexRanges[i].nor }, indices: partIndexAccessors[i], material: item.material, extras: { partName: item.name } }));
  const json = Buffer.from(JSON.stringify(gltf));
  const pad4 = n => (n + 3) & ~3;
  const jsonPadded = Buffer.concat([json, Buffer.alloc(pad4(json.length) - json.length, 0x20)]);
  const binPadded = Buffer.concat([buffer, Buffer.alloc(pad4(buffer.length) - buffer.length)]);
  const total = 12 + 8 + jsonPadded.length + 8 + binPadded.length;
  const out = Buffer.alloc(total);
  out.writeUInt32LE(0x46546c67, 0); out.writeUInt32LE(2, 4); out.writeUInt32LE(total, 8);
  let cursor = 12; out.writeUInt32LE(jsonPadded.length, cursor); out.writeUInt32LE(0x4e4f534a, cursor + 4); jsonPadded.copy(out, cursor + 8); cursor += 8 + jsonPadded.length;
  out.writeUInt32LE(binPadded.length, cursor); out.writeUInt32LE(0x004e4942, cursor + 4); binPadded.copy(out, cursor + 8);
  return { out, stats: { assetName, parts: parts.length, vertices: positions.length / 3, triangles: indices.length / 3, materials: materials.length, boundsMin: min, boundsMax: max, bytes: out.length } };
}

function writeAsset(name, parts, folder) {
  const target = path.join(outputRoot, folder, `${name}.glb`);
  fs.mkdirSync(path.dirname(target), { recursive: true });
  const { out, stats } = makeBinaryGLB(parts, name);
  writeGeneratedFile(target, out);
  return { ...stats, path: path.relative(root, target).replaceAll('\\', '/') };
}

function writeGeneratedFile(target, contents) {
  // Unity/OneDrive can keep an unchanged imported model open. Avoid touching
  // it when the generated bytes are already identical, while still failing on
  // a real write error instead of silently losing generated output.
  if (fs.existsSync(target)) {
    const existing = fs.readFileSync(target);
    if (existing.equals(contents)) return;
  }
  fs.writeFileSync(target, contents);
}

function objMaterialName(material) {
  return materials[material].name.replace(/[^A-Za-z0-9_]+/g, '_');
}

function makeObj(parts, assetName) {
  const lines = [`# Generated by Tools/GenerateFoundationAssets.mjs`, `mtllib ${assetName}.mtl`];
  let vertexOffset = 1;
  let normalOffset = 1;
  let vertexCount = 0;
  let triangleCount = 0;
  const min = [Infinity, Infinity, Infinity], max = [-Infinity, -Infinity, -Infinity];

  for (const item of parts) {
    lines.push(`o ${item.name}`, `usemtl ${objMaterialName(item.material)}`);
    for (const vertex of item.vertices) {
      lines.push(`v ${vertex[0]} ${vertex[1]} ${vertex[2]}`);
      vertexCount++;
      for (let axis = 0; axis < 3; axis++) {
        min[axis] = Math.min(min[axis], vertex[axis]);
        max[axis] = Math.max(max[axis], vertex[axis]);
      }
    }
    for (const face of item.faces) {
      const a = item.vertices[face[0]], b = item.vertices[face[1]], c = item.vertices[face[2]];
      const normal = norm(cross(sub(b, a), sub(c, a)));
      lines.push(`vn ${normal[0]} ${normal[1]} ${normal[2]}`);
      const indices = face.map(index => `${vertexOffset + index}//${normalOffset}`);
      for (let i = 1; i < indices.length - 1; i++) {
        lines.push(`f ${indices[0]} ${indices[i]} ${indices[i + 1]}`);
        triangleCount++;
      }
      normalOffset++;
    }
    vertexOffset += item.vertices.length;
  }

  return {
    out: Buffer.from(lines.join('\n') + '\n', 'utf8'),
    stats: {
      assetName,
      format: 'obj',
      parts: parts.length,
      vertices: vertexCount,
      triangles: triangleCount,
      materials: materials.length,
      boundsMin: min,
      boundsMax: max,
    },
  };
}

function makeMtl(assetName) {
  const lines = [`# Generated by Tools/GenerateFoundationAssets.mjs`];
  for (const material of materials) {
    const name = material.name.replace(/[^A-Za-z0-9_]+/g, '_');
    lines.push(
      `newmtl ${name}`,
      `Kd ${material.color[0]} ${material.color[1]} ${material.color[2]}`,
      `Ks ${material.metallic ?? 0} ${material.metallic ?? 0} ${material.metallic ?? 0}`,
      `Ns ${Math.max(1, (1 - material.roughness) * 1000)}`,
      `d ${material.color[3] ?? 1}`,
    );
  }
  return Buffer.from(lines.join('\n') + '\n', 'utf8');
}

function writeObj(name, parts, folder) {
  const directory = path.join(outputRoot, folder);
  fs.mkdirSync(directory, { recursive: true });
  const objPath = path.join(directory, `${name}.obj`);
  const mtlPath = path.join(directory, `${name}.mtl`);
  const { out, stats } = makeObj(parts, name);
  writeGeneratedFile(objPath, out);
  writeGeneratedFile(mtlPath, makeMtl(name));
  return {
    ...stats,
    path: path.relative(root, objPath).replaceAll('\\', '/'),
    materialPath: path.relative(root, mtlPath).replaceAll('\\', '/'),
    bytes: out.length,
  };
}

const townCenterParts = buildTownCenter();
const resourceDropoffParts = buildResourceDropoff();
const lumberCampParts = buildLumberCamp();
const miningCampParts = buildMiningCamp();
const report = {
  generator: 'Tools/GenerateFoundationAssets.mjs',
  coordinateSystem: 'Y-up, right-handed, metres',
  assets: [
    writeAsset('TownCenter_authoring', townCenterParts, 'town-center'),
    writeObj('TownCenter_authoring', townCenterParts, 'town-center'),
    writeAsset('ResourceDropoff_authoring', resourceDropoffParts, 'resource-dropoff'),
    writeObj('ResourceDropoff_authoring', resourceDropoffParts, 'resource-dropoff'),
    writeAsset('LumberCamp_authoring', lumberCampParts, 'lumber-camp'),
    writeObj('LumberCamp_authoring', lumberCampParts, 'lumber-camp'),
    writeAsset('MiningCamp_authoring', miningCampParts, 'mining-camp'),
    writeObj('MiningCamp_authoring', miningCampParts, 'mining-camp'),
  ],
};
const reportPath = path.join(outputRoot, 'foundation-asset-metrics.json');
fs.writeFileSync(reportPath, JSON.stringify(report, null, 2) + '\n');
console.log(JSON.stringify(report, null, 2));
