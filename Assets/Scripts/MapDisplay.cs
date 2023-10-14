using System.Collections;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

public class MapDisplay : MonoBehaviour
{
    [SerializeField] private Renderer textureRenderer;
    
    public void DrawTexture(Texture2D texture)
    {
        textureRenderer.sharedMaterial.SetTexture("_BaseMap", texture);
        textureRenderer.transform.localScale = new Vector3(texture.width, 1, texture.height);
    }
}
