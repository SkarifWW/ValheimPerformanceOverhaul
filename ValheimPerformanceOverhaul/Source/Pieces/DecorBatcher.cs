using System.Collections.Generic;
using UnityEngine;
using HarmonyLib;
using BepInEx.Configuration;
using ValheimPerformanceOverhaul;

namespace ValheimPerformanceOverhaul.Pieces
{
    public class DecorBatcher : MonoBehaviour
    {
        public static DecorBatcher Instance { get; private set; }

                        private static ConfigEntry<bool> _batchingEnabledEntry;
        private static ConfigEntry<int> _minPiecesToBatch;
        private static ConfigEntry<float> _rebuildCooldown;

        private static bool BatchingEnabled
        {
            get
            {
                if (_batchingEnabledEntry == null && Plugin.Instance != null)
                {
                    _batchingEnabledEntry = Plugin.Instance.Config.Bind(
                        "17. Decor Batching",
                        "Enabled",
                        false,
                        "Combines meshes of decorative (Misc category) pieces in a chunk grid " +
                        "to reduce draw calls. Only affects pieces without WearNTear. " +
                        "Enable on potato PCs with many decorations; leave OFF if unsure.");

                    _minPiecesToBatch = Plugin.Instance.Config.Bind(
                        "17. Decor Batching",
                        "Min Pieces To Batch",
                        3,
                        new ConfigDescription(
                            "Minimum number of pieces in a chunk before batching is applied.",
                            new AcceptableValueRange<int>(2, 20)));

                    _rebuildCooldown = Plugin.Instance.Config.Bind(
                        "17. Decor Batching",
                        "Rebuild Cooldown (seconds)",
                        2.0f,
                        new ConfigDescription(
                            "How long to wait after a piece change before rebuilding the combined mesh.",
                            new AcceptableValueRange<float>(0.5f, 10.0f)));
                }
                return _batchingEnabledEntry?.Value ?? false;
            }
        }

        private const int GRID_SIZE = 32;

        private readonly Dictionary<Vector2Int, BatchChunk> _chunks =
            new Dictionary<Vector2Int, BatchChunk>();

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

                        if (piece.GetComponent<WearNTear>() != null) return;

                        if (piece.GetComponent<ZSyncTransform>() != null) return;
            if (piece.GetComponent<Rigidbody>() != null) return;

            var mf = piece.GetComponentInChildren<MeshFilter>();
            var mr = piece.GetComponentInChildren<MeshRenderer>();
            if (mf == null || mr == null || mr.sharedMaterial == null) return;

            Vector2Int gridPos = GetGridPos(piece.transform.position);
            if (!_chunks.ContainsKey(gridPos))
                _chunks[gridPos] = new BatchChunk(gridPos);

            _chunks[gridPos].AddPiece(piece, mf, mr);
        }

        public void UnregisterPiece(Piece piece)
        {
            if (!BatchingEnabled) return;

            Vector2Int gridPos = GetGridPos(piece.transform.position);
            if (_chunks.TryGetValue(gridPos, out var chunk))
                chunk.RemovePiece(piece);
        }

        private Vector2Int GetGridPos(Vector3 pos) =>
            new Vector2Int(
                Mathf.FloorToInt(pos.x / GRID_SIZE),
                Mathf.FloorToInt(pos.z / GRID_SIZE));

        private void Update()
        {
            if (!BatchingEnabled) return;

            float cooldown = _rebuildCooldown?.Value ?? 2.0f;

            foreach (var chunk in _chunks.Values)
            {
                if (chunk.IsDirty && Time.time - chunk.LastUpdateTime > cooldown)
                {
                    chunk.Rebuild(_minPiecesToBatch?.Value ?? 3);
                    break;                 }
            }
        }

        private void OnDestroy()
        {
                        foreach (var chunk in _chunks.Values)
                chunk.RestoreAllRenderers();

            _chunks.Clear();
            Instance = null;
        }

        
        private class BatchChunk
        {
            public Vector2Int GridPos;
            public bool IsDirty;
            public float LastUpdateTime;

            private readonly List<PieceEntry> _entries = new List<PieceEntry>();
            private GameObject _batchRoot;
            private Mesh _combinedMesh; 
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
                MarkDirty();
            }

            public void RemovePiece(Piece p)
            {
                int idx = _entries.FindIndex(e => e.Piece == p);
                if (idx == -1) return;

                                                if (_entries[idx].MR != null)
                    _entries[idx].MR.enabled = true;

                _entries.RemoveAt(idx);
                MarkDirty();
            }

            private void MarkDirty()
            {
                IsDirty = true;
                LastUpdateTime = Time.time;
            }

                                                public void RestoreAllRenderers()
            {
                foreach (var e in _entries)
                    if (e.MR != null) e.MR.enabled = true;

                DestroyBatchRoot();
            }

            public void Rebuild(int minPieces)
            {
                IsDirty = false;

                                                foreach (var e in _entries)
                    if (e.MR != null) e.MR.enabled = true;

                DestroyBatchRoot();

                                for (int i = _entries.Count - 1; i >= 0; i--)
                {
                    if (_entries[i].Piece == null || _entries[i].MF == null || _entries[i].MR == null)
                        _entries.RemoveAt(i);
                }

                                if (_entries.Count < minPieces)
                    return;

                                var matGroups = new Dictionary<Material, List<CombineInstance>>();

                foreach (var e in _entries)
                {
                    Material mat = e.MR.sharedMaterial;
                    if (mat == null) continue;

                    if (!matGroups.ContainsKey(mat))
                        matGroups[mat] = new List<CombineInstance>();

                    matGroups[mat].Add(new CombineInstance
                    {
                        mesh = e.MF.sharedMesh,
                        transform = e.MF.transform.localToWorldMatrix
                    });

                                        e.MR.enabled = false;
                }

                _batchRoot = new GameObject($"_Batch_{GridPos.x}_{GridPos.y}");

                foreach (var kvp in matGroups)
                {
                    if (kvp.Value.Count == 0) continue;

                    var go = new GameObject($"Batch_{kvp.Key.name}");
                    go.transform.SetParent(_batchRoot.transform);
                    go.transform.position = Vector3.zero;

                    var mf = go.AddComponent<MeshFilter>();
                    var mr = go.AddComponent<MeshRenderer>();

                                        _combinedMesh = new Mesh();
                    _combinedMesh.CombineMeshes(kvp.Value.ToArray(), true, true);
                    mf.sharedMesh = _combinedMesh;
                    mr.sharedMaterial = kvp.Key;
                }

                if (Plugin.DebugLoggingEnabled.Value)
                    Plugin.Log.LogInfo(
                        $"[DecorBatcher] Rebuilt chunk {GridPos} with {_entries.Count} pieces.");
            }

            private void DestroyBatchRoot()
            {
                                if (_combinedMesh != null)
                {
                    Object.Destroy(_combinedMesh);
                    _combinedMesh = null;
                }

                if (_batchRoot != null)
                {
                    Object.Destroy(_batchRoot);
                    _batchRoot = null;
                }
            }
        }
    }

            
    [HarmonyPatch(typeof(Piece), "Awake")]
    public static class DecorBatcher_Awake_Patch
    {
        [HarmonyPostfix]
        private static void Postfix(Piece __instance)
        {
            if (__instance.gameObject.layer == LayerMask.NameToLayer("ghost")) return;
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