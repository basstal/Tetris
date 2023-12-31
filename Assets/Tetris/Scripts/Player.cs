﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.Mathematics;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.VFX;
using Whiterice;
using Random = UnityEngine.Random;

namespace Tetris.Scripts
{
    public class Player : NetworkBehaviour
    {
        private float2[] _shapeData; // 当前形状的数据


        [HideInInspector] public InputControl inputControl;
        public bool gameEnd;
        [NonSerialized] public TetrisShape CurrentFallingShape;
        [NonSerialized] public TetrisShape PredictionShape;

        [HideInInspector] public bool waitDeleteLine;
        public static TetrisShape[] AllShapes => FindObjectsByType<TetrisShape>(FindObjectsSortMode.None).Where(shape => !shape.isPredictor).ToArray();

        [NonSerialized] public readonly NetworkVariable<float> WaitForFinalModify = new NetworkVariable<float>();
        [NonSerialized] public readonly NetworkVariable<int> ShapeIndex = new NetworkVariable<int>(-1);
        [NonSerialized] public NetworkList<int> InputCommands;
        [NonSerialized] public NetworkVariable<int> NextShapeIndex = new NetworkVariable<int>(-1);

        public int haveDeleteLineCount;

        private void Awake()
        {
            InputCommands = new NetworkList<int>();
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            name = $"OwnerClientId:{OwnerClientId}";
            // Instance = this;
            ShapeIndex.OnValueChanged += DropATetrisShape;
            WaitForFinalModify.OnValueChanged += WaitForFinalModifyOnValueChanged;
            inputControl = GameObject.Find("Canvas").GetComponentInChildren<InputControl>();
            inputControl.Init(this);
            InputCommands.OnListChanged += inputControl.Execute;
            AssetManager.Instance.InstantiatePrefab("Ground", null, new float3(0, Settings.Instance.groundY, 0));
            ResetGame();
        }


        private void Update()
        {
            if (NetworkManager.Singleton.IsServer)
            {
                // 如果是服务器，则直接使用本地的时间做 tick，将 tick 的结果通过状态同步给客户端
                LogicUpdateClientRpc(Time.deltaTime);
            }
        }

        [ClientRpc]
        public void LogicUpdateClientRpc(float logicDeltaTime)
        {
            if (IsServer && WaitForFinalModify.Value > 0)
            {
                WaitForFinalModify.Value -= logicDeltaTime;
            }

            if (gameEnd || waitDeleteLine)
            {
                return;
            }

            if (CurrentFallingShape != null)
            {
                CurrentFallingShape.LogicUpdate(logicDeltaTime);
            }
            else
            {
                if (ShapeIndex.Value < 0 && IsServer)
                {
                    ShapeIndex.Value = Random.Range(0, Settings.Instance.shapes.Length);
                }
            }
        }

        public void WaitForFinalModifyOnValueChanged(float previous, float current)
        {
            if (previous >= 0 && current < 0)
            {
                if (CurrentFallingShape != null && !CurrentFallingShape.CanMove(-Settings.Instance.Up))
                {
                    CurrentFallingShape.IsStopped = true;
                }
            }
        }

        static readonly List<int> DeletedRowPositions = new List<int>();
        static readonly Dictionary<Color, int> Colors = new Dictionary<Color, int>();

        public void CheckDeleteLine()
        {
            // 获得场景中所有的 TetrisCollider 组件
            var allTetrisColliders = AllShapes.SelectMany(shape => shape.blocks).ToArray();
            // 将它们按照 y 坐标分组
            var groupedColliders = allTetrisColliders.GroupBy(colliderComponent => (int)math.round(colliderComponent.transform.position.y * 4));
            // 将 groupedColliders 按 y 从小到大排列
            groupedColliders = groupedColliders.OrderBy(group => group.Key);
            DeletedRowPositions.Clear();
            Colors.Clear();
            var width = Settings.Instance.width;
            var deleteLineCount = width;
            foreach (var group in groupedColliders)
            {
                // 如果有一组的数量等于 CellWidth 则将这一组 TetrisCollider 移除
                if (group.Count() == deleteLineCount)
                {
                    DeletedRowPositions.Add(group.Key);
                    waitDeleteLine = true;
                    foreach (var tetrisCollider in group)
                    {
                        tetrisCollider.toBeDelete = true;
                        var meshRenderer = tetrisCollider.GetComponent<MeshRenderer>();
                        var color = meshRenderer.material.color;
                        if (Colors.ContainsKey(color))
                        {
                            Colors[color]++;
                        }
                        else
                        {
                            Colors[color] = 1;
                        }

                        meshRenderer.material = Settings.Instance.deleteLineMaterial;
                    }
                }
            }

            if (waitDeleteLine)
            {
                Color toColor = Colors.OrderByDescending(pair => pair.Value).First().Key;
                // 将AudioClip分配给AudioSource并播放
                var deleteLineSource = GetComponent<AudioSource>();
                if (deleteLineSource == null)
                {
                    deleteLineSource = gameObject.AddComponent<AudioSource>();
                }

                deleteLineSource.clip = Settings.Instance.deleteLine;
                deleteLineSource.Play();
                StartCoroutine(DeleteLine(allTetrisColliders, toColor));
            }
        }

