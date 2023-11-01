using System;
using UnityEngine;
using Whiterice;

[Serializable]
public class TetrisShapeSetting
{
    public GameObject basic;
    public Vector2[] blockPosition;
    public Vector2 startPositionOffset;
    public int rotateThreshold;
    public string baseName;
}

[CreateAssetMenu(menuName = "Tetris/Settings", fileName = "Settings")]
public class Settings : ScriptableObject

{
    public const int CellWidth = 10;
    public const int CellHeight = 10;
    public static float LeftLimit = -CellWidth / 2f + 0.5f; // 设置左边界
    public static float RightLimit = CellWidth / 2f + 0.5f; // 设置右边界
    public TetrisShapeSetting[] shapes; // 这应该是一个正方形的预制体，表示基础的方块单元
    public Material predictorMaterial;
    public Material deleteLineMaterial;
    public AudioClip deleteLine;

    private static Settings _instance;

    public static Settings Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = AssetManager.Instance.LoadAsset<Settings>("Settings", null);
            }

            return _instance;
        }
    }
    // [Button]
    // public void Init()
    // {
    //     Settings settings = AssetDatabase.LoadAssetAtPath<Settings>("Assets/Tetris/Resources/Settings.asset");
    //     settings.shapes = new TetrisShapeSetting[7];
    //     for (int i = 0; i < 7; ++i)
    //     {
    //         settings.shapes[i] = new TetrisShapeSetting()
    //         {
    //             blockPosition = BlockPositions[i],
    //             startPositionOffset = StartPositionOffset[i],
    //             rotateThreshold = RotateThreshold[i],
    //             baseName = BaseNames[i],
    //         };
    //     }
    //     EditorUtility.SetDirty(settings);
    //     AssetDatabase.SaveAssets();
    //     
    // }

    // // 以下是7种不同的方块形状的数据
    // public static readonly Vector2[][] BlockPositions =
    // {
    //     new[] { new Vector2(-1, 0), new Vector2(0, 0), new Vector2(1, 0), new Vector2(2, 0) }, // I
    //     new[] { new Vector2(-0.5f, -0.5f), new Vector2(0.5f, -0.5f), new Vector2(-0.5f, 0.5f), new Vector2(0.5f, 0.5f) }, // O
    //     new[] { new Vector2(-1, 0), new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 1) }, // T
    //     new[] { new Vector2(0, -1), new Vector2(1, -1), new Vector2(-1, 0), new Vector2(0, 0) }, // S
    //     new[] { new Vector2(-1, -1), new Vector2(0, -1), new Vector2(0, 0), new Vector2(1, 0) }, // Z
    //     new[] { new Vector2(-1, 0), new Vector2(-1, 1), new Vector2(0, 1), new Vector2(1, 1) }, // J
    //     new[] { new Vector2(1, 0), new Vector2(-1, 1), new Vector2(0, 1), new Vector2(1, 1) } // L
    // };
    //
    //
    // public static readonly Vector2[] StartPositionOffset =
    // {
    //     Vector2.zero,
    //     new Vector2(0.5f, 0.5f),
    //     Vector2.zero,
    //     Vector2.zero,
    //     Vector2.zero,
    //     Vector2.zero,
    //     Vector2.zero,
    // };
    //
    // public static readonly int[] RotateThreshold =
    // {
    //     180,
    //     360,
    //     360,
    //     180,
    //     180,
    //     360,
    //     360,
    // };
    //
    // public static readonly string[] BaseNames =
    // {
    //     "I",
    //     "O",
    //     "T",
    //     "S",
    //     "Z",
    //     "J",
    //     "L",
    // };
}