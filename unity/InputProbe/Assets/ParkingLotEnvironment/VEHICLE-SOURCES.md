# ParkingLot vehicle sources

Retrieved 2026-11-02. These are independently published 3D models, not Hyundai-issued assets. The original public Sketchfab API metadata was fetched at retrieval time and is retained beside each source. Each source declares Creative Commons Attribution 4.0: https://creativecommons.org/licenses/by/4.0/ . Preserve author, work URL, license URL, and modification notice in the project credits.

## Attributions

- **2015 Hyundai Tucson**, by **razor24** (https://sketchfab.com/razor24). https://sketchfab.com/3d-models/7674a3bd168a451daf73fc0a33d39589 . CC BY 4.0. Creator description states it was made in Blender 2.79. Download: https://raw.githubusercontent.com/pedrosoandre/carros/main/assets/hyundai-tucson.glb . 1,152,688 bytes; exported 33,152 triangles, 18 materials.
- **2011 Hyundai i30 FD**, by **D3NK** (https://sketchfab.com/leeyunhe902). https://sketchfab.com/3d-models/036a13aefc3f48c5ba996aa2361b429e . CC BY 4.0. Creator profile links the author's SketchUp modeling portfolio. Download: https://raw.githubusercontent.com/pedrosoandre/carros/main/assets/hyundai-i30.glb . 1,380,712 bytes; exported 98,040 triangles, 96 source materials. Original SketchUp edge lines were excluded from the triangle-only intermediate.
- **2009 Hyundai Azera**, by **Nieve5677** (https://sketchfab.com/niev). https://sketchfab.com/3d-models/711b67ea05f9441a863dce4badaca660 . CC BY 4.0. Source description is blank; no contrary provenance statement was found. Download: https://raw.githubusercontent.com/pedrosoandre/carros/main/assets/hyundai-azera.glb . 1,452,700 bytes; exported 153,476 triangles, 77 materials.
- **LCT 3000 '95 - Low poly model**, by **Daniel Zhabotinsky**. https://sketchfab.com/3d-models/663a0953c038434a918cb85725c88ffa . CC BY 4.0. Creator explicitly describes improving their own model and permits modifications and digital-product use. A generic Japanese-style small-cabin 3-ton box truck, not a Hyundai model. Download: https://raw.githubusercontent.com/pedrosoandre/carros/main/assets/generico-caminhao.glb . 6,678,868 bytes; exported 10,286 triangles, 8 materials, 13 images.

## Modification notice

### Additional Ioniq 5 (2026-11-02)

- **Hyundai Ioniq 5 2022**, original **andikapratamaw**, https://sketchfab.com/3d-models/hyundai-ioniq-5-lowpoly-675e78311e8440d88714bd212cb7a8fb , CC BY 4.0. OpenX adaptation **Dogan Ulus / VevaLabs**, `MPL-2.0 AND CC-BY-4.0`, https://github.com/vevalabs/openx-assets/releases/tag/20261015 . 37,350 triangles; no photographic bitmap textures. Three scene instances selected after comparison. Exact source commit, hashes, full notices and editable Blender/GLB source are retained at `SourceModels~/hyundai-ioniq5-openx/`.
- Scene-only changes: paint palette, 35-degree weighted normal derivatives, interior/trim material derivatives, Korean sample number plates and shared-material batching. Neither the original GLB nor Blender source is overwritten.
- The same OpenX release's Tucson was also compared and retained as an unplaced candidate. Its detailed credit and source files are at `SourceModels~/hyundai-tucson-openx/`. Full attribution and modification notes for both are in `CREDITS.md`.
- Legacy Azera correction: six side-window parts carry opaque paint properties in the source. `ParkingLotAzeraFinish` applies glass and trim on scene instances and their separate distant derivatives; the source/native prefabs remain unchanged. The left/right quarter-window names are asymmetric and were confirmed with color-mask renders.

### Original vehicle conversion

The public `pedrosoandre/carros` library normalized, simplified, pruned, and compressed the original models; its `LICENSE-3D-MODELS.md` is preserved in `Tools~/`. For ParkingLot integration we decompressed/dequantized the GLBs, baked scene node transforms into position/normal data, extracted embedded images, and excluded non-triangle line primitives. No manufacturer website viewer assets or protected downloads were extracted.

## Intermediate format

Each `model.json` holds material properties and triangle mesh records. Each mesh `.bin` contains `vertexCount * 32` bytes of interleaved little-endian float32 position XYZ, normal XYZ, UV XY; followed by `indexCount * 4` bytes of little-endian uint32 indices.

- Position is in the original glTF world coordinate system; normals use the inverse transpose of that transform and are normalized.
- Negative world-transform determinant has already caused the corresponding triangle winding to be reversed. Apply the additional handedness conversion needed by Unity once.
- UVs are unchanged glTF UV0, with image origin at the top-left and positive V downward. Adapt V once when importing through Unity's normal image importer.
- No tangents are stored; recalculate tangents after coordinate and UV conversion if a normal texture is used.
- Material `baseColor` and `emissiveFactor` are glTF linear color factors. Base-color/emissive image samples use sRGB; normal and metallic-roughness maps are linear. Metallic is the B channel and roughness is the G channel of metallic-roughness images. Unity smoothness is `1 - roughness`.
- Three Hyundai source files contain geometry/material factors only, without bitmap images. The truck includes PBR images.
- Normalize using manifest bounds. Tucson length axis is Z (453.2303 units); i30 is Z (428647.8125 units, large world offset); Azera is Z (189.7037 units); truck is X (6.0226 units).
- Suggested rendered lengths: Tucson 4.53 m, i30 4.25 m, Azera 4.895 m. Truck source scale is already approximately meters; its 6.02 m length needs an appropriately long bay.
- The old car sources use very rough legacy materials. Calibrate car paint, chrome, and glass in Unity. Tucson body paint is `varnish`; i30 body paint is `paint` (not the repository's inaccurate `black_1` suggestion); Azera painted panels share the `[0.749,0.749,0.749,1]` base color and `0.968377` roughness.

## Excluded candidates

- Hyundai Santa Fe 2020: author description says it was taken from Minibus Vietnam Simulator.
- Hyundai Veloster Turbo: author description says it was taken from Asphalt 7.
- Elantra N 2021-23: insufficient original-author provenance; model/material naming suggests an imported game asset. Not downloaded for integration.
- Hyundai Porter by jjj0605: search index describes an original Blender model with CC BY, but the current official model API returns 404, and no usable download was verified.

No official Hyundai GitHub sample with a verifiable model redistribution license was found during this research.
- v24: Regrouped the razor24 Tucson glass triangles into eight cabin panes and one combined lamp group per LOD for transparent sorting. The eighteen derived meshes in Meshes/TucsonGlazing preserve every ordered triangle corner position, normal, UV and tangent. Source meshes/prefabs/materials remain unchanged; scene instances share the derivatives and a stable probe anchor. This is a rendering adaptation of the credited CC BY 4.0 model, not a new scan.
- v25: Applied a 50% masked normal refinement to the Tucson side panels in a separate near-LOD mesh. Linear midpoint subdivision affects 954 parent triangles; 3,818 remain original. Original corner positions/UVs and the piecewise-triangular surface are retained. The six cars share `Tucson_door_reflections.asset`; original mesh/material/prefab files and far LODs remain unchanged. Inputs, checksums and reproduction notes are in `SourceModels~/tucson-door-reflections/README.md`. Attribution remains razor24 / CC BY 4.0.
