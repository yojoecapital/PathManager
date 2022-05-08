using System.Collections;
using System.Collections.Generic;
using UnityEngine.UI;
using UnityEngine;

public class PlayerController : MonoBehaviour
{
    public float scrollWheelDelta = 0.1f;
    public float minDistance = 3f;

    //entities
    Camera mainCamera;
    GameObject origin, cameraBox;
    GameObject stats, beatTimer;
    GameObject reelPivot;
    public GameObject line = null;

    //player components
    //player animators are under [HideInInspector]
    AudioSource audioSource;
    Rigidbody rigidBody;
    CapsuleCollider capsuleCollider;
    Image cursorImage;

    //public var

    /*ground check*/
    public float radiusMult = 1.1f;
    public float posMult = 1.1f;
    public bool grounded = true;
    public float groundedDistance = 1f;

    /*momentum*/
    public float[] pSpeeds;
    public float rotSpeed = 200f;

    /*tilting*/
    public float tiltSpeed = 5f, maxTilt = 60f;

    /*key press timer*/
    public AudioClip[] clips = new AudioClip[4];
    public float downDuration = 0.1f;

    /*notes*/
    public float clipDuration = 1.2f;
    public int successfulSets = 2;
    public float maxFOV = 75f;

    /*dashing*/
    public float dashSpeed = 20f;
    public float dashDuration = 10f;

    /*reel freelooking*/
    public Vector3 cameraZoomPos = Vector3.zero;
    public float POVSpeed = 5f, maxPOVX = 180f, maxPOVY = 15f;

    /*reel casting*/
    public Vector3 cameraPanPos = Vector3.zero;
    public float reelCursorRadius = 5f;
    public float reelCastRange = 10f;

    public float maxReelLength = 9f, minReelLength = 3f;
    public float swingSpeed = 10f;

    /*reel launching*/
    public GameObject linePrefab;
    public float jumpForce = 10f;

    /*reel rotate*/
    public float maxRotSpeed = 12f;
    public float shootAngle = 0.1f;
    public float shootForceMult = 15f;

    [HideInInspector]
    public Animator bodyAnim, legsAnim;

    //private var
    private int state = 0; /*0 idle, 1 walk, 2 run*/
    public int State { get { return state; } set { state = value; } }
    /*momentum*/
    private int pCount;
    private int pIndex = 0;
    private bool moving = false;
    public bool Moving { get { return moving; } set { moving = value; } }

    /*tilting*/
    private Vector3 curRot = Vector3.zero;

    /*transform player*/
    private Vector3 transformPos = Vector3.zero;
    private Vector3 transformRot;
    private Vector3 cameraRight, cameraUp, cameraForward;
    private Vector2 cursorOrigin = new Vector2(Screen.width / 2, Screen.height / 2);

    /*key press timer*/
    private float downTime = 0;
    private bool down = false;

    /*notes*/
    private Vector3 initSizeBeatTimer;
    private float clipTime = 0;
    private float initIntervalTime, curIntervalTime, beatTime;
    private bool beatReady = false;
    private int beats, mishaps = 0, cool = 0, curSet = 0;
    private float initFOV, curFOV, segFOV;

    /*dashing*/
    private int dashDir = 0;
    public int DashDir { get { return dashDir; } set { dashDir = value; } }
    private float curDashTime;

    /*shifting*/
    private Vector3 initShiftRotation;
    private float initShiftTime = 0, curShiftTime = 0;
    private bool shifting = false;

    /*reel freelooking*/
    private bool freelooking = false, readyToSwing = false, onTar = false;
    private Vector3 initCameraPos, curCameraPos;
    private Vector3 initCameraRot, curCameraRot;

    /*reel launching*/
    private GameObject attachPoint = null;
    private Vector3 initPos = Vector3.zero;
    private Quaternion initRot;
    private float finalSwingTime;
    private float curSwingTime = 0;
    private bool swing = false;

    /*reel rotate*/
    private float curSwingSpeed, curReelLength;
    private float curRotTime = 0;
    private bool rotate = false;
    private float shootSpeed = 0;
    private int shootDir = 1;

