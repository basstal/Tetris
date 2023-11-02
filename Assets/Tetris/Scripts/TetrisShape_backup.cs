// using System;
// using System.Collections.Generic;
// using System.Linq;
// using Unity.Mathematics;
// using Unity.Netcode;
// using UnityEngine;
// using Whiterice;
//
// public class TetrisShape : NetworkBehaviour
// {
//     /// <summary>
//     /// 决定下落时间间隔
//     /// </summary>
//     public const float Intervals = 1;
//
//     [NonSerialized] public float intervalCounter;
//     private bool _isStopped;
//     [NonSerialized] public Vector3 bornPos;
//     [NonSerialized] public List<TetrisBlock> blocks = new List<TetrisBlock>();
//     [NonSerialized] public int rotateThreshold;
//     [NonSerialized] public bool isPredictor;
//
//     public bool isStopped
//     {
//         get => _isStopped;
//         set
//         {
//             // Debug.LogWarning($"Stopped name :{name}");
//             bool beStopped = value != _isStopped;
//             _isStopped = value;
//             if (beStopped)
//             {
//                 Player.Instance.predictionShape.NetworkDestroy();
//
//                 if (Player.Instance.currentFallingShape == this)
//                 {
//                     if (math.distance(Player.Instance.currentFallingShape.bornPos, Player.Instance.currentFallingShape.transform.position) < 0.1f)
//                     {
//                         // Debug.LogWarning($" Game end at : {Gameplay.currentFallingShape.name}");
//                         Player.GameEnd = true;
//                     }
//
//                     Player.Instance.currentFallingShape = null;
//                 }
//
//                 // 将它的所有 BoxCollider 组件的 gameObject 的 tag 设置为 "StoppedTetrisShape"
//                 foreach (var boxCollider in GetComponentsInChildren<BoxCollider>())
//                 {
//                     boxCollider.gameObject.tag = "StoppedTetrisShape";
//                 }
//
//                 Player.Instance.CheckDeleteLine();
//             }
//         }
//     }
//
//
//     public void NetworkDestroy()
//     {
//         foreach (var networkObject in GetComponentsInChildren<NetworkObject>())
//         {
//             if (networkObject.gameObject != gameObject)
//             {
//                 networkObject.Despawn();
//             }
//         }
//
//         GetComponent<NetworkObject>().Despawn();
//     }
//
//     [ClientRpc]
//     public void InitClientRpc(int inShapeIndex, bool inIsPredictor)
//     {
//         var shapeSetting = Settings.Instance.shapes[inShapeIndex];
//         if (NetworkManager.Singleton.IsServer)
//         {
//             foreach (Vector2 pos in shapeSetting.blockPosition)
//             {
//                 var tetrisBlockGameObject = AssetManager.Instance.InstantiatePrefab("TetrisBlock", this);
//                 var tetrisBlockNetworkObject = tetrisBlockGameObject.GetComponent<NetworkObject>();
//                 tetrisBlockNetworkObject.Spawn();
//                 tetrisBlockNetworkObject.TrySetParent(transform);
//                 var tetrisBlock = tetrisBlockGameObject.GetComponent<TetrisBlock>();
//                 tetrisBlock.InitClientRpc(inShapeIndex, pos);
//             }
//         }
//
//         // 设置 tetrisShape 的世界坐标位置在 CellWidth 中心和 CellHeight 的高度位置
//         var startPositionOffset = shapeSetting.startPositionOffset;
//         transform.position = new Vector3(startPositionOffset.x, Player.CellHeight + startPositionOffset.y, isPredictor ? 1 : 0);
//         bornPos = transform.position;
//         rotateThreshold = shapeSetting.rotateThreshold;
//
//         if (inIsPredictor)
//         {
//             name = $"{name}_Predictor";
//             isPredictor = true;
//             var defaultColor = blocks[0].transform.GetChild(0).GetComponent<MeshRenderer>().material.GetColor(Color);
//             Settings.Instance.predictorMaterial.SetColor(Color, defaultColor);
//             Settings.Instance.predictorMaterial.SetFloat(Alpha, 0.4f);
//             foreach (var colliderComponent in blocks)
//             {
//                 var meshRenderer = colliderComponent.transform.GetChild(0).GetComponent<MeshRenderer>();
//                 meshRenderer.material = Settings.Instance.predictorMaterial;
//             }
//
//             Player.Instance.predictionShape = this;
//         }
//         else
//         {
//             Player.Instance.currentFallingShape = this;
//         }
//     }
//
//     private static readonly int Color = Shader.PropertyToID("_Color");
//     private static readonly int Alpha = Shader.PropertyToID("_Alpha");
//
//
//     public void LogicUpdate(float logicDeltaTime)
//     {
//         if (isStopped || isPredictor)
//         {
//             return;
//         }
//
//         intervalCounter += logicDeltaTime;
//         if (intervalCounter >= Intervals)
//         {
//             intervalCounter = 0;
//             DownOneCell();
//         }
//
//         foreach (var block in blocks)
//         {
//             block.LogicUpdate(logicDeltaTime);
//         }
//     }
//
//     public void UpdatePredictor(TetrisShape other)
//     {
//         var transform1 = other.transform;
//         // Debug.LogWarning($"update transform : {transform.name}");
//         var transform2 = transform;
//         transform2.position = transform1.position;
//         transform2.rotation = transform1.rotation;
//         // Physics.SyncTransforms();
//         var minDistance = InputControl.FindFastDownDistance(this);
//         if (minDistance > 0)
//         {
//             var position = transform2.position;
//             position += Vector3.down * (minDistance - 0.5f); // 0.5f为Box的半高，确保方块底部与目标表面对齐
//             position.z = 1;
//             transform2.position = position;
//         }
//     }
//
//     public void DownOneCell()
//     {
//         if (!CanMove(Vector3.down))
//         {
//             return;
//         }
//
//         transform.position += Vector3.down;
//     }
//
//     public Bounds MaxBounds
//     {
//         get
//         {
//             BoxCollider[] colliderComponents = GetComponentsInChildren<BoxCollider>();
//
//             // 如果没有子图形，直接返回默认值
//             if (colliderComponents.Length == 0)
//             {
//                 return new Bounds(transform.position, Vector3.zero);
//             }
//
//             // 初始化为第一个子图形的包围盒
//             Bounds bounds = colliderComponents[0].bounds;
//
//             // 扩展包围盒以包含其他子图形的包围盒
//             for (int i = 1; i < colliderComponents.Length; i++)
//             {
//                 bounds.Encapsulate(colliderComponents[i].bounds);
//             }
//
//             return bounds;
//         }
//     }
//
//     public bool CanMove(Vector3 direction)
//     {
//         foreach (var colliderCell in blocks)
//         {
//             if (colliderCell.CheckContactGroundOrOtherShape(direction))
//             {
//                 return false;
//             }
//         }
//
//         return true;
//     }
//
//     public bool Rotate(bool force = false)
//     {
//         var trans = transform;
//         var defaultRotation = trans.rotation;
//         var changeToRotation = new Quaternion();
//         changeToRotation.eulerAngles = new Vector3(0, 0, (defaultRotation.eulerAngles.z + 90) % rotateThreshold);
//         trans.rotation = changeToRotation;
//
//         if (!force && ColliderWithAny())
//         {
//             trans.rotation = defaultRotation;
//             return false;
//         }
//
//         CheckAndPushAfterRotation(trans);
//         return true;
//     }
//
//     bool ColliderWithAny()
//     {
//         foreach (var tetrisShape in Player.AllShapes)
//         {
//             if (tetrisShape == this)
//             {
//                 continue;
//             }
//
//             // 检查当前 TetrisShape 的所有子物体是否与其他 TetrisShape 的子物体发生碰撞
//             foreach (var colliderCell in blocks)
//             {
//                 if (colliderCell.CheckOverlapping(tetrisShape))
//                 {
//                     return true;
//                 }
//             }
//         }
//
//
//         return false;
//     }
//
//     // 旋转后，检查是否超出边界并进行必要的推挤
//     void CheckAndPushAfterRotation(Transform trans)
//     {
//         List<BoxCollider> childBlocks = new List<BoxCollider>();
//
//         // 1. 获取所有子物体
//         foreach (BoxCollider child in trans.GetComponentsInChildren<BoxCollider>())
//         {
//             childBlocks.Add(child);
//         }
//
//         HashSet<float> uniqueOutOfBoundX = new HashSet<float>();
//
//         // 2. 对于旋转后的子物体，找出那些其 X 坐标超出左/右界限的
//         foreach (BoxCollider block in childBlocks)
//         {
//             if (block.transform.position.x <= Player.LeftLimit)
//             {
//                 uniqueOutOfBoundX.Add(block.transform.position.x);
//             }
//             else if (block.transform.position.x >= Player.RightLimit)
//             {
//                 uniqueOutOfBoundX.Add(block.transform.position.x);
//             }
//         }
//
//         // Debug.LogWarning($"uniqueOutOfBoundX : {uniqueOutOfBoundX.Count}");
//         // 3. 根据找到的唯一的 X 坐标数量来推挤方块
//         if (uniqueOutOfBoundX.Count > 0)
//         {
//             if (uniqueOutOfBoundX.Min() <= Player.LeftLimit)
//             {
//                 // Debug.LogWarning($"Push right for {finalOutOfBoundX.Count} units due to min.x : {finalOutOfBoundX.Min()}");
//                 trans.position += Vector3.right * uniqueOutOfBoundX.Count;
//             }
//             else if (uniqueOutOfBoundX.Max() >= Player.RightLimit)
//             {
//                 // Debug.LogWarning($"Push left for {finalOutOfBoundX.Count} units due to max.x : {finalOutOfBoundX.Max()}");
//                 trans.position += Vector3.left * uniqueOutOfBoundX.Count;
//             }
//         }
//     }
//     //
//     // public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
//     // {
//     //     serializer.SerializeValue(ref _isStopped);
//     //     serializer.SerializeValue(ref intervalCounter);
//     //     serializer.SerializeValue(ref bornPos);
//     //     serializer.SerializeValue(ref rotateThreshold);
//     //     serializer.SerializeValue(ref isPredictor);
//     // }
// }