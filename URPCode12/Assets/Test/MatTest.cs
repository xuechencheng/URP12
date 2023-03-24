using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MatTest : MonoBehaviour
{
    public Material mat;
    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.S))
        {
            Debug.LogError("111");
            Shader.DisableKeyword("_MAIN_LIGHT_SHADOWS");
        }
    }
}
