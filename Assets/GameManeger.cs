using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GameManeger : MonoBehaviour
{
    public float gravityAcceleration = 9.81f;
    void Start()
    {
        Physics.gravity = new Vector3(0, -gravityAcceleration, 0);
    }
}
