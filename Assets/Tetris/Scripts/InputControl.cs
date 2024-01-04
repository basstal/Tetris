using TMPro;
using Unity.Mathematics;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Tetris.Scripts
{
    public class InputControl : MonoBehaviour
    {
        public Button leftButton;
        public Button rightButton;
        public Button downButton;
        public Button upButton;
        public Button resetButton;
        public Button rotateButton;
        public Button pauseButton;

        public TextMeshProUGUI deleteLineCount;

        // assign the actions asset to this field in the inspector:
        public InputActionAsset actions;


        // private field to store move action reference
        private InputAction leftAction;
        private InputAction rightAction;
        private InputAction downAction;
        private InputAction fastDownAction;
        private InputAction rotateAction;
        private InputAction resetAction;

        private Player player;

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

        public void Init(Player inPlayer)
        {
            this.player = inPlayer;
            if (NetworkManager.Singleton.IsServer)
            {
                // find the "move" action, and keep the reference to it, for use in Update
                var gameplayMap = actions.FindActionMap("gameplay");
                leftAction = gameplayMap.FindAction("left");
                leftAction.performed += _ => { this.player.InputCommands.Add((int)InputCommand.MoveLeft); };
                rightAction = gameplayMap.FindAction("right");
                rightAction.performed += _ => { this.player.InputCommands.Add((int)InputCommand.MoveRight); };
                downAction = gameplayMap.FindAction("down");
                downAction.performed += _ => { this.player.InputCommands.Add((int)InputCommand.MoveDown); };
                fastDownAction = gameplayMap.FindAction("fastDown");
                fastDownAction.performed += _ => { this.player.InputCommands.Add((int)InputCommand.FastDown); };
                rotateAction = gameplayMap.FindAction("rotate");
                rotateAction.performed += _ => { this.player.InputCommands.Add((int)InputCommand.Rotate); };
                resetAction = gameplayMap.FindAction("reset");
                resetAction.performed += _ => { this.player.InputCommands.Add((int)InputCommand.ResetGame); };
                // 为按钮绑定事件
                leftButton.onClick.AddListener(() => { this.player.InputCommands.Add((int)InputCommand.MoveLeft); });
                rightButton.onClick.AddListener(() => { this.player.InputCommands.Add((int)InputCommand.MoveRight); });
                downButton.onClick.AddListener(() => { this.player.InputCommands.Add((int)InputCommand.MoveDown); });
                upButton.onClick.AddListener(() => { this.player.InputCommands.Add((int)InputCommand.FastDown); });
                resetButton.onClick.AddListener(() => { this.player.InputCommands.Add((int)InputCommand.ResetGame); });
                rotateButton.onClick.AddListener(() => { this.player.InputCommands.Add((int)InputCommand.Rotate); });
                pauseButton.onClick.AddListener(() => { this.player.InputCommands.Add((int)InputCommand.Pause); });
            }
        }

        void OnEnable()
        {
            actions.FindActionMap("gameplay").Enable();
        }

        void OnDisable()
        {
            actions.FindActionMap("gameplay").Disable();
        }

        public void ResetGame()
        {
            player.ResetGame();
        }

        public void Pause()
        {
            player.Pause();
        }

        public void Rotate()
        {
            if (player.isPausing)
            {
                return;
            }

            if (player.CurrentFallingShape != null)
            {
                player.CurrentFallingShape.Rotate();
                player.PredictionShape.UpdatePredictor(player.CurrentFallingShape);
            }
        }

        public void MoveLeft()
        {
            if (player.isPausing)
            {
                return;
            }

            if (player.CurrentFallingShape != null)
            {
                // 如果移动后的左边界没有超过游戏的左边界，并且目标位置没有其他方块，执行移动操作
                var minX = player.CurrentFallingShape.MaxBounds.min.x - Settings.UnitSize;
                if ((minX > Settings.LeftLimit || Mathf.Approximately(minX, Settings.LeftLimit)) && player.CurrentFallingShape.CanMove(-Settings.Right))
                {
                    player.CurrentFallingShape.transform.position += (Vector3)(-Settings.Right);
                    player.PredictionShape.UpdatePredictor(player.CurrentFallingShape);
                }
            }
        }

        public void MoveRight()
        {
            if (player.isPausing)
            {
                return;
            }

            if (player.CurrentFallingShape != null)
            {
                // 如果移动后的右边界没有超过游戏的右边界，并且目标位置没有其他方块，执行移动操作
                var maxX = player.CurrentFallingShape.MaxBounds.max.x + Settings.UnitSize;
                if ((maxX <= Settings.RightLimit || Mathf.Approximately(maxX, Settings.RightLimit)) && player.CurrentFallingShape.CanMove(Settings.Right))
                {
                    player.CurrentFallingShape.transform.position += (Vector3)(Settings.Right);
                    player.PredictionShape.UpdatePredictor(player.CurrentFallingShape);
                }
            }
        }


        public void MoveDown()
        {
            if (player.isPausing)
            {
                return;
            }

            if (player.CurrentFallingShape == null || !player.CurrentFallingShape.CanMove(-Settings.Up))
            {
                return;
            }

            player.CurrentFallingShape.transform.position += (Vector3)(-Settings.Up);
            player.CurrentFallingShape.intervalCounter = 0;
        }


        public void FastDown()
        {
            if (player.isPausing)
            {
                return;
            }

            if (player.CurrentFallingShape == null)
            {
                return;
            }

            var minDistance = FindFastDownDistance(player.CurrentFallingShape);
            if (minDistance > 0)
            {
                player.CurrentFallingShape.transform.position += (Vector3)(new float3(0, -1, 0) * (minDistance - Settings.HalfUnitSize)); // 0.5f为Box的半高，确保方块底部与目标表面对齐
                Physics.SyncTransforms();
                // Debug.LogWarning($" from fast down");
                player.CurrentFallingShape.IsStopped = true;
            }
        }

        public static float FindFastDownDistance(TetrisShape shape)
        {
            float minDistance = float.MaxValue; // 最初设置为一个非常大的值

            foreach (TetrisBlock blockCollider in shape.blocks)
            {
                // 从每个方块的中心发出射线
                RaycastHit[] hits = Physics.RaycastAll(blockCollider.transform.position, -Settings.Up);

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
}