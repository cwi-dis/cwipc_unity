using Cwipc;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.XR;

using UnityEngine.XR.Interaction.Toolkit.Inputs;

public class ViewAdjust : UnityEngine.XR.Interaction.Toolkit.Locomotion.LocomotionProvider
{
    

    [Tooltip("The object of which the height is adjusted, and that resetting origin will modify")]
    [SerializeField] GameObject cameraOffset;

    [Tooltip("Toplevel object of this player, usually the XROrigin, for resetting origin")]
    [SerializeField] GameObject player;

    [Tooltip("Point cloud pipeline GameObject")]
    [SerializeField] GameObject pointCloudGO;

    [Tooltip("GameObject that follows center of gravity of the captured pointcloud")]
    [SerializeField] Transform pointCloudCenterOfGravityIndicator;

    [Tooltip("Camera used for determining zero position and orientation, for resetting origin")]
    [SerializeField] Camera playerCamera;

    [Tooltip("Teleport to 0,0,0 before adjustment")] [SerializeField]
    private bool teleportToOrigin = false;
    
    [Tooltip("Adjust camera continually during adjustment. May induce motion sickness")]
    [SerializeField] bool adjustCameraContinuous = true;
    
    [Tooltip("How much the camera should be moved forward after adjustment")]
    [SerializeField] float cameraFudgeFactor;

    [Tooltip("How many meters forward the center of gravity of the point cloud should be moved for single camera capturers")]
    [SerializeField] float singleCameraExtraFudgeFactor = 0.05f;

    [Tooltip("Multiplication factor for height adjustment")]
    [SerializeField] float heightFactor = 1;

    [Tooltip("Callback done after view has been adjusted")]
    public UnityEvent viewAdjusted;

    [Tooltip("The Input System Action that will be used to change view height. Must be a Value Vector2 Control of which y is used.")]
    [SerializeField] InputActionProperty m_ViewHeightAction;

    [Tooltip("The Input System Action that will be used to reset view origin.")]
    [SerializeField] InputActionProperty m_resetOriginAction;

    [Tooltip("The Input System Action that determines whether the HMD is tracking. Set this if putting on the HMD should start a ViewAdjust action.")]
    [SerializeField] InputActionProperty m_hmdTrackingAction;


    [Tooltip("Position indicator, visible while adjusting position")]
    [SerializeField] GameObject positionIndicator;

    [Tooltip("Best forward direction indicator, visible while adjusting position")]
    [SerializeField] GameObject forwardIndicator;

    [Tooltip("Forward indicator instructions")]
    [SerializeField] UnityEngine.UI.Text forwardIndicatorInstructions;
    
    [Tooltip("Floor instructions")]
    [SerializeField] UnityEngine.UI.Text floorInstructions;

    [Tooltip("Forward indicator countdown")]
    [SerializeField] UnityEngine.UI.Text forwardIndicatorCountdown;

    [Tooltip("How many seconds is the position indicator visible?")]
    [SerializeField] float positionIndicatorDuration = 5f;

    [SerializeField] bool ViewAdjustInProgress = false;
    [SerializeField] bool ViewAdjustDone = false;

    float positionIndicatorInvisibleAfter = 0;

    [Tooltip("Debug output")]
    [SerializeField] bool debug = false;

    // Start is called before the first frame update
    void Start()
    {
        optionalHideIndicators();
    }

    private void optionalHideIndicators()
    {
        if (positionIndicator != null && positionIndicator.activeSelf && Time.time > positionIndicatorInvisibleAfter) positionIndicator.SetActive(false);
        if (forwardIndicator != null && forwardIndicator.activeSelf && Time.time > positionIndicatorInvisibleAfter) forwardIndicator.SetActive(false);
    }

    // Update is called once per frame
    void Update()
    {
        optionalHideIndicators();
        Vector2 heightInput = m_ViewHeightAction.action?.ReadValue<Vector2>() ?? Vector2.zero;
        float deltaHeight = heightInput.y * heightFactor;
        if (deltaHeight != 0 && BeginLocomotion())
        {
            ShowPositionIndicator();
            cameraOffset.transform.position += new Vector3(0, deltaHeight, 0);
            // Note: we don't save height changes. But if you reset view position
            // afterwards we do also save height changes.
            EndLocomotion();
        }
        if (m_resetOriginAction != null)
        {
            bool doResetOrigin = m_resetOriginAction.action.WasPerformedThisFrame();
            if (doResetOrigin)
            {
                ResetOrigin(true);
            }
        }
        if (m_hmdTrackingAction != null && m_hmdTrackingAction.action != null && m_hmdTrackingAction.action.WasPerformedThisFrame()) 
        {
            if (debug)
            {
                Debug.Log("ViewAdjust: HMD started tracking. Adjust view.");
            }
            ResetOrigin(false);
        }
    }

