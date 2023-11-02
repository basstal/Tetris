using System;
using Tetris.Scripts;
using Unity.Netcode;
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


    // private field to store move action reference
    private InputAction leftAction;
    private InputAction rightAction;
    private InputAction downAction;
    private InputAction fastDownAction;
    private InputAction rotateAction;
    private InputAction resetAction;

    private Player _player;

    public void Execute(NetworkListEvent<int> current)
    {
        InputCommand command = (InputCommand)current.Value;
        if (command == InputCommand.None)
        {
            return;
        }

        switch (command)
        {
            case InputCommand.MoveLeft:
                MoveLeft();
                break;
            case InputCommand.MoveRight:
                MoveRight();
                break;
            case InputCommand.MoveDown:
                MoveDown();
                break;
            case InputCommand.FastDown:
                FastDown();
                break;
            case InputCommand.ResetGame:
                ResetGame();
                break;
            case InputCommand.Rotate:
                Rotate();
                break;
            case InputCommand.Pause:
                Pause();
                break;
            default:
                Debug.LogError($"failed to execute command {current}");
                break;
        }
    }

    public void Init(Player player)
    {
        _player = player;
        if (NetworkManager.Singleton.IsServer)
        {
            if (_player.OwnerClientId == 1)
            {
                // find the "move" action, and keep the reference to it, for use in Update
                var gameplayMap = actions.FindActionMap("gameplay");
                leftAction = gameplayMap.FindAction("left");
                leftAction.performed += context => { _player.inputCommands.Add((int)InputCommand.MoveLeft); };
                rightAction = gameplayMap.FindAction("right");
                rightAction.performed += context => { _player.inputCommands.Add((int)InputCommand.MoveRight); };
                downAction = gameplayMap.FindAction("down");
                downAction.performed += context => { _player.inputCommands.Add((int)InputCommand.MoveDown); };
                fastDownAction = gameplayMap.FindAction("fastDown");
                fastDownAction.performed += context => { _player.inputCommands.Add((int)InputCommand.FastDown); };
                rotateAction = gameplayMap.FindAction("rotate");
                rotateAction.performed += context => { _player.inputCommands.Add((int)InputCommand.Rotate); };
                resetAction = gameplayMap.FindAction("reset");
                resetAction.performed += context => { _player.inputCommands.Add((int)InputCommand.ResetGame); };
                // 为按钮绑定事件
                leftButton.onClick.AddListener(() => { _player.inputCommands.Add((int)InputCommand.MoveLeft); });
                rightButton.onClick.AddListener(() => { _player.inputCommands.Add((int)InputCommand.MoveRight); });
                downButton.onClick.AddListener(() => { _player.inputCommands.Add((int)InputCommand.MoveDown); });
                upButton.onClick.AddListener(() => { _player.inputCommands.Add((int)InputCommand.FastDown); });
                resetButton.onClick.AddListener(() => { _player.inputCommands.Add((int)InputCommand.ResetGame); });
                rotateButton.onClick.AddListener(() => { _player.inputCommands.Add((int)InputCommand.Rotate); });
                pauseButton.onClick.AddListener(() => { _player.inputCommands.Add((int)InputCommand.Pause); });
            }
        }
    }

    // void OnEnable()
    // {
    //     actions.FindActionMap("gameplay").Enable();
    // }
    //
    // void OnDisable()
    // {
    //     actions.FindActionMap("gameplay").Disable();
    // }

    public void ResetGame()
    {
        _player.ResetGame();
    }

    public void Pause()
    {
        _player.Pause();
    }

    public void Rotate()
    {
        if (_player.IsPausing)
        {
            return;
        }

        if (_player.currentFallingShape != null)
        {
            _player.currentFallingShape.Rotate();
            _player.predictionShape.UpdatePredictor(_player.currentFallingShape);
        }
    }

    public void MoveLeft()
    {
        if (_player.IsPausing)
        {
            return;
        }

        if (_player.currentFallingShape != null)
        {
            // 如果移动后的左边界没有超过游戏的左边界，并且目标位置没有其他方块，执行移动操作
            var minX = _player.currentFallingShape.MaxBounds.min.x - 1;
            if ((minX > Settings.LeftLimit || Mathf.Approximately(minX, Settings.LeftLimit)) && _player.currentFallingShape.CanMove(Vector3.left))
            {
                _player.currentFallingShape.transform.position += Vector3.left;
                _player.predictionShape.UpdatePredictor(_player.currentFallingShape);
            }
        }
    }

    public void MoveRight()
    {
        if (_player.IsPausing)
        {
            return;
        }

        if (_player.currentFallingShape != null)
        {
            // 如果移动后的右边界没有超过游戏的右边界，并且目标位置没有其他方块，执行移动操作
            var maxX = _player.currentFallingShape.MaxBounds.max.x + 1;
            if ((maxX <= Settings.RightLimit || Mathf.Approximately(maxX, Settings.RightLimit)) && _player.currentFallingShape.CanMove(Vector3.right))
            {
                // Debug.LogWarning($"Move Right {Gameplay.currentFallingShape.name}, and update predictionShape : {Gameplay.predictionShape.name}");
                _player.currentFallingShape.transform.position += Vector3.right;
                _player.predictionShape.UpdatePredictor(_player.currentFallingShape);
            }
        }
    }


    public void MoveDown()
    {
        if (_player.IsPausing)
        {
            return;
        }

        if (_player.currentFallingShape == null || !_player.currentFallingShape.CanMove(Vector3.down))
        {
            return;
        }

        _player.currentFallingShape.transform.position += Vector3.down;
        _player.currentFallingShape.intervalCounter = 0;
    }


    public void FastDown()
    {
        if (_player.IsPausing)
        {
            return;
        }

        if (_player.currentFallingShape == null)
        {
            return;
        }

        var minDistance = FindFastDownDistance(_player.currentFallingShape);
        if (minDistance > 0)
        {
            _player.currentFallingShape.transform.position += Vector3.down * (minDistance - 0.5f); // 0.5f为Box的半高，确保方块底部与目标表面对齐
            Physics.SyncTransforms();
            // Debug.LogWarning($" from fast down");
            _player.currentFallingShape.isStopped = true;
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