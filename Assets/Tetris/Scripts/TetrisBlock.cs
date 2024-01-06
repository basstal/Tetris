using Unity.Mathematics;
using Unity.Netcode;
using UnityEngine;

namespace Tetris.Scripts
{
    public class TetrisBlock : MonoBehaviour
    {
        [HideInInspector] public TetrisShape belongsTo;
        [HideInInspector] public bool toBeDelete;
#if DEBUG
        public float3 cachedPos;
#endif
        public void Init()
        {
            var colliderComponent = gameObject.AddComponent<BoxCollider>();
            colliderComponent.size = new float3(1) * 2;
#if DEBUG

            cachedPos = transform.localPosition;
#endif
        }

        public void LogicUpdate(float logicDeltaTime)
        {
            if (belongsTo.IsStopped || belongsTo.isPredictor)
                return; // 如果已经停止，则直接返回
#if DEBUG

            if ((Vector3)cachedPos != transform.localPosition)
            {
                Debug.LogError($"???? should not run here cachedPos {cachedPos} != transform.localPosition {transform.localPosition}");
            }
#endif
            if (NetworkManager.Singleton.IsServer && CheckContactGroundOrOtherShape(Vector3.down) && belongsTo.player.WaitForFinalModify.Value <= 0)
            {
                belongsTo.player.WaitForFinalModify.Value = 1;
            }
        }

        public bool CheckContactGroundOrOtherShape(Vector3 direction)
        {
            float distanceToCheck = 0.1f + Settings.Instance.unitSize; //稍微大于 UnitSize，以确保它检测到即将到来的碰撞
            RaycastHit[] allHits = Physics.RaycastAll(transform.position, direction, distanceToCheck);

            if (allHits.Length > 0)
            {
                foreach (var hit in allHits)
                {
                    if (hit.collider.CompareTag("Ground") || hit.collider.CompareTag("StoppedTetrisShape"))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        public bool CheckOverlapping(TetrisShape target)
        {
            // 检查当前位置是否临近 target.colliders 中任意节点的位置
            foreach (var tetrisCollider in target.blocks)
            {
                if (Vector3.Distance(transform.position, tetrisCollider.transform.position) < 0.1f)
                {
                    return true;
                }
            }

            return false;
        }
    }
}