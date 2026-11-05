import fs from 'node:fs/promises';
import path from 'node:path';
import {NodeIO} from '@gltf-transform/core';
import {ALL_EXTENSIONS} from '@gltf-transform/extensions';
import {dequantize,weld,compactPrimitive} from '@gltf-transform/functions';
import {MeshoptSimplifier,MeshoptDecoder} from 'meshoptimizer';

// Create a separate distant mesh, keeping original material order and source files intact.
const [sourceFolder,destination]=process.argv.slice(2);
if(!sourceFolder||!destination)throw Error('Usage: node prepare-vehicle-lod.mjs source-folder destination-folder');
await Promise.all([MeshoptSimplifier.ready,MeshoptDecoder.ready]);
const io=new NodeIO().registerExtensions(ALL_EXTENSIONS).registerDependencies({'meshopt.decoder':MeshoptDecoder});
const doc=await io.read(path.join(sourceFolder,'source.glb'));
await doc.transform(dequantize());
const count=()=>doc.getRoot().listMeshes().flatMap(m=>m.listPrimitives()).filter(p=>p.getMode()===4).reduce((n,p)=>n+(p.getIndices()?.getCount()||p.getAttribute('POSITION').getCount())/3,0);
const before=count(),materialNames=doc.getRoot().listMaterials().map(m=>m.getName());
await doc.transform(weld());
for(const mesh of doc.getRoot().listMeshes())for(const p of mesh.listPrimitives()){
 if(p.getMode()!==4)continue;
 const pos=p.getAttribute('POSITION'),normal=p.getAttribute('NORMAL'),uv=p.getAttribute('TEXCOORD_0');
 const attributes=new Float32Array(pos.getCount()*5),n=[],t=[];
 for(let i=0;i<pos.getCount();i++){
  if(normal)normal.getElement(i,n);else n.splice(0,n.length,0,1,0);
  if(uv)uv.getElement(i,t);else t.splice(0,t.length,0,0);
  attributes.set([...n,...t],i*5);
 }
 const indices=new Uint32Array(p.getIndices().getArray()),target=Math.max(3,Math.floor(indices.length*.25/3)*3);
 const weights=p.getMaterial()?.getBaseColorTexture()?[.5,.5,.5,1,1]:[.5,.5,.5,.05,.05];
 const [reduced]=MeshoptSimplifier.simplifyWithAttributes(indices,new Float32Array(pos.getArray()),3,attributes,5,weights,null,target,.005,['Permissive']);
 const oldIndices=p.getIndices();p.setIndices(doc.createAccessor().setType('SCALAR').setArray(reduced));
 if(oldIndices.listParents().length===1)oldIndices.dispose();compactPrimitive(p);
}
if(JSON.stringify(materialNames)!==JSON.stringify(doc.getRoot().listMaterials().map(m=>m.getName())))throw Error('Material order changed');
for(const e of doc.getRoot().listExtensionsUsed())if(e.extensionName==='EXT_meshopt_compression')e.dispose();
await fs.mkdir(destination,{recursive:true});await io.write(path.join(destination,'source.glb'),doc);
const credit=JSON.parse(await fs.readFile(path.join(sourceFolder,'credit.json'),'utf8'));
credit.lodModification={method:'meshoptimizer attribute-aware permissive simplification',ratio:.25,error:.005,trianglesBefore:before,trianglesAfter:count(),originalAssetUnchanged:true};
await fs.writeFile(path.join(destination,'credit.json'),JSON.stringify(credit,null,2));
console.log(JSON.stringify({sourceFolder,destination,before,after:count()}));
