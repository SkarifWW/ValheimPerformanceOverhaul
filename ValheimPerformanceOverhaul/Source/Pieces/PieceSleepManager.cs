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

                        if (context.WearNTear != null || context.NetView != null || context.Colliders.Length > 0)
            {
                _trackedPieces.Add(context);
            }
        }

        public void UnregisterPiece(Piece piece)
        {
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
                                  }

        private void Update()
        {
            if (Player.m_localPlayer == null) return;

            _updateTimer += Time.deltaTime;
            if (_updateTimer < UPDATE_INTERVAL) return;
            _updateTimer = 0f;

            Vector3 playerPos = Player.m_localPlayer.transform.position;
            float sleepDistSqr = SleepDistance * SleepDistance;

                                                
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
            
                        if (ctx.WearNTear != null)
            {
                ctx.WearNTear.enabled = !sleep; 
            }

                                                            
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
