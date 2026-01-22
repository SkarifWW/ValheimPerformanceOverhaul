using System.Collections.Generic;
using UnityEngine;
using ValheimPerformanceOverhaul;

namespace ValheimPerformanceOverhaul.Pieces
{
    public class PieceSleepManager : MonoBehaviour
    {
        public static PieceSleepManager Instance { get; private set; }

        private readonly List<PieceContext> _trackedPieces = new List<PieceContext>();
        private const float UPDATE_INTERVAL = 5.0f;
        private float _updateTimer;

        // Configuration
        public float SleepDistance => Plugin.PieceUpdateSkipDistance.Value;
        public bool DisableColliders = false;

        private class PieceContext
        {
            public Piece Piece;
            public WearNTear WearNTear;
            public ZNetView NetView;
            public Collider[] Colliders;
            public Transform Transform;
            public bool IsSleeping;
            public float LastInteractionTime;
        }

        private void Awake()
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            Plugin.Log.LogInfo("[PieceSleepManager] Initialized.");
        }

        public void RegisterPiece(Piece piece)
        {
            if (piece == null) return;

            var context = new PieceContext
            {
                Piece = piece,
                WearNTear = piece.GetComponent<WearNTear>(),
                NetView = piece.GetComponent<ZNetView>(),
                Transform = piece.transform,
                Colliders = piece.GetComponentsInChildren<Collider>(true),
                LastInteractionTime = Time.time
            };

            // Only track if it has components we want to optimize
            if (context.WearNTear != null || context.NetView != null || context.Colliders.Length > 0)
            {
                _trackedPieces.Add(context);
            }
        }

        public void UnregisterPiece(Piece piece)
        {
            // Linear removal might be slow for massive bases. 
            // TODO: Optimize if needed (Dictionary mapping).
            for (int i = _trackedPieces.Count - 1; i >= 0; i--)
            {
                if (_trackedPieces[i].Piece == piece)
                {
                    _trackedPieces.RemoveAt(i);
                    return;
                }
            }
        }

        public void WakeUp(Piece piece)
        {
             // Helper to force wake up a piece
             // Find context and set IsSleeping = false;
        }

        private void Update()
        {
            if (Player.m_localPlayer == null) return;

            _updateTimer += Time.deltaTime;
            if (_updateTimer < UPDATE_INTERVAL) return;
            _updateTimer = 0f;

            Vector3 playerPos = Player.m_localPlayer.transform.position;
            float sleepDistSqr = SleepDistance * SleepDistance;

            // Process a chunk of pieces per frame? 
            // For now, process all every 5 seconds (existing plan).
            // Optimization: split processing over frames if list is huge.
            
            for (int i = _trackedPieces.Count - 1; i >= 0; i--)
            {
                var ctx = _trackedPieces[i];
                if (ctx.Piece == null) 
                {
                    _trackedPieces.RemoveAt(i);
                    continue;
                }

                float distSqr = (ctx.Transform.position - playerPos).sqrMagnitude;
                bool shouldSleep = distSqr > sleepDistSqr;

                if (shouldSleep && !ctx.IsSleeping)
                {
                    SetSleepState(ctx, true);
                }
                else if (!shouldSleep && ctx.IsSleeping)
                {
                    SetSleepState(ctx, false);
                }
            }
        }

        private void SetSleepState(PieceContext ctx, bool sleep)
        {
            ctx.IsSleeping = sleep;
            
            // Logic for Sleeping
            if (ctx.WearNTear != null)
            {
                ctx.WearNTear.enabled = !sleep; 
            }

            // ZNetView throttling is handled separately usually, 
            // but we can disable local update if ZNetView has one.
            // Generally ZNetView doesn't have an Update() method that is heavy, 
            // it's the syncing.
            
            // Colliders
            if (DisableColliders && ctx.Colliders != null)
            {
               foreach(var col in ctx.Colliders)
               {
                   if (col != null) col.enabled = !sleep;
               }
            }
        }

        private void OnDestroy()
        {
            _trackedPieces.Clear();
            Instance = null;
        }
    }
}
