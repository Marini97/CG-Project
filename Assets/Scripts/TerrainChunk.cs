using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
public class TerrainChunk {
	
	const float colliderGenerationDistanceThreshold = 5000;
	public event System.Action<TerrainChunk, bool> onVisibilityChanged;
	public Vector2 coord;
	 
	GameObject meshObject;
	Vector2 sampleCentre;
	Bounds bounds;

	MeshRenderer meshRenderer;
	MeshFilter meshFilter;
	MeshCollider meshCollider;

	LODInfo[] detailLevels;
	LODMesh[] lodMeshes;
	int colliderLODIndex;

	HeightMap heightMap;
	bool heightMapReceived;
	int previousLODIndex = -1;
	bool hasSetCollider;
	float maxViewDst;
	public bool created = false; 
	HeightMapSettings heightMapSettings;
	MeshSettings meshSettings;
	Transform viewer;
	GameObject tree;
	GameObject boat;

	public List<GameObject> trees = new List<GameObject>();
	public List<GameObject> boats = new List<GameObject>();

	System.Random rnd = new System.Random();

	GameObject water;
	public TerrainChunk(Vector2 coord, HeightMapSettings heightMapSettings, MeshSettings meshSettings, LODInfo[] detailLevels, int colliderLODIndex, Transform parent, Transform viewer, Material material, GameObject water, GameObject tree, GameObject boat) {
		this.coord = coord;
		this.detailLevels = detailLevels;
		this.colliderLODIndex = colliderLODIndex;
		this.heightMapSettings = heightMapSettings;
		this.meshSettings = meshSettings;
		this.viewer = viewer;
		this.tree = tree;
		this.boat = boat;
		
		sampleCentre = coord * meshSettings.meshWorldSize / meshSettings.meshScale;
		Vector2 position = coord * meshSettings.meshWorldSize ;
		bounds = new Bounds(position,Vector2.one * meshSettings.meshWorldSize );

		this.water = GameObject.Instantiate(water, new Vector3(position.x, 7, position.y), Quaternion.identity) as GameObject;

		meshObject = new GameObject("Terrain Chunk");
		meshRenderer = meshObject.AddComponent<MeshRenderer>();
		meshFilter = meshObject.AddComponent<MeshFilter>();
		meshCollider = meshObject.AddComponent<MeshCollider>();

		meshCollider.cookingOptions = MeshColliderCookingOptions.CookForFasterSimulation;
		meshRenderer.material = material;

		meshObject.transform.position = new Vector3(position.x,0,position.y);
		meshObject.transform.parent = parent;

		lodMeshes = new LODMesh[detailLevels.Length];
		for (int i = 0; i < detailLevels.Length; i++) {
			lodMeshes[i] = new LODMesh(detailLevels[i].lod);
			lodMeshes[i].updateCallback += UpdateTerrainChunk;
			if (i == colliderLODIndex) {
				lodMeshes[i].updateCallback += UpdateCollisionMesh;
			}
		}

		maxViewDst = detailLevels [detailLevels.Length - 1].visibleDstThreshold;

		SetVisible(false);
	}

	public void Load() {
		ThreadedDataRequester.RequestData(() => HeightMapGenerator.GenerateHeightMap (meshSettings.numVertsPerLine, meshSettings.numVertsPerLine, heightMapSettings, sampleCentre), OnHeightMapReceived);
	}

	public IEnumerator CreateTrees()
	{
		//returning 0 will make it wait 1 frame
		//yield return new WaitForSeconds(2);
		yield return 0;
		//code goes here
		Vector2 position = coord * meshSettings.meshWorldSize;
        while (boats.Count < 1)
        {
			float x = (float)(rnd.Next(-10, 10) * (position.x + 20 - position.x) + position.x);
			float z = (float)(rnd.Next(-10, 10) * (position.y + 20 - position.y) + position.y);

			float y = 100;
			RaycastHit hit;
			float hoverHeight = 100;
			Ray ray = new Ray(new Vector3(x, hoverHeight, z), Vector3.down);
			if (Physics.Raycast(ray, out hit, 200))
			{
				y = hoverHeight - hit.distance;
				if (y < 1)
				{
					GameObject t = GameObject.Instantiate(this.boat, new Vector3(x, y+4, z), Quaternion.identity) as GameObject;
					t.SetActive(true);
					this.boats.Add(t);
				}
			}
		}
		while (trees.Count<10)
		{
			float x = (float)(rnd.Next(-10, 10) * (position.x + 20 - position.x) + position.x);
			float z = (float)(rnd.Next(-10, 10) * (position.y + 20 - position.y) + position.y);

			float y = 100;
			RaycastHit hit;
			float hoverHeight = 100;
			Ray ray = new Ray(new Vector3(x, hoverHeight, z), Vector3.down);
			if (Physics.Raycast(ray, out hit, 200))
			{
				y = hoverHeight - hit.distance;
				if (y > 10)
				{

					GameObject t = GameObject.Instantiate(this.tree, new Vector3(x, y, z), Quaternion.identity) as GameObject;
					t.SetActive(true);
					
					this.trees.Add(t);
				}
			}
			
			
		}


	}

