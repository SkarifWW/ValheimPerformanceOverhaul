using System.Collections.Generic;
using UnityEngine;
using HarmonyLib;
using ValheimPerformanceOverhaul; // Plugin

namespace ValheimPerformanceOverhaul.Pieces
{
    public class DecorBatcher : MonoBehaviour
    {
        public static DecorBatcher Instance { get; private set; }

        // Grid system for batching (size 32x32?)
        private const int GRID_SIZE = 32;
        private readonly Dictionary<Vector2Int, BatchChunk> _chunks = new Dictionary<Vector2Int, BatchChunk>();

        // Setup via Plugin?
        public bool BatchingEnabled => Plugin.PieceOptimizationEnabled.Value; // Should have its own config?

        private void Awake()
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            Plugin.Log.LogInfo("[DecorBatcher] Initialized.");
        }

        public void RegisterPiece(Piece piece)
        {
            if (!BatchingEnabled) return;
            if (piece.m_category != Piece.PieceCategory.Misc) return;

            // Only batch if it has a MeshFilter/Renderer and is static
            var mf = piece.GetComponentInChildren<MeshFilter>();
            var mr = piece.GetComponentInChildren<MeshRenderer>();

            if (mf == null || mr == null) return;
            if (mr.sharedMaterial == null) return;

            Vector2Int gridPos = GetGridPos(piece.transform.position);

            if (!_chunks.ContainsKey(gridPos))
            {
                _chunks[gridPos] = new BatchChunk(gridPos);
            }

            _chunks[gridPos].AddPiece(piece, mf, mr);
        }

        public void UnregisterPiece(Piece piece)
        {
            // If a piece is destroyed, we must invalidate the batch
            Vector2Int gridPos = GetGridPos(piece.transform.position);
            if (_chunks.TryGetValue(gridPos, out BatchChunk chunk))
            {
                chunk.RemovePiece(piece);
            }
        }

        private Vector2Int GetGridPos(Vector3 pos)
        {
            return new Vector2Int(Mathf.FloorToInt(pos.x / GRID_SIZE), Mathf.FloorToInt(pos.z / GRID_SIZE));
        }

        private void Update()
        {
            if (!BatchingEnabled) return;

            // Process one dirty chunk per frame to avoid lag spikes
            foreach (var chunk in _chunks.Values)
            {
                if (chunk.IsDirty && Time.time - chunk.LastUpdateTime > 2.0f)
                {
                    chunk.Rebuild();
                    break;
                }
            }
        }

        private class BatchChunk
        {
            public Vector2Int GridPos;
            public bool IsDirty;
            public float LastUpdateTime;

            private List<PieceEntry> _entries = new List<PieceEntry>();
            private GameObject _batchRoot;

            private struct PieceEntry
            {
                public Piece Piece;
                public MeshFilter MF;
                public MeshRenderer MR;
            }

            public BatchChunk(Vector2Int pos)
            {
                GridPos = pos;
            }

            public void AddPiece(Piece p, MeshFilter mf, MeshRenderer mr)
            {
                _entries.Add(new PieceEntry { Piece = p, MF = mf, MR = mr });
                IsDirty = true;
                LastUpdateTime = Time.time;
            }

            public void RemovePiece(Piece p)
            {
                int index = _entries.FindIndex(e => e.Piece == p);
                if (index != -1)
                {
                    // If we remove an item, we must re-enable its renderer if it was hidden?
                    // But it's being destroyed, so who cares.
                    // However, we MUST rebuild the batch to remove it from visual.
                    _entries.RemoveAt(index);
                    IsDirty = true;
                    LastUpdateTime = Time.time;
                }
            }

            public void Rebuild()
            {
                IsDirty = false;
                LastUpdateTime = Time.time;

                // Cleanup old batch
                if (_batchRoot != null)
                {
                    Destroy(_batchRoot);
                }

                if (_entries.Count < 2)
                {
                    // Not enough items to batch, just enable originals
                    foreach (var e in _entries)
                    {
                        if (e.MR != null) e.MR.enabled = true;
                    }
                    return;
                }

                // Group by Material
                var matGroups = new Dictionary<Material, List<CombineInstance>>();

                foreach (var e in _entries)
                {
                    if (e.Piece == null || e.MF == null || e.MR == null) continue;

                    Material mat = e.MR.sharedMaterial;
                    if (mat == null) continue;

                    if (!matGroups.ContainsKey(mat))
                    {
                        matGroups[mat] = new List<CombineInstance>();
                    }

                    // Create combine instance relative to world? 
                    // Mesh.CombineMeshes can take world transform.
                    var combine = new CombineInstance();
                    combine.mesh = e.MF.sharedMesh;
                    combine.transform = e.MF.transform.localToWorldMatrix;

                    matGroups[mat].Add(combine);

                    // Disable original
                    e.MR.enabled = false;
                }

                // Create Batches
                _batchRoot = new GameObject($"_Batch_{GridPos.x}_{GridPos.y}");

                foreach (var kvp in matGroups)
                {
                    Material mat = kvp.Key;
                    List<CombineInstance> combines = kvp.Value;

                    // Unity mesh limit
                    if (combines.Count > 0)
                    {
                        GameObject go = new GameObject($"Batch_{mat.name}");
                        go.transform.SetParent(_batchRoot.transform);
                        go.transform.position = Vector3.zero; // World space meshes

                        var mf = go.AddComponent<MeshFilter>();
                        var mr = go.AddComponent<MeshRenderer>();

                        Mesh newMesh = new Mesh();
                        newMesh.CombineMeshes(combines.ToArray(), true, true);

                        mf.sharedMesh = newMesh;
                        mr.sharedMaterial = mat;
                    }
                }
            }
        }
    }

    // Patch to hook into DecorBatcher
    [HarmonyPatch(typeof(Piece), "Awake")]
    public static class DecorBatcher_Awake_Patch
    {
        [HarmonyPostfix]
        private static void Postfix(Piece __instance)
        {
            // ✅ FIX: Пропускаем ghost объекты (проекции при строительстве)
            if (__instance.gameObject.layer == LayerMask.NameToLayer("ghost"))
            {
                return;
            }

            DecorBatcher.Instance?.RegisterPiece(__instance);
        }
    }

    [HarmonyPatch(typeof(Piece), "OnDestroy")]
    public static class DecorBatcher_OnDestroy_Patch
    {
        [HarmonyPostfix]
        private static void Postfix(Piece __instance)
        {
            DecorBatcher.Instance?.UnregisterPiece(__instance);
        }
    }
}