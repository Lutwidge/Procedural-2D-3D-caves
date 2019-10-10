using UnityEngine;
using System.Collections.Generic;

// Class reponsible for creating mesh with the marching cubes method
public class MeshGenerator3D : MonoBehaviour
{
    [SerializeField] private MeshFilter _caveMesh;
    [SerializeField] private Material _caveMat;
    private List<GameObject> meshes = new List<GameObject>();

    public void GenerateMesh(int[,,] map)
    {
        if (meshes != null)
            foreach (GameObject go in meshes)
                Destroy(go);

        MarchingCubes mc = new MarchingCubes();

        List<Vector3> vertices = new List<Vector3>();
        List<int> triangles = new List<int>();
        mc.Generate(map, ref vertices, ref triangles);

        // Split the meshes in several ones
        int maxVertsPerMesh = 60000; //must be divisible by 3, ie 3 verts == 1 triangle
        int numMeshes = vertices.Count / maxVertsPerMesh + 1;

        for (int i = 0; i < numMeshes; i++)
        {
            List<Vector3> splitVertices = new List<Vector3>();
            List<int> splitIndices = new List<int>();

            for (int j = 0; j < maxVertsPerMesh; j++)
            {
                int idx = i * maxVertsPerMesh + j;

                if (idx < vertices.Count)
                {
                    splitVertices.Add(vertices[idx]);
                    splitIndices.Add(j);
                }
            }

            if (splitVertices.Count == 0) continue;

            // Create the mesh and the gameobject
            Mesh mesh = new Mesh();
            mesh.vertices = splitVertices.ToArray();
            mesh.triangles = splitIndices.ToArray();
            mesh.RecalculateBounds();
            mesh.RecalculateNormals();

            GameObject go = new GameObject("Mesh");
            go.transform.parent = transform;
            go.AddComponent<MeshFilter>();
            go.AddComponent<MeshRenderer>();
            go.GetComponent<Renderer>().material = _caveMat;
            go.GetComponent<MeshFilter>().mesh = mesh;
            go.transform.localPosition = new Vector3(-map.GetLength(0) / 2, -map.GetLength(1) / 2, -map.GetLength(2) / 2);

            // Collider
            MeshCollider meshCollider = go.AddComponent<MeshCollider>();
            meshCollider.sharedMesh = mesh;

            meshes.Add(go);
        }
    }
}
