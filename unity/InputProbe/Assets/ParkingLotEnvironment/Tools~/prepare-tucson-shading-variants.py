#!/usr/bin/env python3
"""Shading-only 1-to-4 midpoint subdivision variants of the reviewed Tucson paint mesh.

Exported geometry is ALWAYS the original piecewise-triangular surface (original corners + arithmetic
edge midpoints, same for UVs). A hypothetical Loop step is computed only to derive normals; its
positions are never written. Outputs go to ./tucson-subdivision-experiment/:
lin1.bin, hybrid-bounded.bin, hybrid-full.bin, report.json (vertex 32 B: px py pz nx ny nz u v f32 LE,
then uint32 LE triangle indices).
"""
import hashlib
import json
import math
import os
from pathlib import Path

os.environ['OPENBLAS_NUM_THREADS'] = '1'
import numpy as np

BASE = Path(__file__).resolve().parents[1] / 'SourceModels~' / 'tucson-door-reflections'
OUT = BASE / "tucson-subdivision-experiment"
EXPECT_V, EXPECT_T = 3999, 4772
WELD_SCALE = 100000
SHARP_DEG = 35.0
COS_SHARP = math.cos(math.radians(SHARP_DEG))
BOUND_M = 0.005  # bounded variant: max hypothetical displacement 5 mm
LOC = np.array([[0, 3, 5], [3, 1, 4], [5, 4, 2], [3, 4, 5]])  # a,b,c,ab,bc,ca -> 0..5


def fail(msg):
    raise SystemExit("FAIL: " + msg)


def sha(b):
    return hashlib.sha256(b).hexdigest()


def edge_map(tris):
    d = {}
    for t, tri in enumerate(tris.tolist()):
        for k in range(3):
            a, b = tri[k], tri[(k + 1) % 3]
            d.setdefault((a, b) if a < b else (b, a), []).append((t, k))
    return d


def face_normals(pos, tris):
    p = pos[tris]
    c = np.cross(p[:, 1] - p[:, 0], p[:, 2] - p[:, 0])
    ln = np.linalg.norm(c, axis=1)
    return c / np.maximum(ln, 1e-300)[:, None], ln


def corner_angles(pos, tris):
    p = pos[tris]
    ang = np.empty(tris.shape)
    for k in range(3):
        e1, e2 = p[:, (k + 1) % 3] - p[:, k], p[:, (k + 2) % 3] - p[:, k]
        ang[:, k] = np.arctan2(np.linalg.norm(np.cross(e1, e2), axis=1), (e1 * e2).sum(1))
    return ang


def wedge_normals(G, C, sharp_keys):
    """Angle-weighted normals; union-find on face corners joined only across shared smooth edges."""
    nc = len(C)
    FN, ln = face_normals(G, C)
    if not (np.isfinite(FN).all() and (ln > 1e-12).all()):
        fail("degenerate/non-finite child face")
    uf = list(range(3 * nc))

    def find(x):
        while uf[x] != x:
            uf[x] = uf[uf[x]]
            x = uf[x]
        return x

    Cl = C.tolist()
    n_sharp = 0
    for key, occ in edge_map(C).items():
        if len(occ) != 2 or key in sharp_keys or FN[occ[0][0]] @ FN[occ[1][0]] < COS_SHARP:
            n_sharp += 1
            continue
        (t0, k0), (t1, _) = occ
        for j in (k0, (k0 + 1) % 3):
            v = Cl[t0][j]
            a, b = find(t0 * 3 + j), find(t1 * 3 + Cl[t1].index(v))
            if a != b:
                uf[a] = b
    roots = np.array([find(i) for i in range(3 * nc)])
    acc = np.zeros((3 * nc, 3))
    np.add.at(acc, roots, (FN[:, None, :] * corner_angles(G, C)[:, :, None]).reshape(-1, 3))
    n = acc[roots]
    ln = np.linalg.norm(n, axis=1)
    if not (np.isfinite(n).all() and (ln > 1e-12).all()):
        fail("non-finite or zero wedge normal")
    return (n / ln[:, None]).reshape(nc, 3, 3), n_sharp, len(set(roots.tolist()))


