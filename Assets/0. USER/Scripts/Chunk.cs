using UnityEngine;

public class Chunk : MonoBehaviour
{
    public Vector3 centerPt;
    [SerializeField] private int speedLimit = 60;

    void Start()
    {
        centerPt = transform.position;
    }

    // use this method to access the speedlimit value!
    int GetSpeedLimit()
    {
        return speedLimit;
    }
}
