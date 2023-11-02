using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.Mathematics;
using Unity.Netcode;
using UnityEditor.VersionControl;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.Serialization;
using UnityEngine.VFX;
using Whiterice;
using Random = UnityEngine.Random;

public class Player : NetworkBehaviour
{
    // public GameObject[] Basics; // 这应该是一个正方形的预制体，表示基础的方块单元
    // public Material predictorMaterial;
    // public Material deleteLineMaterial;
    private Vector2[] shapeData; // 当前形状的数据


    [HideInInspector] public InputControl InputControl;
    public bool GameEnd;
    [NonSerialized] public TetrisShape currentFallingShape;
    [NonSerialized] public TetrisShape predictionShape;

    // public static Player Instance;

    // public AudioClip deleteLine;
    [HideInInspector] public bool waitDeleteLine;
    public static TetrisShape[] AllShapes => FindObjectsOfType<TetrisShape>().Where(shape => !shape.isPredictor).ToArray();

    [NonSerialized] public NetworkVariable<float> waitForFinalModify = new NetworkVariable<float>();
    [NonSerialized] public NetworkVariable<int> shapeIndex = new NetworkVariable<int>(-1);
    [NonSerialized] public NetworkList<int> inputCommands = new NetworkList<int>(); // TODO: memory leak??

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        name = $"OwnerClientId:{OwnerClientId}";
        // Instance = this;
        shapeIndex.OnValueChanged += DropATetrisShape;
        waitForFinalModify.OnValueChanged += WaitForFinalModify;
        InputControl = GameObject.Find("Canvas").GetComponentInChildren<InputControl>();
        InputControl.Init(this);
        inputCommands.OnListChanged += InputControl.Execute;
    }


    private void Update()
    {
        if (NetworkManager.Singleton.IsServer)
        {
            // 如果是服务器，则直接使用本地的时间做 tick，将 tick 的结果通过状态同步给客户端
            if (OwnerClientId == 1) // 先等客户端连进来同步客户端 1 的数据
            {
                LogicUpdateClientRpc(Time.deltaTime);
            }
        }
    }

    [ClientRpc]
    public void LogicUpdateClientRpc(float logicDeltaTime)
    {
        if (IsServer && waitForFinalModify.Value > 0)
        {
            waitForFinalModify.Value -= logicDeltaTime;
        }

        if (GameEnd || waitDeleteLine)
        {
            return;
        }

        if (currentFallingShape != null)
        {
            currentFallingShape.LogicUpdate(logicDeltaTime);
        }
        else
        {
            if (shapeIndex.Value < 0 && IsServer)
            {
                shapeIndex.Value = Random.Range(0, Settings.Instance.shapes.Length);
            }
        }
    }

    public void WaitForFinalModify(float previous, float current)
    {
        if (previous >= 0 && current < 0)
        {
            if (currentFallingShape != null && !currentFallingShape.CanMove(Vector3.down))
            {
                currentFallingShape.isStopped = true;
            }
        }
    }

    static List<int> DeletedRowPositions = new List<int>();
    static Dictionary<Color, int> Colors = new Dictionary<Color, int>();

    public void CheckDeleteLine()
    {
        // 获得场景中所有的 TetrisCollider 组件
        var allTetrisColliders = AllShapes.SelectMany(shape => shape.blocks).ToArray();
        // 将它们按照 y 坐标分组
        var groupedColliders = allTetrisColliders.GroupBy(colliderComponent => (int)math.round(colliderComponent.transform.position.y));
        // 将 groupedColliders 按 y 从小到大排列
        groupedColliders = groupedColliders.OrderBy(group => group.Key);
        DeletedRowPositions.Clear();
        Colors.Clear();
        foreach (var group in groupedColliders)
        {
            // 如果有一组的数量等于 CellWidth 则将这一组 TetrisCollider 移除
            if (group.Count() == Settings.CellWidth)
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

    List<GameObject> deleteLineEffects = new List<GameObject>();

    public IEnumerator DeleteLine(TetrisBlock[] allTetrisColliders, Color toColor)
    {
        GameObject deleteLineEffectTemplate = AssetManager.Instance.LoadAsset<GameObject>("DeleteLine", this);
        var visualEffect = deleteLineEffectTemplate.GetComponent<VisualEffect>();
        var duration = visualEffect.GetFloat("Duration");
        var gradient = visualEffect.GetGradient("ColorGradient");
        var gradientColorKeys = gradient.colorKeys;
        GradientColorKey[] keys = new GradientColorKey[3]
        {
            new GradientColorKey(gradientColorKeys[0].color, gradientColorKeys[0].time),
            new GradientColorKey(gradientColorKeys[1].color, gradientColorKeys[1].time),
            new GradientColorKey(toColor, gradientColorKeys[2].time),
        };
        gradient.SetKeys(keys, gradient.alphaKeys);
        Assert.IsTrue(gradient.colorKeys[2].color == toColor);
        visualEffect.SetGradient("ColorGradient", gradient);
        foreach (var deleteLineEffect in deleteLineEffects)
        {
            DestroyImmediate(deleteLineEffect);
        }

        deleteLineEffects.Clear();
        Settings.Instance.deleteLineMaterial.SetFloat(Duration, duration);
        Settings.Instance.deleteLineMaterial.SetColor(ToColor, toColor);
        // Debug.LogWarning($"DeleteLine effect duration : {duration}");
        foreach (var line in DeletedRowPositions)
        {
            var pos = new Vector3(0, line, -1);
            var instanceDeleteLineEffect = Instantiate(deleteLineEffectTemplate);
            instanceDeleteLineEffect.transform.position = pos;
            deleteLineEffects.Add(instanceDeleteLineEffect);
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

                var position = tetrisCollider.transform.position;
                var rowY = (int)math.round(position.y);
                var count = DeletedRowPositions.Count(deletedRowY => deletedRowY < rowY);
                if (count > 0)
                {
                    position += Vector3.down * count;
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
    }

    public void DropATetrisShape(int previousShapeIndex, int currentShapeIndex)
    {
        Debug.LogWarning($"DropATetrisShape : {previousShapeIndex} -> {currentShapeIndex}");
        if (currentShapeIndex < 0)
        {
            return;
        }

        if (currentFallingShape != null)
        {
            DelayDropShapeServerRpc();
            return;
        }

        Assert.IsTrue(currentShapeIndex < Settings.Instance.shapes.Length);
        currentFallingShape = CreateShape(currentShapeIndex);
        // Debug.LogWarning($"DropATetrisShape : {tetrisShape}", tetrisShape);
        predictionShape = CreateShape(currentShapeIndex, true);
        Physics.SyncTransforms();
        predictionShape.UpdatePredictor(currentFallingShape);
        if (!IsServer)
        {
            DoneDropATetrisShapeServerRpc();
        }
    }

    [ServerRpc]
    public void DelayDropShapeServerRpc(ServerRpcParams rpcParams = default)
    {
        shapeIndex.Value = shapeIndex.Value;
        shapeIndex.SetDirty(true);
    }

    [ServerRpc]
    public void DoneDropATetrisShapeServerRpc(ServerRpcParams rpcParams = default)
    {
        shapeIndex.Value = -1;
    }

    public TetrisShape CreateShape(int inShapeIndex, bool isPredictor = false)
    {
        var shapeSetting = Settings.Instance.shapes[inShapeIndex];
        var tetrisShape = new GameObject($"TetrisShape_{shapeSetting.baseName}");
        tetrisShape.name = $"{tetrisShape.name}_{tetrisShape.GetInstanceID()}";
        var tetrisShapeComponent = tetrisShape.AddComponent<TetrisShape>();
        tetrisShapeComponent.Init(this);
        shapeData = shapeSetting.blockPosition;
        var component = shapeSetting.basic;
        foreach (Vector2 pos in shapeData)
        {
            GameObject block = Instantiate(component, tetrisShape.transform);
            block.transform.localScale = new Vector3(0.5f, 0.5f, 0.5f);
            block.transform.localPosition = pos;
            block.transform.SetParent(tetrisShape.transform, true);
            var tetrisBlock = block.AddComponent<TetrisBlock>();
            tetrisBlock.Init();
            tetrisBlock.belongsTo = tetrisShapeComponent;
            tetrisShapeComponent.blocks.Add(tetrisBlock);
        }

        // 设置 tetrisShape 的世界坐标位置在 CellWidth 中心和 CellHeight 的高度位置
        var startPositionOffset = shapeSetting.startPositionOffset;
        tetrisShape.transform.position = new Vector3(startPositionOffset.x, Settings.CellHeight + startPositionOffset.y, isPredictor ? 1 : 0);
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
        IsPausing = false;
        GameEnd = false;
        if (predictionShape != null)
        {
            DestroyImmediate(predictionShape.gameObject);
        }

        predictionShape = null;
        foreach (var tetrisShape in AllShapes)
        {
            DestroyImmediate(tetrisShape.gameObject);
        }

        currentFallingShape = null;
        Physics.SyncTransforms();
    }

    public bool IsPausing;
    private static readonly int Color1 = Shader.PropertyToID("_Color");
    private static readonly int Alpha = Shader.PropertyToID("_Alpha");
    private static readonly int AnimationTime = Shader.PropertyToID("_AnimationTime");
    private static readonly int Duration = Shader.PropertyToID("_Duration");
    private static readonly int ToColor = Shader.PropertyToID("_ToColor");


    public void Pause()
    {
        IsPausing = !IsPausing;
        Time.timeScale = IsPausing ? 0 : 1;
    }
}