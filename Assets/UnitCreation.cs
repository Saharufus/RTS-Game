using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class UnitCreation : MonoBehaviour
{
    public GameObject gamemanager;
    public Mesh unitMesh;
    public Material selectedMaterial;
    public Material notSelectedMaterial;

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.K))
        {
            CreateUnit(1);
        }
    }

    void CreateUnit(int unitTypeId)
    {
        // string unitName = unitNameDict[unitTypeId];
        string unitName = "unit";
        // Type colliderType = unitColliderDict[unitTypeId];
        Type colliderType = typeof(CapsuleCollider);
        GameObject unit = new GameObject(unitName, colliderType, typeof(MeshFilter), typeof(MeshRenderer));

        unit.transform.position = new Vector3(transform.position.x + transform.localScale.x / 2 + unit.transform.localScale.x / 2, unit.transform.localScale.y / 2, transform.position.z);

        unit.layer = 6;
        unit.tag = "Unit";

        Unit unitScript = unit.AddComponent<Unit>();
        unitScript.gameManager = gamemanager;
        unitScript.movingSpeedPixelPerSecond = 20;
        unitScript.selectedMaterial = selectedMaterial;
        unitScript.notSelectedMaterial = notSelectedMaterial;
        unitScript.rotationAnglePerSec = 1440;
        unitScript.timeToRecordPos = 0.5f;

        Rigidbody unitRigidBody = unit.AddComponent<Rigidbody>();
        unitRigidBody.constraints = RigidbodyConstraints.FreezeAll;

        CapsuleCollider unitCollider = unit.GetComponent<CapsuleCollider>();
        unitCollider.radius = 0.7f;

        unit.GetComponent<MeshFilter>().mesh = unitMesh;
    }
}
