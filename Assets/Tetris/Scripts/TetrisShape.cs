using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Mathematics;
using UnityEngine;

namespace Tetris.Scripts
{
    public class TetrisShape : MonoBehaviour
    {
        /// <summary>
        /// 决定下落时间间隔
        /// </summary>
        public const float Intervals = 1;

        public float intervalCounter;
        private bool isStopped;
        public float3 bornPos;
        public List<TetrisBlock> blocks = new List<TetrisBlock>();
        [NonSerialized] public int RotateThreshold;
        public bool isPredictor;
        public bool isNextPreview;
        public Player player;

        public bool IsStopped
        {
            get => isStopped;
            set
            {
                // Debug.LogWarning($"Stopped name :{name}");
                bool beStopped = value != isStopped;
                isStopped = value;
                // Debug.LogWarning($"beStopped : {beStopped}");
                if (beStopped)
                {
                    DestroyImmediate(player.PredictionShape.gameObject);
                    player.PredictionShape = null;

                    if (player.CurrentFallingShape == this)
                    {
                        if (math.distance(player.CurrentFallingShape.bornPos, player.CurrentFallingShape.transform.position) < 0.1f)
                        {
                            // Debug.LogWarning($" Game end at : {player.currentFallingShape.name}");
                            player.gameEnd = true;
                        }

                        player.CurrentFallingShape = null;
                    }

                    Debug.LogWarning($"change tag with object {name}");
                    // 将它的所有 BoxCollider 组件的 gameObject 的 tag 设置为 "StoppedTetrisShape"
                    foreach (var boxCollider in GetComponentsInChildren<BoxCollider>())
                    {
                        boxCollider.gameObject.tag = "StoppedTetrisShape";
                    }

                    player.CheckDeleteLine();
                }
            }
        }

        public void Init(Player inPlayer)
        {
            player = inPlayer;
        }

        public void LogicUpdate(float logicDeltaTime)
        {
            if (IsStopped || isPredictor)
            {
                return;
            }

            intervalCounter += logicDeltaTime;
            if (intervalCounter >= Intervals)
            {
                intervalCounter = 0;
                DownOneCell();
            }

            foreach (var block in blocks)
            {
                block.LogicUpdate(logicDeltaTime);
            }
        }


        public void UpdatePredictor(TetrisShape other)
        {
            var transform1 = other.transform;
            // Debug.LogWarning($"update transform : {transform.name}");
            var transform2 = transform;
            transform2.position = transform1.position;
            transform2.rotation = transform1.rotation;
            // Physics.SyncTransforms();
            var minDistance = InputControl.FindFastDownDistance(this);
            if (minDistance > 0)
            {
                var position = transform2.position;
                position += (Vector3)(new float3(0, -1, 0) * (minDistance - Settings.HalfUnitSize)); // 0.5f为Box的半高，确保方块底部与目标表面对齐
                position.z = 1;
                transform2.position = position;
            }
        }

        public void DownOneCell()
        {
            if (!CanMove(-Settings.Up))
            {
                return;
            }

            transform.position += (Vector3)(-Settings.Up);
        }

        public Bounds MaxBounds
        {
            get
            {
                BoxCollider[] colliderComponents = GetComponentsInChildren<BoxCollider>();

                // 如果没有子图形，直接返回默认值
                if (colliderComponents.Length == 0)
                {
                    return new Bounds(transform.position, float3.zero);
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

        public bool CanMove(float3 direction)
        {
            foreach (var colliderCell in blocks)
            {
                if (colliderCell.CheckContactGroundOrOtherShape(direction))
                {
                    return false;
                }
            }

            return true;
        }

        public bool Rotate(bool force = false)
        {
            var trans = transform;
            var defaultRotation = trans.rotation;
            var changeToRotation = quaternion.Euler(0, 0, math.radians((defaultRotation.eulerAngles.z + 90) % RotateThreshold));
            trans.rotation = changeToRotation;

            if (!force && ColliderWithAny())
            {
                trans.rotation = defaultRotation;
                return false;
            }

            CheckAndPushAfterRotation(trans);
            return true;
        }

        bool ColliderWithAny()
        {
            foreach (var tetrisShape in Player.AllShapes)
            {
                if (tetrisShape == this)
                {
                    continue;
                }

                // 检查当前 TetrisShape 的所有子物体是否与其他 TetrisShape 的子物体发生碰撞
                foreach (var colliderCell in blocks)
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
            foreach (BoxCollider child in trans.GetComponentsInChildren<BoxCollider>())
            {
                childBlocks.Add(child);
            }

            HashSet<float> uniqueOutOfBoundX = new HashSet<float>();

            // 2. 对于旋转后的子物体，找出那些其 X 坐标超出左/右界限的
            foreach (BoxCollider block in childBlocks)
            {
                if (block.transform.position.x <= Settings.LeftLimit)
                {
                    uniqueOutOfBoundX.Add(block.transform.position.x);
                }
                else if (block.transform.position.x >= Settings.RightLimit)
                {
                    uniqueOutOfBoundX.Add(block.transform.position.x);
                }
            }

            // Debug.LogWarning($"uniqueOutOfBoundX : {uniqueOutOfBoundX.Count}");
            // 3. 根据找到的唯一的 X 坐标数量来推挤方块
            if (uniqueOutOfBoundX.Count > 0)
            {
                if (uniqueOutOfBoundX.Min() <= Settings.LeftLimit)
                {
                    // Debug.LogWarning($"Push right for {finalOutOfBoundX.Count} units due to min.x : {finalOutOfBoundX.Min()}");
                    trans.position += (Vector3)(Settings.Right * uniqueOutOfBoundX.Count);
                }
                else if (uniqueOutOfBoundX.Max() >= Settings.RightLimit)
                {
                    // Debug.LogWarning($"Push left for {finalOutOfBoundX.Count} units due to max.x : {finalOutOfBoundX.Max()}");
                    trans.position += (Vector3)(-Settings.Right * uniqueOutOfBoundX.Count);
                }
            }
        }
    }
}