    /*sudo parenting player*/
    private Transform relativeParent;
    private Vector3 prevPos, prevRot;

    // Start is called before the first frame update
    void Start()
    {
        Cursor.lockState = CursorLockMode.Locked;

        mainCamera = Camera.main;
        origin = transform.Find("Origin").gameObject;
        reelPivot = origin.transform.Find("Reel Pivot").gameObject;
        cameraBox = transform.Find("Camera Box").gameObject;
        stats = transform.Find("Stats").gameObject;
        beatTimer = stats.transform.Find("BeatTimer").gameObject;
        cursorImage = mainCamera.transform.Find("Cursor").gameObject.transform.GetComponent<Image>(); ;
        bodyAnim = GetComponent<Animator>();
        legsAnim = origin.GetComponent<Animator>();
        audioSource = transform.GetComponent<AudioSource>();
        rigidBody = transform.GetComponent<Rigidbody>();
        capsuleCollider = origin.GetComponent<CapsuleCollider>();

        transformRot = transform.localEulerAngles;
        cameraRight = mainCamera.transform.right; cameraUp = mainCamera.transform.up; cameraForward = mainCamera.transform.forward;
        pCount = pSpeeds.Length;

        initSizeBeatTimer = beatTimer.transform.localScale;
        beats = (int)Mathf.Pow(2, pCount);
        beatTime = clipDuration / beats;
        initIntervalTime = curIntervalTime = beats / 2 * beatTime;
        initFOV = curFOV = mainCamera.fieldOfView;
        segFOV = (maxFOV - initFOV) / pCount;

        curDashTime = dashDuration;

        initCameraPos = curCameraPos = cameraBox.transform.localPosition;
        initCameraRot = curCameraRot = cameraBox.transform.localEulerAngles;

        curReelLength = maxReelLength;
        curSwingSpeed = swingSpeed;
    }

