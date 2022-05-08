using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PathManager : MonoBehaviour
{
    //public var
    public GameObject target = null;
    public Vector3 offset = Vector3.zero;
    public GameObject[] prefabs;
    public int[] path;
    public int radius = 1;
    public float maxDistance = 2f;
    public float startDistance = 64f;

    public float maxWaitTime = 1f;
    public float minWaitTime = 0.3f;

    public GameObject[] vehicles;

    //private var
    private GameObject[] curMap;
    private Vector3[] starts;
    private Transform[] nexts, prevs;
    private Waypoint[] startWaypoints, finalWaypoints, startWaypoints2, finalWaypoints2;
    private int midIndex;

    private float[] curTimes = new float[6];

    /*path*/
    private int curIndex = 0;

    void Start()
    {
        if (target == null)
            target = this.gameObject;
        if (radius < 1)
            radius = 1;

        int length = radius * 2 + 1;
        curMap = new GameObject[length];
        starts = new Vector3[length];
        nexts = new Transform[length];
        prevs = new Transform[length];
        startWaypoints = new Waypoint[length];
        finalWaypoints = new Waypoint[length];
        startWaypoints2 = new Waypoint[length];
        finalWaypoints2 = new Waypoint[length];
        midIndex = curMap.Length / 2;

        //instantiate the mid
        Vector3 tarPos = new Vector3(target.transform.position.x, 0f, target.transform.position.z) + offset; //spawn the middle street block
        curMap[midIndex] = Instantiate(prefabs[path[0]], tarPos, target.transform.rotation);
        FillChildArrays(midIndex);

        //instantiate the "nexts"
        int inext = 1;
        for (int i = midIndex + 1; i < curMap.Length; i++)
        {
            InstantiateNext(i, i - 1, NormalizeIndex(++inext));
        }

        //instantiate the "prevs"
        int iprev = path.Length - 1;
        for (int i = midIndex - 1; i >= 0; i--)
        {
            InstantiatePrev(i, i + 1, NormalizeIndex(--iprev));
        }

        //spawn initial vehicles
        for (int i = 0; i < curMap.Length - 1; i++)
        {
            finalWaypoints[i].next = startWaypoints[i + 1];
            SpawnVehicle(Random.Range(0, 2), i, true);
            SpawnVehicle(Random.Range(0, 2), i, false);
        }

        //set the spawn times for vehicles
        for (int i = 0; i < 6; i++)
            curTimes[i] = Random.Range(minWaitTime, maxWaitTime);
    }

    void Update()
    {
        int marker = midIndex;
        float difference = 0;

        int f = midIndex, b = midIndex - 1;
        //find the player's position relative the spawned bloocks by checking each block's distance the player
        //we check starting from the middle block and move concurrently in both directions
        //break when we find a distacne less that startDistance
        while (f < curMap.Length || b >= 0) 
        {
            if (Vector3.Distance(target.transform.position, starts[f]) < startDistance) 
            {
                marker = f;
                break;
            }
            if (b >= 0 && Vector3.Distance(target.transform.position, starts[b]) < startDistance)
            {
                marker = b;
                break;
            }
            f++;
            b--;
        }

        for (int i = 0; i < curMap.Length - 1; i++)
        {
            if (i >= Mathf.Min(midIndex, marker) && i <= Mathf.Max(midIndex, marker))
                Debug.DrawLine(starts[i], starts[i + 1], Color.red);
            else Debug.DrawLine(starts[i], starts[i + 1]);
        }

        if (marker >= midIndex)
        {
            for (int i = midIndex; i < marker; i++)
                difference += Vector3.Distance(starts[i], starts[i + 1]);
        }
        else
        {
            for (int i = midIndex; i > marker; i--)
                difference -= Vector3.Distance(starts[i], starts[i - 1]);
        }

        //target moved too far forward
        if (difference > maxDistance)
        {
            //destroy last block
            Destroy(curMap[0]);

            //every block becomes its next
            for (int i = 0; i < curMap.Length - 1; i++)
            {
                curMap[i] = curMap[i + 1];
                nexts[i] = nexts[i + 1];
                prevs[i] = prevs[i + 1];
                starts[i] = starts[i + 1];
                startWaypoints[i] = startWaypoints[i + 1];
                finalWaypoints[i] = finalWaypoints[i + 1];
                startWaypoints2[i] = startWaypoints2[i + 1];
                finalWaypoints2[i] = finalWaypoints2[i + 1];
            }

            InstantiateNext(curMap.Length - 1, curMap.Length - 2, NormalizeIndex(curIndex++ + radius - 1));
        }
        //target moved too far backward
        else if (difference < -maxDistance)
        {
            //destroy first block
            Destroy(curMap[curMap.Length - 1]);

            //every block becomes its prev
            for (int i = curMap.Length - 1; i > 0; i--)
            {
                curMap[i] = curMap[i - 1];
                nexts[i] = nexts[i - 1];
                prevs[i] = prevs[i - 1];
                starts[i] = starts[i - 1];
                startWaypoints[i] = startWaypoints[i - 1];
                finalWaypoints[i] = finalWaypoints[i - 1];
                startWaypoints2[i] = startWaypoints2[i - 1];
                finalWaypoints2[i] = finalWaypoints2[i - 1];
            }

            InstantiatePrev(0, 1, NormalizeIndex(curIndex-- - radius + 1));
        }

        //spawn rights 
        for (int i = 0; i < 3; i++)
        {
            if (curTimes[i] < 0)
            {
                SpawnVehicle(i, 0, true);
                curTimes[i] = Random.Range(minWaitTime, maxWaitTime);
            }
            else curTimes[i] -= Time.deltaTime;
        }
        //spawn lefts
        for (int i = 3; i < 6; i++)
        {
            if (curTimes[i] < 0)
            {
                SpawnVehicle(i - 3, curMap.Length - 1, false);
                curTimes[i] = Random.Range(minWaitTime, maxWaitTime);
            }
            else curTimes[i] -= Time.deltaTime;
        }
    }

    private int NormalizeIndex(int index)
    {
        int ret = index % path.Length;
        if (ret < 0)
            return path.Length + ret;
        return ret;
    }

    private void InstantiateNext(int atIndex, int tarIndex, int pathIndex)
    {
        curMap[atIndex] = Instantiate(prefabs[path[pathIndex]], nexts[tarIndex].position, nexts[tarIndex].rotation);
        FillChildArrays(atIndex);
        finalWaypoints[tarIndex].next = startWaypoints[atIndex];
        finalWaypoints2[atIndex].next = startWaypoints2[tarIndex];
    }

    private void InstantiatePrev(int atIndex, int tarIndex, int pathIndex)
    {
        curMap[atIndex] = Instantiate(prefabs[path[pathIndex]], prevs[tarIndex].position, prevs[tarIndex].rotation);

        FillChildArrays(atIndex);
        finalWaypoints[atIndex].next = startWaypoints[tarIndex];
        finalWaypoints2[tarIndex].next = startWaypoints2[atIndex];

        Vector3 atRot = nexts[atIndex].localRotation.eulerAngles;
        atRot = new Vector3(
             curMap[atIndex].transform.rotation.eulerAngles.x,
             curMap[atIndex].transform.rotation.eulerAngles.y - atRot.y,
             curMap[atIndex].transform.rotation.eulerAngles.z);
        curMap[atIndex].transform.rotation = Quaternion.Euler(atRot);
    }

    private void FillChildArrays(int index)
    {
        foreach (Transform child in curMap[index].transform)
        {
            if (child.gameObject.tag == "Path_Next")
                nexts[index] = child.gameObject.transform;
            else if (child.gameObject.tag == "Path_Prev")
                prevs[index] = child.gameObject.transform;
            else if (child.gameObject.tag == "Path_Start")
                starts[index] = child.gameObject.transform.position;
            else if (child.gameObject.tag == "Path_StartWaypoint")
                startWaypoints[index] = child.gameObject.transform.GetComponent<Waypoint>();
            else if (child.gameObject.tag == "Path_FinalWaypoint")
                finalWaypoints[index] = child.gameObject.transform.GetComponent<Waypoint>();
            else if (child.gameObject.tag == "Path_StartWaypoint2")
                startWaypoints2[index] = child.gameObject.transform.GetComponent<Waypoint>();
            else if (child.gameObject.tag == "Path_FinalWaypoint2")
                finalWaypoints2[index] = child.gameObject.transform.GetComponent<Waypoint>();
        }
    }

    public void SpawnVehicle(int posIndex, int startIndex, bool isRight)
    {
        int vehicleIndex = Random.Range(0, vehicles.Length - 1);
        Waypoint tarWaypoint;
        Transform parent;
        if (isRight)
        {
            tarWaypoint = startWaypoints[startIndex];
            parent = curMap[startIndex].transform;
        }
        else
        {
            tarWaypoint = startWaypoints2[startIndex];
            parent = curMap[startIndex].transform;
        }

        Vector3 pos = tarWaypoint.GetPosition(posIndex);

        GameObject vehicle = Instantiate(vehicles[vehicleIndex], pos, tarWaypoint.GetRotation(), parent);
        VehicleController vehicleController = vehicle.transform.GetComponent<VehicleController>();
        vehicleController.tarWaypoint = tarWaypoint;
        vehicleController.pos = posIndex;
    }
}