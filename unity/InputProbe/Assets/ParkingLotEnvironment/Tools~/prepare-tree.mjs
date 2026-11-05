import fs from 'node:fs/promises';
import path from 'node:path';
import {NodeIO} from '@gltf-transform/core';
import {ALL_EXTENSIONS} from '@gltf-transform/extensions';
import {weld,simplify,prune} from '@gltf-transform/functions';
import {MeshoptSimplifier} from 'meshoptimizer';
const folder='tree-small-real';
const data=JSON.parse(await fs.readFile(path.join(folder,'source-files.json'),'utf8'));
await Promise.all(Object.entries(data.include).map(async([name,item])=>{
const r=await fetch(item.url);if(!r.ok)throw Error(r.status+' '+item.url);await fs.writeFile(path.join(folder,name),new Uint8Array(await r.arrayBuffer()));console.log('downloaded',name,item.size);
}));
await MeshoptSimplifier.ready;
const io=new NodeIO().registerExtensions(ALL_EXTENSIONS);
const doc=await io.read(path.join(folder,'original.gltf'));
function counts(){return doc.getRoot().listMeshes().flatMap(m=>m.listPrimitives().map(p=>({name:p.getMaterial()?.getName(),triangles:p.getIndices().getCount()/3,vertices:p.getAttribute('POSITION').getCount()})));}
console.log('before',JSON.stringify(counts()));
await doc.transform(weld(),simplify({simplifier:MeshoptSimplifier,ratio:0.0388,error:0.01,lockBorder:false}),prune());
console.log('after',JSON.stringify(counts()));
// Leaves have modeled outlines, not alpha-cutout cards; use OPAQUE to avoid transparent sorting.
for(const mat of doc.getRoot().listMaterials())if(mat.getName().includes('leaves'))mat.setAlphaMode('OPAQUE');
await io.write(path.join(folder,'source.glb'),doc);
await fs.writeFile(path.join(folder,'credit.json'),JSON.stringify({name:'Tree Small 02',source:'https://polyhaven.com/a/tree_small_02',files:'https://api.polyhaven.com/files/tree_small_02',license:'CC0 1.0',licenseUrl:'https://creativecommons.org/publicdomain/zero/1.0/',modifications:'Simplified with meshoptimizer to target 3.88% ratio, max error 1%, preserving materials, UVs and vertex normals. Original leaves have geometric silhouette and no alpha image, set opaque with double-sided material.'},null,2));
