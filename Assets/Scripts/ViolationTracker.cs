using UnityEngine;

public class ViolationTracker : MonoBehaviour
{
    public int maxViolations = 3;
    public int currentViolations;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        currentViolations = 0;
    }

    // Update is called once per frame
    void Update()
    {
        Debug.Log($"Violations: {currentViolations}");
    }

    /*
     * grab the MPH from the carUI manager, it will be attacted to car.
     * 
     */
}
