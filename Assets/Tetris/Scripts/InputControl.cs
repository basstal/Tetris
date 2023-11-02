using Tetris.Scripts;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class InputControl : MonoBehaviour
{
    public Button leftButton;
    public Button rightButton;
    public Button downButton;
    public Button upButton;
    public Button resetButton;
    public Button rotateButton;
    public Button pauseButton;

    // assign the actions asset to this field in the inspector:
    public InputActionAsset actions;

    public InputCommand lastInputCommand;

    // private field to store move action reference
    private InputAction leftAction;
    private InputAction rightAction;
    private InputAction downAction;
    private InputAction fastDownAction;
    private InputAction rotateAction;
    private InputAction resetAction;


    void Awake()
    {
        // find the "move" action, and keep the reference to it, for use in Update
        var gameplayMap = actions.FindActionMap("gameplay");
        leftAction = gameplayMap.FindAction("left");
        leftAction.performed += context =>
        {
            lastInputCommand = InputCommand.MoveLeft;
            MoveLeft();
        };
        rightAction = gameplayMap.FindAction("right");
        rightAction.performed += context =>
        {
            lastInputCommand = InputCommand.MoveRight;
            MoveRight();
        };
        downAction = gameplayMap.FindAction("down");
        downAction.performed += context =>
        {
            lastInputCommand = InputCommand.MoveDown;
            MoveDown();
        };
        fastDownAction = gameplayMap.FindAction("fastDown");
        fastDownAction.performed += context =>
        {
            lastInputCommand = InputCommand.FastDown;
            FastDown();
        };
        rotateAction = gameplayMap.FindAction("rotate");
        rotateAction.performed += context =>
        {
            lastInputCommand = InputCommand.Rotate;
            Rotate();
        };
        resetAction = gameplayMap.FindAction("reset");
        resetAction.performed += context =>
        {
            lastInputCommand = InputCommand.ResetGame;
            ResetGame();
        };
    }


    void OnEnable()
    {
        actions.FindActionMap("gameplay").Enable();
    }

    void OnDisable()
    {
        actions.FindActionMap("gameplay").Disable();
    }

    private void Start()
    {
        // 为按钮绑定事件
        leftButton.onClick.AddListener(MoveLeft);
        rightButton.onClick.AddListener(MoveRight);
        downButton.onClick.AddListener(MoveDown);
        upButton.onClick.AddListener(FastDown);
        resetButton.onClick.AddListener(ResetGame);
        rotateButton.onClick.AddListener(Rotate);
        pauseButton.onClick.AddListener(Pause);
    }

    public void ResetGame()
    {
        Player.Instance.ResetGame();
    }

    public void Pause()
    {
        Player.Instance.Pause();
    }

    public void Rotate()
    {
        if (Player.Instance.IsPausing)
        {
            return;
        }

        if (Player.Instance.currentFallingShape != null)
        {
            Player.Instance.currentFallingShape.Rotate();
            Player.Instance.predictionShape.UpdatePredictor(Player.Instance.currentFallingShape);
        }
    }

    public void MoveLeft()
    {
        if (Player.Instance.IsPausing)
        {
            return;
        }

        if (Player.Instance.currentFallingShape != null)
        {
            // 如果移动后的左边界没有超过游戏的左边界，并且目标位置没有其他方块，执行移动操作
            var minX = Player.Instance.currentFallingShape.MaxBounds.min.x - 1;
            if ((minX > Settings.LeftLimit || Mathf.Approximately(minX, Settings.LeftLimit)) && Player.Instance.currentFallingShape.CanMove(Vector3.left))
            {
                Player.Instance.currentFallingShape.transform.position += Vector3.left;
                Player.Instance.predictionShape.UpdatePredictor(Player.Instance.currentFallingShape);
            }
        }
    }

    public void MoveRight()
    {
        if (Player.Instance.IsPausing)
        {
            return;
        }

        if (Player.Instance.currentFallingShape != null)
        {
            // 如果移动后的右边界没有超过游戏的右边界，并且目标位置没有其他方块，执行移动操作
            var maxX = Player.Instance.currentFallingShape.MaxBounds.max.x + 1;
            if ((maxX <= Settings.RightLimit || Mathf.Approximately(maxX, Settings.RightLimit)) && Player.Instance.currentFallingShape.CanMove(Vector3.right))
            {
                // Debug.LogWarning($"Move Right {Gameplay.currentFallingShape.name}, and update predictionShape : {Gameplay.predictionShape.name}");
                Player.Instance.currentFallingShape.transform.position += Vector3.right;
                Player.Instance.predictionShape.UpdatePredictor(Player.Instance.currentFallingShape);
            }
        }
    }


    public void MoveDown()
    {
        if (Player.Instance.IsPausing)
        {
            return;
        }

        if (Player.Instance.currentFallingShape == null || !Player.Instance.currentFallingShape.CanMove(Vector3.down))
        {
            return;
        }

        Player.Instance.currentFallingShape.transform.position += Vector3.down;
        Player.Instance.currentFallingShape.intervalCounter = 0;
    }


    public void FastDown()
    {
        if (Player.Instance.IsPausing)
        {
            return;
        }

        if (Player.Instance.currentFallingShape != null)
        {
            var minDistance = FindFastDownDistance(Player.Instance.currentFallingShape);
            if (minDistance > 0)
            {
                Player.Instance.currentFallingShape.transform.position += Vector3.down * (minDistance - 0.5f); // 0.5f为Box的半高，确保方块底部与目标表面对齐
                Physics.SyncTransforms();
                // Debug.LogWarning($" from fast down");
                Player.Instance.currentFallingShape.isStopped = true;
            }
        }
    }

    public static float FindFastDownDistance(TetrisShape shape)
    {
        float minDistance = float.MaxValue; // 最初设置为一个非常大的值

        foreach (TetrisBlock blockCollider in shape.blocks)
        {
            // 从每个方块的中心发出射线
            RaycastHit[] hits = Physics.RaycastAll(blockCollider.transform.position, Vector3.down);

            foreach (RaycastHit hit in hits)
            {
                if (hit.collider.CompareTag("Ground") || hit.collider.CompareTag("StoppedTetrisShape"))
                {
                    // 取得最小的距离
                    if (hit.distance < minDistance)
                    {
                        minDistance = hit.distance;
                    }
                }
            }
        }

        // 如果找到了碰撞，移动形状到该位置
        if (minDistance < float.MaxValue)
        {
            return minDistance;
        }

        return 0;
    }
}