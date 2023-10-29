using System;
using UnityEngine;
using UnityEngine.Serialization;

public class TetrisCollider : MonoBehaviour
{
    [HideInInspector] public TetrisShape belongsTo;
    [HideInInspector] public bool toBeDelete;
#if DEBUG
    public Vector3 cachedPos;
#endif
    public void Start()
    {
        var colliderComponent = gameObject.AddComponent<BoxCollider>();
        colliderComponent.size = Vector3.one * 2;
#if DEBUG

        cachedPos = transform.localPosition;
#endif
    }

    private void Update()
    {
        if (belongsTo.isStopped || belongsTo.isPredictor)
            return; // 如果已经停止，则直接返回
#if DEBUG

        if (cachedPos != transform.localPosition)
        {
            Debug.LogError($"???? should not run here");
        }
#endif
        if (CheckContactGroundOrOtherShape(Vector3.down) && Gameplay.WaitForFinalModify <= 0)
        {
            Gameplay.WaitForFinalModify = 1;
        }
    }

    public bool CheckContactGroundOrOtherShape(Vector3 direction)
    {
        float distanceToCheck = 1.1f; //稍微大于1，以确保它检测到即将到来的碰撞
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
        foreach (var tetrisCollider in target.colliders)
        {
            if (Vector3.Distance(transform.position, tetrisCollider.transform.position) < 0.1f)
            {
                return true;
            }
        }

        return false;
    }
}