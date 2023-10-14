using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MapGenerator : MonoBehaviour
{
    [Tooltip("What map mode should be drawn")]
    public DrawMode drawMode;
    
    [Tooltip("Width of the map.")]
    [Min(1)]
    public int mapWidth;
    
    [Tooltip("Height of map.")]
    [Min(1)]
    public int mapHeight;
    
    [Tooltip("Number that determines at what distance to view the noise map.")]
    [Range(0.251f, 50f)]
    public float noiseScale;
    
    [Tooltip("Number that determines the levels of detail perlin noise will have.")]
    [Min(0)]
    public int octaves;
    
    [Tooltip("Number that determines how much detail is added or removed at each octave (adjusts frequency).")]
    [Range(0f, 1f)]
    public float persistance;
    
    [Tooltip("Number that determines how much each octave contributes to the overall shape (adjusts amplitude).")]
    [Min(1)]
    public float lacunarity;
    
    [Tooltip("Number that determines a new position on the map")]
    public int seed;
    
    [Tooltip("Vector that allows to offset the position of the map")]
    public Vector2 offset;
    
    [Tooltip("Should map be updated automatically.")]
    public bool autoUpdate;

    public TerrainType[] regions;

    public void GenerateMap()
    {
        float[,] noiseMap = Noise.GenerateNoiseMap(mapWidth, mapHeight, seed, noiseScale, octaves, persistance, lacunarity, offset);
        Color[] colorMap = new Color[mapWidth * mapHeight];
        
        for (int y = 0; y < mapHeight; y++)
        {
            for (int x = 0; x < mapWidth; x++)
            {
                float currentHeight = noiseMap[x, y];
                
                for (int i = 0; i < regions.Length; i++)
                {
                    if (currentHeight <= regions[i].height)
                    {
                        colorMap[y * mapWidth + x] = regions[i].color;
                        break;
                    }
                }
            }
        }

        MapDisplay display = FindObjectOfType<MapDisplay>();
        if(drawMode == DrawMode.NoiseMap)
            display.DrawTexture(TextureGenerator.TextureFromHeightMap(noiseMap));
        else if(drawMode == DrawMode.ColorMap)
            display.DrawTexture(TextureGenerator.TextureFromColorMap(colorMap, mapWidth, mapHeight));
            
    }
    
    void OnValidate() {
        if (mapWidth < 1) {
            mapWidth = 1;
        }
        if (mapHeight < 1) {
            mapHeight = 1;
        }
        if (lacunarity < 1) {
            lacunarity = 1;
        }
        if (octaves < 0) {
            octaves = 0;
        }
    }
    
    [System.Serializable]
    public struct TerrainType
    {
        public string name;
        public float height;
        public Color color;
    }

    public enum DrawMode
    {
        NoiseMap,
        ColorMap
    }

}
