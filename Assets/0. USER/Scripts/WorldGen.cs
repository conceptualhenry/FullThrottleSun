using UnityEngine;

public class WorldGen : MonoBehaviour
{
    public enum Direction
    {
        North, South, East, West
    }

    public Chunk currentChunk;
    public GameObject toSpawn;
    public Direction spawnDirection;

    public float chunkWidth;
    public float genDist;

    Vector3 exitN;
    Vector3 exitS;
    Vector3 exitE;
    Vector3 exitW;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        setExits();
        generateChunk(Direction.North);
    }

    // Update is called once per frame
    void Update()
    {
        //check how far car is from center point 
        //if further than genDist, figure out which side to spawn new chunk on & spawn it
        //when car is closer to new pt than old, destroy old & switch currentChunk to the one you just spawned
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

    void generateChunk(Direction d)
    {

        Vector3 spawnPt = Vector3.zero;

        if (d == Direction.North)  spawnPt = new Vector3(exitN.x,0,exitN.z + chunkWidth);
        else if (d == Direction.South)  spawnPt = new Vector3(exitN.x,0,exitN.z - chunkWidth);
        else if (d == Direction.East)  spawnPt = new Vector3(exitN.x + chunkWidth,0,exitN.z);
        else if (d == Direction.West)  spawnPt = new Vector3(exitN.x - chunkWidth,0,exitN.z);
        

        Instantiate(toSpawn, spawnPt, Quaternion.identity);
    }
}
