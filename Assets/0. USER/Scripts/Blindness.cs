using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

public class Blindness : MonoBehaviour
{

    [SerializeField] float increaseRate = 1.1f;
    [SerializeField] float decreaseRate = .9f;
    GameObject image;
    

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        image = this.gameObject;
    }

    // Update is called once per frame
    void Update()
    {

        if (Keyboard.current.tKey.wasPressedThisFrame)
        {
            Debug.Log("BIGGER");
            float newScale = image.transform.localScale.y * increaseRate;
            image.transform.localScale = new Vector3(newScale * 2, newScale, 1);
        }

        
    }
}
