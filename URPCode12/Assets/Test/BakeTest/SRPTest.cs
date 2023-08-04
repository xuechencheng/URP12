using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class SRPTest : MonoBehaviour
{
    // Start is called before the first frame update
    void Start()
    {
        var urp = UnityEngine.QualitySettings.renderPipeline as UniversalRenderPipelineAsset;
        if (urp == null)
        {
            return;
        }
        urp.useSRPBatcher = false;
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
