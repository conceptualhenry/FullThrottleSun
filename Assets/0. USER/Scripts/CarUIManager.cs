using TMPro;
using UnityEngine;

public class CarUIManager : MonoBehaviour
{

    GameObject car;
    public float milesDriven = 0;
    public float mph = 0;
    float mphScalar = 1.75f;

    [SerializeField] TextMeshProUGUI milesDisplay;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        car = this.gameObject;
        InvokeRepeating("milesUpdate", 0, 1);
    }

    // Update is called once per frame
    void Update()
    {
        
    }


    void milesUpdate()
    {
        mph = car.GetComponent<Rigidbody>().linearVelocity.magnitude * mphScalar;
        float mps = mph / 3600;
        milesDriven += mps;
        if (milesDisplay.enabled) milesDisplay.text = milesDriven.ToString() +" miles driven";

    }
}
