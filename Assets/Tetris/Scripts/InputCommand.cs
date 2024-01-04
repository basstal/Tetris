namespace Tetris.Scripts
{
    public enum InputCommand
    {
        None = 0,
        MoveLeft = 1,
        MoveRight = 2,
        MoveDown = 3,
        FastDown = 4,
        ResetGame = 5,
        Rotate = 6,
        Pause = 7, // 多人是否允许暂停？这个要特殊处理
    }
}