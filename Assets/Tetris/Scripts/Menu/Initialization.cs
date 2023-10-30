using System.Collections;
using UnityEngine;
using Whiterice;

namespace Tetris.Scripts.Menu
{
    public class Initialization : MonoBehaviour
    {
        public IEnumerator Start()
        {
            yield return AssetManager.Initialize();
            yield return AssetManager.SwitchMode(true);
            yield return AssetManager.Instance.LoadSceneAsync("Gameplay");
        }
    }
}