import fs from 'node:fs';
import path from 'node:path';

const root = process.cwd();
const units = [
  ['Archer', 'archer'],
  ['Horseman', 'horseman'],
  ['BatteringRam', 'battering_ram'],
];
const fail = message => { throw new Error(message); };
const read = relative => fs.readFileSync(path.join(root, relative), 'utf8');

for (const [name, code] of units) {
  const prefabPath = `Assets/Resources/Prefabs/Units/Characters/${name}.prefab`;
  const prefabMetaPath = `${prefabPath}.meta`;
  const dataPath = `Assets/Resources/ScriptableObjects/Units/Characters/${name}.asset`;
  const dataMetaPath = `${dataPath}.meta`;
  for (const file of [prefabPath, prefabMetaPath, dataPath, dataMetaPath]) {
    if (!fs.existsSync(path.join(root, file))) fail(`missing ${file}`);
  }
  const prefab = read(prefabPath);
  const prefabMeta = read(prefabMetaPath);
  const data = read(dataPath);
  const dataMeta = read(dataMetaPath);
  const guid = prefabMeta.match(/^guid:\s*([0-9a-f]{32})\s*$/m)?.[1];
  if (!guid) fail(`${name}: invalid prefab GUID`);
  if (!prefab.includes(`m_Name: ${name}\n`)) fail(`${name}: root name missing`);
  if (!prefabMeta.includes(`era-imperial-${code}-runtime-proxy`)) fail(`${name}: runtime marker missing`);
  if (!data.includes(`code: ${code}\n`)) fail(`${name}: CharacterData code missing`);
  if (!data.includes(`prefab: {fileID: 100100000, guid: ${guid}, type: 3}`)) fail(`${name}: CharacterData prefab reference mismatch`);
  if (!dataMeta.match(/^guid:\s*[0-9a-f]{32}\s*$/m)) fail(`${name}: invalid CharacterData GUID`);
  for (const field of ['healthpoints:', 'attackRange:', 'attackDamage:', 'attackRate:', 'cost:']) {
    if (!data.includes(`  ${field}`)) fail(`${name}: missing ${field}`);
  }
  console.log(`PASS ${code}: CharacterData + prefab + GUID references`);
}
console.log(`Military unit asset validation passed: ${units.length} production units.`);
