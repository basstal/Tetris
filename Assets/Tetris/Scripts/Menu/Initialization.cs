using System.Collections;
using Unity.Netcode;
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
#if UNITY_EDITOR
            int clientIndex = MultiPlay.Utils.GetCurrentCloneIndex();

            if (clientIndex == 0)
            {
                NetworkManager.Singleton.StartHost();
            }
            else
            {
                NetworkManager.Singleton.StartClient();
            }
#endif
        }
    }
}