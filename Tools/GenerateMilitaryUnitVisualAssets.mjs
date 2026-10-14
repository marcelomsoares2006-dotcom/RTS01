import fs from 'node:fs';
import path from 'node:path';

/**
 * Deterministic, Unity-native low-poly silhouettes for the first military roster.
 * These are authored source models, not grayboxes: every named part has a material
 * assignment and a stable metric record. Unity imports OBJ/MTL without a package.
 */
const root = process.cwd();
const outputRoot = path.join(root, 'Assets', 'Resources', 'Imports', 'Units', 'Military');
const metricsPath = path.join(outputRoot, 'military-unit-asset-metrics.json');

const materials = {
  cloth: { kd: [0.16, 0.28, 0.52], rough: 0.82, metal: 0.0 },
  leather: { kd: [0.24, 0.10, 0.045], rough: 0.9, metal: 0.0 },
  wood: { kd: [0.34, 0.16, 0.055], rough: 0.86, metal: 0.0 },
  metal: { kd: [0.34, 0.38, 0.43], rough: 0.42, metal: 0.75 },
  skin: { kd: [0.66, 0.37, 0.22], rough: 0.92, metal: 0.0 },
  accent: { kd: [0.76, 0.55, 0.10], rough: 0.58, metal: 0.15 },
  wheel: { kd: [0.12, 0.055, 0.018], rough: 0.92, metal: 0.0 },
};

function writeIfChanged(file, content) {
  fs.mkdirSync(path.dirname(file), { recursive: true });
  const normalized = content.replace(/\r?\n/g, '\n');
  if (!fs.existsSync(file) || fs.readFileSync(file, 'utf8') !== normalized) fs.writeFileSync(file, normalized, 'utf8');
}

function unitMesh() {
  return { vertices: [], faces: [], parts: [], materials: new Set(), bounds: [Infinity, Infinity, Infinity, -Infinity, -Infinity, -Infinity] };
}

function addVertex(mesh, p) {
  mesh.vertices.push(p);
  mesh.bounds[0] = Math.min(mesh.bounds[0], p[0]); mesh.bounds[1] = Math.min(mesh.bounds[1], p[1]); mesh.bounds[2] = Math.min(mesh.bounds[2], p[2]);
  mesh.bounds[3] = Math.max(mesh.bounds[3], p[0]); mesh.bounds[4] = Math.max(mesh.bounds[4], p[1]); mesh.bounds[5] = Math.max(mesh.bounds[5], p[2]);
  return mesh.vertices.length;
}

function part(mesh, name, material) {
  mesh.parts.push({ name, material, triangles: 0 });
  mesh.materials.add(material);
  return mesh.parts[mesh.parts.length - 1];
}

function face(mesh, current, a, b, c) { mesh.faces.push([a, b, c]); current.triangles += 1; }

function box(mesh, name, material, cx, cy, cz, sx, sy, sz, rotation = 0) {
  const p = part(mesh, name, material);
  const c = Math.cos(rotation), s = Math.sin(rotation);
  const corners = [];
  for (const [x, y, z] of [[-1,-1,-1],[1,-1,-1],[1,-1,1],[-1,-1,1],[-1,1,-1],[1,1,-1],[1,1,1],[-1,1,1]]) {
    const lx = x * sx / 2, lz = z * sz / 2;
    corners.push(addVertex(mesh, [cx + lx * c - lz * s, cy + y * sy / 2, cz + lx * s + lz * c]));
  }
  for (const t of [[0,1,2],[0,2,3],[4,6,5],[4,7,6],[0,4,5],[0,5,1],[1,5,6],[1,6,2],[2,6,7],[2,7,3],[3,7,4],[3,4,0]]) face(mesh, p, corners[t[0]], corners[t[1]], corners[t[2]]);
}

function cylinder(mesh, name, material, cx, cy, cz, radius, height, segments = 8, axis = 'y') {
  const p = part(mesh, name, material); const bottom = [], top = [];
  for (let i = 0; i < segments; i += 1) {
    const a = (Math.PI * 2 * i) / segments, u = Math.cos(a) * radius, v = Math.sin(a) * radius;
    const low = axis === 'y' ? [cx + u, cy - height / 2, cz + v] : axis === 'x' ? [cx - height / 2, cy + u, cz + v] : [cx + u, cy + v, cz - height / 2];
    const high = axis === 'y' ? [cx + u, cy + height / 2, cz + v] : axis === 'x' ? [cx + height / 2, cy + u, cz + v] : [cx + u, cy + v, cz + height / 2];
    bottom.push(addVertex(mesh, low)); top.push(addVertex(mesh, high));
  }
  for (let i = 0; i < segments; i += 1) { const n = (i + 1) % segments; face(mesh, p, bottom[i], bottom[n], top[n]); face(mesh, p, bottom[i], top[n], top[i]); }
  for (let i = 1; i < segments - 1; i += 1) { face(mesh, p, bottom[0], bottom[i + 1], bottom[i]); face(mesh, p, top[0], top[i], top[i + 1]); }
}

