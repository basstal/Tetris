using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Mathematics;
using UnityEngine;

public class TetrisShape : MonoBehaviour
{
    /// <summary>
    /// 决定下落时间间隔
    /// </summary>
    public const float Intervals = 1;

    public float intervalCounter = 0;
    public bool _isStopped;
    public Vector3 bornPos;
    public List<TetrisCollider> colliders = new List<TetrisCollider>();
    [NonSerialized] public int RotateThreshold;

    public bool isStopped
    {
        get => _isStopped;
        set
        {
            // Debug.LogWarning($"Stopped name :{name}");
            bool beStopped = value != _isStopped;
            _isStopped = value;
            if (beStopped)
            {
                if (Gameplay.currentFallingShape == this)
                {
                    if (math.distance(Gameplay.currentFallingShape.bornPos, Gameplay.currentFallingShape.transform.position) < 0.1f)
                    {
                        // Debug.LogWarning($" Game end at : {Gameplay.currentFallingShape.name}");
                        Gameplay.GameEnd = true;
                    }

                    Gameplay.currentFallingShape = null;
                }

                // 将它的所有 BoxCollider 组件的 gameObject 的 tag 设置为 "StoppedTetrisShape"
                foreach (var boxCollider in GetComponentsInChildren<BoxCollider>())
                {
                    boxCollider.gameObject.tag = "StoppedTetrisShape";
                }

                Gameplay.CheckDeleteLine();
            }
        }
    }

    private void Update()
    {
        if (isStopped)
        {
            return;
        }

        intervalCounter += Time.deltaTime;
        if (intervalCounter >= Intervals)
        {
            intervalCounter = 0;
            DownOneCell();
        }
    }


    public void DownOneCell()
    {
        if (!CanMove(Vector3.down))
        {
            return;
        }

        transform.position += Vector3.down;
    }

    public Bounds MaxBounds
    {
        get
        {
            BoxCollider[] colliderComponents = GetComponentsInChildren<BoxCollider>();

            // 如果没有子图形，直接返回默认值
            if (colliderComponents.Length == 0)
            {
                return new Bounds(transform.position, Vector3.zero);
            }

            // 初始化为第一个子图形的包围盒
            Bounds bounds = colliderComponents[0].bounds;

            // 扩展包围盒以包含其他子图形的包围盒
            for (int i = 1; i < colliderComponents.Length; i++)
            {
                bounds.Encapsulate(colliderComponents[i].bounds);
            }

            return bounds;
        }
    }

    public bool CanMove(Vector3 direction)
    {
        foreach (var colliderCell in colliders)
        {
            if (colliderCell.CheckContactGroundOrOtherShape(direction))
            {
                return false;
            }
        }

        return true;
    }

    public void Rotate()
    {
        var trans = transform;
        var defaultRotation = trans.rotation;
        var changeToRotation = new Quaternion();
        changeToRotation.eulerAngles = new Vector3(0, 0, (defaultRotation.eulerAngles.z + 90) % RotateThreshold);
        trans.rotation = changeToRotation;
        if (ColliderWithAny())
        {
            trans.rotation = defaultRotation;
            return;
        }

        CheckAndPushAfterRotation(trans);
    }

    bool ColliderWithAny()
    {
        foreach (var tetrisShape in Gameplay.AllShapes)
        {
            if (tetrisShape == this)
            {
                continue;
            }

            // 检查当前 TetrisShape 的所有子物体是否与其他 TetrisShape 的子物体发生碰撞
            foreach (var colliderCell in colliders)
            {
                if (colliderCell.CheckOverlapping(tetrisShape))
                {
                    return true;
                }
            }
        }


        return false;
    }

    // 旋转后，检查是否超出边界并进行必要的推挤
    void CheckAndPushAfterRotation(Transform trans)
    {
        List<BoxCollider> childBlocks = new List<BoxCollider>();

        // 1. 获取所有子物体
        foreach (BoxCollider child in Gameplay.currentFallingShape.transform.GetComponentsInChildren<BoxCollider>())
        {
            childBlocks.Add(child);
        }

        HashSet<float> uniqueOutOfBoundX = new HashSet<float>();

        // 2. 对于旋转后的子物体，找出那些其 X 坐标超出左/右界限的
        foreach (BoxCollider block in childBlocks)
        {
            if (block.transform.position.x <= Gameplay.LeftLimit)
            {
                uniqueOutOfBoundX.Add(block.transform.position.x);
            }
            else if (block.transform.position.x >= Gameplay.RightLimit)
            {
                uniqueOutOfBoundX.Add(block.transform.position.x);
            }
        }

        // Debug.LogWarning($"uniqueOutOfBoundX : {uniqueOutOfBoundX.Count}");
        // 3. 根据找到的唯一的 X 坐标数量来推挤方块
        if (uniqueOutOfBoundX.Count > 0)
        {
            if (uniqueOutOfBoundX.Min() <= Gameplay.LeftLimit)
            {
                // Debug.LogWarning($"Push right for {finalOutOfBoundX.Count} units due to min.x : {finalOutOfBoundX.Min()}");
                trans.position += Vector3.right * uniqueOutOfBoundX.Count;
            }
            else if (uniqueOutOfBoundX.Max() >= Gameplay.RightLimit)
            {
                // Debug.LogWarning($"Push left for {finalOutOfBoundX.Count} units due to max.x : {finalOutOfBoundX.Max()}");
                trans.position += Vector3.left * uniqueOutOfBoundX.Count;
            }
        }
    }
}