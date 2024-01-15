using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

public class UnitSelection : MonoBehaviour
{
    public Dictionary<int, GameObject> selectedDict = new();
    Camera mainCam;
    Vector3 boxInitialCornerPos;
    Vector3 boxEndCornerPos;
    public float boxMinRadius;
    bool isBoxSelecting = false;
    public LayerMask groundLayerMask;
    public LayerMask unitLayerMask;
    RaycastHit hit;
    Ray[] boxRays;
    bool selected = false;
    Vector3[] rayGroundHitPonts = new Vector3[4];
    MeshCollider selectionBox;
    void Start()
    {
        mainCam = gameObject.GetComponent<MainObjects>().mainCam;
        
    }

    void Update()
    {
        if (Input.GetMouseButtonDown(0))
        {
            boxInitialCornerPos = Input.mousePosition;
        }
        if (Input.GetMouseButton(0))
        {
            float boxRadius = (boxInitialCornerPos - Input.mousePosition).magnitude;
            isBoxSelecting = boxRadius >= boxMinRadius;
        }
        if (Input.GetMouseButtonUp(0))
        {
            boxEndCornerPos = Input.mousePosition;
            if (isBoxSelecting)
            {
                BoxSelectUnits();
                isBoxSelecting = false;
            }
            else
            {
                SelectUnits();
            }
        }
    }

    void SelectUnits()
    {
        Ray cameraToScreenRay = mainCam.ScreenPointToRay(Input.mousePosition);
        if (Physics.Raycast(cameraToScreenRay, out RaycastHit hit))
        {
            if (hit.transform.tag == "Unit")
            {
                if (!Input.GetKey(KeyCode.LeftShift))
                {
                    ClearSelectedDict();
                }
                UpdateSelectedDict(hit.transform.gameObject);
            }
            else if (!Input.GetKey(KeyCode.LeftShift))
            {
                ClearSelectedDict();
            }
        }
    }

    void BoxSelectUnits()
    {
        selected = true;
        if (!Input.GetKey(KeyCode.LeftShift))
        {
            ClearSelectedDict();
        }
        
        Vector3 boxCorener0 = new Vector3(Mathf.Min(boxInitialCornerPos.x, boxEndCornerPos.x), Mathf.Max(boxInitialCornerPos.y, boxEndCornerPos.y), 0); // top left
        Vector3 boxCorener1 = new Vector3(Mathf.Max(boxInitialCornerPos.x, boxEndCornerPos.x), Mathf.Max(boxInitialCornerPos.y, boxEndCornerPos.y), 0); // top right
        Vector3 boxCorener2 = new Vector3(Mathf.Min(boxInitialCornerPos.x, boxEndCornerPos.x), Mathf.Min(boxInitialCornerPos.y, boxEndCornerPos.y), 0); // bottom left
        Vector3 boxCorener3 = new Vector3(Mathf.Max(boxInitialCornerPos.x, boxEndCornerPos.x), Mathf.Min(boxInitialCornerPos.y, boxEndCornerPos.y), 0); // bottom right

        boxRays = new Ray[4]{mainCam.ScreenPointToRay(boxCorener0),
                             mainCam.ScreenPointToRay(boxCorener1),
                             mainCam.ScreenPointToRay(boxCorener2),
                             mainCam.ScreenPointToRay(boxCorener3)};

        Vector3[] boxVertices = new Vector3[8];
        for (int i = 0; i < 4; i++)
        {
            if (Physics.Raycast(boxRays[i], out hit, Mathf.Infinity, groundLayerMask))
            {
                Vector3 c1 = new(hit.point.x, -30, hit.point.z);
                Vector3 c2 = new(hit.point.x, 30, hit.point.z);
                rayGroundHitPonts[i] = hit.point;
                boxVertices[i] = c1;
                boxVertices[i + 4] = c2;
            }
        }
        
        selectionBox = gameObject.AddComponent<MeshCollider>();
        Mesh selectionMesh = new Mesh
        {
            vertices = boxVertices,
            triangles = new int[]{0, 1, 2, 2, 1, 3, // bottom
                                  4, 6, 0, 0, 6, 2, // left
                                  6, 7, 2, 2, 7, 3, // front
                                  7, 5, 3, 3, 5, 1, // right
                                  5, 4, 1, 1, 4 ,0, // back
                                  4, 5, 6, 6, 5, 7}// top
        };
        selectionBox.sharedMesh = selectionMesh;
        selectionBox.includeLayers = unitLayerMask;
        selectionBox.convex = true;
        selectionBox.isTrigger = true;
        Destroy(selectionBox, 0.1f);
    }

    void ClearSelectedDict()
    { 
        selectedDict.Clear();
    }

    void UpdateSelectedDict(GameObject unit)
    {
        if (!selectedDict.ContainsValue(unit))
        {
            selectedDict.Add(unit.GetInstanceID(), unit);
        }
    }

    void OnTriggerEnter(Collider unit)
    {
        UpdateSelectedDict(unit.gameObject);
    }

    void OnDrawGizmos()
    {
        if (selected)
        {
            Gizmos.color = Color.blue;
            foreach (Vector3 p in rayGroundHitPonts)
            {
                Gizmos.DrawRay(mainCam.transform.position, p - mainCam.transform.position);
            }
        }
    }
}

