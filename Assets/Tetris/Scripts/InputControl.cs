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
        private InputAction _leftAction;
        private InputAction _rightAction;
        private InputAction _downAction;
        private InputAction _fastDownAction;
        private InputAction _rotateAction;
        private InputAction _resetAction;

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

        public void Init(Player inPlayer)
        {
            this._player = inPlayer;
            if (NetworkManager.Singleton.IsServer)
            {
                // find the "move" action, and keep the reference to it, for use in Update
                var gameplayMap = actions.FindActionMap("gameplay");
                _leftAction = gameplayMap.FindAction("left");
                _leftAction.performed += _ => { this._player.InputCommands.Add((int)InputCommand.MoveLeft); };
                _rightAction = gameplayMap.FindAction("right");
                _rightAction.performed += _ => { this._player.InputCommands.Add((int)InputCommand.MoveRight); };
                _downAction = gameplayMap.FindAction("down");
                _downAction.performed += _ => { this._player.InputCommands.Add((int)InputCommand.MoveDown); };
                _fastDownAction = gameplayMap.FindAction("fastDown");
                _fastDownAction.performed += _ => { this._player.InputCommands.Add((int)InputCommand.FastDown); };
                _rotateAction = gameplayMap.FindAction("rotate");
                _rotateAction.performed += _ => { this._player.InputCommands.Add((int)InputCommand.Rotate); };
                _resetAction = gameplayMap.FindAction("reset");
                _resetAction.performed += _ => { this._player.InputCommands.Add((int)InputCommand.ResetGame); };
                // 为按钮绑定事件
                leftButton.onClick.AddListener(() => { this._player.InputCommands.Add((int)InputCommand.MoveLeft); });
                rightButton.onClick.AddListener(() => { this._player.InputCommands.Add((int)InputCommand.MoveRight); });
                downButton.onClick.AddListener(() => { this._player.InputCommands.Add((int)InputCommand.MoveDown); });
                upButton.onClick.AddListener(() => { this._player.InputCommands.Add((int)InputCommand.FastDown); });
                resetButton.onClick.AddListener(() => { this._player.InputCommands.Add((int)InputCommand.ResetGame); });
                rotateButton.onClick.AddListener(() => { this._player.InputCommands.Add((int)InputCommand.Rotate); });
                pauseButton.onClick.AddListener(() => { this._player.InputCommands.Add((int)InputCommand.Pause); });
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
            _player.ResetGame();
        }

        public void Pause()
        {
            _player.Pause();
        }

        public void Rotate()
        {
            if (_player.isPausing)
            {
                return;
            }

            if (_player.CurrentFallingShape != null)
            {
                _player.CurrentFallingShape.Rotate();
                _player.PredictionShape.UpdatePredictor(_player.CurrentFallingShape);
            }
        }

        public void MoveLeft()
        {
            if (_player.isPausing)
            {
                return;
            }

            if (_player.CurrentFallingShape != null)
            {
                // 如果移动后的左边界没有超过游戏的左边界，并且目标位置没有其他方块，执行移动操作
                var minX = _player.CurrentFallingShape.MaxBounds.min.x - Settings.Instance.unitSize;
                if ((minX > Settings.Instance.LeftLimit || Mathf.Approximately(minX, Settings.Instance.LeftLimit)) && _player.CurrentFallingShape.CanMove(-Settings.Instance.Right))
                {
                    _player.CurrentFallingShape.transform.position += (Vector3)(-Settings.Instance.Right);
                    _player.PredictionShape.UpdatePredictor(_player.CurrentFallingShape);
                }
            }
        }

        public void MoveRight()
        {
            if (_player.isPausing)
            {
                return;
            }

            if (_player.CurrentFallingShape != null)
            {
                // 如果移动后的右边界没有超过游戏的右边界，并且目标位置没有其他方块，执行移动操作
                var maxX = _player.CurrentFallingShape.MaxBounds.max.x + Settings.Instance.unitSize;
                if ((maxX <= Settings.Instance.RightLimit || Mathf.Approximately(maxX, Settings.Instance.RightLimit)) && _player.CurrentFallingShape.CanMove(Settings.Instance.Right))
                {
                    _player.CurrentFallingShape.transform.position += (Vector3)(Settings.Instance.Right);
                    _player.PredictionShape.UpdatePredictor(_player.CurrentFallingShape);
                }
            }
        }


        public void MoveDown()
        {
            if (_player.isPausing)
            {
                return;
            }

            if (_player.CurrentFallingShape == null || !_player.CurrentFallingShape.CanMove(-Settings.Instance.Up))
            {
                return;
            }

            _player.CurrentFallingShape.transform.position += (Vector3)(-Settings.Instance.Up);
            _player.CurrentFallingShape.intervalCounter = 0;
        }


        public void FastDown()
        {
            if (_player.isPausing)
            {
                return;
            }

            if (_player.CurrentFallingShape == null)
            {
                return;
            }

            var minDistance = FindFastDownDistance(_player.CurrentFallingShape);
            if (minDistance > 0)
            {
                _player.CurrentFallingShape.transform.position += (Vector3)(new float3(0, -1, 0) * (minDistance - Settings.Instance.HalfUnitSize)); // 0.5f为Box的半高，确保方块底部与目标表面对齐
                Physics.SyncTransforms();
                // Debug.LogWarning($" from fast down");
                _player.CurrentFallingShape.IsStopped = true;
            }
        }

        public static float FindFastDownDistance(TetrisShape shape)
        {
            float minDistance = float.MaxValue; // 最初设置为一个非常大的值

            foreach (TetrisBlock blockCollider in shape.blocks)
            {
                // 从每个方块的中心发出射线
                RaycastHit[] hits = Physics.RaycastAll(blockCollider.transform.position, -Settings.Instance.Up);

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