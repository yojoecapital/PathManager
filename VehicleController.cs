using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class VehicleController : MonoBehaviour
{
    public float speed = 100f;
    public float threshold = 0.5f;
    public float parentDistance = 1f;

    public Waypoint tarWaypoint = null;
    public int pos = 0;

    private Vector3 initPos;
    private Quaternion initRot;
    private float distanceToNext, distanceCovered = 0;

    void Start()
    {
        UpdateWaypoint();
    }

    // Update is called once per frame
    void LateUpdate()
    {
        if (tarWaypoint == null)
            Destroy(this.gameObject);
        else if (Vector3.Distance(transform.position, tarWaypoint.GetPosition(pos)) < threshold)
        {
            UpdateWaypoint();
        }
        else 
        {
            distanceCovered += Time.deltaTime * speed;
            transform.position = Vector3.Lerp(initPos, tarWaypoint.GetPosition(pos), distanceCovered / distanceToNext);
            transform.rotation = Quaternion.Lerp(initRot, tarWaypoint.GetRotation(), distanceCovered / distanceToNext);
        }
    }

    void UpdateWaypoint()
    {
        tarWaypoint = tarWaypoint.next;
        if (tarWaypoint == null)
            return;
        initPos = transform.position;
        initRot = transform.rotation;
        distanceToNext = Vector3.Distance(initPos, tarWaypoint.GetPosition(pos));
        distanceCovered = 0;
        transform.SetParent(tarWaypoint.GetParent());
    }
}
