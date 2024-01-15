using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using TMPro;
using Unity.VisualScripting;
using UnityEngine;

public class PathFind : MonoBehaviour
{
    MainObjects mainObjects;
    Dictionary<int, GameObject> selectedUnitsDictionary;
    Transform map;
    public LayerMask groundLayerMask;
    public float stepSize;
    public int rows;
    public int cols;
    public Vector3[,] positionGrid;
    Camera mainCam;
    int[,] costGrid;
    public bool[,] walkableGrid;
    public float[,] speedModifierGrid;
    public Dictionary<int, Vector3[,]> directionGridsDict;
    readonly Dictionary<string, int> groundTagWeights = new();
    readonly Dictionary<string, float> speedModifiersFromTags = new();
    public float maxSlope;
    float maxHightJump;
    public int extraPathFindBlocks;
    public int cellsToConvolute;
    public bool drawGrid;
    public bool useAdvancedDirFunc;

    void Start()
    {
        mainObjects = gameObject.GetComponent<MainObjects>();
        mainCam = mainObjects.mainCam;
        map = mainObjects.map;
        Vector3 mapSize = map.localScale;
        rows = Mathf.RoundToInt(mapSize.z / stepSize);
        cols = Mathf.RoundToInt(mapSize.x / stepSize);
        positionGrid = new Vector3[rows, cols];
        walkableGrid = new bool[rows, cols];
        costGrid = new int[rows, cols];
        speedModifierGrid = new float[rows, cols];
        maxHightJump = Mathf.Tan(maxSlope * Mathf.PI / 180) * stepSize;

        groundTagWeights.Add("Ground", 1);
        groundTagWeights.Add("Water", 3);
        groundTagWeights.Add("Unwalkable", 255);

        speedModifiersFromTags.Add("Ground", 1);
        speedModifiersFromTags.Add("Water", 0.6f);
        speedModifiersFromTags.Add("Unwalkable", 0);

        selectedUnitsDictionary = gameObject.GetComponent<UnitSelection>().selectedDict;

        directionGridsDict = new Dictionary<int, Vector3[,]>();

        GeneratePositionGrid();
    }
    void Update()
    {
        if (Input.GetMouseButtonUp(1))
        {
            Ray cameraToScreenRay = mainCam.ScreenPointToRay(Input.mousePosition);
            RaycastHit[] allHits = Physics.RaycastAll(cameraToScreenRay);
            Vector3 newPos = Vector3.zero;
            float highestCollHitY = -Mathf.Infinity;
            bool destinationFound = false;
            foreach (RaycastHit hit in allHits)
            {
                if (hit.transform.tag != "Unwalkable" && hit.point.y > highestCollHitY)
                {
                    newPos = hit.point;
                    highestCollHitY = newPos.y;
                    destinationFound = true;
                }
                else if (hit.transform.tag == "Unwalkable")
                {
                    destinationFound = false;
                    break;
                }
            }

            if (destinationFound)
            {
                int[] rowCol = GetRowColFromPos(newPos);
                GenerateDirectionGrid(rowCol[0], rowCol[1], newPos);
            }
        }
    }

    public int[] GetRowColFromPos(Vector3 pos)
    {
        int row = Mathf.RoundToInt((-pos.z + (map.position.z + map.localScale.z/2) - stepSize/2) / stepSize);
        if (row > rows)
        {
            row = rows - 1;
        }
        if (row < 0)
        {
            row = 0;
        }
        int col = Mathf.RoundToInt((pos.x - (map.position.x - map.localScale.x/2) - stepSize/2) / stepSize);
        if (col > rows)
        {
            col = cols - 1;
        }
        if (col < 0)
        {
            col = 0;
        }
        int[] rowCol = new int[2]{row, col};
        return rowCol;
    }

    public Vector3 GetPosFromRowCol(Vector2 rowCol)
    {
        float x = (stepSize / 2) + (rowCol.x * stepSize) - (map.localScale.x / 2);
        float z = (stepSize / 2) + (rowCol.y * stepSize) - (map.localScale.z / 2);
        return new Vector3(x, 100, z);
    }

    public Vector3 GetPosFromRowCol(int row, int col, int yOffset)
    {
        float x = (stepSize / 2) + (col * stepSize) - (map.localScale.x / 2);
        float z = -(stepSize / 2) - (row * stepSize) + (map.localScale.z / 2);
        return new Vector3(x, yOffset, z);
    }

    void GeneratePositionGrid()
    {
        for (int row = 0; row < rows; row++)
        {
            for (int col = 0; col < cols; col++)
            {
                Vector3 position = GetPosFromRowCol(row, col, 100);
                if (Physics.Raycast(position, Vector3.down, out RaycastHit hit, Mathf.Infinity, groundLayerMask))
                {
                    positionGrid[row, col] = hit.point;
                    string cellTag = hit.transform.tag;
                    costGrid[row, col] = groundTagWeights[cellTag];
                    speedModifierGrid[row, col] = speedModifiersFromTags[cellTag];

                    walkableGrid[row, col] = hit.transform.tag != "Unwalkable";
                }
            }
        }
    }

