using UnityEngine;
using UnityEngine.InputSystem;

/*
 * This file adds controls for moving the ground truth drone objects
 */
public class GroundTruthController : MonoBehaviour, IMotionLimiter
{
    [Header("Drone Identity")]
    public int droneId = 0;

    [Header("Movement Settings")]
    public float moveSpeed = 1.5f;
    public float rotateSpeed = 90f;

    [Header("Collision Avoidance")]
    [Range(0f, 1f)]
    [SerializeField] private float speedScale = 1.0f;

    private MotionAxisMask axisMask = new MotionAxisMask
    {
        allowX = true,
        allowY = true,
        allowZ = true,
        allowYaw = true
    };
    private MotionRejection rejection;


    private Vector2 moveInput;
    private float yawInput;
    private float verticalInput;

    void Update()
    {
        ReadInput();
        ApplyMovement();
    }

    private void ReadInput()
    {
        if (Keyboard.current == null)
            return;

        // --- Drone 0 Controls (WASD) ---
        if (droneId == 0)
        {
            moveInput = Vector2.zero;
            yawInput = 0f;
            verticalInput = 0f;

            if (Keyboard.current.wKey.isPressed) moveInput.y += 1f;
            if (Keyboard.current.sKey.isPressed) moveInput.y -= 1f;
            if (Keyboard.current.dKey.isPressed) moveInput.x += 1f;
            if (Keyboard.current.aKey.isPressed) moveInput.x -= 1f;

            if (Keyboard.current.eKey.isPressed) yawInput += 1f;
            if (Keyboard.current.qKey.isPressed) yawInput -= 1f;

            if (Keyboard.current.rKey.isPressed) verticalInput += 1f;
            if (Keyboard.current.fKey.isPressed) verticalInput -= 1f;

        }

        // --- Drone 1 Controls (IJKL) ---
        if (droneId == 1)
        {
            moveInput = Vector2.zero;
            yawInput = 0f;
            verticalInput = 0f;

            if (Keyboard.current.iKey.isPressed) moveInput.y += 1f;
            if (Keyboard.current.kKey.isPressed) moveInput.y -= 1f;
            if (Keyboard.current.lKey.isPressed) moveInput.x += 1f;
            if (Keyboard.current.jKey.isPressed) moveInput.x -= 1f;

            if (Keyboard.current.oKey.isPressed) yawInput += 1f;
            if (Keyboard.current.uKey.isPressed) yawInput -= 1f;

            if (Keyboard.current.yKey.isPressed) verticalInput += 1f;
            if (Keyboard.current.hKey.isPressed) verticalInput -= 1f;

        }
    }

    private void ApplyMovement()
    {
        //Expressed in local frame
        Vector3 movement = transform.forward * moveInput.y + transform.right * moveInput.x + transform.up * verticalInput;

        //DOF contraints
        if (!axisMask.allowX) movement.x = 0f;
        if (!axisMask.allowY) movement.y = 0f;
        if (!axisMask.allowZ) movement.z = 0f;
        float yaw = axisMask.allowYaw ? yawInput : 0f;

        //Puts a hard contraint/stop towards movement in the direction of the other drone if close enough
        if (rejection.active && movement.sqrMagnitude > 1e-6f)
        {
            Vector3 dir = movement.normalized;
            float toward = Vector3.Dot(dir, rejection.worldNormal);

            if (toward > 0f)
            {
                Vector3 blocked = rejection.worldNormal * toward;
                Vector3 safeDir = dir - blocked;

                if (safeDir.sqrMagnitude > 1e-6f)
                    movement = safeDir.normalized * movement.magnitude;
                else
                    movement = Vector3.zero;
            }
        }

        transform.position += movement * moveSpeed * speedScale * Time.deltaTime;
        transform.rotation *= Quaternion.Euler(0f, yawInput * rotateSpeed * Time.deltaTime, 0f);

        //Debug.Log(
        //    $"[GroundTruth {droneId}] Pos={transform.position.ToString("F2")} RotY={transform.eulerAngles.y:F1} SpeedScale={speedScale:F2}"
        //);
    }

    //Sets the speed of the drone to be within a valid range
    public void SetSpeedScale(float scale)
    {
        speedScale = Mathf.Clamp01(scale);
        Debug.Log($"[LimiterApply] GroundTruth {droneId} speedScale set => {speedScale:F2}");
    }

    public void SetAxisMask(MotionAxisMask mask)
    {
        axisMask = mask;
    }

    public void SetMotionRejection(MotionRejection rej)
    {
        rejection = rej;
    }


}
