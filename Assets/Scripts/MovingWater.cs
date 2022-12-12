using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MovingWater : MonoBehaviour
{
    public float flowSpeed = 0.008f;
    public float upDownMoveSpeed = 0.05f;

    MeshRenderer meshRenderer;
    // Start is called before the first frame update
    void Start()
    {
        meshRenderer = GetComponent<MeshRenderer>();

    }

    // Update is called once per frame
    void Update()
    {
        meshRenderer.material.mainTextureOffset = new Vector2(Mathf.Sin(Time.time * flowSpeed), Mathf.Sin(Time.time * flowSpeed)/2);
        transform.position = new Vector3(transform.position.x, 5+Mathf.Sin(Time.time * upDownMoveSpeed), transform.position.z);
    }
}