    // Update is called once per frame
    void Update()
    {
        float y_angle;
        bool sprinting = false;
        RaycastHit hit;
        Vector3 inputTransformPos = Vector3.zero, attachPos = Vector3.zero;

        onTar = false;

        switch (state)
        {
            case -1: //smash and shift (freeze player until idle)
                pIndex = 0;
                curSet = 0;
                freelooking = false;

                UnsetBeatTimer();

                swing = false;
                curSwingTime = 0;
                attachPoint = null;

                break;
            case 0: //idle
            case 1: //walk
                rigidBody.velocity = Vector3.zero;

                if (Input.GetKeyDown("w"))
                {
                    downTime = Time.timeSinceLevelLoad;
                    down = false;
                }
                else if (Input.GetKeyUp("w"))
                {
                    if (!down)
                    {
                        pIndex = 1;
                        clipTime = 0;
                        initIntervalTime = curIntervalTime = beats / 2 * beatTime;
                        state = 2; //set run
                        break;
                    }
                    down = false;
                }

                if (Input.GetKey("w") && Time.timeSinceLevelLoad - downTime > downDuration)
                {
                    inputTransformPos.z += 1;
                    down = true;
                }
                if (Input.GetKey("a"))
                    inputTransformPos.x -= 1;
                if (Input.GetKey("s"))
                    inputTransformPos.z -= 1;
                if (Input.GetKey("d"))
                    inputTransformPos.x += 1;

                if (inputTransformPos.magnitude > 0)
                    state = 1; //set walk

                //reel
                FreeLookPlayer(true);

                //final break
                break;

            case 2: //run
                sprinting = true;
                inputTransformPos = Vector3.forward;
                beatTimer.SetActive(true);

                //update beatTimer's properties
                Color setCol = Color.white;
                if (!beatReady)
                    setCol.a = 0.4f;
                if (pIndex > 2) setCol.a /= pIndex * 2;
                beatTimer.GetComponent<SpriteRenderer>().color = setCol;
                beatTimer.transform.localScale = initSizeBeatTimer * (curIntervalTime / initIntervalTime);

                //repeating the single clip
                if (clipTime == 0)
                {
                    if (beatReady)
                        initIntervalTime = curIntervalTime = beats / (pIndex * 2) * beatTime;
                    audioSource.PlayOneShot(clips[pIndex - 1], 1);
                    cool = pIndex;
                }
                if (clipTime < clipDuration)
                    clipTime += Time.deltaTime;
                else
                {
                    curSet += 1;
                    clipTime = 0;
                }

                //beats
                if (curIntervalTime > 0)
                {
                    curIntervalTime -= Time.deltaTime;
                    if (!beatReady)
                    {
                        if (Input.GetKeyDown("w"))
                        {
                            mishaps--;
                            //collapsing
                            if (mishaps <= 0)
                            {
                                audioSource.Stop();
                                curSet = 0;
                                pIndex = 0;
                            }
                        }
                    }
                    else if (Input.GetKeyDown("w"))
                    {
                        beatReady = false;
                        //after successfulSets of sets, increase the speed
                        if (curSet >= successfulSets && pIndex < pCount - 1)
                        {
                            curSet = 0;
                            pIndex++;
                        }
                    }
                    else if (Input.GetKey("w")) //hold to maintain speed
                        beatReady = false;
                }
                else
                {
                    initIntervalTime = curIntervalTime = beats / (pIndex * 2) * beatTime;
                    if (beatReady)
                    {
                        cool--;
                        if (cool <= 0)
                        {
                            curSet = 0;
                            pIndex--;
                        }
                    }
                    beatReady = true;
                }

                //if reached the final pIndex then send back to state 1
                if (pIndex <= 0)
                {
                    pIndex = 0;
                    state = 1; //set walk
                    UnsetBeatTimer();
                }
                //update the collapse leniency through mishaps
                mishaps = pIndex;

                //smash, bodyAnim triggers legsAnim thru event
                if (Physics.Raycast(origin.transform.position + capsuleCollider.center, transform.forward, out hit, 0.65f, ~(1 << 6)))
                {
                    state = -1;
                    bodyAnim.SetTrigger("Smash");
                    break;
                }

                //reel
                if (FreeLookPlayer(true))
                    break;

                //dashes, bodyAnim triggers legsAnim thru event
                if (Input.GetKeyDown("d"))
                {
                    bodyAnim.SetTrigger("Dash Right");
                }
                else if (Input.GetKeyDown("a"))
                {
                    bodyAnim.SetTrigger("Dash Left");
                }
                DashPlayer();

                //shift, bodyAnim triggers legsAnim thru event
                if (Input.GetKeyDown("s"))
                {
                    state = -1;
                    bodyAnim.SetTrigger("Shift");
                    break;
                }
                //final break
                break;

            case 3: //launch

                freelooking = false;

                if (attachPoint != null)
                {
                    if (Input.GetKey(KeyCode.Mouse1))
                    {
                        moving = false;

                        attachPos = attachPoint.transform.position;
                        Vector3 hangPos = new Vector3(attachPos.x, attachPos.y - maxReelLength - reelPivot.transform.localPosition.y, attachPos.z);

                        swing = true;

                        if (curSwingTime < finalSwingTime)
                        {
                            //lerp to hang distance
                            transform.position = Vector3.Lerp(initPos, hangPos, curSwingTime / finalSwingTime);

                            //rotate to closest direction based on angular distance
                            if (Vector3.Angle(transform.rotation * Vector3.forward, attachPoint.transform.rotation * Vector3.forward) < Vector3.Angle(transform.rotation * Vector3.forward, attachPoint.transform.rotation * -Vector3.forward))
                            {
                                transformRot = Quaternion.Lerp(initRot, attachPoint.transform.rotation, curSwingTime / finalSwingTime).eulerAngles;
                            }
                            else
                            {
                                Vector3 attachRot = attachPoint.transform.rotation.eulerAngles;
                                attachRot = new Vector3(attachRot.x, attachRot.y + 180, attachRot.z);
                                transformRot = Quaternion.Lerp(initRot, Quaternion.Euler(attachRot), curSwingTime / finalSwingTime).eulerAngles;
                            }
                            transform.rotation = Quaternion.Euler(transformRot);
                            curSwingTime += Time.deltaTime;
                        }
                        else
                        {
                            curSwingSpeed = swingSpeed * pSpeeds[pIndex] / pSpeeds[pSpeeds.Length - 1];
                            state = 5; //set rotate
                        }
                    }
                    else
                    {
                        if (line != null)
                        {
                            LineManager lineManager = line.transform.GetComponent<LineManager>();
                            lineManager.drawLine = false;
                            line = null;
                        }

                        swing = false;
                        state = -1; //set freeze
                    }
                }
                else
                {
                    //jump player
                    rigidBody.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);
                    state = -1; //set freeze
                }
                break;

            case 4: //freefall

                UnsetBeatTimer();

                //reel
                FreeLookPlayer(false);

                //grounded
                if (grounded)
                {
                        pIndex = 0;
                        state = 0; //set idle
                }
                break;

            case 5: //rotate
                //controlling the length of the reel
                curReelLength += Input.mouseScrollDelta.y * scrollWheelDelta;
                if (curReelLength > maxReelLength)
                    curReelLength = maxReelLength;
                else if (curReelLength < minReelLength)
                    curReelLength = minReelLength;

                float rotSpeed = curSwingSpeed * maxReelLength / curReelLength;

                curSwingSpeed += Time.deltaTime * Physics.gravity.y * Mathf.Sin(curRotTime);

                if (rotSpeed < 0)
                    shootDir = -1;
                else shootDir = 1;

                if (rotSpeed > maxRotSpeed)
                    rotSpeed = maxRotSpeed;
                else if (rotSpeed < -maxRotSpeed)
                    rotSpeed = -maxRotSpeed;

                curRotTime += Time.deltaTime * rotSpeed;

                if (curRotTime * Mathf.Rad2Deg > 360)
                    curRotTime = 0;
                else if (curRotTime < 0)
                    curRotTime = Mathf.PI * 2 - 0.1f;

                if (Input.GetKey(KeyCode.Mouse1) || !(curRotTime < 0.1f && curRotTime > -0.1f))
                {
                    rotate = true;
                    float x  = 0, y, z;
                    
                    float theta = curRotTime - 0.5f * Mathf.PI;
                    y = Mathf.Sin(theta) * curReelLength;
                    z = Mathf.Cos(theta) * curReelLength;

                    Vector3 tarRot = cameraRight * x + cameraUp * y + cameraForward * z;
                    tarRot.x += attachPoint.transform.position.x;
                    tarRot.y += attachPoint.transform.position.y - reelPivot.transform.localPosition.y;
                    tarRot.z += attachPoint.transform.position.z;
                    transform.position = tarRot;
                }
                else
                {
                    shootSpeed = Mathf.Abs(rotSpeed);

                    curSwingSpeed = swingSpeed;
                    curReelLength = maxReelLength;

                    rotate = false;
                    swing = false;

                    curRotTime = 0;

                    rigidBody.velocity = Vector3.zero;

                    if (line != null)
                    {
                        LineManager lineManager = line.transform.GetComponent<LineManager>();
                        lineManager.drawLine = false;
                        line = null;
                    }

                    Vector3 force = new Vector3(0, Mathf.Sin(shootAngle), Mathf.Cos(shootAngle) * shootDir);
                    Vector3 camForce = force.x * cameraRight + force.y * cameraUp + force.z * cameraForward;
                    rigidBody.AddForce(camForce.normalized * shootSpeed * shootForceMult, ForceMode.Impulse);
                    state = -1;
                }
                break;
        }

