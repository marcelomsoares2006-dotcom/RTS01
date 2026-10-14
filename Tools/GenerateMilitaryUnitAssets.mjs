import fs from 'node:fs';
import path from 'node:path';

const root = process.cwd();
const template = path.join(root, 'Assets/Resources/Prefabs/Units/Characters/Soldier.prefab');
const templateText = fs.readFileSync(template, 'utf8');
const units = [
  {
    name: 'Archer', code: 'archer', description: 'Arqueiro de apoio a distância.', health: 70,
    prefabGuid: 'a2f3e5d1b7c44e30a9f86d12c5b7e401', assetGuid: 'b1e4d7f3c8a9421db6e9f2057c3a8142',
    costs: ['- code: 0\n    amount: 30', '- code: 1\n    amount: 50'], fieldOfView: 20, range: 7, damage: 12, rate: 1.5,
  },
  {
    name: 'Horseman', code: 'horseman', description: 'Cavaleiro móvel para ataques e flanqueamento.', health: 125,
    prefabGuid: 'c3e8a1f5d9b2476ab4e0c7138f2d5603', assetGuid: 'd4f9b2e6a0c3487ba5e1d8249f3c6704',
    costs: ['- code: 0\n    amount: 100', '- code: 1\n    amount: 20'], fieldOfView: 18, range: 1.6, damage: 20, rate: 1.8,
  },
  {
    name: 'BatteringRam', code: 'battering_ram', description: 'Aríete de cerco contra construções.', health: 500,
    prefabGuid: 'e5a0c3f7b1d4498ca6e2f9350b4d7815', assetGuid: 'f6b1d4a8c2e5509db7f3a0461c5e8926',
    costs: ['- code: 1\n    amount: 160', '- code: 2\n    amount: 40'], fieldOfView: 12, range: 1.8, damage: 40, rate: 3,
  },
];

function write(file, content) {
  fs.mkdirSync(path.dirname(file), { recursive: true });
  fs.writeFileSync(file, content.replace(/\r?\n/g, '\n'), 'utf8');
}

for (const unit of units) {
  const prefabDir = path.join(root, 'Assets/Resources/Prefabs/Units/Characters');
  const prefabPath = path.join(prefabDir, `${unit.name}.prefab`);
  const prefab = templateText.replace(/m_Name: Soldier\r?\n/, `m_Name: ${unit.name}\n`);
  write(prefabPath, prefab);
  write(`${prefabPath}.meta`, `fileFormatVersion: 2\nguid: ${unit.prefabGuid}\nPrefabImporter:\n  externalObjects: {}\n  userData: era-imperial-${unit.code}-runtime-proxy\n  assetBundleName: \n  assetBundleVariant: \n`);

  const asset = `%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n--- !u!114 &11400000\nMonoBehaviour:\n  m_ObjectHideFlags: 0\n  m_CorrespondingSourceObject: {fileID: 0}\n  m_PrefabInstance: {fileID: 0}\n  m_PrefabAsset: {fileID: 0}\n  m_GameObject: {fileID: 0}\n  m_Enabled: 1\n  m_EditorHideFlags: 0\n  m_Script: {fileID: 11500000, guid: 0d3d7baebcf3947ce9b0366a4348bef9, type: 3}\n  m_Name: ${unit.name}\n  m_EditorClassIdentifier: \n  code: ${unit.code}\n  unitName: ${unit.name}\n  description: ${unit.description}\n  healthpoints: ${unit.health}\n  prefab: {fileID: 100100000, guid: ${unit.prefabGuid}, type: 3}\n  sprite: {fileID: 0}\n  cost:\n${unit.costs.map(x => `  ${x}`).join('\n')}\n  canProduce: \n  skills: []\n  fieldOfView: ${unit.fieldOfView}\n  attackRange: ${unit.range}\n  attackDamage: ${unit.damage}\n  attackRate: ${unit.rate}\n  armor: 0\n  onSelectSound: {fileID: 0}\n  onMoveValidSound: {fileID: 0}\n  onMoveInvalidSound: {fileID: 0}\n  buildRange: 0\n  buildPower: 0\n  buildRate: 0\n`;
  const assetPath = path.join(root, `Assets/Resources/ScriptableObjects/Units/Characters/${unit.name}.asset`);
  write(assetPath, asset);
  write(`${assetPath}.meta`, `fileFormatVersion: 2\nguid: ${unit.assetGuid}\nNativeFormatImporter:\n  externalObjects: {}\n  mainObjectFileID: 11400000\n  userData: era-imperial-${unit.code}-character-data\n  assetBundleName: \n  assetBundleVariant: \n`);
}

console.log(`Generated ${units.length} CharacterData assets and runtime prefabs.`);