def main():
    # ---- read & validate input ----
    meta = json.loads((BASE / "tucson-paint-current.json").read_text())
    nv, nt = int(meta["vertices"]), int(meta["triangles"])
    if (nv, nt) != (EXPECT_V, EXPECT_T):
        fail("metadata counts %d/%d unexpected" % (nv, nt))
    unit = float(meta['worldMetresPerLocalUnit'])
    if not np.isfinite(unit) or unit <= 0:
        fail('invalid physical model scale')
    src_path = BASE / "tucson-paint-current.bin"
    if not src_path.is_file():
        fail("exact reviewed source tucson-paint-current.bin is missing")
    raw = src_path.read_bytes()
    if len(raw) != nv * 32 + nt * 12:
        fail("byte length %d != expected %d" % (len(raw), nv * 32 + nt * 12))
    vdat = np.frombuffer(raw, "<f4", nv * 8).reshape(nv, 8)
    src_tri = np.frombuffer(raw, "<u4", nt * 3, nv * 32).reshape(nt, 3).astype(np.int64)
    if not np.isfinite(vdat).all():
        fail("non-finite vertex data")
    if src_tri.max() >= nv:
        fail("triangle index out of range")
    src_pos, src_nrm, src_uv = vdat[:, 0:3], vdat[:, 3:6], vdat[:, 6:8]
    p64 = src_pos.astype(np.float64)

    # ---- 1. weld (geometric adjacency only), coarse edges, sharp/locked ----
    keys = np.rint(p64 * (WELD_SCALE * unit)).astype(np.int64)
    _, first, inv = np.unique(keys, axis=0, return_index=True, return_inverse=True)
    inv = inv.reshape(-1)
    wpos = p64[first]
    V0 = len(wpos)
    T = inv[src_tri]
    if (T[:, 0] == T[:, 1]).any() or (T[:, 1] == T[:, 2]).any() or (T[:, 2] == T[:, 0]).any():
        fail("degenerate source triangle after weld")
    fn0, ln0 = face_normals(wpos, T)
    if not (ln0 > 1e-12).all():
        fail("zero-area source triangle")
    emap = edge_map(T)
    edges = sorted(emap)
    eid = {k: i for i, k in enumerate(edges)}
    E = len(edges)
    sharp = np.zeros(E, bool)
    n_boundary = n_nonmanifold = n_sharp_dihedral = n_inconsistent = 0
    for i, k in enumerate(edges):
        occ = emap[k]
        if len(occ) != 2:
            sharp[i] = True
            n_boundary += len(occ) == 1
            n_nonmanifold += len(occ) > 2
            continue
        (t0, k0), (t1, k1) = occ
        if (T[t0][k0], T[t0][(k0 + 1) % 3]) == (T[t1][k1], T[t1][(k1 + 1) % 3]):
            n_inconsistent += 1
            sharp[i] = True  # inconsistent winding cannot define a trustworthy smooth join
        if fn0[t0] @ fn0[t1] < COS_SHARP:
            sharp[i] = True
            n_sharp_dihedral += 1
    locked = np.zeros(V0, bool)
    for i in np.nonzero(sharp)[0]:
        locked[list(edges[i])] = True

    # ---- 2. shared geometric subdivision topology + per-corner wedge attributes ----
    P = nt
    g6 = np.empty((P, 6), np.int64)
    g6[:, :3] = T
    for p in range(P):
        for j in range(3):
            a, b = T[p, j], T[p, (j + 1) % 3]
            g6[p, 3 + j] = V0 + eid[(a, b) if a < b else (b, a)]
    C = g6[:, LOC].reshape(-1, 3)  # child geometric indices (winding preserved)
    NC, NV = len(C), len(C) * 3
    parent = np.repeat(np.arange(P), 4)
    child_loc = np.tile(LOC, (P, 1))

    def local6(attr):  # original corners then arithmetic midpoints ab, bc, ca (float32)
        a = attr[src_tri].astype(np.float64)
        out = np.concatenate([a, (a + a[:, [1, 2, 0]]) / 2], axis=1)
        return out.astype(np.float32)

    pos6, uv6, nrm6 = local6(src_pos), local6(src_uv), local6(src_nrm)
    out_pos = pos6[:, LOC].reshape(-1, 3)
    out_uv = uv6[:, LOC].reshape(-1, 2)

    # ---- 3. hypothetical Loop step (normals only; never exported) ----
    ea = np.array(edges)
    lin = np.zeros((V0 + E, 3))
    lin[:V0] = wpos
    lin[V0:] = (wpos[ea[:, 0]] + wpos[ea[:, 1]]) / 2
    loop = lin.copy()
    nbrs = [[] for _ in range(V0)]
    for a, b in edges:
        nbrs[a].append(b)
        nbrs[b].append(a)
    for v in range(V0):
        n = len(nbrs[v])
        if locked[v] or n == 0:
            continue
        beta = 3 / 16 if n == 3 else 3 / (8 * n)
        loop[v] = (1 - n * beta) * wpos[v] + beta * wpos[nbrs[v]].sum(0)
    for i, (a, b) in enumerate(edges):
        if sharp[i]:
            continue
        (t0, k0), (t1, k1) = emap[(a, b)]
        o0, o1 = T[t0][(k0 + 2) % 3], T[t1][(k1 + 2) % 3]
        loop[V0 + i] = 3 / 8 * (wpos[a] + wpos[b]) + 1 / 8 * (wpos[o0] + wpos[o1])
    disp = np.linalg.norm(loop - lin, axis=1)
    max_disp = float(disp.max())
    factor = min(1.0, BOUND_M / (max_disp * unit)) if max_disp > 0 else 1.0
    initial_factor = factor
    safety_halvings = 0
    # Even a bounded hypothetical normal source can fold very thin triangles.
    # Reduce the SINGLE global factor until every child keeps a reliable orientation.
    source_fn, _ = face_normals(p64, src_tri)
    while True:
        bounded_fn, bounded_area = face_normals(lin + factor * (loop - lin), C)
        dot_parent = (bounded_fn * source_fn[parent]).sum(1)
        if np.all(bounded_area > 1e-12) and np.all(dot_parent > .7):
            break
        factor *= .5
        safety_halvings += 1
        if safety_halvings > 20:
            fail('could not find a valid bounded normal-source surface')
    geoms = {
        "lin1": lin,
        "hybrid-bounded": lin + factor * (loop - lin),
        "hybrid-full": loop,
    }

    # ---- 4. crease-aware wedge normals per variant ----
    sk = set()
    for i in np.nonzero(sharp)[0]:
        a, b = edges[i]
        m = V0 + int(i)
        sk.add((min(a, m), max(a, m)))
        sk.add((min(b, m), max(b, m)))

    pfn, pln = face_normals(p64, src_tri)  # parent face normal from original (unwelded) corners
    sn6 = nrm6.astype(np.float64)
    sn6 = sn6 / np.maximum(np.linalg.norm(sn6, axis=2, keepdims=True), 1e-12)
    ref_face = np.repeat(pfn, 4, axis=0)[:, None, :]
    ref_vert = sn6[:, LOC].reshape(NC, 3, 3)

    OUT.mkdir(exist_ok=True)
    idx_bytes = np.arange(NV, dtype="<u4").tobytes()
    report_var = {}
    for name, G in geoms.items():
        nrm, n_sharp_child, n_comp = wedge_normals(G, C, sk)
        child_face_normals, _ = face_normals(G, C)
        geometric_dot = (child_face_normals * pfn[parent]).sum(1)
        n32 = nrm.reshape(-1, 3).astype(np.float32)
        if not np.isfinite(n32).all():
            fail(name + ": non-finite normals")
        unit_err = float(np.abs(np.linalg.norm(n32.astype(np.float64), axis=1) - 1).max())
        verts = np.concatenate([out_pos, n32, out_uv], axis=1).astype("<f4")
        (OUT / (name + ".bin")).write_bytes(verts.tobytes() + idx_bytes)
        d_face = (nrm * ref_face).sum(2)
        d_vert = (nrm * ref_vert).sum(2)
        report_var[name] = {
            "file": name + ".bin",
            "smoothing_components": n_comp,
            "sharp_or_nonmanifold_child_edges": n_sharp_child,
            "normals_finite": True,
            "max_abs_normal_length_error": unit_err,
            "hypothetical_min_child_face_dot_parent_face": float(geometric_dot.min()),
            "hypothetical_child_faces_dot_parent_le_0.7": int((geometric_dot <= .7).sum()),
            "hypothetical_flipped_child_faces": int((geometric_dot <= 0).sum()),
            "min_wedge_normal_dot_parent_source_face_normal": float(d_face.min()),
            "count_wedge_normal_dot_parent_source_face_normal_le_0.7": int((d_face <= 0.7).sum()),
            "min_wedge_normal_dot_interpolated_source_vertex_normal": float(d_vert.min()),
            "count_wedge_normal_dot_interpolated_source_vertex_normal_le_0.7": int((d_vert <= 0.7).sum()),
        }

    # ---- verify by reading the written files back ----
    pos_sha, uv_sha, max_orig_disp, max_orig_uv = {}, {}, 0.0, 0.0
    sel = child_loc < 3
    sidx = src_tri[parent[:, None], np.minimum(child_loc, 2)]
    for name in geoms:
        b = (OUT / (name + ".bin")).read_bytes()
        if len(b) != NV * 32 + NC * 12:
            fail(name + ": written size mismatch")
        v = np.frombuffer(b, "<f4", NV * 8).reshape(NV, 8)
        ix = np.frombuffer(b, "<u4", NC * 3, NV * 32)
        if ix.max() >= NV or not np.isfinite(v).all():
            fail(name + ": readback check failed")
        pos_sha[name], uv_sha[name] = sha(np.ascontiguousarray(v[:, 0:3]).tobytes()), sha(np.ascontiguousarray(v[:, 6:8]).tobytes())
        rp = v[:, 0:3].reshape(NC, 3, 3)
        ru = v[:, 6:8].reshape(NC, 3, 2)
        max_orig_disp = max(max_orig_disp, float(np.abs(rp[sel].astype(np.float64) - p64[sidx[sel]]).max()))
        max_orig_uv = max(max_orig_uv, float(np.abs(ru[sel].astype(np.float64) - src_uv.astype(np.float64)[sidx[sel]]).max()))
        report_var[name]["file_sha256"] = sha(b)
        report_var[name]["position_sha256"] = pos_sha[name]
        report_var[name]["uv_sha256"] = uv_sha[name]
    if len(set(pos_sha.values())) != 1 or len(set(uv_sha.values())) != 1:
        fail("position/UV arrays differ between variants")
    if max_orig_disp != 0.0 or max_orig_uv != 0.0:
        fail("original corner position/UV changed")

    report = {
        "vertices": NV,
        "triangles": NC,
        "format": "32-byte f32 LE [px,py,pz,nx,ny,nz,u,v] x vertices, then uint32 LE indices x triangles*3; Unity mesh LOCAL units, unchanged prefab transform supplies world scale",
        "world_metres_per_local_unit": unit,
        "source": {
            "file": src_path.name, "sha256": sha(raw), "vertices": nv, "triangles": nt,
            "welded_vertices": V0, "coarse_edges": E,
            "boundary_edges": n_boundary, "nonmanifold_edges": n_nonmanifold,
            "sharp_edges_total": int(sharp.sum()), "sharp_edges_by_dihedral_gt_35deg": n_sharp_dihedral,
            "inconsistent_winding_manifold_edges_diagnostic": n_inconsistent,
            "locked_vertices": int(locked.sum()),
        },
        "exported_surface": {
            "note": "Unchanged original piecewise-triangular surface: original corners + arithmetic midpoints only.",
            "children_per_parent": 4,
            "max_original_corner_position_displacement": max_orig_disp,
            "max_original_corner_uv_displacement": max_orig_uv,
            "identical_position_checksum_across_variants": True,
            "identical_uv_checksum_across_variants": True,
            "position_sha256": next(iter(pos_sha.values())),
            "uv_sha256": next(iter(uv_sha.values())),
            "loop_positions_exported": False,
        },
        "hypothetical_loop_diagnostics_not_exported": {
            "note": "Used only to derive normals; these positions are NOT in any output file.",
            "full_max_displacement_m": max_disp * unit,
            "max_even_vertex_displacement_m": float(disp[:V0].max()) * unit,
            "max_odd_vertex_displacement_m": float(disp[V0:].max()) * unit if E else 0.0,
            "bound_m": BOUND_M,
            "initial_five_mm_lerp_factor": initial_factor,
            "global_factor_safety_halvings": safety_halvings,
            "bounded_global_lerp_factor": factor,
            "bounded_max_displacement_m": factor * max_disp * unit,
        },
        "variants": report_var,
    }
    (OUT / "report.json").write_text(json.dumps(report, indent=2))
    print(json.dumps({k: report[k] for k in ("vertices", "triangles")}), "->", OUT)


if __name__ == "__main__":
    main()
