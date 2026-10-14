#!/usr/bin/env node
/** Dependency-free structural validator for GLB files emitted by the generator. */
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');
const files = [
  'Assets/Resources/Imports/Units/Buildings/town-center/TownCenter_authoring.glb',
  'Assets/Resources/Imports/Units/Buildings/resource-dropoff/ResourceDropoff_authoring.glb',
];
const failures = [];
for (const relative of files) {
  const file = path.join(root, relative);
  const data = fs.readFileSync(file);
  if (data.readUInt32LE(0) !== 0x46546c67 || data.readUInt32LE(4) !== 2) failures.push(`${relative}: invalid GLB header`);
  const length = data.readUInt32LE(8);
  if (length !== data.length) failures.push(`${relative}: declared length ${length} != ${data.length}`);
  const jsonLength = data.readUInt32LE(12);
  const jsonType = data.readUInt32LE(16);
  if (jsonType !== 0x4e4f534a) failures.push(`${relative}: first chunk is not JSON`);
  const gltf = JSON.parse(data.subarray(20, 20 + jsonLength).toString('utf8').trim());
  if (gltf.asset?.version !== '2.0' || !gltf.meshes?.length || !gltf.nodes?.length || !gltf.materials?.length) failures.push(`${relative}: incomplete glTF schema`);
  for (const [meshIndex, mesh] of (gltf.meshes ?? []).entries()) {
    for (const [primitiveIndex, primitive] of (mesh.primitives ?? []).entries()) {
      const pos = gltf.accessors[primitive.attributes?.POSITION];
      const normal = gltf.accessors[primitive.attributes?.NORMAL];
      const index = gltf.accessors[primitive.indices];
      if (!pos || pos.type !== 'VEC3' || !normal || normal.count !== pos.count || !index || index.type !== 'SCALAR' || (index.max?.[0] ?? pos.count) >= pos.count) {
        failures.push(`${relative}: mesh ${meshIndex} primitive ${primitiveIndex} has invalid accessors`);
      }
    }
  }
  console.log(`${relative}: OK | ${gltf.meshes[0].primitives.length} parts | ${gltf.materials.length} materials | ${data.length} bytes`);
}
if (failures.length) { for (const failure of failures) console.error(`FAIL: ${failure}`); process.exitCode = 1; }