    private void ShowPositionIndicator(string stage = null, string instructions = "", string floor = "")
    {
        if (positionIndicator != null)
        {
            positionIndicator.SetActive(true);
        }
        if (forwardIndicator != null && stage != null)
        {
            if (stage == "")
            {
                forwardIndicator.SetActive(false);
            }
            else
            {
                forwardIndicator.SetActive(true);
                forwardIndicatorCountdown.text = stage;
                forwardIndicatorInstructions.text = instructions;
                floorInstructions.text = floor;
            }
        }
        positionIndicatorInvisibleAfter = Time.time + positionIndicatorDuration;
    }

    /// <summary>
    /// The user wants the current head position, (X,Z) only, to be the (0, Y, 0), right above the XROrigin.
    /// </summary>
    public void ResetOrigin(bool canStop=true)
    {
        if (!ViewAdjustInProgress)
        {
            if (debug)
            {
                Debug.Log("ViewAdjust: ResetOrigin: starting");
            }
            // If we are not adjusting the view we start adjusting the view
            StartCoroutine(_ResetOrigin());
        }
        else if (canStop)
        {
            if (debug)
            {
                Debug.Log("ViewAdjust: ResetOrigin: stopping");
            }
            // If we are already adjusting the view we might want to stop it.
            ViewAdjustDone = true;
        }
        else
        {
            if (debug)
            {
                Debug.Log("ViewAdjust: ResetOrigin: ignored, already active");
            }
        }
   }

