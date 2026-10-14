import fs from 'node:fs';
import path from 'node:path';

const root = process.cwd();
const units = [
  ['Archer', 'archer'],
  ['Horseman', 'horseman'],
  ['BatteringRam', 'battering_ram'],
];
const animationBindingGuid = 'c2e5f9a0b7d14c638e91f4a6b8d20357';
const fail = message => { throw new Error(message); };
// Unity/YAML assets may be checked out with CRLF or LF depending on the
// authoring tool and Git settings.  The validator must inspect serialized
// fields, not the platform-specific line ending, otherwise a valid prefab
// such as Archer can be reported as "root name missing".
const read = relative => fs.readFileSync(path.join(root, relative), 'utf8').replace(/\r/g, '');

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
  const animationBlocks = prefab.split(/--- !u!114 /).filter(block => block.includes(`guid: ${animationBindingGuid}`));
  if (animationBlocks.length !== 1) fail(`${name}: animation binding component missing or duplicated`);
  const animation = animationBlocks[0];
  for (const [field, value] of [
    ['profileCode', code],
    ['idleClipCode', `${code}_idle`],
    ['moveClipCode', `${code}_move`],
    ['attackClipCode', `${code}_attack`],
    ['deathClipCode', `${code}_death`],
  ]) {
    if (!animation.includes(`  ${field}: ${value}\n`)) fail(`${name}: animation ${field} mismatch`);
  }
  if (!data.includes(`code: ${code}\n`)) fail(`${name}: CharacterData code missing`);
  if (!data.includes(`prefab: {fileID: 100100000, guid: ${guid}, type: 3}`)) fail(`${name}: CharacterData prefab reference mismatch`);
  if (!dataMeta.match(/^guid:\s*[0-9a-f]{32}\s*$/m)) fail(`${name}: invalid CharacterData GUID`);
  for (const field of ['healthpoints:', 'attackRange:', 'attackDamage:', 'attackRate:', 'cost:']) {
    if (!data.includes(`  ${field}`)) fail(`${name}: missing ${field}`);
  }
  console.log(`PASS ${code}: CharacterData + prefab + GUID references + animation binding`);
}
console.log(`Military unit asset validation passed: ${units.length} production units.`);
