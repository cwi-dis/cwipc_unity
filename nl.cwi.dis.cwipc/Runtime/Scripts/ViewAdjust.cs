using Cwipc;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Inputs;

public class ViewAdjust : LocomotionProvider
{
    [Serializable]
    public enum ViewAdjustStage
    {
        idle,
        position,
        orientation,
        done
    };

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

    [Tooltip("How many meters forward the camera should be positioned relative to player origin")]
    [SerializeField] float cameraZFudgeFactor = 0;

    [Tooltip("How many meters forward the center of gravity of the point cloud should be moved for single camera capturers")]
    [SerializeField] float singleCameraCoGForwardMove = 0.05f;

    [Tooltip("Multiplication factor for height adjustment")]
    [SerializeField] float heightFactor = 1;

    [Tooltip("Reset viewpoint height when resetting position (otherwise height is untouched)")]
    [SerializeField] bool resetHeightWithPosition = false;

    [Tooltip("Callback done after view has been adjusted")]
    public UnityEvent viewAdjusted;

    [Tooltip("The Input System Action that will be used to change view height. Must be a Value Vector2 Control of which y is used.")]
    [SerializeField] InputActionProperty m_ViewHeightAction;

    [Tooltip("Use Reset Origin action. Unset if ResetOrigin() is called from a script.")]
    [SerializeField] bool useResetOriginAction = true;

    [Tooltip("The Input System Action that will be used to reset view origin.")]
    [SerializeField] InputActionProperty m_resetOriginAction;

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

    [SerializeField] ViewAdjustStage stage = ViewAdjustStage.idle;

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
        if (useResetOriginAction && m_resetOriginAction != null)
        {
            bool doResetOrigin = m_resetOriginAction.action.ReadValue<float>() >= 0.5;
            if (doResetOrigin)
            {
                ResetOrigin(false);
            }
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
        if (stage == ViewAdjustStage.idle)
        {
            // If we are not adjusting the view we start adjusting the view
            StartCoroutine(_ResetOrigin());
        }
        else if (canStop)
        {
            // If we are already adjusting the view we might want to stop it.
            stage = ViewAdjustStage.done;
        }
   }

