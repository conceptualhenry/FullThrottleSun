using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

public class Blindness : MonoBehaviour
{
    [SerializeField] Camera cam;

    [SerializeField] float increaseRate = 1.1f;
    [SerializeField] float decreaseRate = .9f;
    [SerializeField] float sizeMin = .05f;
    [SerializeField] float sizeMax = 50f;
    GameObject image;

    LayerMask sunLayerMask;
    

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        image = this.gameObject;
        sunLayerMask = LayerMask.GetMask("Sun");
    }

    // Update is called once per frame
    void Update()
    {

        RaycastHit hit;

        float newScale = 1;

        if (Physics.Raycast(cam.transform.position, cam.transform.TransformDirection(Vector3.forward), out hit, 20, sunLayerMask) && image.transform.localScale.y < sizeMax)
        {
            newScale = image.transform.localScale.y * increaseRate;
            image.transform.localScale = new Vector3(newScale * 2, newScale, 1);
        }
        else if (image.transform.localScale.y > sizeMin)
        {
            newScale = image.transform.localScale.y * decreaseRate;
            image.transform.localScale = new Vector3(newScale * 2, newScale, 1);
        }

        
        
        

        
    }
}
