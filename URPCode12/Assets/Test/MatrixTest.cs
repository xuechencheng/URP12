using System.Collections;
using System.Collections.Generic;
using Unity.Mathematics;
using Unity.VisualScripting;
using UnityEngine;

public class MatrixTest : MonoBehaviour
{
    private Vector3 normalVector = Vector3.zero;


    // Start is called before the first frame update
    void Start()
    {
        normalVector = new Vector3(1, 1, 1);
        var transformMatrix = CreateTransform();
        Debug.LogError(transformMatrix);
        var reuslt = math.mul(transformMatrix, new Vector3(1, 1, 1));
        Debug.LogError(reuslt);
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    private float3x3 CreateTransform() {
        normalVector = normalVector.normalized;
        Vector3 v1 = Vector3.Cross(normalVector, new Vector3( 1, 0, 0));
        v1 = v1.normalized;

        Vector3 v2 = Vector3.Cross(normalVector, v1);
        v2 = v2.normalized;

        //Vector3 v3 = Vector3.Cross(normalVector, v2);
        //v3 = v3.normalized;

        return new float3x3(normalVector, v1, v2);
    }
}