function cone(mesh, name, material, cx, cy, cz, r0, r1, height, segments = 8) {
  const p = part(mesh, name, material); const low = [], high = [];
  for (let i = 0; i < segments; i += 1) { const a = (Math.PI * 2 * i) / segments; low.push(addVertex(mesh, [cx + Math.cos(a) * r0, cy - height / 2, cz + Math.sin(a) * r0])); high.push(addVertex(mesh, [cx + Math.cos(a) * r1, cy + height / 2, cz + Math.sin(a) * r1])); }
  for (let i = 0; i < segments; i += 1) { const n = (i + 1) % segments; face(mesh, p, low[i], low[n], high[n]); face(mesh, p, low[i], high[n], high[i]); }
}

function makeArcher() {
  const m = unitMesh();
  box(m, 'Boots', 'leather', -0.13, 0.12, 0, 0.18, 0.24, 0.28); box(m, 'Boots_R', 'leather', 0.13, 0.12, 0, 0.18, 0.24, 0.28);
  box(m, 'Tunic', 'cloth', 0, 0.84, 0, 0.52, 0.92, 0.32); box(m, 'Belt', 'leather', 0, 0.61, -0.01, 0.56, 0.10, 0.36);
  cylinder(m, 'Head', 'skin', 0, 1.52, 0, 0.19, 0.32); cone(m, 'Hood', 'cloth', 0, 1.76, 0, 0.30, 0.06, 0.25);
  box(m, 'Arm_L', 'cloth', -0.34, 1.02, 0, 0.16, 0.58, 0.16, -0.18); box(m, 'Arm_R', 'cloth', 0.34, 1.02, 0, 0.16, 0.58, 0.16, 0.18);
  box(m, 'Quiver', 'leather', -0.22, 1.12, 0.22, 0.14, 0.48, 0.14, -0.22); box(m, 'Bow', 'wood', 0.47, 1.10, 0.05, 0.08, 1.05, 0.08, 0.12);
  box(m, 'Bowstring', 'accent', 0.42, 1.10, 0.055, 0.025, 0.92, 0.025, 0.12);
  return m;
}

function makeHorseman() {
  const m = unitMesh();
  box(m, 'HorseBody', 'leather', 0, 0.82, 0, 1.22, 0.72, 0.46, 0.02); box(m, 'HorseChest', 'leather', 0, 1.08, 0.48, 0.66, 0.90, 0.34, -0.16);
  box(m, 'HorseHead', 'leather', 0, 1.52, 0.78, 0.36, 0.46, 0.50, -0.15); box(m, 'HorseMane', 'accent', 0, 1.48, 0.56, 0.12, 0.56, 0.12);
  for (const [x, z] of [[-0.42,-0.20],[0.42,-0.20],[-0.42,0.22],[0.42,0.22]]) { cylinder(m, `HorseLeg_${x}_${z}`, 'leather', x, 0.38, z, 0.11, 0.82, 6); box(m, `HorseHoof_${x}_${z}`, 'wheel', x, 0.06, z + 0.03, 0.20, 0.12, 0.27); }
  box(m, 'Saddle', 'accent', 0, 1.26, -0.02, 0.72, 0.12, 0.64); box(m, 'RiderTorso', 'cloth', 0, 1.78, -0.02, 0.46, 0.70, 0.30); cylinder(m, 'RiderHead', 'skin', 0, 2.28, -0.02, 0.17, 0.30); cone(m, 'RiderHelm', 'metal', 0, 2.47, -0.02, 0.25, 0.08, 0.18);
  box(m, 'Lance', 'wood', 0.42, 1.98, 0.12, 0.07, 1.80, 0.07, -0.22); box(m, 'Shield', 'metal', -0.32, 1.78, 0.23, 0.12, 0.48, 0.42, 0.12);
  return m;
}

