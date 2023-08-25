using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public class HaltonSequenceTest : MonoBehaviour
{
    public GameObject gameObj;
    public float scale;
    public int index = 0;
    // Start is called before the first frame update
    void Start()
    {

    }

    // Update is called once per frame
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.S))
        {
            var obj = GameObject.Instantiate(gameObj);
            var xCoord = HaltonSequence.Get(index, 2) * scale - 0.5f * scale;
            var zCoord = HaltonSequence.Get(index, 3) * scale - 0.5f * scale;
            index++;
            obj.transform.SetParent(gameObject.transform);
            obj.transform.position = new Vector3(xCoord, 0, zCoord);
        }
    }
}
