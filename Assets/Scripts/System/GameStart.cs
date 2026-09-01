using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class GameStart : MonoBehaviour
{
    // 「GameStart」ボタンを押した時
    public void StartGame()
    {
        SceneManager.LoadScene("StageBase");
    }
}