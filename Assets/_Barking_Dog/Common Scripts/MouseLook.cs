using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public class MouseLook : MonoBehaviour
{
    [Header("Camera & Sensitivity")]
    public Transform cameraTransform;
    public float mouseSensitivity = 1.5f;
    public float verticalLookLimit = 85f;

    [Header("Movement")]
    public float moveSpeed = 5f;

    private Vector2 lookInput;
    private Vector2 moveInput;
    private float xRotation;

    private CharacterController controller;

    void Awake()
    {
        if (cameraTransform == null)
        {
            Debug.LogError("cameraTransform não está atribuído no MouseLook! Desabilitando script.");
            enabled = false;
            return;
        }

        controller = GetComponent<CharacterController>();

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void Update()
    {
        HandleInput();
        HandleLook();
        HandleMove();
    }

    void HandleInput()
    {
        if (Mouse.current != null)
        {
            lookInput = Mouse.current.delta.ReadValue() * mouseSensitivity * Time.deltaTime;
        }

        if (Keyboard.current != null)
        {
            moveInput = Vector2.zero;
            if (Keyboard.current.wKey.isPressed) moveInput.y += 1;
            if (Keyboard.current.sKey.isPressed) moveInput.y -= 1;
            if (Keyboard.current.aKey.isPressed) moveInput.x -= 1;
            if (Keyboard.current.dKey.isPressed) moveInput.x += 1;
            moveInput = Vector2.ClampMagnitude(moveInput, 1);
        }
    }

    void HandleLook()
    {
        xRotation -= lookInput.y;
        xRotation = Mathf.Clamp(xRotation, -verticalLookLimit, verticalLookLimit);

        cameraTransform.localRotation = Quaternion.Euler(xRotation, 0f, 0f);
        transform.Rotate(Vector3.up * lookInput.x);
    }

    void HandleMove()
    {
        Vector3 direction = transform.right * moveInput.x + transform.forward * moveInput.y;
        controller.Move(direction * moveSpeed * Time.deltaTime);
    }
}
