using UnityEngine;

public class WorldGen : MonoBehaviour
{
    public enum Direction
    {
        North, South, East, West, undefined
    }

    public Chunk currentChunk;
    public Chunk newChunk;
    public GameObject toSpawn;
    public Direction spawnDirection;

    [SerializeField] float chunkWidth;
    [SerializeField] public float genDist;

    Vector3 exitN;
    Vector3 exitS;
    Vector3 exitE;
    Vector3 exitW;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        setExits();
    }

    // Update is called once per frame
    void Update()
    {
        //check how far car is from center point 
        Vector2 carPt2D = new Vector2(transform.position.x, transform.position.z);
        Vector2 centerPt2D = new Vector2(currentChunk.transform.position.x, currentChunk.transform.position.z);
        float centerDist = Vector2.Distance(carPt2D, centerPt2D);

        //if further than genDist, figure out which side to spawn new chunk on & spawn it
        if (centerDist > genDist && newChunk == null)
        {
            spawnDirection = chooseDirection(carPt2D, centerPt2D);
            newChunk = generateChunk(spawnDirection);
        }

        else if ( newChunk != null)
        {

            //when car is closer to new pt than old, destroy old & switch currentChunk to the one you just spawned
            if (Vector2.Distance(carPt2D, new Vector2(newChunk.transform.position.x, newChunk.transform.position.z)) < centerDist)
            {
                switchChunk();
                centerPt2D = new Vector2(currentChunk.transform.position.x, currentChunk.transform.position.z);
            }

            //if car goes back the other way 
            if (chooseDirection(carPt2D, centerPt2D) != spawnDirection)
            {
                //print("chooseDirection " + chooseDirection(carPt2D, centerPt2D) + " did not match spawn direction " + spawnDirection);
                Destroy(newChunk.gameObject);
                newChunk = null;
                spawnDirection = Direction.undefined;
            }
        }
    }

    void setExits()
    {
        if (currentChunk != null)
        {
            exitN = new Vector3(currentChunk.centerPt.x, 0, currentChunk.centerPt.z + chunkWidth);
            exitS = new Vector3(currentChunk.centerPt.x, 0, currentChunk.centerPt.z - chunkWidth);
            exitE = new Vector3(currentChunk.centerPt.x + chunkWidth, 0, currentChunk.centerPt.z);
            exitW = new Vector3(currentChunk.centerPt.x - chunkWidth, 0, currentChunk.centerPt.z);
        }
    }

    Chunk generateChunk(Direction d)
    {

        Vector3 spawnPt = Vector3.zero;

        if (d == Direction.North)  spawnPt = new Vector3(exitN.x,0,exitN.z + chunkWidth);
        else if (d == Direction.South)  spawnPt = new Vector3(exitS.x,0,exitS.z - chunkWidth);
        else if (d == Direction.East)  spawnPt = new Vector3(exitE.x + chunkWidth,0,exitE.z);
        else if (d == Direction.West)  spawnPt = new Vector3(exitW.x - chunkWidth,0,exitW.z);
        

        GameObject g = Instantiate(toSpawn, spawnPt, Quaternion.identity);
        return g.GetComponent<Chunk>();
    }

    void switchChunk()
    {
        Chunk temp = currentChunk;
        currentChunk = newChunk;
        newChunk = temp;
        spawnDirection = switchDirection(spawnDirection);
        setExits();
    }

    Direction chooseDirection(Vector2 car, Vector2 cen)
    {
        float x = car.x - cen.x;
        float y = car.y - cen.y;

        
        //N - y, |y|         E - x, |x|        S - x, |y|        W - y, |x|  
        if (x >= y)
        {
            if (Mathf.Abs(x) >= Mathf.Abs(y)) return Direction.East;
            else return Direction.South;
        }
        else
        {
            if (Mathf.Abs(x) >= Mathf.Abs(y)) return Direction.West;
            else return Direction.North;
        }
    }

    Direction switchDirection(Direction d)
    {
        if (d == Direction.North) return Direction.South;
        else if (d == Direction.South) return Direction.North;
        else if (d == Direction.West) return Direction.East;
        else if (d == Direction.East) return Direction.West;

        return Direction.undefined;
    }
}