        if (!CheckGrounded() && state < 3)
        {
            state = 4; //set freefall
        }

        y_angle = Vector2.SignedAngle(new Vector2(inputTransformPos.x, inputTransformPos.z), new Vector2(0, 1));
        bodyAnim.SetBool("Grounded", grounded);
        bodyAnim.SetInteger("y Angle", (int)y_angle);
        bodyAnim.SetInteger("Move State", state);
        bodyAnim.SetFloat("Sqrt Speed Mult", Mathf.Sqrt(pIndex + 1));
        bodyAnim.SetFloat("Speed Mult", pIndex + 1);
        bodyAnim.SetBool("Reel", freelooking);
        bodyAnim.SetBool("Swing", swing);
        bodyAnim.SetBool("Rotate", rotate); //bodyAnim triggers legsAnim thru event
        bodyAnim.SetFloat("z Angle", curRotTime * Mathf.Rad2Deg / 360);
        legsAnim.SetBool("Grounded", grounded);
        legsAnim.SetInteger("x Axis", (int)inputTransformPos.x);
        legsAnim.SetInteger("z Axis", (int)inputTransformPos.z);
        legsAnim.SetInteger("Move State", state);
        legsAnim.SetFloat("Speed Mult", Mathf.Sqrt(pIndex + 1));
        legsAnim.SetBool("Swing", swing);

