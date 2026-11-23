using UnityEngine;
using UnityEngine.AI;

public class CopBehavior : MonoBehaviour
{
    public GameObject copPrefab;
    private GameObject spawnedCop;
    
    public Transform player;
    public float speed;
    private float distance;
    //private NavMeshAgent nav;
    //private Vector3 targetDir;
    private bool followingPlayer;

    public ViolationTracker violationTracker;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        spawnedCop = Instantiate(copPrefab, Vector3.zero, Quaternion.identity);
        spawnedCop.SetActive(false);
        //violationTracker = GetComponent<ViolationTracker>();

        //player = GameObject.FindGameObjectWithTag("Player").transform;
        //nav = GetComponent<NavMeshAgent>();
        followingPlayer = false;
    }

    // Update is called once per frame
    void Update()
    {
        if (violationTracker.currentViolations == 1)
        {
            followingPlayer = true;
            spawnedCop.SetActive(true);

            float distance = Vector3.Distance(transform.position, player.position);
            Debug.Log("Distance from player: {distance}");
            //Cop follows 15 meters behind
        }
        else if(violationTracker.currentViolations == 2 && followingPlayer)
        {
            //cop follows 10 meters behind
        }
        else if(violationTracker.currentViolations == 3 && followingPlayer)
        {
            //cop follows 5 metters behing
        }
    }
}
