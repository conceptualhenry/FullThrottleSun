using UnityEngine;

public class Chunk : MonoBehaviour
{
    public Vector3 centerPt;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        centerPt = transform.position;
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
