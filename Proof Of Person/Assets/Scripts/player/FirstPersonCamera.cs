using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FirstPersonCamera : MonoBehaviour
{

    // variables
    public Transform player;
    public float mouseSensitivity = 2f;
    float cameraVerticalRotation = 0f;
    float cameraHorizontalRotation = 0f;

    bool lockedCursor = true;


    void Start()
    {
        // rock and hide the cursor
        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Locked;

    }

    
    void Update()
    {
        // collect mouse input
        float inputX = Input.GetAxis("Mouse X") * mouseSensitivity;
        float inputY = Input.GetAxis("Mouse Y") * mouseSensitivity;

        // rotate the camera around its local X axis (vertical look)
        cameraVerticalRotation -= inputY;
        cameraVerticalRotation = Mathf.Clamp(cameraVerticalRotation, -90f, 90f);

        // rotate the camera around its Y axis (horizontal look)
        cameraHorizontalRotation += inputX;

        // apply both rotations to the camera
        transform.localEulerAngles = new Vector3(cameraVerticalRotation, cameraHorizontalRotation, 0f);
    }
}