function makeBatteringRam() {
  const m = unitMesh();
  box(m, 'Frame', 'wood', 0, 1.03, 0, 1.80, 0.78, 0.92); box(m, 'Roof', 'cloth', 0, 1.60, 0, 1.92, 0.14, 1.02); box(m, 'RamBeam', 'wood', 0, 0.95, 0.58, 1.25, 0.24, 1.40); cone(m, 'RamHead', 'metal', 0, 0.95, 1.43, 0.42, 0.08, 0.55, 8);
  cylinder(m, 'Wheel_L', 'wheel', -0.76, 0.38, 0, 0.38, 0.18, 10, 'x'); cylinder(m, 'Wheel_R', 'wheel', 0.76, 0.38, 0, 0.38, 0.18, 10, 'x');
  box(m, 'WheelAxle', 'metal', 0, 0.38, 0, 1.72, 0.12, 0.12); box(m, 'Banner', 'accent', 0.62, 1.98, -0.30, 0.08, 0.72, 0.08); box(m, 'BannerCloth', 'cloth', 0.72, 2.10, -0.30, 0.42, 0.42, 0.05);
  return m;
}

function objText(mesh, unit) {
  const lines = [`# Era Imperial authored military silhouette: ${unit.name}`, `mtllib ${unit.name}_authoring.mtl`, ''];
  let vertexOffset = 0;
  for (const p of mesh.parts) {
    lines.push(`o ${p.name}`, `usemtl ${p.material}`);
    const start = vertexOffset;
    // Parts are emitted in vertex order by filtering faces through their triangle range.
    const needed = new Set(); let seen = 0;
    for (const f of mesh.faces) { if (seen >= p.triangles) break; if (seen + 1 > 0) { for (const idx of f) needed.add(idx); seen += 1; } }
    // Faces are contiguous but vertices are shared between adjacent parts only in no case;
    // use a global vertex list and emit all vertices once below for standard OBJ import.
    if (vertexOffset === 0) for (const v of mesh.vertices) lines.push(`v ${v.map(x => x.toFixed(6)).join(' ')}`);
    break;
  }
  let faceOffset = 0;
  for (const p of mesh.parts) { lines.push(`o ${p.name}`, `usemtl ${p.material}`); for (let i = 0; i < p.triangles; i += 1) { const f = mesh.faces[faceOffset++]; lines.push(`f ${f[0]} ${f[1]} ${f[2]}`); } }
  return `${lines.join('\n')}\n`;
}

function mtlText() {
  const lines = ['# Deterministic PBR intent for authored military silhouettes'];
  for (const [name, mat] of Object.entries(materials)) { lines.push(`newmtl ${name}`, `Kd ${mat.kd.join(' ')}`, `Ns ${(1 - mat.rough) * 1000}`, `metallic ${mat.metal}`, 'illum 2', ''); }
  return lines.join('\n');
}

const units = [
  { name: 'Archer', code: 'archer', build: makeArcher },
  { name: 'Horseman', code: 'horseman', build: makeHorseman },
  { name: 'BatteringRam', code: 'battering_ram', build: makeBatteringRam },
];
const metrics = { schema: 1, generatedBy: 'Tools/GenerateMilitaryUnitVisualAssets.mjs', units: [] };
for (const unit of units) {
  const mesh = unit.build(); const dir = path.join(outputRoot, unit.code);
  const obj = objText(mesh, unit); const mtl = mtlText();
  writeIfChanged(path.join(dir, `${unit.name}_authoring.obj`), obj);
  writeIfChanged(path.join(dir, `${unit.name}_authoring.mtl`), mtl);
  metrics.units.push({ name: unit.name, code: unit.code, authoredSource: `Assets/Resources/Imports/Units/Military/${unit.code}/${unit.name}_authoring.obj`, materialSource: `Assets/Resources/Imports/Units/Military/${unit.code}/${unit.name}_authoring.mtl`, parts: mesh.parts.map(p => ({ name: p.name, material: p.material, triangles: p.triangles })), vertexCount: mesh.vertices.length, triangleCount: mesh.faces.length, materialCount: mesh.materials.size, bounds: mesh.bounds.map(v => Number(v.toFixed(6))), sourceBytes: Buffer.byteLength(obj), materialBytes: Buffer.byteLength(mtl), budget: { maxTriangles: unit.code === 'battering_ram' ? 220 : unit.code === 'horseman' ? 300 : 180, maxMaterials: 7 } });
}
writeIfChanged(metricsPath, `${JSON.stringify(metrics, null, 2)}\n`);
console.log(`Generated ${units.length} deterministic military silhouettes with OBJ/MTL sources and metrics.`);
