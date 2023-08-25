using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.Rendering.Universal;

public class ShadowTest : MonoBehaviour
{
    public Camera camera;
    // Start is called before the first frame update
    void Start()
    {
        
    }
    static int count = 0;
    static GameObject capsule = null;
    // Update is called once per frame
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.S))
        {
            Debug.LogError(camera.worldToCameraMatrix);
            Debug.LogError(camera.projectionMatrix);
            ViewProjMatrixVisTool.VisiableVPMat(camera.cameraToWorldMatrix, camera.projectionMatrix);
            return;

            if (camera != null)
            {
                Debug.Log("view Mat");
                Debug.LogError(camera.worldToCameraMatrix);
                Debug.Log("Proj Mat");
                Debug.LogError(camera.projectionMatrix);

                Debug.Log("view Mat v");
                Debug.LogError(camera.cameraToWorldMatrix);

                Debug.LogError(camera.worldToCameraMatrix.inverse);

                if (capsule != null)
                {
                    GameObject.Destroy(capsule);
                }
                capsule = GameObject.CreatePrimitive(PrimitiveType.Cube);
                capsule.transform.position = V4ToV3(camera.cameraToWorldMatrix.GetColumn(3));
                //capsule.transform.forward = V4ToV3(camera.worldToCameraMatrix.GetColumn(2));
                //capsule.transform.up = V4ToV3(camera.worldToCameraMatrix.GetColumn(1));
                //capsule.transform.right = V4ToV3(camera.worldToCameraMatrix.GetColumn(0));
                capsule.transform.localScale = new Vector3(1, 1, 10);
                capsule.name = "TTTTTT";
                capsule.transform.rotation = camera.worldToCameraMatrix.rotation;
                
                var planes = camera.projectionMatrix.decomposeProjection;
                capsule.transform.localScale = new Vector3(planes.right - planes.left, planes.top - planes.bottom, planes.zFar - planes.zNear);
                capsule.transform.position += capsule.transform.forward * (planes.zFar - planes.zNear) / 2;

            }
        }
    }

    private Vector3 V4ToV3(Vector4 v4) {
        return new Vector3(v4.x, v4.y, v4.z);
    }
}