        TransformPlayer(moving, moving, moving);
        TiltPlayer(sprinting && !freelooking, RotatePlayer(moving && !freelooking && !swing));
        CameraFOV(!freelooking);
        CameraZoom(freelooking && !rotate);
        CameraPan(!freelooking && rotate);
        CameraPOV(freelooking);
        CursorFill(freelooking);
        ShiftPlayer(0.5f);
        if (!moving)
        {
            if (inputTransformPos.magnitude > 0)
            {
                moving = true;
                transformPos = inputTransformPos;
            }
            //updates the cursorOrigin to wherever the cursor currently is
            //cursorOrigin = new Vector2(Input.mousePosition.x, Input.mousePosition.y);
        }

        CameraCollision();
        UpdateFromRelativeParent();
    }

    bool CheckGrounded()
    {
        float radius = capsuleCollider.radius * radiusMult;
        Vector3 pos = origin.transform.position + Vector3.up * (radius * posMult);
        grounded = Physics.CheckSphere(pos, radius, (1 << 3) | (1 << 9));
        return grounded;
    }

    void OnCollisionEnter(Collision collision)
    {
        if (state < 3 && collision.gameObject.layer == 9) //where 3 is ground
        {
            relativeParent = collision.gameObject.transform;
            prevPos = collision.gameObject.transform.position;
            prevRot = collision.gameObject.transform.rotation.eulerAngles;
        }
    }

    void OnCollisionExit(Collision collision)
    {
        relativeParent = null;
    }

    //uses a sudo parent to update the player's transform
    void UpdateFromRelativeParent()
    {
        if (relativeParent != null)
        {
            Vector3 offsetPos = relativeParent.position - prevPos;
            Vector3 offsetRot = relativeParent.rotation.eulerAngles - prevRot;

            transform.position += offsetPos;
            transformRot += offsetRot;
            transform.rotation = Quaternion.Euler(transformRot);

            prevPos = relativeParent.position;
            prevRot = relativeParent.rotation.eulerAngles;
        }
    }

    void TransformPlayer(bool x_on, bool y_on, bool z_on)
    {
        if (!freelooking)
        {
            cameraRight = mainCamera.transform.right;
            cameraUp = mainCamera.transform.up;
            cameraForward = mainCamera.transform.forward;
        }

        Vector3 camtransformPos =
            (x_on ? 1 : 0) * transformPos.x * cameraRight +
            (y_on ? 1 : 0) * transformPos.y * cameraUp +
            (z_on ? 1 : 0) * transformPos.z * cameraForward;

        transform.position += camtransformPos.normalized * Time.deltaTime * pSpeeds[pIndex];
    }

    //rotates player about the y_axis
    float RotatePlayer(bool y_on)
    {
        float tar_y, initRotSpeed = rotSpeed;
        rotSpeed *=  pSpeeds[pIndex];
        tar_y = (y_on ? 1 : 0) * Input.GetAxis("Mouse X");

        float cur_y = transformRot.y + tar_y * Time.deltaTime * rotSpeed / pSpeeds[pIndex];

        transformRot = new Vector3(0, cur_y, 0);

        transform.rotation = Quaternion.Euler(transformRot);

        rotSpeed = initRotSpeed;

        return -tar_y * rotSpeed * 10;
    }

    //tilts origin about the z_axis
    void TiltPlayer(bool z_on, float tar_z)
    {
        //clamping
        if (tar_z > maxTilt)
            tar_z = maxTilt;
        else if (tar_z < -maxTilt)
            tar_z = -maxTilt;

        Vector3 tarRot = new Vector3(curRot.x, curRot.y, (z_on ? 1 : 0) * tar_z);
        curRot = Vector3.Lerp(curRot, tarRot, (z_on ? 1 : 10) * Time.deltaTime * tiltSpeed * pSpeeds[pIndex]);
        origin.transform.localRotation = Quaternion.Euler(curRot);
    }

    void DashPlayer()
    {
        if (dashDir != 0) {
            bodyAnim.ResetTrigger("Dash Right");
            curDashTime -= Time.deltaTime;
            if (curDashTime < 0)
            {
                curDashTime = dashDuration;
                dashDir = 0;
                return;
            }
            Vector3 dashPosition = Vector3.right * dashDir;
            Vector3 camtransformPos =
                dashPosition.x * mainCamera.transform.right +
                dashPosition.y * mainCamera.transform.up +
                dashPosition.z * mainCamera.transform.forward;
            transform.position += camtransformPos * Time.deltaTime * dashSpeed * curDashTime / dashDuration;
        }
        else curDashTime = dashDuration;
    }

    void ShiftPlayer(float time)
    {
        if (shifting)
        {
            if (initShiftTime == 0)
            {
                initShiftRotation = transformRot;
                initShiftTime = time;
            }
            if (curShiftTime < initShiftTime)
            {
                curShiftTime += Time.deltaTime;
                Vector3 rotTar = new Vector3(initShiftRotation.x, initShiftRotation.y + 180, initShiftRotation.z);
                transformRot = Vector3.Lerp(initShiftRotation, rotTar, curShiftTime / initShiftTime);

                transform.rotation = Quaternion.Euler(transformRot);
            }
            else
            {
                initShiftTime = curShiftTime = 0;
                shifting = false;
            }
        }
    }

    bool FreeLookPlayer(bool launchRegardless)
    {
        RaycastHit hit;
        Vector3 attachPos = Vector3.zero;

        //reel
        if (Input.GetKey(KeyCode.Mouse1))
        {
            freelooking = true;

            bool objectInRange, castable = false;
            if(objectInRange = Physics.SphereCast(mainCamera.transform.position, reelCursorRadius, mainCamera.transform.forward, out hit, reelCastRange, ~(1 << 6), QueryTriggerInteraction.Collide))
                castable = hit.collider.gameObject.tag == "Castable";

            //setting cursor color
            if (objectInRange && castable)
            {
                onTar = true;
            }
            else
            {
                onTar = false;
            }

            //cast
            if (Input.GetKeyDown(KeyCode.Mouse0))
            {
                if (objectInRange && castable)
                {
                    attachPoint = hit.collider.gameObject;
                    attachPos = attachPoint.transform.position;
                    initPos = transform.position;
                    initRot = transform.rotation;
                    finalSwingTime = Vector3.Distance(transform.position, new Vector3(attachPos.x, attachPos.y - maxReelLength, attachPos.z)) / (swingSpeed * pSpeeds[pIndex]);

                    //draw line
                    LineManager lineManager;
                    if (line != null)
                    {
                        lineManager = line.transform.GetComponent<LineManager>();
                        lineManager.drawLine = false;
                        line = null;
                    }
                    line = Instantiate(linePrefab, reelPivot.transform, false);
                    lineManager = line.transform.GetComponent<LineManager>();
                    lineManager.drawLine = true;
                    lineManager.attachPoint = attachPoint;
                }
                else attachPoint = null;

                UnsetBeatTimer();
                curSwingTime = 0;

                if (readyToSwing)
                {
                    if (launchRegardless)
                    {
                        bodyAnim.SetTrigger("Launch");
                        state = 3; //set launch
                    }
                    else if (attachPoint != null)
                    {
                        bodyAnim.SetTrigger("Launch");
                        state = 3; //set launch
                    }
                }
            }
            return true;
        }
        freelooking = false;
        return false;
    }

    public void UnsetBeatTimer()
    {
        beatTimer.SetActive(false);
        beatReady = false;
    }

    void CameraFOV(bool f_on)
    {
        //fov camera for sprinting
        float tar_FOV = initFOV + (f_on ? 1 : 0) * segFOV * pIndex;
        curFOV = Mathf.Lerp(curFOV, tar_FOV, Time.deltaTime * pSpeeds[pSpeeds.Length - 1]);
        mainCamera.fieldOfView = curFOV;
    }

    void CameraZoom(bool z_on)
    {
        //zoom camera for freelooking
        Vector3 tarCamZoom = z_on ? cameraZoomPos : initCameraPos;
        curCameraPos = Vector3.Lerp(curCameraPos, tarCamZoom, Time.deltaTime * 10);
        cameraBox.transform.localPosition = curCameraPos;
    }

    void CameraPan(bool z_on)
    {
        //zoom camera for freelooking
        Vector3 tarCamZoom = z_on ? cameraPanPos : initCameraPos;
        curCameraPos = Vector3.Lerp(curCameraPos, tarCamZoom, Time.deltaTime * 10);
        cameraBox.transform.localPosition = curCameraPos;
    }

    void CameraPOV(bool p_on)
    {
        float cur_x = curCameraRot.x + (p_on ? 1 : 0) * Input.GetAxis("Mouse Y") * Time.deltaTime * POVSpeed * 100;
        float cur_y = curCameraRot.y + (p_on ? 1 : 0) * -Input.GetAxis("Mouse X") * Time.deltaTime * POVSpeed * 100;

        //clamping
        cur_x = Mathf.Clamp(cur_x, -maxPOVX, maxPOVX);
        cur_y = Mathf.Clamp(cur_y, -maxPOVY, maxPOVY);

        curCameraRot = new Vector3(cur_x, cur_y, 0);

        curCameraRot = Vector3.Lerp(curCameraRot, initCameraRot, (p_on ? 0 : 1) * Time.deltaTime * POVSpeed);

        mainCamera.transform.localRotation = Quaternion.Euler(curCameraRot);
    }

    void CameraCollision()
    {
        Vector3 targetPos = capsuleCollider.center;
        float maxDistance = Vector3.Distance(curCameraPos, targetPos), curDistance = maxDistance;
        RaycastHit hit;
        if (Physics.Linecast(cameraBox.transform.position, origin.transform.TransformPoint(targetPos), out hit, ~(1 << 6) & ~(1 << 7)))
        {
            curDistance = Mathf.Clamp(hit.distance, minDistance, maxDistance);
        }
        cameraBox.transform.localPosition -= targetPos;
        cameraBox.transform.localPosition = cameraBox.transform.localPosition.normalized * curDistance + targetPos;
    }

    void CursorFill(bool f_on)
    {
        AnimatorStateInfo info = bodyAnim.GetCurrentAnimatorStateInfo(0);
        if (f_on)
        {
            if (!readyToSwing && (info.IsName("idle-reel idle") || info.IsName("walk-reel walk") || info.IsName("run-reel run") || info.IsName("freefall-reel")))
            {
                cursorImage.fillAmount = info.normalizedTime;
            }
            if(readyToSwing)
                cursorImage.fillAmount = 1f;
        }
        else
        {
            readyToSwing = false;
            cursorImage.fillAmount = Mathf.Lerp(cursorImage.fillAmount, 0, Time.deltaTime * 10f);
        }

        //color of cursor
        if (onTar)
            cursorImage.color = Color.red;
        else cursorImage.color = Color.white;
    }

    void _beginDashRight()
    {
        legsAnim.SetTrigger("Dash Right");
        dashDir = 1;
    }

    void _beginDashLeft()
    {
        legsAnim.SetTrigger("Dash Left");
        dashDir = -1;
    }

    void _smash()
    {
        audioSource.Stop();
        transformPos = Vector3.zero; //freeze player
        legsAnim.SetTrigger("Smash");
    }

    void _shift()
    {
        transformPos = Vector3.zero; //freeze player
        legsAnim.SetTrigger("Shift");
    }

    void _shiftPlayer()
    {
        shifting = true;
    }

    void _setReadyToSwing()
    {
        readyToSwing = true;
    }

    void _launchPlayer()
    {
        legsAnim.SetTrigger("Launch");
    }

    void _rotateOn()
    {
        legsAnim.SetBool("Rotate", true);
    }

    void _rotateOff()
    {
        legsAnim.SetBool("Rotate", false);
    }
}