    private IEnumerator _ResetOrigin()
    {
        //
        // First determine if we have a pointcloud representation.
        // If not things are easy.
        //
        ViewAdjustInProgress = true;
        ViewAdjustDone = false;
        IPointCloudPositionProvider pointCloudPipeline = null;
        if (pointCloudGO != null) pointCloudPipeline = pointCloudGO.GetComponentInChildren<IPointCloudPositionProvider>();

        if (teleportToOrigin)
        {
            player.transform.position = Vector3.zero;
            player.transform.rotation = Quaternion.identity;
        }
        yield return null;

        if (pointCloudPipeline == null)
        {
            // Nothing more to do.
            ViewAdjustDone = true;
        }
        else
        {
            //
            // We are a pointcloud.
            //
          
            // We start by resetting the cameraOffset to known values.
            cameraOffset.transform.localPosition = Vector3.zero;
            cameraOffset.transform.localRotation = Quaternion.identity;
            //
            // During calibration, we are going to move the camera once, to coincide with the
            // pointcloud center of gravity. We undo this move later, before recording calibration
            // values.
            //
            bool tempCameraMoveDone = false;
            Vector3 tempCameraOffset = Vector3.zero;
            float tempCameraYRotation = 0;

            //
            // Now we want to ensure that a camera Y angle of 0 (note: camera, not cameraOffset)
            // corresponds to t world angle of 0 (note: world, not player).
            // We do this by rotating the player.
            //
            float cameraAngle = playerCamera.transform.rotation.eulerAngles.y;
            float playerAngle = player.transform.rotation.eulerAngles.y;
            //
            // Rotate the whole player so it is facing in the (virtual) world Z axis direction
            //
            cameraOffset.transform.Rotate(0, -cameraAngle - playerAngle, 0);

            // now instruct the user position correctly.
            int lastDistanceCm = -1;
            int lastDistanceSameCount = 0;
            while (!ViewAdjustDone)
            {
                Vector3? _pcPosition = pointCloudPipeline.GetPosition();
                if (_pcPosition == null)
                {
                    if (debug)
                    {
                        Debug.Log("ViewAdjust: Cannot determine point cloud position");
                    }
                    ShowPositionIndicator(stage: "No Point Cloud", instructions:
                        "You are not seen by the cameras. Please move into the capture area"
                        );
                    lastDistanceCm = -1;
                    lastDistanceSameCount = 0;
                    yield return new WaitForSeconds(0.3f);
                    continue;
                }
                //
                // We apparently have captured a pointcloud.
                //

                Vector3 pcLocalPosition = (Vector3)_pcPosition;
                Vector3 pcPosition = pointCloudGO.transform.TransformPoint(pcLocalPosition);
                
                if (pointCloudCenterOfGravityIndicator != null)
                {
                    pointCloudCenterOfGravityIndicator.position = pcPosition;
                }
                // xxxjack the next line depends on PointCloudGO having an identity transform
                tempCameraOffset = pcPosition - playerCamera.transform.position;
                tempCameraYRotation = (player.transform.rotation.eulerAngles.y - playerCamera.transform.rotation.eulerAngles.y);
                tempCameraOffset.y = 0;
                if (!tempCameraMoveDone || adjustCameraContinuous)
                {
                    // We move the camera to the first point cloud position. We do this only 
                    // once otherwise it will induce motion sickness.
                    tempCameraMoveDone = true;
                    cameraOffset.transform.position += tempCameraOffset;
                    cameraOffset.transform.Rotate(0, tempCameraYRotation, 0);
                }
               
                // Finally we tell the user how far and where they should move
                float distance = pcLocalPosition.magnitude;
                int distanceCm = (int)(distance * 100);

                float angle = Vector3.SignedAngle(Vector3.forward, pcLocalPosition, Vector3.up) - 180;
                int dir = (int)(angle / 30);
                if (dir <= 0) dir += 12;

                ShowPositionIndicator(stage: "Adjust Position",
                    instructions:
                    $"First rotate your body until it aligns.\n\nThen move {distanceCm} cm in the {dir} o'clock direction.\n\nFinally look down and align your feet.",
                    floor:
                    $"You are {distanceCm} cm away from where you should be."
                    );
                // Check whether the user has been standing still in a reasonable position for 1 second.
                if (distanceCm == lastDistanceCm && distanceCm < 5)
                {
                    lastDistanceSameCount++;
                    if (lastDistanceSameCount > 10)
                    {
                        ViewAdjustDone = true;
                    }
                }
                else
                {
                    lastDistanceSameCount = 0;
                    lastDistanceCm = distanceCm;
                }
                yield return new WaitForSeconds(0.1f);
            }
            cameraOffset.transform.position -= tempCameraOffset;
            cameraOffset.transform.Rotate(0, -tempCameraYRotation, 0);

        }

        if (BeginLocomotion())
        {
            Debug.Log("ViewAdjust: ResetOrigin");
            // Rotation of camera relative to the player
            float cameraToPlayerRotationY = playerCamera.transform.rotation.eulerAngles.y - player.transform.rotation.eulerAngles.y;
            if (debug) Debug.Log($"ViewAdjust: camera rotation={cameraToPlayerRotationY}");
            // Apply the inverse rotation to cameraOffset to make the camera point in the same direction as the player
            cameraOffset.transform.Rotate(0, -cameraToPlayerRotationY, 0);
            // Next set correct position on the camera
            Vector3 moveXZ = playerCamera.transform.position - player.transform.position;
            bool resetHeightWithPosition = XRSettings.enabled && XRSettings.isDeviceActive;
            if (debug)
            {
                Debug.Log($"ViewAdjust: resetHeightWithPosition={resetHeightWithPosition}");
            }
            if (resetHeightWithPosition)
            {
                moveXZ.y = cameraOffset.transform.position.y-player.transform.position.y;
            }
            else
            {
                moveXZ.y = 0;
            }
            cameraOffset.transform.position -= moveXZ;
            if (debug) Debug.Log($"ResetOrigin: moved cameraOffset by {-moveXZ} to worldpos={playerCamera.transform.position}");
            // See how much we have to move the camera forward, so it isn't at the center of the
            // head but at approximately eye position.
            float forwardMove = cameraFudgeFactor;
            if (pointCloudPipeline != null && pointCloudPipeline.GetCameraCount() == 1)
            {
                // For single-camera we add an extra factor (because the center of gravity of the point cloud
                // isn't the center of gravity of the user).
                forwardMove += singleCameraExtraFudgeFactor;
            }
            if (forwardMove != 0)
            {
                Vector3 fudgeVector = playerCamera.transform.forward * forwardMove;
                cameraOffset.transform.position += fudgeVector;
            }
            viewAdjusted.Invoke();
            EndLocomotion();
        }
        ShowPositionIndicator(stage: "");
        ViewAdjustInProgress = false;
        ViewAdjustDone = false;
    }

    public void HigherView(float deltaHeight = 0.02f)
    {
        ShowPositionIndicator();
        if (deltaHeight != 0 && BeginLocomotion())
        {
            ShowPositionIndicator();
            cameraOffset.transform.position += new Vector3(0, deltaHeight, 0);
            if (debug) Debug.Log($"ViewAdjust: new height={cameraOffset.transform.position.y}");
            viewAdjusted.Invoke();
            EndLocomotion();
        }
    }

    public void LowerView()
    {
        HigherView(-0.02f);
    }

    protected void OnEnable()
    {
        m_ViewHeightAction.EnableDirectAction();
        m_resetOriginAction.EnableDirectAction();
    }

    protected void OnDisable()
    {
        m_ViewHeightAction.DisableDirectAction();
        m_resetOriginAction.DisableDirectAction();
    }
}
