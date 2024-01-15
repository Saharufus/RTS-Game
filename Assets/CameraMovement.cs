using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CameraMovement : MonoBehaviour
{
    public float movementSpeed;
    public float zoomingSpeed;
    public float maxHight;
    public float minHight;
    public bool edgeScrol;
    
    void Start()
    {
        maxHight = transform.position.y;
    }

    void Update()
    {
        
        MoveCamera();

        if (Input.GetKeyDown(KeyCode.R))
        {
            ResetCameraPosition();
        }
    }

    void ResetCameraPosition()
    {
        transform.position = new Vector3(0, maxHight, 0);
    }

    void MoveCamera()
    {
        // plain movement
        Vector3 horizontalMovement = Input.GetAxis("Horizontal") * Vector3.right;
        Vector3 verticalMovement = Input.GetAxis("Vertical") * Vector3.forward;
        if (edgeScrol)
        {
            if (Input.mousePosition.y >= Screen.height)
            {
                verticalMovement = Vector3.forward;
            }
            if (Input.mousePosition.y <= 0)
            {
                verticalMovement = Vector3.back;
            }
            if (Input.mousePosition.x >= Screen.width)
            {
                horizontalMovement = Vector3.right;
            }
            if (Input.mousePosition.x <= 0)
            {
                horizontalMovement = Vector3.left;
            }
        }

        Vector3 movementVector = horizontalMovement + verticalMovement;
        // adjusst to camera tilt
        movementVector = Quaternion.Euler(-transform.eulerAngles.x, 0, 0) * movementVector * movementSpeed;

        // zoom
        Vector3 zoom = Input.mouseScrollDelta.y * Vector3.forward * zoomingSpeed;
        float newHight = transform.position.y + Input.mouseScrollDelta.y*zoomingSpeed*Mathf.Sin(transform.eulerAngles.x);
        if (newHight <= maxHight && newHight >= minHight)
        {
            movementVector += zoom;
        }

        // move camera
        transform.Translate(movementVector);

        // fix if overshot zoom
        if (transform.position.y > maxHight)
        {
            transform.position = new Vector3(transform.position.x, maxHight, transform.position.z);
        }
        if (transform.position.y < minHight)
        {
            transform.position = new Vector3(transform.position.x, minHight, transform.position.z);
        }
    }
}
