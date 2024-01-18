using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using TMPro;
using Unity.VisualScripting;
using UnityEngine;
using Configs;

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
    public bool drawGrid;

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
        groundTagWeights.Add("Unwalkable", 15);

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
            Vector3 newPos = Vector3.zero;

            if (Physics.Raycast(cameraToScreenRay, out RaycastHit hit, Mathf.Infinity))
            {
                int[] rowCol = GetRowColFromPos(hit.point);
                Cell destCell = cellsGrid[rowCol[0], rowCol[1]];
                if (destCell.Walkable)
                {
                    GenerateDirectionGrid(destCell);
                }
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
            for (int rowOffset = -1; rowOffset <= 1; rowOffset++)
            {
                for (int colOffset = -1; colOffset <= 1; colOffset++)
                {
                    int newRow = cell.Row + rowOffset;
                    int newCol = cell.Col + colOffset;
                    if ((rowOffset != 0 || colOffset != 0) && newRow >= 0 && newRow < rows && newCol >=0 && newCol < cols)
                    {
                        Cell neighborCell = cellsGrid[cell.Row + rowOffset, cell.Col + colOffset];

                        if (cell.Tag == "Unwalkable")
                        {
                            neighborCell.Walkable = false;
                            neighborCell.Cost = groundTagWeights["Unwalkable"];
                        }

                        float dy = Mathf.Abs(cell.Position.y - neighborCell.Position.y);
                        if (dy > maxHightJump)
                        {
                            cell.Walkable = false;
                            cell.Cost = groundTagWeights["Unwalkable"];
                        }
                    }
                }
            }
        }
    }

    float[,] GenerateIntegrationGrid(Cell destCell, int minRow, int maxRow, int minCol, int maxCol)
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
        integrationGrid[destCell.Row, destCell.Col] = 0;
        cellsToCheck.Add(cellsGrid[destCell.Row, destCell.Col]);

        while (cellsToCheck.Count > 0)
        {
            List<Cell> newCellsToCheck = new();
            foreach (Cell cell in cellsToCheck)
            {
                float integrationValue = integrationGrid[cell.Row, cell.Col];
                IntegrateNeighbors(cell, integrationValue, integrationGrid, newCellsToCheck, minRow, maxRow, minCol, maxCol);
            }
            cellsToCheck = newCellsToCheck;
        }

        return integrationGrid;
    }

    void IntegrateNeighbors(Cell cell, float integrationValue, float[,] integrationGrid, List<Cell> cellsToCheck, int minRow, int maxRow, int minCol, int maxCol)
    {
        for (int rowOffset = -1; rowOffset < 2; rowOffset++)
        {
            for (int colOffset = -1; colOffset < 2; colOffset++)
            {
                int newRow = cell.Row + rowOffset;
                int newCol = cell.Col + colOffset;
                if (newCol >= minCol && newCol <= maxCol && newRow >= minRow && newRow <= maxRow && (rowOffset != 0 || colOffset != 0)) // in grid bounds and not the cell itself
                {
                    Cell neighborCell = cellsGrid[newRow, newCol];
                    float distMod = 1;
                    if (Mathf.Abs(rowOffset) + Mathf.Abs(colOffset) == 2)
                    {
                        distMod = 1.4f;
                    }
                    float newIntegrationValue = integrationValue + (neighborCell.Cost * distMod);
                    if (newIntegrationValue < integrationGrid[newRow, newCol])
                    {
                        integrationGrid[newRow, newCol] = newIntegrationValue;
                        if (neighborCell.Walkable)
                        {
                            cellsToCheck.Add(neighborCell);
                        }
                    }
                }
            }
        }
    }

    public void GenerateDirectionGrid(Cell destCell)
    {
        int[] gridCorners = GetDirectionGridCorners(destCell);
        int minRow = gridCorners[0];
        int maxRow = gridCorners[1];
        int minCol = gridCorners[2];
        int maxCol = gridCorners[3];

        float[,] integrationGrid = GenerateIntegrationGrid(destCell, minRow, maxRow, minCol, maxCol);

        Vector3[,] directionGrid = new Vector3[rows, cols];

        for (int row = 0; row < rows; row++)
        {
            for (int col = 0; col < cols; col++)
            {
                if (row >= minRow && row <= maxRow && col >= minCol && col <= maxCol)
                {
                    Cell cell = cellsGrid[row, col];
                    if (cell.Tag != "Unwalkable")
                    {
                        directionGrid[row, col] = GetDirection(cell, integrationGrid, directionGrid);
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

    int[] GetDirectionGridCorners(Cell destCell)
    {
        int maxRow = destCell.Row;
        int minRow = destCell.Row;
        int maxCol = destCell.Col;
        int minCol = destCell.Col;

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

    Vector3 GetDirection(Cell cell, float[,] integrationGrid, Vector3[,] directionGrid)
    {
        Vector3 direction = Vector3.zero;
        if (integrationGrid[cell.Row, cell.Col] == 0)
        {
            return direction;
        }

        for (int rowOffset = -PathFindConfigs.cellsToConvolute; rowOffset <= PathFindConfigs.cellsToConvolute; rowOffset++)
        {
            for (int colOffset = -PathFindConfigs.cellsToConvolute; colOffset <= PathFindConfigs.cellsToConvolute; colOffset++)
            {
                int newRow = cell.Row + rowOffset;
                int newCol = cell.Col + colOffset;
                if (newCol >= 0 && newCol < cols && newRow >= 0 && newRow < rows && (rowOffset != 0 || colOffset != 0))
                {
                    if (integrationGrid[newRow, newCol] < Mathf.Infinity)
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

        if (ShouldTurn90Deg(cell.Row, cell.Col, direction, directionGrid))
        {
            direction = Quaternion.Euler(0, 90, 0) * direction;
        }

        return direction;
    }

    bool ShouldTurn90Deg(int row, int col, Vector3 direction, Vector3[,] directionGrid)
    {
        if (col - 1 > 0 && row - 1 > 0)
        {
            if (direction == -directionGrid[row - 1, col])
            {
                return true;
            }
            if (direction == -directionGrid[row, col - 1])
            {
                return true;
            }
            if (direction == -directionGrid[row - 1, col - 1])
            {
                return true;
            }
        }
        return false;
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
