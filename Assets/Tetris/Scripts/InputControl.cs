using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
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


    private void Start()
    {
        // 为按钮绑定事件
        leftButton.onClick.AddListener(MoveLeft);
        rightButton.onClick.AddListener(MoveRight);
        downButton.onClick.AddListener(MoveDown);
        upButton.onClick.AddListener(FastDown);
        resetButton.onClick.AddListener(Gameplay.ResetGame);
        rotateButton.onClick.AddListener(() =>
        {
            if (Gameplay.currentFallingShape != null)
            {
                Gameplay.currentFallingShape.Rotate();
            }
        });
        pauseButton.onClick.AddListener(Gameplay.Pause);
    }


    void MoveLeft()
    {
        if (Gameplay.currentFallingShape != null)
        {
            // 如果移动后的左边界没有超过游戏的左边界，并且目标位置没有其他方块，执行移动操作
            if (Gameplay.currentFallingShape.MaxBounds.min.x - 1 >= Gameplay.LeftLimit && Gameplay.currentFallingShape.CanMove(Vector3.left))
            {
                Gameplay.currentFallingShape.transform.position += Vector3.left;
            }
        }
    }

    void MoveRight()
    {
        if (Gameplay.currentFallingShape != null)
        {
            // 如果移动后的右边界没有超过游戏的右边界，并且目标位置没有其他方块，执行移动操作
            if (Gameplay.currentFallingShape.MaxBounds.max.x + 1 <= Gameplay.RightLimit && Gameplay.currentFallingShape.CanMove(Vector3.right))
            {
                Gameplay.currentFallingShape.transform.position += Vector3.right;
            }
        }
    }


    void MoveDown()
    {
        if (Gameplay.currentFallingShape == null || !Gameplay.currentFallingShape.CanMove(Vector3.down))
        {
            return;
        }

        Gameplay.currentFallingShape.transform.position += Vector3.down;
        Gameplay.currentFallingShape.intervalCounter = 0;
    }


    void FastDown()
    {
        if (Gameplay.currentFallingShape != null)
        {
            BoxCollider[] blockColliders = Gameplay.currentFallingShape.GetComponentsInChildren<BoxCollider>();
            float minDistance = float.MaxValue; // 最初设置为一个非常大的值

            foreach (BoxCollider blockCollider in blockColliders)
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
                Gameplay.currentFallingShape.transform.position += Vector3.down * (minDistance - 0.5f); // 0.5f为Box的半高，确保方块底部与目标表面对齐
                Physics.SyncTransforms();
                // Debug.LogWarning($" from fast down");
                Gameplay.currentFallingShape.isStopped = true;
            }
        }
    }
}