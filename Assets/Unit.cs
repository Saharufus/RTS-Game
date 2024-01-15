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
    public float rotationAnglePerSec;
    public Material selectedMaterial;
    public Material notSelectedMaterial;
    public bool isEffectedBySlope;
    UnitSelection unitSelection;
    bool shouldMove = false;
    int unitId;
    public bool drawDirections;
    bool unitIsSelected = false;
    Rigidbody unitBody;
    bool gameStarted = false;
    float minStopMovingRadius;
    Vector3 lastRecordedPos;
    float timeFromLastRecordedPos = 0;
    public float timeToRecordPos;

    void Start()
    {
        unitSelection = gameManager.GetComponent<UnitSelection>();
        unitId = gameObject.GetInstanceID();
        pathFind = gameManager.GetComponent<PathFind>();
        unitBody = gameObject.GetComponent<Rigidbody>();
        unitBody.constraints = RigidbodyConstraints.FreezeAll;
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
        Vector3 moveVector = pathFind.directionGridsDict[unitId][rowCol[0], rowCol[1]];

        // move unit
        unitBody.velocity = moveVector * movingSpeedPixelPerSecond * pathFind.speedModifierGrid[rowCol[0], rowCol[1]];

        if (distFromLastRecordedPos <= minStopMovingRadius)
        {
            // delete the direction grid of this unit
            shouldMove = false;
            unitBody.constraints = RigidbodyConstraints.FreezeAll;
            pathFind.directionGridsDict.Remove(unitId);
        }
        else
        {
            // rotate to diraction
            transform.LookAt(transform.position + moveVector);
            // transform.rotation = Quaternion.Euler(0, Mathf.Asin(internalMoveVector.x/internalMoveVector.magnitude) * rotationAnglePerSec * Time.deltaTime, 0) * transform.rotation;
            
            // make orthogonal to the ground *** not working correctly
            if (isEffectedBySlope)
            {
                float dydx = (pathFind.positionGrid[rowCol[0], rowCol[1] + Mathf.RoundToInt(moveVector.x)].y - pathFind.positionGrid[rowCol[0], rowCol[1]].y) / pathFind.stepSize;
                float dydz = (pathFind.positionGrid[rowCol[0] + Mathf.RoundToInt(moveVector.z), rowCol[1]].y - pathFind.positionGrid[rowCol[0], rowCol[1]].y) / pathFind.stepSize;
                float slopeAngleX = Mathf.Atan(dydx) * 180 / Mathf.PI;
                float slopeAngleZ = Mathf.Atan(dydz) * 180 / Mathf.PI;
                if (Double.IsNaN(slopeAngleX))
                {
                    slopeAngleX = 0;
                }
                if (Double.IsNaN(slopeAngleZ))
                {
                    slopeAngleZ = 0;
                }
                Vector3 orthogonalVector = Quaternion.AngleAxis(slopeAngleX, Vector3.forward) * Quaternion.AngleAxis(slopeAngleZ, Vector3.right) * Vector3.up;
                
                transform.rotation = Quaternion.Euler(100*Vector3.Cross(orthogonalVector, -transform.up)) * transform.rotation;
            }
        }
    }

    List<Vector3> GetFriendlyUnitDetectionAdjustments()
    {
        List<Vector3> vectorsToAdjust = new();
        float minDistFromFriend = Mathf.Max(transform.localScale.x, transform.localScale.z);

        for (int i = 0; i < 16; i++)
        {
            Vector3 directionToCheck = Quaternion.Euler(0, 22.5f*i, 0) * Vector3.forward;
            if (Physics.Raycast(transform.position + (minDistFromFriend/2 * directionToCheck), directionToCheck, out RaycastHit hit, 0.5f, 1 << gameObject.layer))
            {
                float distFromFriend = (hit.point - transform.position).magnitude;
                vectorsToAdjust.Add((transform.position - hit.point) * (minDistFromFriend / distFromFriend));
            }
        }

        return vectorsToAdjust;
    }

    void AdjustDistFromGround(int row, int col)
    {
        transform.position = new Vector3(transform.position.x, (pathFind.positionGrid[row, col].y + transform.localScale.y / 2), transform.position.z);
    }
    void AdjustNormalToGround()
    {

    }

    void OnDrawGizmos()
    {
        if (drawDirections && gameStarted)
        {
            for (int row = 0; row < pathFind.rows; row++)
            {
                for (int col = 0; col < pathFind.cols; col++)
                {
                    if (shouldMove && pathFind.directionGridsDict[unitId][row, col] != Vector3.zero)
                    {
                        Gizmos.DrawRay(pathFind.positionGrid[row, col], pathFind.directionGridsDict[unitId][row, col]);
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