	void OnHeightMapReceived(object heightMapObject) {
		this.heightMap = (HeightMap)heightMapObject;
		heightMapReceived = true;

		UpdateTerrainChunk ();
	}

	Vector2 viewerPosition {
		get {
			return new Vector2 (viewer.position.x, viewer.position.z);
		}
	}


	public void UpdateTerrainChunk() {
		if (heightMapReceived) {
			float viewerDstFromNearestEdge = Mathf.Sqrt (bounds.SqrDistance (viewerPosition));

			bool wasVisible = IsVisible ();
			bool visible = viewerDstFromNearestEdge <= maxViewDst;

			if (visible) {
				int lodIndex = 0;

				for (int i = 0; i < detailLevels.Length - 1; i++) {
					if (viewerDstFromNearestEdge > detailLevels [i].visibleDstThreshold) {
						lodIndex = i + 1;
					} else {
						break;
					}
				}

				if (lodIndex != previousLODIndex) {
					LODMesh lodMesh = lodMeshes [lodIndex];
					if (lodMesh.hasMesh) {
						previousLODIndex = lodIndex;
						meshFilter.mesh = lodMesh.mesh;
					} else if (!lodMesh.hasRequestedMesh) {
						lodMesh.RequestMesh (heightMap, meshSettings);
					}
				}


			}

			if (wasVisible != visible) {
				
				SetVisible (visible);
				if (onVisibilityChanged != null) {
					onVisibilityChanged (this, visible);
				}
			}
		}
	}

	public void UpdateCollisionMesh() {
		if (!hasSetCollider) {
			float sqrDstFromViewerToEdge = bounds.SqrDistance (viewerPosition);

			if (sqrDstFromViewerToEdge < detailLevels [colliderLODIndex].sqrVisibleDstThreshold) {
				if (!lodMeshes [colliderLODIndex].hasRequestedMesh) {
					lodMeshes [colliderLODIndex].RequestMesh (heightMap, meshSettings);
				}
			}

			if (sqrDstFromViewerToEdge < colliderGenerationDistanceThreshold * colliderGenerationDistanceThreshold) {
				if (lodMeshes [colliderLODIndex].hasMesh) {
					meshCollider.sharedMesh = lodMeshes [colliderLODIndex].mesh;
					hasSetCollider = true;
				}
			}
		}
	}

	public void SetVisible(bool visible) {
		meshObject.SetActive (visible);
		water.SetActive(visible);
		for (int i = 0; i < trees.Count; i++)
		{
			trees[i].SetActive (visible);
		}
		for (int i = 0; i < boats.Count; i++)
		{
			boats[i].SetActive (visible);
		}

	}

	public bool IsVisible() {
		return meshObject.activeSelf;
	}

}

class LODMesh {

	public Mesh mesh;
	public bool hasRequestedMesh;
	public bool hasMesh;
	int lod;
	public event System.Action updateCallback;

	public LODMesh(int lod) {
		this.lod = lod;
	}

	void OnMeshDataReceived(object meshDataObject) {
		mesh = ((MeshData)meshDataObject).CreateMesh ();
		hasMesh = true;

		updateCallback ();
	}

	public void RequestMesh(HeightMap heightMap, MeshSettings meshSettings) {
		hasRequestedMesh = true;
		ThreadedDataRequester.RequestData (() => MeshGenerator.GenerateTerrainMesh (heightMap.values, meshSettings, lod), OnMeshDataReceived);
	}

}