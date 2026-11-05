import fs from 'node:fs/promises';
import path from 'node:path';
for(const folder of process.argv.slice(2)){
 const m=JSON.parse(await fs.readFile(path.join(folder,'model.json'),'utf8'));
 let checkedVertices=0,checkedIndices=0,minNormal=Infinity,maxNormal=0;
 for(const mesh of m.meshes){
  const b=await fs.readFile(path.join(folder,mesh.file));
  if(b.length!==mesh.vertexCount*32+mesh.indexCount*4)throw Error('Size mismatch '+mesh.file);
  if(mesh.material<0||mesh.material>=m.materials.length)throw Error('Material mismatch');
  for(let i=0;i<mesh.vertexCount;i++){
   const values=Array.from({length:8},(_,j)=>b.readFloatLE(i*32+j*4));
   if(values.some(v=>!Number.isFinite(v)))throw Error('Invalid vertex');
   const n=Math.hypot(...values.slice(3,6));minNormal=Math.min(minNormal,n);maxNormal=Math.max(maxNormal,n);
   if(Math.abs(n-1)>0.001&&!mesh.recalculateNormals)throw Error('Unnormalized normal '+n);
  }
  for(let i=0;i<mesh.indexCount;i++)if(b.readUInt32LE(mesh.vertexCount*32+i*4)>=mesh.vertexCount)throw Error('Bad index');
  checkedVertices+=mesh.vertexCount;checkedIndices+=mesh.indexCount;
 }
 for(const mat of m.materials)for(const field of ['baseColorTexture','normalTexture','metallicRoughnessTexture','emissiveTexture','occlusionTexture'])if(mat[field])await fs.access(path.join(folder,mat[field]));
 console.log(JSON.stringify({folder,result:'PASS',checkedVertices,checkedIndices,minNormal,maxNormal}));
}
