using UnityEngine;

public class ViolationTracker : MonoBehaviour
{
    public int maxViolations = 3;
    public int currentViolations;
    public CopBehavior copBehavior;
    public StopSign stopSign;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        currentViolations = 0;
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
