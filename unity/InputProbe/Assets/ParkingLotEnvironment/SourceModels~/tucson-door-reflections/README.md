# Tucson door reflection derivative

Derivative of **2015 Hyundai Tucson** by **razor24**, CC BY 4.0.
Original work: https://sketchfab.com/3d-models/7674a3bd168a451daf73fc0a33d39589
Author: https://sketchfab.com/razor24
License: https://creativecommons.org/licenses/by/4.0/
The public carros conversion and the scene's weighted normal adaptation are documented in VEHICLE-SOURCES.md and CREDITS.md.

The adopted `door-50.bin` changes shading normals within the side-panel mask, with linear subdivision of 954 original triangles. The other 3,818 original triangles stay unsubdivided. Original corner positions and UVs do not move. Midpoints lie on the existing faces. No smoothed positions are exported. This is a rendering adaptation, not a vehicle scan or a Hyundai-issued model.

- Source dump: 3,999 vertices / 4,772 triangles, SHA256 `3ff22dd22f2fa3f2f16712fd5be04f8fe45ad70fbbdf7fa67616a414f30c62d7`.
- Adopted binary: 22,902 vertices / 7,634 triangles, SHA256 `98eb1ecc9ae4c133ce2ca3593d7b448b3abc7ff33b1bc3d11b41d5ed40a349f8`.
- Layout: float32 LE position XYZ, normal XYZ, UV XY (32 bytes per vertex), then uint32 LE triangle indices. Coordinates are already Unity mesh-local; do not apply another axis or UV flip.
- Six near-LOD instances share one mesh. Far LODs, materials, glass, layout and collision remain as before.
- Known limits: softened lower door highlight and small dark handle-pocket highlights remain. The rear quarter and tire geometry still show facets.

Reproduce with Python + NumPy, one process at a time:

```
py Assets/ParkingLotEnvironment/Tools~/prepare-tucson-shading-variants.py
py Assets/ParkingLotEnvironment/Tools~/prepare-tucson-door-shading.py
```

The second script writes experimental outputs in `tucson-door-shading-experiment/`. Its `door-50.bin` must match the adopted checksum above. The scripts do not overwrite the adopted binary or Unity assets. The full-body and strength-0/80 variants are comparison data only. The Unity finish helper validates source and adopted checksums before assigning the derivative and refuses to overwrite an edited derivative.
