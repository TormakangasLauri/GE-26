using UnityEngine;
using UnityEngine.SceneManagement;
public class LoadScene : MonoBehaviour
{
    public string _sceneToLoad;
    public bool _loadAdditive = true;

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            if (_loadAdditive)
                SceneManager.LoadScene(_sceneToLoad, LoadSceneMode.Additive);
            else
            SceneManager.LoadScene(_sceneToLoad);
            gameObject.SetActive(false);
        }
    }
    
}