        List<GameObject> _deleteLineEffects = new List<GameObject>();

        public IEnumerator DeleteLine(TetrisBlock[] allTetrisColliders, Color toColor)
        {
            GameObject deleteLineEffectTemplate = AssetManager.Instance.LoadAsset<GameObject>("DeleteLine", this);
            var visualEffect = deleteLineEffectTemplate.GetComponent<VisualEffect>();
            var duration = visualEffect.GetFloat("Duration");
            var gradient = visualEffect.GetGradient("ColorGradient");
            var gradientColorKeys = gradient.colorKeys;
            GradientColorKey[] keys = new GradientColorKey[]
            {
                new GradientColorKey(gradientColorKeys[0].color, gradientColorKeys[0].time),
                new GradientColorKey(gradientColorKeys[1].color, gradientColorKeys[1].time),
                new GradientColorKey(toColor, gradientColorKeys[2].time),
            };
            gradient.SetKeys(keys, gradient.alphaKeys);
            Assert.IsTrue(gradient.colorKeys[2].color == toColor);
            visualEffect.SetGradient("ColorGradient", gradient);
            foreach (var deleteLineEffect in _deleteLineEffects)
            {
                DestroyImmediate(deleteLineEffect);
            }

            _deleteLineEffects.Clear();
            Settings.Instance.deleteLineMaterial.SetFloat(Duration, duration);
            Settings.Instance.deleteLineMaterial.SetColor(ToColor, toColor);
            // Debug.LogWarning($"DeleteLine effect duration : {duration}");
            foreach (var line in DeletedRowPositions)
            {
                var pos = new float3(0, line, -1);
                var instanceDeleteLineEffect = Instantiate(deleteLineEffectTemplate);
                instanceDeleteLineEffect.transform.position = pos;
                _deleteLineEffects.Add(instanceDeleteLineEffect);
                ++haveDeleteLineCount;
            }

            float animationTime = 0;
            while (animationTime <= duration)
            {
                animationTime += Time.deltaTime;
                Settings.Instance.deleteLineMaterial.SetFloat(AnimationTime, animationTime);
                yield return null;
            }


            OnDeleteAnimationComplete(allTetrisColliders);


            waitDeleteLine = false;
        }

        public void OnDeleteAnimationComplete(TetrisBlock[] allTetrisColliders)
        {
            foreach (var tetrisCollider in allTetrisColliders)
            {
                if (tetrisCollider.toBeDelete)
                {
                    tetrisCollider.belongsTo.blocks.Remove(tetrisCollider);
                    DestroyImmediate(tetrisCollider.gameObject);
                }
            }

            // 对所有行进行统计，统计在删除一行以后，原来的行需要向下位移的数量，统计位移数量要按照 groupedColliders 中 group.Key 的值，如果当前行位置大于 group.Key 值则下移一个单位
            if (DeletedRowPositions.Count > 0)
            {
                foreach (var tetrisCollider in allTetrisColliders)
                {
                    if (tetrisCollider == null || !tetrisCollider.CompareTag("StoppedTetrisShape"))
                    {
                        continue;
                    }

                    float3 position = tetrisCollider.transform.position;
                    var rowY = (int)math.round(position.y);
                    var count = DeletedRowPositions.Count(deletedRowY => deletedRowY < rowY);
                    if (count > 0)
                    {
                        position += -Settings.Instance.Up * count;
                        tetrisCollider.transform.position = position;
                    }
                }
            }

            // 删除那些 colliders 已经为空的 TetrisShape
            foreach (var tetrisShape in AllShapes)
            {
                if (tetrisShape.blocks.Count == 0)
                {
                    DestroyImmediate(tetrisShape.gameObject);
                }
            }

            inputControl.deleteLineCount.text = $"{math.min(999, haveDeleteLineCount)}";
        }

