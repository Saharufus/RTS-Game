using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using Unity.VisualScripting;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

public class Unit : MonoBehaviour
{
    public GameObject gameManager;
    PathFind pathFind;
    public float movingSpeedPixelPerSecond;
    public Material selectedMaterial;
    public Material notSelectedMaterial;
    UnitSelection unitSelection;
    bool shouldMove = false;
    bool isMoving = false;
    int unitId;
    public bool drawDirections;
    bool unitIsSelected = false;
    Rigidbody unitBody;
    bool gameStarted = false;
    float minStopMovingRadius;
    Vector3 lastRecordedPos;
    float timeFromLastRecordedPos = 0;
    public float timeToRecordPos;
    int failsToMove = 0;
    Vector3 oldMoveVector = Vector3.zero;
    Vector3 moveVector = Vector3.zero;
    void Start()
    {
        unitSelection = gameManager.GetComponent<UnitSelection>();
        unitId = gameObject.GetInstanceID();
        pathFind = gameManager.GetComponent<PathFind>();
        unitBody = gameObject.GetComponent<Rigidbody>();
        unitBody.constraints = RigidbodyConstraints.FreezeRotation;// | RigidbodyConstraints.FreezePositionX | RigidbodyConstraints.FreezePositionZ;
        gameStarted = true;
        lastRecordedPos = transform.position;
        minStopMovingRadius = Mathf.Max(transform.localScale.x, transform.localScale.z) * 1.5f;
    }

    void Update()
    {
        unitIsSelected = unitSelection.selectedDict.ContainsValue(gameObject);
        if (unitIsSelected)
        {
            MeshRenderer renderer = GetComponent<MeshRenderer>();
            renderer.material = selectedMaterial;
        }
        else
        {
            MeshRenderer renderer = GetComponent<MeshRenderer>();
            renderer.material = notSelectedMaterial;
        }
        shouldMove = pathFind.directionGridsDict.ContainsKey(unitId);
        isMoving = unitBody.velocity != Vector3.zero;
        if (shouldMove)
        {
            MoveOnDirectionGrid();
        }
        else
        {
            timeFromLastRecordedPos = 0;
            lastRecordedPos = transform.position;
        }
    }

    void MoveOnDirectionGrid()
    {
        float distFromLastRecordedPos = minStopMovingRadius * 2;
        if (timeFromLastRecordedPos >= timeToRecordPos)
        {
            timeFromLastRecordedPos = 0;
            distFromLastRecordedPos = (transform.position - lastRecordedPos).magnitude;
            lastRecordedPos = transform.position;
        }
        timeFromLastRecordedPos += Time.deltaTime;

        unitBody.constraints = RigidbodyConstraints.FreezeRotation;
        int[] rowCol = pathFind.GetRowColFromPos(transform.position);
        Vector3 newMoveVector = GetMoveVector(rowCol[0], rowCol[1], pathFind.directionGridsDict[unitId]);//pathFind.directionGridsDict[unitId][rowCol[0], rowCol[1]];
        if (newMoveVector != moveVector)
        {
            oldMoveVector = moveVector;
            moveVector = newMoveVector;
        }

        // move unit
        unitBody.velocity = moveVector * movingSpeedPixelPerSecond * pathFind.cellsGrid[rowCol[0], rowCol[1]].SpeedModifier;

        if (moveVector == Vector3.zero || failsToMove >= 8)
        {
            // delete the direction grid of this unit
            shouldMove = false;
            unitBody.constraints = RigidbodyConstraints.FreezeRotation | RigidbodyConstraints.FreezePositionX | RigidbodyConstraints.FreezePositionZ;
            pathFind.directionGridsDict.Remove(unitId);
            failsToMove = 0;
            moveVector = Vector3.zero;
        }
        else if (distFromLastRecordedPos <= minStopMovingRadius)
        {
            unitBody.velocity = oldMoveVector * movingSpeedPixelPerSecond * pathFind.cellsGrid[rowCol[0], rowCol[1]].SpeedModifier;
            failsToMove++;
        }
        else
        {
            // rotate to diraction
            transform.LookAt(transform.position + moveVector);
        }
    }

    Vector3 GetMoveVector(int row, int col, Vector3[,] directionGrid)
    {
        Vector3 moveVector = Vector3.zero;
        for (int rowOffset = -1; rowOffset < 2; rowOffset++)
        {
            for (int colOffset = -1; colOffset < 2; colOffset++)
            {
                int newRow = row + rowOffset;
                int newCol = col + colOffset;
                if (newCol >= 0 && newCol < pathFind.Cols && newRow >= 0 && newRow <= pathFind.Rows && pathFind.cellsGrid[newRow, newCol].Walkable)
                {
                    moveVector += directionGrid[newRow, newCol];
                }
            }
        }
        return moveVector.normalized;
    }

    void OnCollisionEnter(Collision collision)
    {
        if (collision.transform.tag == transform.tag)
        {
            Debug.Log("HI!!!");
            unitBody.constraints = RigidbodyConstraints.FreezeRotation | RigidbodyConstraints.FreezePositionY;
        }
    }

    void OnCollisionExit(Collision collision)
    {
        if (collision.transform.tag == transform.tag)
        {
            Debug.Log("OK");
            unitBody.constraints = RigidbodyConstraints.FreezeRotation | RigidbodyConstraints.FreezePositionX | RigidbodyConstraints.FreezePositionZ;
        }
    }

    void OnDrawGizmos()
    {
        if (drawDirections && gameStarted)
        {
            for (int row = 0; row < pathFind.Rows; row++)
            {
                for (int col = 0; col < pathFind.Cols; col++)
                {
                    if (shouldMove && pathFind.directionGridsDict[unitId][row, col] != Vector3.zero)
                    {
                        Gizmos.DrawRay(pathFind.cellsGrid[row, col].Position, pathFind.directionGridsDict[unitId][row, col]);
                    }
                }
            }
        }
        if (unitIsSelected)
        {
            Gizmos.color = Color.green;
            for (int i = 0; i < 16; i++)
            {
                Gizmos.DrawRay(transform.position, transform.forward * 2);
            }
        }
    }
}
