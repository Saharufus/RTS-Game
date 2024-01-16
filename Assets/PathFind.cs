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
    public Cell[,] cellsGrid;
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
        cellsGrid = new Cell[rows, cols];
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

        GenerateCellGrid();
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

    public Vector3 GetPosFromRowCol(int row, int col, int yOffset)
    {
        float x = (stepSize / 2) + (col * stepSize) - (map.localScale.x / 2);
        float z = -(stepSize / 2) - (row * stepSize) + (map.localScale.z / 2);
        return new Vector3(x, yOffset, z);
    }

    void GenerateCellGrid()
    {
        for (int row = 0; row < rows; row++)
        {
            for (int col = 0; col < cols; col++)
            {
                Vector3 position = GetPosFromRowCol(row, col, 100);
                if (Physics.Raycast(position, Vector3.down, out RaycastHit hit, Mathf.Infinity, groundLayerMask))
                {
                    string cellTag = hit.transform.tag;
                    cellsGrid[row, col] = new Cell(row, col, hit.point, cellTag, groundTagWeights[cellTag], speedModifiersFromTags[cellTag], cellTag != "Unwalkable");
                }
            }
        }

        foreach (Cell cell in cellsGrid)
        {
            if (cell.Tag == "Unwalkable")
            {
                for (int rowOffset = -1; rowOffset <= 1; rowOffset++)
                {
                    for (int colOffset = -1; colOffset <= 1; colOffset++)
                    {
                        cellsGrid[cell.Row + rowOffset, cell.Col + colOffset].Walkable = false;
                    }
                }
            }
        }
    }

    void ReleseUnitFromWalkableGrid(GameObject unit)
    {
        int radiusToCheck = Mathf.RoundToInt(Mathf.Max(unit.transform.localScale.x, unit.transform.localScale.z) / 2) + 3;
        for (int rowOffset = -radiusToCheck; rowOffset <= radiusToCheck; rowOffset++)
        {
            for (int colOffset = -radiusToCheck; colOffset <= radiusToCheck; colOffset++)
            {
                int[] rowCol = GetRowColFromPos(unit.transform.position);
                int newRow = rowCol[0] + rowOffset;
                int newCol = rowCol[1] + colOffset;
                Vector3 position = GetPosFromRowCol(newRow, newCol, 100);
                if (Physics.Raycast(position, Vector3.down, out RaycastHit hit, Mathf.Infinity))
                {
                    if (hit.transform.gameObject == unit)
                    {
                        cellsGrid[newRow, newCol].Walkable = true;
                    }
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
                integrationGrid[row, col] = Mathf.Infinity;
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
                    float newIntegrationValue = integrationValue + (cellsGrid[newRow, newCol].Cost * distMod);
                    float dy = Mathf.Abs(cellsGrid[row, col].Position.y - cellsGrid[newRow, newCol].Position.y);
                    if (newIntegrationValue < integrationGrid[newRow, newCol] && dy <= maxHightJump)
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
        foreach (KeyValuePair<int, GameObject> keyValUnit in selectedUnitsDictionary)
        {
            ReleseUnitFromWalkableGrid(keyValUnit.Value);
        }

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
                    float dy = Mathf.Abs(cellsGrid[row, col].Position.y - cellsGrid[newRow, newCol].Position.y);
                    if ((rowOffset != 0 || colOffset != 0) && integrationGrid[newRow, newCol] < bestDirectionVal && dy < maxHightJump)
                    {
                        bestDirectionVal = integrationGrid[newRow, newCol];
                        direction = new Vector3(colOffset, 0, -rowOffset).normalized;
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

        for (int rowOffset = -1; rowOffset >= -cellsToConvolute; rowOffset--)
        {
            int newRow = row + rowOffset;
            float dy = Mathf.Abs(cellsGrid[row, col].Position.y - cellsGrid[newRow, col].Position.y);
            if (dy >= maxHightJump)
            {
                negativeRowsToConvolute = rowOffset + 1;
                break;
            }
        }
        for (int rowOffset = 1; rowOffset <= cellsToConvolute; rowOffset++)
        {
            int newRow = row + rowOffset;
            float dy = Mathf.Abs(cellsGrid[row, col].Position.y - cellsGrid[newRow, col].Position.y);
            if (dy >= maxHightJump)
            {
                positiveRowsToConvolute = rowOffset - 1;
                break;
            }
        }
        for (int colOffset = -1; colOffset >= -cellsToConvolute; colOffset--)
        {
            int newCol = col + colOffset;
            float dy = Mathf.Abs(cellsGrid[row, col].Position.y - cellsGrid[row, newCol].Position.y);
            if (dy >= maxHightJump)
            {
                negativeColsToConvolute = colOffset + 1;
                break;
            }
        }
        for (int colOffset = 1; colOffset <= cellsToConvolute; colOffset++)
        {
            int newCol = col + colOffset;
            float dy = Mathf.Abs(cellsGrid[row, col].Position.y - cellsGrid[row, newCol].Position.y);
            if (dy >= maxHightJump)
            {
                positiveColsToConvolute = colOffset - 1;
                break;
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
                    float dy = Mathf.Abs(cellsGrid[row, col].Position.y - cellsGrid[newRow, newCol].Position.y);
                    if ((rowOffset != 0 || colOffset != 0) && (dy <= maxHightJump || !cellsGrid[newRow, newCol].Walkable) && cellsGrid[row, col].SpeedModifier == cellsGrid[newRow, newCol].SpeedModifier)
                    {
                        if (integrationGrid[newRow, newCol] == 0 && (Mathf.Abs(colOffset) == 1 || Mathf.Abs(rowOffset) == 1))
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

        int colDir = col + (int)Mathf.Sign(direction.x);
        int rowDir = row - (int)Mathf.Sign(direction.z);
        if ((Mathf.Abs(cellsGrid[row, colDir].Position.y - cellsGrid[row, col].Position.y) > maxHightJump || !cellsGrid[row, colDir].Walkable) && direction.x != 0)
        {
            direction.x = 0;
        }
        if ((Mathf.Abs(cellsGrid[rowDir, col].Position.y - cellsGrid[row, col].Position.y) > maxHightJump || !cellsGrid[rowDir, col].Walkable) && direction.z != 0)
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
                    if (cellsGrid[row, col].Walkable)
                    {
                        Gizmos.color = Color.red;
                    }
                    else
                    {
                        Gizmos.color = Color.blue;
                    }
                    Gizmos.DrawWireCube(cellsGrid[row, col].Position, stepSize * Vector3.one);
                }
            }
        }
    }
}
