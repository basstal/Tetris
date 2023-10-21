using System;
using System.Collections;
using System.Linq;
using Sirenix.OdinInspector;
using UnityEngine;
using Whiterice;
using Random = UnityEngine.Random;

public class Gameplay : MonoBehaviour
{
    public GameObject[] Basics; // 这应该是一个正方形的预制体，表示基础的方块单元
    private Vector2[] shapeData; // 当前形状的数据
    public const int CellWidth = 10;
    public const int CellHeight = 10;
    public static float LeftLimit = -CellWidth / 2f; // 设置左边界
    public static float RightLimit = CellWidth / 2f; // 设置右边界
    public InputControl InputControl;
    public static bool GameEnd;
    [NonSerialized] public static TetrisShape currentFallingShape;
    public static float WaitForFinalModify;
    public static TetrisShape[] AllShapes => FindObjectsOfType<TetrisShape>();


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
        DontDestroyOnLoad(this);
        InputControl = GameObject.Find("Canvas").GetComponentInChildren<InputControl>();
    }

    private void Update()
    {
        if (GameEnd)
        {
            return;
        }

        if (WaitForFinalModify > 0)
        {
            WaitForFinalModify -= Time.deltaTime;
            if (WaitForFinalModify <= 0 && currentFallingShape != null && !currentFallingShape.CanMove(Vector3.down))
            {
                currentFallingShape.isStopped = true;
            }
        }

        if (currentFallingShape == null)
        {
            DropATetrisShape();
        }

        CheckDeleteLine();
    }

    public void CheckDeleteLine()
    {
        // 获得场景中所有的 TetrisCollider 组件
        var allTetrisColliders = FindObjectsOfType<TetrisCollider>();
        // 将它们按照 y 坐标分组
        var groupedColliders = allTetrisColliders.GroupBy(collider => (int)collider.transform.position.y);
        // 将 groupedColliders 按 y 从小到大排列
        groupedColliders = groupedColliders.OrderBy(group => group.Key);
        // 如果有一组的数量等于 CellWidth 则将这一组 TetrisCollider 移除
        foreach (var group in groupedColliders)
        {
            if (group.Count() == CellWidth - 1)
            {
                foreach (var tetrisCollider in group)
                {
                    tetrisCollider.belongsTo.colliders.Remove(tetrisCollider);
                    DestroyImmediate(tetrisCollider.gameObject);
                }

                // 将这一组的所有 TetrisCollider 的 y 坐标向下移动一个单位
                foreach (var tetrisCollider in allTetrisColliders)
                {
                    if (tetrisCollider != null && tetrisCollider.transform.position.y > group.Key)
                    {
                        tetrisCollider.transform.position += Vector3.down;
                    }
                }
            }
        }
    }

    [Button]
    public void DropATetrisShape()
    {
        var tetrisShape = CreateShape(Random.Range(0, 7));
        // Debug.LogWarning($"currentFallingShape : {tetrisShape}");
        currentFallingShape = tetrisShape;
    }

    public TetrisShape CreateShape(int shapeIndex)
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
        return tetrisShapeComponent;
    }

    public static void ResetGame()
    {
        // Debug.LogWarning("Reset");
        GameEnd = false;
        foreach (var tetrisShape in AllShapes)
        {
            Destroy(tetrisShape.gameObject);
        }

        currentFallingShape = null;
    }
}