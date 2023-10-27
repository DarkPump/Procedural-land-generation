using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;
using System;
using System.Threading;

public class MapGenerator : MonoBehaviour
{
    public const int mapChunkSize = 241;
    
    [Tooltip("What map mode should be drawn")]
    [SerializeField] private DrawMode drawMode;

    [SerializeField] private Noise.NormalizeMode normalizeMode;

    [FormerlySerializedAs("levelOfDetail")]
    [Range(0, 6)]
    [SerializeField] private int editorPreviewLOD;
    
    [Tooltip("Number that determines at what distance to view the noise map.")]
    [Range(0.251f, 50f)]
    [SerializeField] private float noiseScale;
    
    [Tooltip("Number that determines the levels of detail perlin noise will have.")]
    [Min(0)]
    [SerializeField] private int octaves;
    
    [Tooltip("Number that determines how much detail is added or removed at each octave (adjusts frequency).")]
    [Range(0f, 1f)]
    [SerializeField] private float persistance;
    
    [Tooltip("Number that determines how much each octave contributes to the overall shape (adjusts amplitude).")]
    [Min(1)]
    [SerializeField] private float lacunarity;
    
    [Tooltip("Number that determines a new position on the map")]
    [SerializeField] private int seed;
    
    [Tooltip("Vector that allows to offset the position of the map")]
    [SerializeField] private Vector2 offset;

    [SerializeField] private float meshHeightMultiplier;

    [SerializeField] private AnimationCurve meshHeightCurve;
    
    [Tooltip("Should map be updated automatically.")]
    public bool AutoUpdate;
    
    [Tooltip("Should falloff map be used when generating map.")]
    [SerializeField] private bool useFalloffMap;

    [Tooltip("Array of regions")]
    [SerializeField] private TerrainType[] regions;

    private float[,] falloffMap;
    
    private Queue<MapThreadInfo<MapData>> mapDataThreadInfoQueue = new();
    private Queue<MapThreadInfo<MeshData>> meshDataThreadInfoQueue = new();

    private void Awake()
    {
        falloffMap = FalloffGenerator.GenerateFalloffMap(mapChunkSize);
    }

    private void Update()
    {
        if (mapDataThreadInfoQueue.Count > 0)
        {
            for (int i = 0; i < mapDataThreadInfoQueue.Count; i++)
            {
                MapThreadInfo<MapData> threadInfo = mapDataThreadInfoQueue.Dequeue();
                threadInfo.callback(threadInfo.parameter);
            }
        }

        if (meshDataThreadInfoQueue.Count > 0)
        {
            for (int i = 0; i < meshDataThreadInfoQueue.Count; i++)
            {
                MapThreadInfo<MeshData> threadInfo = meshDataThreadInfoQueue.Dequeue();
                threadInfo.callback(threadInfo.parameter);
            }
        }
    }

    private MapData GenerateMapData(Vector2 center)
    {
        float[,] noiseMap = Noise.GenerateNoiseMap(mapChunkSize, mapChunkSize, seed, noiseScale, octaves, persistance,
            lacunarity, center + offset, normalizeMode);
        
        Color[] colorMap = new Color[mapChunkSize * mapChunkSize];
        
        for (int y = 0; y < mapChunkSize; y++)
        {
            for (int x = 0; x < mapChunkSize; x++)
            {
                if (useFalloffMap)
                {
                    noiseMap[x, y] = Mathf.Clamp01(noiseMap[x, y] - falloffMap[x, y]);
                }
                
                
                float currentHeight = noiseMap[x, y];
                
                for (int i = 0; i < regions.Length; i++)
                {
                    if (currentHeight >= regions[i].height)
                    {
                        colorMap[y * mapChunkSize + x] = regions[i].color;
                    }
                    else
                    {
                        break;
                    }
                }
            }
        }

        return new MapData(noiseMap, colorMap);
    }

    public void DrawMapInEditor()
    {
        MapData mapData = GenerateMapData(Vector2.zero);
        MapDisplay display = FindObjectOfType<MapDisplay>();
        switch (drawMode)
        {
            case DrawMode.NoiseMap:
                display.DrawTexture(TextureGenerator.TextureFromHeightMap(mapData.heightMap));
                break;
            case DrawMode.ColorMap:
                display.DrawTexture(TextureGenerator.TextureFromColorMap(mapData.colorMap, mapChunkSize, mapChunkSize));
                break;
            case DrawMode.MeshMap:
                display.DrawMesh(
                    MeshGenerator.GenerateTerrainMesh(mapData.heightMap, meshHeightMultiplier, meshHeightCurve, editorPreviewLOD),
                    TextureGenerator.TextureFromColorMap(mapData.colorMap, mapChunkSize, mapChunkSize));
                break;
            case DrawMode.FalloffMap:
                display.DrawTexture(TextureGenerator.TextureFromHeightMap(FalloffGenerator.GenerateFalloffMap(mapChunkSize)));
                break;
        }
    }

    public void RequestMapData(Vector2 center, Action<MapData> callback)
    {
        ThreadStart threadStart = delegate
        {
            MapDataThread(center, callback);
        };

        new Thread(threadStart).Start();
    }

    private void MapDataThread(Vector2 center, Action<MapData> callback)
    {
        MapData mapData = GenerateMapData(center);
        lock (mapDataThreadInfoQueue)
        {
            mapDataThreadInfoQueue.Enqueue(new MapThreadInfo<MapData>(callback, mapData));
        }
    }

    public void RequestMeshData(MapData mapData, int lod, Action<MeshData> callback)
    {
        ThreadStart threadStart = delegate
        {
            MeshDataThread(mapData, lod, callback);
        };
        
        new Thread(threadStart).Start();
    }
    
    private void MeshDataThread(MapData mapData, int lod, Action<MeshData> callback)
    {
        MeshData meshData =
            MeshGenerator.GenerateTerrainMesh(mapData.heightMap, meshHeightMultiplier, meshHeightCurve, lod);
        lock (meshDataThreadInfoQueue)
        {
            meshDataThreadInfoQueue.Enqueue(new MapThreadInfo<MeshData>(callback, meshData));
        }
    }

    void OnValidate() 
    {
        if (lacunarity < 1) {
            lacunarity = 1;
        }
        
        if (octaves < 0) {
            octaves = 0;
        }

        falloffMap ??= FalloffGenerator.GenerateFalloffMap(mapChunkSize);
    }

    public enum DrawMode
    {
        NoiseMap,
        ColorMap,
        MeshMap,
        FalloffMap
    }

    struct MapThreadInfo<T>
    {
        public readonly Action<T> callback;
        public readonly T parameter;

        public MapThreadInfo(Action<T> callback, T parameter)
        {
            this.callback = callback;
            this.parameter = parameter;
        }
    }
}

    
[System.Serializable]
public struct TerrainType
{
    public string name;
    public float height;
    public Color color;
}

    
[System.Serializable]
public struct MapData
{
    public readonly float[,] heightMap;
    public readonly Color[] colorMap;

    public MapData(float[,] heightMap, Color[] colorMap)
    {
        this.heightMap = heightMap;
        this.colorMap = colorMap;
    }
}
