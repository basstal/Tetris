using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.Mathematics;
using UnityEditor.VersionControl;
using UnityEngine;
using UnityEngine.VFX;
using Whiterice;
using Random = UnityEngine.Random;

public class Gameplay : MonoBehaviour
{
    public GameObject[] Basics; // 这应该是一个正方形的预制体，表示基础的方块单元
    public Material predictorMaterial;
    public Material deleteLineMaterial;
    private Vector2[] shapeData; // 当前形状的数据
    public const int CellWidth = 10;
    public const int CellHeight = 10;
    public static float LeftLimit = -CellWidth / 2f + 0.5f; // 设置左边界
    public static float RightLimit = CellWidth / 2f + 0.5f; // 设置右边界
    [HideInInspector] public InputControl InputControl;
    public static bool GameEnd;
    [NonSerialized] public static TetrisShape currentFallingShape;
    [NonSerialized] public static TetrisShape predictionShape;
    public static float WaitForFinalModify;
    public static Gameplay Instance;
    [HideInInspector] public bool waitDeleteLine;
    public static TetrisShape[] AllShapes => FindObjectsOfType<TetrisShape>().Where(shape => !shape.isPredictor).ToArray();


    // 以下是7种不同的方块形状的数据
    private static readonly Vector2[][] ShapePositions = new Vector2[][]
    {
        new Vector2[] { new Vector2(-1, 0), new Vector2(0, 0), new Vector2(1, 0), new Vector2(2, 0) }, // I
        new Vector2[] { new Vector2(-0.5f, -0.5f), new Vector2(0.5f, -0.5f), new Vector2(-0.5f, 0.5f), new Vector2(0.5f, 0.5f) }, // O
        new Vector2[] { new Vector2(-1, 0), new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 1) }, // T
        new Vector2[] { new Vector2(0, -1), new Vector2(1, -1), new Vector2(-1, 0), new Vector2(0, 0) }, // S
        new Vector2[] { new Vector2(-1, -1), new Vector2(0, -1), new Vector2(0, 0), new Vector2(1, 0) }, // Z
        new Vector2[] { new Vector2(-1, 0), new Vector2(-1, 1), new Vector2(0, 1), new Vector2(1, 1) }, // J
        new Vector2[] { new Vector2(1, 0), new Vector2(-1, 1), new Vector2(0, 1), new Vector2(1, 1) } // L
    };


    private static readonly Vector2[] StartPositionOffset = new Vector2[]
    {
        Vector2.zero,
        new Vector2(0.5f, 0.5f),
        Vector2.zero,
        Vector2.zero,
        Vector2.zero,
        Vector2.zero,
        Vector2.zero,
    };

    private static readonly int[] RotateThreshold = new int[]
    {
        180,
        360,
        360,
        180,
        180,
        360,
        360,
    };

    private static string[] BaseNames = new string[]
    {
        "I",
        "O",
        "T",
        "S",
        "Z",
        "J",
        "L",
    };

    private IEnumerator Start()
    {
        yield return AssetManager.Initialize();
        // DontDestroyOnLoad(this);
        InputControl = GameObject.Find("Canvas").GetComponentInChildren<InputControl>();
        Instance = this;
    }

    private void Update()
    {
        if (WaitForFinalModify > 0)
        {
            WaitForFinalModify -= Time.deltaTime;
            if (WaitForFinalModify <= 0 && currentFallingShape != null && !currentFallingShape.CanMove(Vector3.down))
            {
                currentFallingShape.isStopped = true;
            }
        }

        if (GameEnd || waitDeleteLine)
        {
            return;
        }

        if (currentFallingShape == null)
        {
            DropATetrisShape();
        }
    }

    static List<int> DeletedRowPositions = new List<int>();

    public void CheckDeleteLine()
    {
        // 获得场景中所有的 TetrisCollider 组件
        var allTetrisColliders = AllShapes.SelectMany(shape => shape.colliders).ToArray();
        // 将它们按照 y 坐标分组
        var groupedColliders = allTetrisColliders.GroupBy(colliderComponent => (int)math.round(colliderComponent.transform.position.y));
        // 将 groupedColliders 按 y 从小到大排列
        groupedColliders = groupedColliders.OrderBy(group => group.Key);
        DeletedRowPositions.Clear();
        foreach (var group in groupedColliders)
        {
            // 如果有一组的数量等于 CellWidth 则将这一组 TetrisCollider 移除
            if (group.Count() == CellWidth)
            {
                DeletedRowPositions.Add(group.Key);
                waitDeleteLine = true;
                foreach (var tetrisCollider in group)
                {
                    tetrisCollider.toBeDelete = true;
                    var meshRenderer = tetrisCollider.GetComponent<MeshRenderer>();
                    meshRenderer.material = deleteLineMaterial;
                }
            }
        }

        if (waitDeleteLine)
        {
            StartCoroutine(DeleteLine(allTetrisColliders));
        }
    }

    List<GameObject> deleteLineEffects = new List<GameObject>();

    public IEnumerator DeleteLine(TetrisCollider[] allTetrisColliders)
    {
        GameObject deleteLineEffectTemplate = AssetManager.Instance.LoadAsset<GameObject>("DeleteLine", this);
        var visualEffect = deleteLineEffectTemplate.GetComponent<VisualEffect>();
        var duration = visualEffect.GetFloat("Duration");
        var gradient = visualEffect.GetGradient("ColorGradient");
        var toColor = gradient.colorKeys[2].color;
        foreach (var deleteLineEffect in deleteLineEffects)
        {
            DestroyImmediate(deleteLineEffect);
        }

        deleteLineEffects.Clear();
        deleteLineMaterial.SetFloat(Duration, duration);
        deleteLineMaterial.SetColor(ToColor, toColor);
        // Debug.LogWarning($"DeleteLine effect duration : {duration}");
        foreach (var line in DeletedRowPositions)
        {
            var pos = new Vector3(0, line, 0);
            var instanceDeleteLineEffect = Instantiate(deleteLineEffectTemplate);
            instanceDeleteLineEffect.transform.position = pos;
            deleteLineEffects.Add(instanceDeleteLineEffect);
        }

        float animationTime = 0;
        while (animationTime <= duration)
        {
            animationTime += Time.deltaTime;
            deleteLineMaterial.SetFloat(AnimationTime, animationTime);
            yield return null;
        }


        OnDeleteAnimationComplete(allTetrisColliders);
        waitDeleteLine = false;
    }

    public void OnDeleteAnimationComplete(TetrisCollider[] allTetrisColliders)
    {
        foreach (var tetrisCollider in allTetrisColliders)
        {
            if (tetrisCollider.toBeDelete)
            {
                tetrisCollider.belongsTo.colliders.Remove(tetrisCollider);
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
            if (tetrisShape.colliders.Count == 0)
            {
                DestroyImmediate(tetrisShape.gameObject);
            }
        }
    }

    public void DropATetrisShape()
    {
        var shapeIndex = Random.Range(0, 7);
        var tetrisShape = CreateShape(shapeIndex);
        // Debug.LogWarning($"DropATetrisShape : {tetrisShape}", tetrisShape);
        currentFallingShape = tetrisShape;
        predictionShape = CreateShape(shapeIndex, true);
        Physics.SyncTransforms();
        predictionShape.UpdatePredictor(tetrisShape);
    }

    public TetrisShape CreateShape(int shapeIndex, bool isPredictor = false)
    {
        var tetrisShape = new GameObject($"TetrisShape_{BaseNames[shapeIndex]}");
        tetrisShape.name = $"{tetrisShape.name}_{tetrisShape.GetInstanceID()}";
        var tetrisShapeComponent = tetrisShape.AddComponent<TetrisShape>();

        shapeData = ShapePositions[shapeIndex];
        var component = Basics[shapeIndex];
        foreach (Vector2 pos in shapeData)
        {
            GameObject block = Instantiate(component, tetrisShape.transform);
            block.transform.localScale = new Vector3(0.5f, 0.5f, 0.5f);
            block.transform.localPosition = pos;
            block.transform.SetParent(tetrisShape.transform, true);
            var tetrisCollider = block.AddComponent<TetrisCollider>();
            tetrisCollider.belongsTo = tetrisShapeComponent;
            tetrisShapeComponent.colliders.Add(tetrisCollider);
        }

        // 设置 tetrisShape 的世界坐标位置在 CellWidth 中心和 CellHeight 的高度位置
        tetrisShape.transform.position = new Vector3(StartPositionOffset[shapeIndex].x, CellHeight + StartPositionOffset[shapeIndex].y, 0);
        tetrisShapeComponent.bornPos = tetrisShape.transform.position;
        tetrisShapeComponent.RotateThreshold = RotateThreshold[shapeIndex];

        if (isPredictor)
        {
            tetrisShape.name = $"{tetrisShape.name}_Predictor";
            tetrisShapeComponent.isPredictor = true;
            var defaultColor = tetrisShapeComponent.colliders[0].GetComponent<MeshRenderer>().material.GetColor(Color1);
            predictorMaterial.SetColor(Color1, defaultColor);
            predictorMaterial.SetFloat(Alpha, 0.5f);
            foreach (var colliderComponent in tetrisShapeComponent.colliders)
            {
                var meshRenderer = colliderComponent.GetComponent<MeshRenderer>();
                meshRenderer.material = predictorMaterial;
            }
        }

        return tetrisShapeComponent;
    }

    public static void ResetGame()
    {
        // Debug.LogWarning("Reset");
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

    private static bool IsPausing;
    private static readonly int Color1 = Shader.PropertyToID("_Color");
    private static readonly int Alpha = Shader.PropertyToID("_Alpha");
    private static readonly int AnimationTime = Shader.PropertyToID("_AnimationTime");
    private static readonly int Duration = Shader.PropertyToID("_Duration");
    private static readonly int ToColor = Shader.PropertyToID("_ToColor");


    public static void Pause()
    {
        IsPausing = !IsPausing;
        Time.timeScale = IsPausing ? 0 : 1;
    }
}