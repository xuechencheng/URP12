using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;

public static class ViewProjMatrixVisTool
{
    static GameObject cubeObj = null;
    public static void VisiableVPMat(Matrix4x4 view, Matrix4x4 project) {
        Debug.LogError("view Mat");
        Debug.LogError(view);
        Debug.LogError("Proj Mat");
        Debug.LogError(project);
        Debug.LogError("view Mat Inverse");
        var viewInverse = view.inverse;
        Debug.LogError(view.inverse);
        if (cubeObj != null)
        {
            GameObject.Destroy(cubeObj);
        }
        cubeObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cubeObj.transform.position = view.GetColumn(3);
        cubeObj.name = "TTTTTT";
        cubeObj.transform.rotation = GetRotation(view);// view.rotation;
        var planes = project.decomposeProjection;
        cubeObj.transform.localScale = new Vector3(planes.right - planes.left, planes.top - planes.bottom, planes.zFar - planes.zNear);
        cubeObj.transform.position += cubeObj.transform.forward * (planes.zFar - planes.zNear) / 2;
    }

    private static Quaternion GetRotation(Matrix4x4 matrix)
    {
        return Quaternion.LookRotation(matrix.GetColumn(2) * (-1), matrix.GetColumn(1));
    }
}
