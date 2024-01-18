using System.Collections;
using System.Collections.Generic;
using TMPro;
using Unity.VisualScripting;
using UnityEngine;

public class Cell
{
    int row;
    int col;
    private Vector3 position;
    string tag;
    int cost;
    float speedModifier;
    bool walkable;

    public Cell(int row, int col, Vector3 position, string tag, int cost, float speedModifier, bool walkable)
    {
        this.row = row;
        this.col = col;
        this.position = position;
        this.tag = tag;
        this.cost = cost;
        this.speedModifier = speedModifier;
        this.walkable = walkable;
        
    }

    public int Row
    {
        get => row;
    }
    public int Col
    {
        get => col;
    }
    public Vector3 Position
    {
        get => position;
    }
    public string Tag
    {
        get => tag;
    }
    public int Cost
    {
        set => cost = value;
        get => cost;
    }
    public float SpeedModifier
    {
        get => speedModifier;
    }
    public bool Walkable
    {
        set => walkable = value;
        get => walkable;
    }
}
