import http from 'node:http';
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');
const assets = {
  '/town.glb': 'Assets/Resources/Imports/Units/Buildings/town-center/TownCenter_authoring.glb',
  '/dropoff.glb': 'Assets/Resources/Imports/Units/Buildings/resource-dropoff/ResourceDropoff_authoring.glb',
  '/lumber.glb': 'Assets/Resources/Imports/Units/Buildings/lumber-camp/LumberCamp_authoring.glb',
  '/mining.glb': 'Assets/Resources/Imports/Units/Buildings/mining-camp/MiningCamp_authoring.glb',
  '/mill.glb': 'Assets/Resources/Imports/Units/Buildings/mill/Mill_authoring.glb',
  '/barracks.glb': 'Assets/Resources/Imports/Units/Buildings/barracks/Barracks_authoring.glb',
  '/archery-range.glb': 'Assets/Resources/Imports/Units/Buildings/archery-range/ArcheryRange_authoring.glb',
};
const html = `<!doctype html><meta charset="utf-8"><title>RTS01 Foundation Preview</title>
<style>html,body{margin:0;height:100%;background:#10151a;color:#e8edf2;font:14px sans-serif}#bar{position:fixed;z-index:2;padding:10px;background:#10151acc}canvas{display:block}</style>
<div id="bar">RTS01 authored foundation preview — drag to orbit, wheel to zoom</div>
<script type="importmap">{"imports":{"three":"https://cdn.jsdelivr.net/npm/three@0.160.0/build/three.module.js"}}</script>
<script type="module">
import * as THREE from 'https://cdn.jsdelivr.net/npm/three@0.160.0/build/three.module.js';
import {OrbitControls} from 'https://cdn.jsdelivr.net/npm/three@0.160.0/examples/jsm/controls/OrbitControls.js';
import {GLTFLoader} from 'https://cdn.jsdelivr.net/npm/three@0.160.0/examples/jsm/loaders/GLTFLoader.js';
const scene=new THREE.Scene(); scene.background=new THREE.Color(0x10151a);
const camera=new THREE.PerspectiveCamera(45,innerWidth/innerHeight,.1,120); camera.position.set(22,15,28);
const renderer=new THREE.WebGLRenderer({antialias:true}); renderer.setPixelRatio(devicePixelRatio); renderer.setSize(innerWidth,innerHeight); document.body.appendChild(renderer.domElement);
const controls=new OrbitControls(camera,renderer.domElement); controls.target.set(0,2,0); controls.update();
scene.add(new THREE.HemisphereLight(0xcfe5ff,0x332516,2.2)); const key=new THREE.DirectionalLight(0xffe4bd,3); key.position.set(7,14,10); scene.add(key);
const ground=new THREE.Mesh(new THREE.PlaneGeometry(40,40),new THREE.MeshStandardMaterial({color:0x26352a,roughness:1})); ground.rotation.x=-Math.PI/2; ground.position.y=-0.05; scene.add(ground);
const loader=new GLTFLoader(); const group=new THREE.Group(); scene.add(group); let loaded=0;
for(const [url,x] of [['/town.glb',-15],['/dropoff.glb',-10],['/lumber.glb',-5],['/mining.glb',0],['/mill.glb',5],['/barracks.glb',10],['/archery-range.glb',15]]) loader.load(url,g=>{g.scene.position.x=x; group.add(g.scene); loaded++;},undefined,e=>console.error(e));
function animate(){requestAnimationFrame(animate); renderer.render(scene,camera)} animate(); addEventListener('resize',()=>{camera.aspect=innerWidth/innerHeight;camera.updateProjectionMatrix();renderer.setSize(innerWidth,innerHeight)});
</script>`;
const server = http.createServer((req,res)=>{
  if (req.url === '/' || req.url === '/index.html') { res.writeHead(200, {'content-type':'text/html'}); res.end(html); return; }
  const rel = assets[req.url]; if (!rel) { res.writeHead(404); res.end(); return; }
  res.writeHead(200, {'content-type':'model/gltf-binary','cache-control':'no-store'}); fs.createReadStream(path.join(root,rel)).pipe(res);
});
server.listen(4173,'127.0.0.1',()=>console.log('Foundation preview: http://127.0.0.1:4173'));
