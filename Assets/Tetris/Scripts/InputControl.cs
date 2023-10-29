using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Mathematics;
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


    void Awake()
    {
        // find the "move" action, and keep the reference to it, for use in Update
        var gameplayMap = actions.FindActionMap("gameplay");
        leftAction = gameplayMap.FindAction("left");
        leftAction.performed += (context) => { MoveLeft(); };
        rightAction = gameplayMap.FindAction("right");
        rightAction.performed += (context) => { MoveRight(); };
        downAction = gameplayMap.FindAction("down");
        downAction.performed += (context) => { MoveDown(); };
        fastDownAction = gameplayMap.FindAction("fastDown");
        fastDownAction.performed += (context) => { FastDown(); };
        rotateAction = gameplayMap.FindAction("rotate");
        rotateAction.performed += (context) => { Rotate(); };
        resetAction = gameplayMap.FindAction("reset");
        resetAction.performed += (context) => { ResetGame(); };
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
        Gameplay.ResetGame();
    }

    public void Pause()
    {
        Gameplay.Pause();
    }

    public void Rotate()
    {
        if (Gameplay.currentFallingShape != null)
        {
            Gameplay.currentFallingShape.Rotate();
            Gameplay.predictionShape.UpdatePredictor(Gameplay.currentFallingShape);
        }
    }

    public void MoveLeft()
    {
        if (Gameplay.currentFallingShape != null)
        {
            // 如果移动后的左边界没有超过游戏的左边界，并且目标位置没有其他方块，执行移动操作
            var minX = Gameplay.currentFallingShape.MaxBounds.min.x - 1;
            if ((minX > Gameplay.LeftLimit || Mathf.Approximately(minX, Gameplay.LeftLimit)) && Gameplay.currentFallingShape.CanMove(Vector3.left))
            {
                Gameplay.currentFallingShape.transform.position += Vector3.left;
                Gameplay.predictionShape.UpdatePredictor(Gameplay.currentFallingShape);
            }
        }
    }

    public void MoveRight()
    {
        if (Gameplay.currentFallingShape != null)
        {
            // 如果移动后的右边界没有超过游戏的右边界，并且目标位置没有其他方块，执行移动操作
            var maxX = Gameplay.currentFallingShape.MaxBounds.max.x + 1;
            if ((maxX <= Gameplay.RightLimit || Mathf.Approximately(maxX, Gameplay.RightLimit)) && Gameplay.currentFallingShape.CanMove(Vector3.right))
            {
                // Debug.LogWarning($"Move Right {Gameplay.currentFallingShape.name}, and update predictionShape : {Gameplay.predictionShape.name}");
                Gameplay.currentFallingShape.transform.position += Vector3.right;
                Gameplay.predictionShape.UpdatePredictor(Gameplay.currentFallingShape);
            }
        }
    }


    public void MoveDown()
    {
        if (Gameplay.currentFallingShape == null || !Gameplay.currentFallingShape.CanMove(Vector3.down))
        {
            return;
        }

        Gameplay.currentFallingShape.transform.position += Vector3.down;
        Gameplay.currentFallingShape.intervalCounter = 0;
    }


    public void FastDown()
    {
        if (Gameplay.currentFallingShape != null)
        {
            var minDistance = FindFastDownDistance(Gameplay.currentFallingShape);
            if (minDistance > 0)
            {
                Gameplay.currentFallingShape.transform.position += Vector3.down * (minDistance - 0.5f); // 0.5f为Box的半高，确保方块底部与目标表面对齐
                Physics.SyncTransforms();
                // Debug.LogWarning($" from fast down");
                Gameplay.currentFallingShape.isStopped = true;
            }
        }
    }

    public static float FindFastDownDistance(TetrisShape shape)
    {
        float minDistance = float.MaxValue; // 最初设置为一个非常大的值

        foreach (TetrisCollider blockCollider in shape.colliders)
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