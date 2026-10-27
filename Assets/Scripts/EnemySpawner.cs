using UnityEngine;

public class EnemySpawner : MonoBehaviour
{
    public GameObject tumbleweedPrefab;
    public float interval = 2f;
    public float sideX = 14f;      // just off-screen horizontally
    public float skyY = 12f;       // above the screen
    public float skyHalfWidth = 10f;
    public float groundY = 1f;

    void Start() { InvokeRepeating(nameof(Spawn), 1f, interval); }

    void Spawn()
    {
        Vector3 pos;
        int zone = Random.Range(0, 3);
        if (zone == 0)      pos = new Vector3(-sideX, groundY + Random.Range(0f, 3f), 0);
        else if (zone == 1) pos = new Vector3(sideX,  groundY + Random.Range(0f, 3f), 0);
        else                pos = new Vector3(Random.Range(-skyHalfWidth, skyHalfWidth), skyY, 0);
        Instantiate(tumbleweedPrefab, pos, Quaternion.identity);
    }
}