    float[,] GenerateIntegrationGrid(int destCellRow, int destCellCol)
    {
        float[,] integrationGrid = new float[rows, cols];
        for (int row = 0; row < rows; row++)
        {
            for (int col = 0; col < cols; col++)
            {
                integrationGrid[row, col] = 2047;
            }
        }

        List<int[]> cellsToCheck = new();
        integrationGrid[destCellRow, destCellCol] = 0;
        cellsToCheck.Add(new int[2]{destCellRow, destCellCol});

        while (cellsToCheck.Count > 0)
        {
            List<int[]> newCellsToCheck = new();
            foreach (int[] cellRowCol in cellsToCheck)
            {
                int row = cellRowCol[0];
                int col = cellRowCol[1];
                float integrationValue = integrationGrid[row, col];
                IntegrateNeighbors(row, col, integrationValue, integrationGrid, newCellsToCheck);
            }
            cellsToCheck = newCellsToCheck;
        }

        return integrationGrid;
    }

    void IntegrateNeighbors(int row, int col, float integrationValue, float[,] integrationGrid, List<int[]> cellsToCheck)
    {
        for (int rowOffset = -1; rowOffset < 2; rowOffset++)
        {
            for (int colOffset = -1; colOffset < 2; colOffset++)
            {
                int newRow = row + rowOffset;
                int newCol = col + colOffset;
                if (newCol >= 0 && newCol < cols && newRow >= 0 && newRow < rows && (rowOffset != 0 || colOffset != 0)) // in grid bounds and not the cell itself
                {
                    float distMod = 1;
                    if (Mathf.Abs(rowOffset) + Mathf.Abs(colOffset) == 2)
                    {
                        distMod = 1.4f;
                    }
                    float newIntegrationValue = integrationValue + (costGrid[newRow, newCol] * distMod);
                    float dy = Mathf.Abs(positionGrid[row, col].y - positionGrid[newRow, newCol].y);
                    if (newIntegrationValue < integrationGrid[newRow, newCol] && walkableGrid[newRow, newCol] && dy <= maxHightJump)
                    {
                        integrationGrid[newRow, newCol] = newIntegrationValue;
                        cellsToCheck.Add(new int[2]{newRow, newCol});
                    }
                }
            }
        }
    }

    public void GenerateDirectionGrid(int destRow, int destCol, Vector3 position)
    {
        int[] gridCorners = GetDirectionGridCorners(destRow, destCol);
        float[,] integrationGrid = GenerateIntegrationGrid(destRow, destCol);
        Vector3[,] directionGrid = new Vector3[rows, cols];

        int minRow = gridCorners[0];
        int maxRow = gridCorners[1];
        int minCol = gridCorners[2];
        int maxCol = gridCorners[3];

        for (int row = 0; row < cols; row++)
        {
            for (int col = 0; col < cols; col++)
            {
                if (row >= minRow && row <= maxRow && col >= minCol && col <= maxCol)
                {
                    if (integrationGrid[row, col] < 2000)
                    {
                        if (useAdvancedDirFunc)
                        {
                            directionGrid[row, col] = GetDirectionAdvanced(row, col, integrationGrid);
                        }
                        else
                        {
                            directionGrid[row, col] = GetDirection(row, col, integrationGrid);
                        }
                    }
                }
                else
                {
                    directionGrid[row, col] = Vector3.zero;
                }
            }
        }

        foreach (KeyValuePair<int, GameObject> keyValUnit in selectedUnitsDictionary)
        {
            if (directionGridsDict.ContainsKey(keyValUnit.Key))
            {
                directionGridsDict[keyValUnit.Key] = directionGrid;
            }
            else
            {
                directionGridsDict.Add(keyValUnit.Key, directionGrid);
            }
        }
    }

    int[] GetDirectionGridCorners(int destRow, int destCol)
    {
        int maxRow = destRow;
        int minRow = destRow;
        int maxCol = destCol;
        int minCol = destCol;

        foreach (KeyValuePair<int, GameObject> keyValUnit in selectedUnitsDictionary)
        {
            int[] unitRowCol = GetRowColFromPos(keyValUnit.Value.transform.position);
            maxRow = Mathf.Max(maxRow, unitRowCol[0]);
            minRow = Mathf.Min(minRow, unitRowCol[0]);
            maxCol = Mathf.Max(maxCol, unitRowCol[1]);
            minCol = Mathf.Min(minCol, unitRowCol[1]);
        }

        // taking one more space to be sure
        maxRow = Mathf.Min(maxRow + extraPathFindBlocks, rows);
        minRow = Mathf.Max(minRow - extraPathFindBlocks, 0);
        maxCol = Mathf.Min(maxCol + extraPathFindBlocks, cols);
        minCol = Mathf.Max(minCol - extraPathFindBlocks, 0);

        int[] corners = new int[4]{minRow, maxRow, minCol, maxCol};

        return corners;
    }

