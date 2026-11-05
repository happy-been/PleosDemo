import fs from 'node:fs/promises';
import path from 'node:path';
import {NodeIO} from '@gltf-transform/core';
import {ALL_EXTENSIONS} from '@gltf-transform/extensions';
import {dequantize} from '@gltf-transform/functions';
import {MeshoptDecoder} from 'meshoptimizer';
import {mat3,mat4,vec3} from 'gl-matrix';

await MeshoptDecoder.ready;
const io = new NodeIO().registerExtensions(ALL_EXTENSIONS).registerDependencies({'meshopt.decoder':MeshoptDecoder});
const folders=process.argv.slice(2);
for(const folder of folders){
 const doc=await io.read(path.join(folder,'source.glb'));
 await doc.transform(dequantize());
 for(const e of doc.getRoot().listExtensionsUsed()) if(e.extensionName==='EXT_meshopt_compression') e.dispose();
 await io.write(path.join(folder,'decompressed.glb'),doc);
 await io.write(path.join(folder,'decompressed.gltf'),doc);
 const root=doc.getRoot();
 const textures=new Map();
 await fs.mkdir(path.join(folder,'textures'),{recursive:true});
 for(const [i,t] of root.listTextures().entries()){
  const ext=({ 'image/jpeg':'jpg','image/png':'png','image/webp':'webp','image/ktx2':'ktx2' })[t.getMimeType()]||'bin';
  const name=`textures/texture-${i}.${ext}`;
  const image=t.getImage();
  if(image){await fs.writeFile(path.join(folder,name),image);textures.set(t,name);}
 }
 const tex=t=>t ? (textures.get(t)||null):null;
 const materialList=root.listMaterials();
 const manifest={name:path.basename(folder),format:'park-vehicle-intermediate-v1',layout:'positions and normals world-baked, little-endian float32 [px,py,pz,nx,ny,nz,u,v] then uint32 indices',coordinateSystem:'glTF right-handed; no Z flip',uvConvention:'Unchanged glTF TEXCOORD_0: image origin top-left, positive V down; importer must adapt to Unity texture convention',materials:materialList.map(m=>({name:m.getName(),baseColor:m.getBaseColorFactor(),metallic:m.getMetallicFactor(),roughness:m.getRoughnessFactor(),alphaMode:m.getAlphaMode(),alphaCutoff:m.getAlphaCutoff(),doubleSided:m.getDoubleSided(),baseColorTexture:tex(m.getBaseColorTexture()),normalTexture:tex(m.getNormalTexture()),normalScale:m.getNormalScale(),metallicRoughnessTexture:tex(m.getMetallicRoughnessTexture()),emissiveTexture:tex(m.getEmissiveTexture()),emissiveFactor:m.getEmissiveFactor(),occlusionTexture:tex(m.getOcclusionTexture()),occlusionStrength:m.getOcclusionStrength()})),meshes:[],bounds:{min:[Infinity,Infinity,Infinity],max:[-Infinity,-Infinity,-Infinity]},skippedNonTriangles:0};
 const scene=root.getDefaultScene()||root.listScenes()[0];
 let meshIndex=0;
 const nodes=[];scene.traverse(n=>nodes.push(n));
 for(const node of nodes){
  const mesh=node.getMesh();if(!mesh)continue;
  if(node.getSkin()) throw new Error('Skinned mesh requires explicit bake: '+node.getName());
  const world=node.getWorldMatrix();
  const normalMatrix=mat3.normalFromMat4(mat3.create(),world);
  const mirrored=mat4.determinant(world)<0;
  for(const [primitiveIndex,p] of mesh.listPrimitives().entries()){
   if(p.getMode()!==4){manifest.skippedNonTriangles++;continue;}
   const pos=p.getAttribute('POSITION');if(!pos)continue;
   const material=p.getMaterial();
   const texInfo=material?.getBaseColorTextureInfo();
   const texCoord=texInfo?.getTexCoord()||0;
   const texTransform=texInfo?.getExtension('KHR_texture_transform');
   const uvOffset=texTransform?.getOffset()||[0,0],uvScale=texTransform?.getScale()||[1,1],uvRotation=texTransform?.getRotation()||0;
   const norm=p.getAttribute('NORMAL'),uv=p.getAttribute('TEXCOORD_'+texCoord),ia=p.getIndices();
   const vertexCount=pos.getCount(),indexCount=ia?ia.getCount():vertexCount;
   if(indexCount%3)throw new Error('Nontriangular index count');
   const buffer=Buffer.alloc(vertexCount*32+indexCount*4);
   const pp=[],nn=[],uu=[];
   for(let i=0;i<vertexCount;i++){
    pos.getElement(i,pp);vec3.transformMat4(pp,pp,world);
    if(norm){norm.getElement(i,nn);vec3.transformMat3(nn,nn,normalMatrix);vec3.normalize(nn,nn);}else{nn[0]=0;nn[1]=1;nn[2]=0;}
    if(uv)uv.getElement(i,uu);else{uu[0]=0;uu[1]=0;}
    if(texTransform){const u=uu[0]*uvScale[0],v=uu[1]*uvScale[1];uu[0]=uvOffset[0]+Math.cos(uvRotation)*u-Math.sin(uvRotation)*v;uu[1]=uvOffset[1]+Math.sin(uvRotation)*u+Math.cos(uvRotation)*v;}
    const vals=[...pp,...nn,...uu];
    if(vals.some(x=>!Number.isFinite(x)))throw new Error('Invalid vertex');
    for(let k=0;k<8;k++)buffer.writeFloatLE(vals[k],i*32+k*4);
    for(let k=0;k<3;k++){manifest.bounds.min[k]=Math.min(manifest.bounds.min[k],pp[k]);manifest.bounds.max[k]=Math.max(manifest.bounds.max[k],pp[k]);}
   }
   const idx=ia?ia.getArray():null;
   for(let i=0;i<indexCount;i+=3){
    const v=[idx?idx[i]:i,idx?idx[i+1]:i+1,idx?idx[i+2]:i+2];
    if(mirrored)[v[1],v[2]]=[v[2],v[1]];
    for(let k=0;k<3;k++){if(v[k]>=vertexCount)throw new Error('Invalid index');buffer.writeUInt32LE(v[k],vertexCount*32+(i+k)*4);}
   }
   const file=`mesh-${String(meshIndex++).padStart(4,'0')}.bin`;
   await fs.writeFile(path.join(folder,file),buffer);
   manifest.meshes.push({name:`${node.getName()||mesh.getName()||'mesh'}_${primitiveIndex}`,file,material:materialList.indexOf(p.getMaterial()),vertexCount,indexCount,mirroredTransformWindingCorrected:mirrored,recalculateNormals:!norm,sourceTexCoord:texCoord,textureTransformBaked:!!texTransform});
  }
 }
 manifest.bounds.size=manifest.bounds.max.map((x,i)=>x-manifest.bounds.min[i]);
 manifest.triangles=manifest.meshes.reduce((s,m)=>s+m.indexCount/3,0);
 manifest.vertices=manifest.meshes.reduce((s,m)=>s+m.vertexCount,0);
 await fs.writeFile(path.join(folder,'model.json'),JSON.stringify(manifest,null,2));
 console.log(JSON.stringify({folder,meshes:manifest.meshes.length,materials:manifest.materials.length,textures:textures.size,triangles:manifest.triangles,vertices:manifest.vertices,bounds:manifest.bounds,skippedNonTriangles:manifest.skippedNonTriangles}));
}
