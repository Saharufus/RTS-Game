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
    private int rows;
    private int cols;
    public Cell[,] cellsGrid;
    Camera mainCam;
    public Dictionary<int, Vector3[,]> directionGridsDict;
    readonly Dictionary<string, int> groundTagWeights = new();
    readonly Dictionary<string, float> speedModifiersFromTags = new();
    public float maxSlope;
    float maxHightJump;
    public int extraPathFindBlocks;
    public int cellsToConvolute;
    public bool drawGrid;
    public bool useAdvancedDirFunc;

    public int Rows {get => rows;}
    public int Cols {get => cols;}

    void Start()
    {
        mainObjects = gameObject.GetComponent<MainObjects>();
        mainCam = mainObjects.mainCam;
        map = mainObjects.map;
        Vector3 mapSize = map.localScale;
        rows = Mathf.RoundToInt(mapSize.z / stepSize);
        cols = Mathf.RoundToInt(mapSize.x / stepSize);
        cellsGrid = new Cell[rows, cols];
        maxHightJump = Mathf.Tan(maxSlope * Mathf.PI / 180) * stepSize;

        groundTagWeights.Add("Ground", 2);
        groundTagWeights.Add("Road", 1);
        groundTagWeights.Add("Water", 4);
        groundTagWeights.Add("Unwalkable", 255);

        speedModifiersFromTags.Add("Ground", 1);
        speedModifiersFromTags.Add("Road", 1.5f);
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
                        int newRow = cell.Row + rowOffset;
                        int newCol = cell.Col + colOffset;
                        if (newRow >= 0 && newRow < rows && newCol >=0 && newCol < cols)
                        cellsGrid[cell.Row + rowOffset, cell.Col + colOffset].Walkable = false;
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

        List<Cell> cellsToCheck = new();
        integrationGrid[destCellRow, destCellCol] = 0;
        cellsToCheck.Add(cellsGrid[destCellRow, destCellCol]);

        while (cellsToCheck.Count > 0)
        {
            List<Cell> newCellsToCheck = new();
            foreach (Cell cell in cellsToCheck)
            {
                float integrationValue = integrationGrid[cell.Row, cell.Col];
                IntegrateNeighbors(cell, integrationValue, integrationGrid, newCellsToCheck);
            }
            cellsToCheck = newCellsToCheck;
        }

        return integrationGrid;
    }

    void IntegrateNeighbors(Cell cell, float integrationValue, float[,] integrationGrid, List<Cell> cellsToCheck)
    {
        for (int rowOffset = -1; rowOffset < 2; rowOffset++)
        {
            for (int colOffset = -1; colOffset < 2; colOffset++)
            {
                int newRow = cell.Row + rowOffset;
                int newCol = cell.Col + colOffset;
                if (newCol >= 0 && newCol < cols && newRow >= 0 && newRow < rows && (rowOffset != 0 || colOffset != 0)) // in grid bounds and not the cell itself
                {
                    Cell neighborCell = cellsGrid[newRow, newCol];
                    float distMod = 1;
                    if (Mathf.Abs(rowOffset) + Mathf.Abs(colOffset) == 2)
                    {
                        distMod = 1.4f;
                    }
                    float newIntegrationValue = integrationValue + (neighborCell.Cost * distMod);
                    float dy = Mathf.Abs(cell.Position.y - neighborCell.Position.y);
                    if (newIntegrationValue < integrationGrid[newRow, newCol] && dy <= maxHightJump)
                    {
                        integrationGrid[newRow, newCol] = newIntegrationValue;
                        cellsToCheck.Add(neighborCell);
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
                            directionGrid[row, col] = GetDirectionAdvanced(cellsGrid[row, col], integrationGrid);
                        }
                        else
                        {
                            directionGrid[row, col] = GetDirection(cellsGrid[row, col], integrationGrid);
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

    Vector3 GetDirection(Cell cell, float[,] integrationGrid)
    {
        Vector3 direction = Vector3.zero;
        if (integrationGrid[cell.Row, cell.Col] == 0)
        {
            return direction;
        }
        float bestDirectionVal = 2047;

        for (int rowOffset = -1; rowOffset < 2; rowOffset++)
        {
            for (int colOffset = -1; colOffset < 2; colOffset++)
            {
                int newRow = cell.Row + rowOffset;
                int newCol = cell.Col + colOffset;
                if (newCol >= 0 && newCol < cols && newRow >= 0 && newRow < rows)
                {
                    float dy = Mathf.Abs(cell.Position.y - cellsGrid[newRow, newCol].Position.y);
                    if ((rowOffset != 0 || colOffset != 0) && integrationGrid[newRow, newCol] < bestDirectionVal && dy < maxHightJump)
                    {
                        bestDirectionVal = integrationGrid[newRow, newCol];
                        direction = new Vector3(colOffset, 0, -rowOffset).normalized;
                        if (rowOffset != 0 && integrationGrid[newRow, cell.Col] > 2000)
                        {
                            direction.z = 0;
                            direction.Normalize();
                        }
                        if (colOffset != 0 && integrationGrid[cell.Row, newCol] > 2000)
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

    Vector3 GetDirectionAdvanced(Cell cell, float[,] integrationGrid)
    {
        Vector3 direction = Vector3.zero;
        if (integrationGrid[cell.Row, cell.Col] == 0)
        {
            return direction;
        }

        int negativeRowsToConvolute = -cellsToConvolute;
        int positiveRowsToConvolute = cellsToConvolute;
        int negativeColsToConvolute = -cellsToConvolute;
        int positiveColsToConvolute = cellsToConvolute;

        for (int rowOffset = -1; rowOffset >= -cellsToConvolute; rowOffset--)
        {
            int newRow = cell.Row + rowOffset;
            float dy = Mathf.Abs(cell.Position.y - cellsGrid[newRow, cell.Col].Position.y);
            if (dy >= maxHightJump)
            {
                negativeRowsToConvolute = rowOffset + 1;
                break;
            }
        }
        for (int rowOffset = 1; rowOffset <= cellsToConvolute; rowOffset++)
        {
            int newRow = cell.Row + rowOffset;
            float dy = Mathf.Abs(cell.Position.y - cellsGrid[newRow, cell.Col].Position.y);
            if (dy >= maxHightJump)
            {
                positiveRowsToConvolute = rowOffset - 1;
                break;
            }
        }
        for (int colOffset = -1; colOffset >= -cellsToConvolute; colOffset--)
        {
            int newCol = cell.Col + colOffset;
            float dy = Mathf.Abs(cell.Position.y - cellsGrid[cell.Row, newCol].Position.y);
            if (dy >= maxHightJump)
            {
                negativeColsToConvolute = colOffset + 1;
                break;
            }
        }
        for (int colOffset = 1; colOffset <= cellsToConvolute; colOffset++)
        {
            int newCol = cell.Col + colOffset;
            float dy = Mathf.Abs(cell.Position.y - cellsGrid[cell.Row, newCol].Position.y);
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
                int newRow = cell.Row + rowOffset;
                int newCol = cell.Col + colOffset;
                if (newCol >= 0 && newCol < cols && newRow >= 0 && newRow < rows)
                {
                    Cell neighborCell = cellsGrid[newRow, newCol];
                    float dy = Mathf.Abs(cell.Position.y - neighborCell.Position.y);
                    if ((rowOffset != 0 || colOffset != 0) && (dy <= maxHightJump || !neighborCell.Walkable) && cell.SpeedModifier == neighborCell.SpeedModifier)
                    {
                        if (integrationGrid[newRow, newCol] == 0 && (Mathf.Abs(colOffset) == 1 || Mathf.Abs(rowOffset) == 1))
                        {
                            return new Vector3(colOffset, 0, -rowOffset).normalized;
                        }
                        else
                        {
                            direction += new Vector3(colOffset, 0, -rowOffset).normalized * (integrationGrid[cell.Row, cell.Col] - integrationGrid[newRow, newCol]);
                        }
                    }
                }
            }
        }

        int colDir = cell.Col + (int)Mathf.Sign(direction.x);
        int rowDir = cell.Row - (int)Mathf.Sign(direction.z);
        if ((Mathf.Abs(cellsGrid[cell.Row, colDir].Position.y - cell.Position.y) > maxHightJump || !cellsGrid[cell.Row, colDir].Walkable) && direction.x != 0)
        {
            direction.x = 0;
        }
        if ((Mathf.Abs(cellsGrid[rowDir, cell.Col].Position.y - cell.Position.y) > maxHightJump || !cellsGrid[rowDir, cell.Col].Walkable) && direction.z != 0)
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