    private IEnumerator _ResetOrigin()
    {
        //
        // First determine if we have a pointcloud representation.
        // If not things are easy.
        //
        IPointCloudPositionProvider pointCloudPipeline = null;
        if (pointCloudGO != null) pointCloudPipeline = pointCloudGO.GetComponentInChildren<IPointCloudPositionProvider>();
        
        yield return null;

        if (pointCloudPipeline == null)
        {
            // Show the position indicator and reset the view point immedeately. 
            ShowPositionIndicator(stage: "Fixing...");
            stage = ViewAdjustStage.done;
        }
        else
        {
            // We are a pointcloud.
            // We start by resetting the cameraOffset to known values (for height)
#if xxxjack_bad_idea
            cameraOffset.transform.Rotate(0, -cameraOffset.transform.localRotation.y, 0);
            cameraOffset.transform.Rotate(0, -playerCamera.transform.localRotation.y, 0);
#endif
            cameraOffset.transform.Translate(0, -cameraOffset.transform.localPosition.y, 0);
            //
            // Now we want to ensure that a camera Y angle of 0 (note: camera, not cameraOffset)
            // corresponds to t world angle of 0 (note: world, not player).
            // We do this by rotating the player.
            //
            float cameraAngle = playerCamera.transform.rotation.eulerAngles.y;
            float playerAngle = player.transform.rotation.eulerAngles.y;
            player.transform.Rotate(0, -cameraAngle - playerAngle, 0);

            // now instruct the user position correctly.
            stage = ViewAdjustStage.position;
            int cameraCount = -1;
            int lastDistanceCm = -1;
            int lastDistanceSameCount = 0;
            while (stage == ViewAdjustStage.position)
            {
                Vector3? _pcPosition = pointCloudPipeline.GetPosition();
                if (_pcPosition == null)
                {
                    ShowPositionIndicator(stage: "No Point Cloud", instructions:
                        "You are not seen by the cameras. Please move into the capture area"
                        );
                    lastDistanceCm = -1;
                    lastDistanceSameCount = 0;
                    yield return new WaitForSeconds(0.3f);
                    continue;
                }
                else
                {
                    Vector3 pcPosition = (Vector3) _pcPosition;
                    if (cameraCount < 0) {
                        // Determine number of cameras.
                        cameraCount = pointCloudPipeline.GetCameraCount();
                    }
                    if (cameraCount == 1)
                    {
                        // For a single-camera setup (and note only if we are sure: we don't do
                        // this for cameraCount == 0) we move the center of gravity, because we
                        // expect to capture only half a person.
                        pcPosition.z += singleCameraCoGForwardMove;
                    }
                    if (pointCloudCenterOfGravityIndicator != null)
                    {
                        pointCloudCenterOfGravityIndicator.localPosition = pcPosition;
                    }
                    float distance = pcPosition.magnitude;
                    int distanceCm = (int)(distance * 100);

                    float angle = Vector3.Angle(-Vector3.forward, pcPosition);
                    int dir = (int)(angle / 30);
                    if (dir <= 0) dir += 12; 

                    ShowPositionIndicator(stage: "Adjust Position",
                        instructions:
                        $"Look down, position your body on the cartoon feet.\n\nYou are {distanceCm} cm away from where you should be.\nMove in the {dir} o'clock direction.",
                        floor:
                        $"You are {distanceCm} cm away from where you should be."
                        );
                    // Check whether the user has been standing still in a reasonable position for 2 seconds.
                    if (distanceCm == lastDistanceCm && distanceCm < 5)
                    {
                        lastDistanceSameCount++;
                        if (lastDistanceSameCount > 4)
                        {
                            stage = ViewAdjustStage.orientation;
                        }
                    }
                    else
                    {
                        lastDistanceSameCount = 0;
                        lastDistanceCm = distanceCm;
                    }
                    yield return new WaitForSeconds(0.3f);
                }
            }
#if xxxjack_bad_idea
            // Show the countdown
            ShowPositionIndicator(stage: "Adjust Orientation", instructions: "To be provided");
            yield return new WaitForSeconds(3);
#endif
            stage = ViewAdjustStage.done;
            
        }

        if (BeginLocomotion())
        {
            Debug.Log("ViewAdjust: ResetOrigin");
            // Rotation of camera relative to the player
            float cameraToPlayerRotationY = playerCamera.transform.rotation.eulerAngles.y - player.transform.rotation.eulerAngles.y;
            if (debug) Debug.Log($"ViewAdjust: camera rotation={cameraToPlayerRotationY}");
            // Apply the inverse rotation to cameraOffset to make the camera point in the same direction as the player
            cameraOffset.transform.Rotate(0, -cameraToPlayerRotationY, 0);
#if xxxjack_bad_idea
            if (pointCloudPipeline != null)
            {
                // Now the camera is pointing forward from the users point of view.
                // Rotate the point cloud so it is in the same direction.
                float cameraToPointcloudRotationY = playerCamera.transform.rotation.eulerAngles.y - pointCloudGO.transform.rotation.eulerAngles.y;
                if (debug) Debug.Log($"ViewAdjust: pointcloud rotation={cameraToPointcloudRotationY}");
                pointCloudGO.transform.Rotate(0, cameraToPointcloudRotationY, 0);
            }
#endif
            // Next set correct position on the camera
            Vector3 moveXZ = playerCamera.transform.position - player.transform.position;
            moveXZ.z += cameraZFudgeFactor;
            if (resetHeightWithPosition)
            {
                moveXZ.y = cameraOffset.transform.position.y;
            }
            else
            {
                moveXZ.y = 0;
            }
            if (debug) Debug.Log($"ResetOrigin: move cameraOffset by {moveXZ} to worldpos={playerCamera.transform.position}");
            cameraOffset.transform.position -= moveXZ;
#if xxxjack_bad_idea
            // Finally adjust the pointcloud position
            if (pointCloudPipeline != null)
            {
                Vector3 pcOriginLocal = pointCloudPipeline.GetPosition();
                if (debug) Debug.Log($"ViewAdjust: adjust pointcloud to {pcOriginLocal}");
                pointCloudGO.transform.localPosition = -pcOriginLocal;
               
            }
#endif
            viewAdjusted.Invoke();
            EndLocomotion();
        }
        ShowPositionIndicator(stage: "");
        stage = ViewAdjustStage.idle;
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
        if (useResetOriginAction) m_resetOriginAction.EnableDirectAction();
    }

    protected void OnDisable()
    {
        m_ViewHeightAction.DisableDirectAction();
        if (useResetOriginAction) m_resetOriginAction.DisableDirectAction();
    }
}
