using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Waypoint : MonoBehaviour
{
    public float radius = 8.5f;
    public Waypoint next = null;

    private Vector3[] positions;
    private Quaternion rotation;

    void Awake()
    {
        positions = new Vector3[3];
        int off = -1;
        for (int i = 0; i < 3; i++)
        {
            positions[i] = off * transform.forward * radius + transform.position;
            off++;
        }
        rotation = transform.rotation;
    }

    void Start()
    {
        positions = new Vector3[3];
        int off = -1;
        for (int i = 0; i < 3; i++)
        {
            positions[i] = off * transform.forward * radius + transform.position;
            off++;
        }
        rotation = transform.rotation;
    }

    public Vector3 GetPosition(int index)
    {
        return positions[index];
    }

    public Quaternion GetRotation()
    {
        return rotation;
    }

    void OnDrawGizmos()
    {
        Gizmos.color = Color.blue;
        Gizmos.DrawLine(-1 * transform.forward * radius + transform.position, transform.forward * radius + transform.position);
        Gizmos.color = Color.red;
        if(next != null)
            Gizmos.DrawLine(transform.position, next.GetPosition(1));
    }

    public Transform GetParent()
    {
        return transform.parent.transform;
    }
}
