using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class ResetApp : MonoBehaviour
{
    public void CallResetApp()
    {
        Debug.Log("Reset button pressed. Restarting app from the beginning.");
        // Reload the current scene
        SceneManager.LoadScene(0);

        // Alternatively, you can load a specific scene
        // SceneManager.LoadScene("YourSceneName");
    }
}
