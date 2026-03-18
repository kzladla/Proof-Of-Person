using System.Collections;
using System.Collections.Generic;
using UnityEngine;

//This Script handles the first person camera controls 
// It allows the player to look around using the mouse and locks the cursor to the center of the screen

public class FirstPersonCamera : MonoBehaviour
{
    [Header("Camera Settings")]

    // gets the player transform to rotate the camera around it
    [SerializeField] private Transform player;

    // how sensitive the mouse movement is for rotating the camera
    [SerializeField] private float mouseSensitivity = 2f;

    [SerializeField] private float cameraVerticalRotation = 0f;
    [SerializeField] private float cameraHorizontalRotation = 0f;

    [SerializeField] private bool lockedCursor = true;


    void Start()
    {
        // lock and hide the cursor
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
