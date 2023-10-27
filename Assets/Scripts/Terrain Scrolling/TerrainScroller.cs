using System;
using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

public class TerrainScroller : MonoBehaviour
{
    private const float scale = 5f;
    private const float viewerChunkUpdateThreshold = 25f;
    private const float sqrViewerChunkUpdateThreshold = viewerChunkUpdateThreshold * viewerChunkUpdateThreshold;
    
    public LODInfo[] detailLevels;
    private static float maxViewDistance;
    
    public Transform viewer;
    public Material mapMaterial;

    private static Vector2 viewerPosition;
    private Vector2 oldViewerPosition;
    private static MapGenerator mapGenerator;
    private int chunkSize;
    private int chunksInViewDistance;
    private Dictionary<Vector2, TerrainChunk> terrainChunks = new();
    private static List<TerrainChunk> terrainChunksVisible = new();

    private void Start()
    {
        mapGenerator = FindObjectOfType<MapGenerator>();
        maxViewDistance = detailLevels[^1].visibleDistanceThreshold;
        chunkSize = MapGenerator.mapChunkSize - 1;
        chunksInViewDistance = Mathf.RoundToInt(maxViewDistance / chunkSize);
        
        UpdateVisibleChunks();
    }

    private void Update()
    {
        var position = viewer.position;
        viewerPosition = new Vector2(position.x, position.z) / scale;
        UpdateVisibleChunks();
        // if ((oldViewerPosition - (Vector2)position).sqrMagnitude > sqrViewerChunkUpdateThreshold)
        // {
        //     oldViewerPosition = position;
        //     
        // }
        
    }

    private void UpdateVisibleChunks()
    {
        for (int i = 0; i < terrainChunksVisible.Count; i++)
        {
            terrainChunksVisible[i].SetVisible(false);
        }
        
        terrainChunksVisible.Clear();
        
        int currentChunkCoordX = Mathf.RoundToInt(viewerPosition.x / chunkSize);
        int currentChunkCoordy = Mathf.RoundToInt(viewerPosition.y / chunkSize);

        for (int yOffset = -chunksInViewDistance; yOffset <= chunksInViewDistance; yOffset++)
        {
            for (int xOffset = -chunksInViewDistance; xOffset < chunksInViewDistance; xOffset++)
            {
                Vector2 viewedChunkCoord = new Vector2(currentChunkCoordX + xOffset, currentChunkCoordy + yOffset);

                if (terrainChunks.ContainsKey(viewedChunkCoord))
                {
                    terrainChunks[viewedChunkCoord].UpdateTerrainChunk();
                }
                else
                {
                    terrainChunks.Add(viewedChunkCoord, new TerrainChunk(viewedChunkCoord, chunkSize, detailLevels, transform, mapMaterial));
                }
            }
        }
    }

    private class TerrainChunk
    {
        private GameObject meshObject;
        private Vector2 position;
        private Bounds bounds;

        private MeshRenderer meshRenderer;
        private MeshFilter meshFilter;
        
        private LODInfo[] detailLevels;
        private LODMesh[] lodMeshes;
        
        private MapData mapData;
        private bool mapDataReceived;
        private int previousLODIndex = -1;
        
        public TerrainChunk(Vector2 coord, int size, LODInfo[] detailLevels, Transform parent, Material material)
        {
            this.detailLevels = detailLevels;
            position = coord * size;
            bounds = new Bounds(position, Vector2.one * size);
            Vector3 positionV3 = new Vector3(position.x, 0, position.y);

            meshObject = new GameObject("Terrain chunk");
            meshRenderer = meshObject.AddComponent<MeshRenderer>();
            meshFilter = meshObject.AddComponent<MeshFilter>();
            meshRenderer.material = material;
            meshObject.transform.position = positionV3 * scale;
            meshObject.transform.parent = parent;
            meshObject.transform.localScale = Vector3.one * scale;
            
            SetVisible(false);

            lodMeshes = new LODMesh[detailLevels.Length];

            for (int i = 0; i < detailLevels.Length; i++)
            {
                lodMeshes[i] = new LODMesh(detailLevels[i].lod, UpdateTerrainChunk);
            }
            
            mapGenerator.RequestMapData(position, OnMapDataReceived);
        }

        private void OnMapDataReceived(MapData mapData)
        {
            this.mapData = mapData;
            mapDataReceived = true;

            var texture = TextureGenerator.TextureFromColorMap(mapData.colorMap, MapGenerator.mapChunkSize, MapGenerator.mapChunkSize);
            meshRenderer.material.mainTexture = texture;
            
            UpdateTerrainChunk();
        }

        public void UpdateTerrainChunk()
        {
            if (!mapDataReceived)
            {
                return;
            }
            
            float viewerDistanceFromEdge = bounds.SqrDistance(viewerPosition);
            bool visible = viewerDistanceFromEdge <= maxViewDistance * maxViewDistance;

            if (visible)
            {
                int lodIndex = 0;

                for (int i = 0; i < detailLevels.Length - 1; i++)
                {
                    if (viewerDistanceFromEdge > detailLevels[i].visibleDistanceThreshold)
                    {
                        lodIndex = i + 1;
                    }
                    else
                    {
                        break;
                    }
                }

                if (lodIndex != previousLODIndex)
                {
                    LODMesh lodMesh = lodMeshes[lodIndex];
                    if (lodMesh.hasMesh)
                    {
                        previousLODIndex = lodIndex;
                        meshFilter.mesh = lodMesh.mesh;
                    }
                    else if (!lodMesh.hasRequestedMesh)
                    {
                        lodMesh.RequestMesh(mapData);
                    }
                }
                
                terrainChunksVisible.Add(this);
            }
            
            SetVisible(visible);
        }

        public void SetVisible(bool visible)
        {
            meshObject.SetActive(visible);
        }

        public bool IsChunkVisible()
        {
            return meshObject.activeSelf;
        }
    }

    private class LODMesh
    {
        public Mesh mesh;
        public bool hasRequestedMesh;
        public bool hasMesh;
        private int lod;
        private Action updateCallback;

        public LODMesh(int lod, Action updateCallback)
        {
            this.lod = lod;
            this.updateCallback = updateCallback;
        }

        public void RequestMesh(MapData mapData)
        {
            hasRequestedMesh = true;
            mapGenerator.RequestMeshData(mapData, lod, OnMeshDataReceived);
        }

        private void OnMeshDataReceived(MeshData meshData)
        {
            mesh = meshData.CreateMesh();
            hasMesh = true;

            updateCallback();
        }
    }
    [System.Serializable]
    public struct LODInfo
    {
        public int lod;
        public float visibleDistanceThreshold;
    }
}
