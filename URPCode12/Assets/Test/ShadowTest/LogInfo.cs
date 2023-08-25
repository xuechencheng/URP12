using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class LogInfo : MonoBehaviour
{
    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        Debug.LogError(gameObject.name + " -- " + transform.right + " -- " + transform.up + " -- " + transform.forward);
    }
}
