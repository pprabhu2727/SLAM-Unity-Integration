using UnityEngine;
using UnityEngine.InputSystem;

public class GroundTruthController : MonoBehaviour
{
    [Header("Drone Identity")]
    public int droneId = 0;

    [Header("Movement Settings")]
    public float moveSpeed = 1.5f;
    public float rotateSpeed = 90f;

    private Vector2 moveInput;
    private float yawInput;

    void Update()
    {
        ReadInput();
        ApplyMovement();
    }

    private void ReadInput()
    {
        if (Keyboard.current == null)
            return;

        // --- Drone 0 Controls ---
        if (droneId == 0)
        {
            moveInput = Vector2.zero;
            yawInput = 0f;

            if (Keyboard.current.wKey.isPressed) moveInput.y += 1f;
            if (Keyboard.current.sKey.isPressed) moveInput.y -= 1f;
            if (Keyboard.current.dKey.isPressed) moveInput.x += 1f;
            if (Keyboard.current.aKey.isPressed) moveInput.x -= 1f;

            if (Keyboard.current.eKey.isPressed) yawInput += 1f;
            if (Keyboard.current.qKey.isPressed) yawInput -= 1f;
        }

        // --- Drone 1 Controls ---
        if (droneId == 1)
        {
            moveInput = Vector2.zero;
            yawInput = 0f;

            if (Keyboard.current.iKey.isPressed) moveInput.y += 1f;
            if (Keyboard.current.kKey.isPressed) moveInput.y -= 1f;
            if (Keyboard.current.lKey.isPressed) moveInput.x += 1f;
            if (Keyboard.current.jKey.isPressed) moveInput.x -= 1f;

            if (Keyboard.current.oKey.isPressed) yawInput += 1f;
            if (Keyboard.current.uKey.isPressed) yawInput -= 1f;
        }
    }

    private void ApplyMovement()
    {
        Vector3 movement =
            transform.forward * moveInput.y +
            transform.right * moveInput.x;

        transform.position += movement * moveSpeed * Time.deltaTime;
        transform.rotation *= Quaternion.Euler(0f, yawInput * rotateSpeed * Time.deltaTime, 0f);

        Debug.Log(
            $"[GroundTruth {droneId}] Pos={transform.position.ToString("F2")} RotY={transform.eulerAngles.y:F1}"
        );
    }
}