    Vector3 GetDirection(int row, int col, float[,] integrationGrid)
    {
        Vector3 direction = Vector3.zero;
        if (integrationGrid[row, col] == 0)
        {
            return direction;
        }
        float bestDirectionVal = 2047;

        for (int rowOffset = -1; rowOffset < 2; rowOffset++)
        {
            for (int colOffset = -1; colOffset < 2; colOffset++)
            {
                int newRow = row + rowOffset;
                int newCol = col + colOffset;
                if (newCol >= 0 && newCol < cols && newRow >= 0 && newRow < rows)
                {
                    float dy = Mathf.Abs(positionGrid[row, col].y - positionGrid[newRow, newCol].y);
                    if ((rowOffset != 0 || colOffset != 0) && integrationGrid[newRow, newCol] < bestDirectionVal && dy < maxHightJump)
                    {
                        bestDirectionVal = integrationGrid[newRow, newCol];
                        direction =  new Vector3(colOffset, 0, -rowOffset).normalized;
                        if (rowOffset != 0 && integrationGrid[newRow, col] > 2000)
                        {
                            direction.z = 0;
                            direction.Normalize();
                        }
                        if (colOffset != 0 && integrationGrid[row, newCol] > 2000)
                        {
                            direction.x = 0;
                            direction.Normalize();
                        }
                    }
                }
            }
        }
        direction.y = 0;
        return direction;
    }

    Vector3 GetDirectionAdvanced(int row, int col, float[,] integrationGrid)
    {
        Vector3 direction = Vector3.zero;
        if (integrationGrid[row, col] == 0)
        {
            return direction;
        }

        int negativeRowsToConvolute = -cellsToConvolute;
        int positiveRowsToConvolute = cellsToConvolute;
        int negativeColsToConvolute = -cellsToConvolute;
        int positiveColsToConvolute = cellsToConvolute;

        for (int rowOffset = -1; rowOffset <= -cellsToConvolute; rowOffset--)
        {
            int newRow = row + rowOffset;
            float dy = Mathf.Abs(positionGrid[row, col].y - positionGrid[newRow, col].y);
            if (dy >= maxHightJump)
            {
                negativeRowsToConvolute = rowOffset + 1;
            }
        }
        for (int rowOffset = 1; rowOffset <= cellsToConvolute; rowOffset++)
        {
            int newRow = row + rowOffset;
            float dy = Mathf.Abs(positionGrid[row, col].y - positionGrid[newRow, col].y);
            if (dy >= maxHightJump)
            {
                positiveRowsToConvolute = rowOffset - 1;
            }
        }
        for (int colOffset = -1; colOffset <= -cellsToConvolute; colOffset--)
        {
            int newCol = col + colOffset;
            float dy = Mathf.Abs(positionGrid[row, col].y - positionGrid[row, newCol].y);
            if (dy >= maxHightJump)
            {
                negativeColsToConvolute = colOffset + 1;
            }
        }
        for (int colOffset = 1; colOffset <= cellsToConvolute; colOffset++)
        {
            int newCol = col + colOffset;
            float dy = Mathf.Abs(positionGrid[row, col].y - positionGrid[row, newCol].y);
            if (dy >= maxHightJump)
            {
                positiveColsToConvolute = colOffset - 1;
            }
        }

        for (int rowOffset = negativeRowsToConvolute; rowOffset <= positiveRowsToConvolute; rowOffset++)
        {
            for (int colOffset = negativeColsToConvolute; colOffset <= positiveColsToConvolute; colOffset++)
            {
                int newRow = row + rowOffset;
                int newCol = col + colOffset;
                if (newCol >= 0 && newCol < cols && newRow >= 0 && newRow < rows)
                {
                    float dy = Mathf.Abs(positionGrid[row, col].y - positionGrid[newRow, newCol].y);
                    if ((rowOffset != 0 || colOffset != 0) && dy <= maxHightJump && speedModifierGrid[row, col] == speedModifierGrid[newRow, newCol])
                    {
                        if (integrationGrid[newRow, newCol] == 0)
                        {
                            return new Vector3(colOffset, 0, -rowOffset).normalized;
                        }
                        else
                        {
                            direction += new Vector3(colOffset, 0, -rowOffset).normalized * (integrationGrid[row, col] - integrationGrid[newRow, newCol]);
                        }
                    }
                }
            }
        }

        if (Mathf.Abs(positionGrid[row, col + (int)Mathf.Sign(direction.x)].y - positionGrid[row, col].y) > maxHightJump)
        {
            direction.x = 0;
        }
        if (Mathf.Abs(positionGrid[row - (int)Mathf.Sign(direction.z), col].y - positionGrid[row, col].y) > maxHightJump)
        {
            direction.z = 0;
        }

        direction.Normalize();
        return direction;
    }

    void OnDrawGizmos()
    {
        if (drawGrid)
        {
            for (int row = 0; row < rows; row++)
            {
                for (int col = 0; col < cols; col++)
                {
                    if (walkableGrid[row, col])
                    {
                        Gizmos.color = Color.red;
                    }
                    else
                    {
                        Gizmos.color = Color.blue;
                    }
                    Gizmos.DrawWireCube(positionGrid[row, col], stepSize * Vector3.one);
                }
            }
        }
    }
}