        public void DropATetrisShape(int previousShapeIndex, int currentShapeIndex)
        {
            // Debug.LogWarning($"DropATetrisShape : {previousShapeIndex} -> {currentShapeIndex}");
            if (currentShapeIndex < 0)
            {
                return;
            }

            if (CurrentFallingShape != null)
            {
                DelayDropShapeServerRpc();
                return;
            }

            Assert.IsTrue(currentShapeIndex < Settings.Instance.shapes.Length);
            CurrentFallingShape = CreateShape(currentShapeIndex);
            // Debug.LogWarning($"DropATetrisShape : {tetrisShape}", tetrisShape);
            PredictionShape = CreateShape(currentShapeIndex, true);
            Physics.SyncTransforms();
            PredictionShape.UpdatePredictor(CurrentFallingShape);
            if (IsOwner)
            {
                DoneDropATetrisShapeServerRpc();
            }
        }

        [ServerRpc]
        public void DelayDropShapeServerRpc(ServerRpcParams rpcParams = default)
        {
            ShapeIndex.Value = ShapeIndex.Value;
            ShapeIndex.SetDirty(true);
        }

        [ServerRpc]
        public void DoneDropATetrisShapeServerRpc(ServerRpcParams rpcParams = default)
        {
            ShapeIndex.Value = -1;
        }

        public TetrisShape CreateShape(int inShapeIndex, bool isPredictor = false)
        {
            var shapeSetting = Settings.Instance.shapes[inShapeIndex];
            var tetrisShape = new GameObject($"TetrisShape_{shapeSetting.baseName}");
            tetrisShape.name = $"{tetrisShape.name}_{tetrisShape.GetInstanceID()}";
            var tetrisShapeComponent = tetrisShape.AddComponent<TetrisShape>();
            tetrisShapeComponent.Init(this);
            _shapeData = shapeSetting.blockPosition;
            var component = shapeSetting.basic;
            foreach (float2 pos in _shapeData)
            {
                GameObject block = Instantiate(component, tetrisShape.transform);
                block.transform.localScale = new float3(Settings.Instance.blockScale);
                block.transform.localPosition = new float3(pos, Settings.Instance.unitSize);
                block.transform.SetParent(tetrisShape.transform, true);
                var tetrisBlock = block.AddComponent<TetrisBlock>();
                tetrisBlock.Init();
                tetrisBlock.belongsTo = tetrisShapeComponent;
                tetrisShapeComponent.blocks.Add(tetrisBlock);
            }

            tetrisShape.transform.localScale = new float3(Settings.Instance.unitSize);
            // 设置 tetrisShape 的世界坐标位置在 CellWidth 中心和 CellHeight 的高度位置
            var startPositionOffset = shapeSetting.startPositionOffset * Settings.Instance.blockScale;
            tetrisShape.transform.position = new float3(startPositionOffset.x, Settings.Instance.height * Settings.Instance.unitSize + startPositionOffset.y, isPredictor ? 1 : 0);
            tetrisShapeComponent.bornPos = tetrisShape.transform.position;
            tetrisShapeComponent.RotateThreshold = shapeSetting.rotateThreshold;

            if (isPredictor)
            {
                tetrisShape.name = $"{tetrisShape.name}_Predictor";
                tetrisShapeComponent.isPredictor = true;
                var defaultColor = tetrisShapeComponent.blocks[0].GetComponent<MeshRenderer>().material.GetColor(Color1);
                Settings.Instance.predictorMaterial.SetColor(Color1, defaultColor);
                Settings.Instance.predictorMaterial.SetFloat(Alpha, 0.4f);
                foreach (var colliderComponent in tetrisShapeComponent.blocks)
                {
                    var meshRenderer = colliderComponent.GetComponent<MeshRenderer>();
                    meshRenderer.material = Settings.Instance.predictorMaterial;
                }
            }

            return tetrisShapeComponent;
        }

        public void ResetGame()
        {
            // Debug.LogWarning("Reset");
            haveDeleteLineCount = 0;
            inputControl.deleteLineCount.text = $"{math.min(999, haveDeleteLineCount)}";
            isPausing = false;
            gameEnd = false;
            if (PredictionShape != null)
            {
                DestroyImmediate(PredictionShape.gameObject);
            }

            PredictionShape = null;
            foreach (var tetrisShape in AllShapes)
            {
                DestroyImmediate(tetrisShape.gameObject);
            }

            CurrentFallingShape = null;
            Physics.SyncTransforms();
        }

        public bool isPausing;
        private static readonly int Color1 = Shader.PropertyToID("_Color");
        private static readonly int Alpha = Shader.PropertyToID("_Alpha");
        private static readonly int AnimationTime = Shader.PropertyToID("_AnimationTime");
        private static readonly int Duration = Shader.PropertyToID("_Duration");
        private static readonly int ToColor = Shader.PropertyToID("_ToColor");


        public void Pause()
        {
            isPausing = !isPausing;
            Time.timeScale = isPausing ? 0 : 1;
        }
    }
}