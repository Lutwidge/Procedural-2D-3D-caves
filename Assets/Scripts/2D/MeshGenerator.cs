using UnityEngine;
using System.Collections.Generic;

// Class reponsible for creating mesh with the marching squares method
public class MeshGenerator : MonoBehaviour
{
    #region Nodes Squares and Triangles Declarations
    public class Node
    {
        public Vector3 Position;
        public int VertexIndex = -1;

        public Node(Vector3 pos)
        {
            Position = pos;
        }
    }

    // Nodes controlling 2 others
    public class ControlNode : Node
    {
        public bool Active;
        public Node Above;
        public Node Right;

        public ControlNode(Vector3 pos, bool active, float squareSize) : base(pos)
        {
            Active = active;
            // Create the two nodes controlled by this control node
            Above = new Node(Position + Vector3.forward * squareSize / 2.0f);
            Right = new Node(Position + Vector3.right * squareSize / 2.0f);
        }
    }

    // Square with 4 control nodes as vertices, and regular nodes as the center of the sides
    public class Square
    {
        public ControlNode TopLeft, TopRight, BottomLeft, BottomRight;
        public Node CenterTop, CenterRight, CenterBottom, CenterLeft;
        public int Configuration = 0; // The configuration the square is in (16 possibles in marching squares)

        public Square(ControlNode topLeft, ControlNode topRight, ControlNode bottomRight, ControlNode bottomLeft)
        {
            TopLeft = topLeft;
            TopRight = topRight;
            BottomLeft = bottomLeft;
            BottomRight = bottomRight;

            CenterTop = TopLeft.Right;
            CenterRight = BottomRight.Above;
            CenterBottom = BottomLeft.Right;
            CenterLeft = BottomLeft.Above;

            // Determine the configuration based on the active nodes
            if (TopLeft.Active) // 1000 = 8
                Configuration += 8;
            if (TopRight.Active) // 0100 = 4
                Configuration += 4;
            if (BottomRight.Active) // 0010 = 2
                Configuration += 2;
            if (BottomLeft.Active) // 0001 = 1
                Configuration += 1;
        }
    }

    // Holds a 2D array of squares
    public class SquareGrid
    {
        public Square[,] Squares;

        public SquareGrid(int[,] map, float squareSize)
        {
            int nodeCountX = map.GetLength(0);
            int nodeCountY = map.GetLength(1);
            float mapWidth = nodeCountX * squareSize;
            float mapHeight = nodeCountY * squareSize;

            ControlNode[,] controlNodes = new ControlNode[nodeCountX, nodeCountY];

            Vector3 pos = Vector3.zero;
            for (int i = 0; i < nodeCountX; i++)
            {
                for (int j = 0; j < nodeCountY; j++)
                {
                    pos.x = -mapWidth / 2.0f + i * squareSize + squareSize / 2.0f;
                    pos.z = -mapHeight / 2.0f + j * squareSize + squareSize / 2.0f;
                    controlNodes[i, j] = new ControlNode(pos, map[i, j] == 1, squareSize);
                }
            }

            Squares = new Square[nodeCountX - 1, nodeCountY - 1]; // There is one less square than there are nodes
            for (int i = 0; i < nodeCountX - 1; i++)
            {
                for (int j = 0; j < nodeCountY - 1; j++)
                {
                    Squares[i, j] = new Square(controlNodes[i, j + 1], controlNodes[i + 1, j + 1], controlNodes[i + 1, j], controlNodes[i, j]);
                }
            }
        }
    }

    struct Triangle
    {
        public int vertexIndexA;
        public int vertexIndexB;
        public int vertexIndexC;
        int[] vertices;

        public Triangle(int a, int b, int c)
        {
            vertexIndexA = a;
            vertexIndexB = b;
            vertexIndexC = c;

            vertices = new int[3];
            vertices[0] = a;
            vertices[1] = b;
            vertices[2] = c;
        }

        public int this[int i]
        {
            get
            {
                return vertices[i];
            }
        }

        public bool Contains(int vertexIndex)
        {
            return vertexIndex == vertexIndexA || vertexIndex == vertexIndexB || vertexIndex == vertexIndexC;
        }
    }

    #endregion

    [SerializeField] private int _wallHeight;
    [SerializeField] private MeshFilter _caveMesh;
    [SerializeField] private MeshFilter _wallMesh;
    private MeshCollider _wallCollider;
    private SquareGrid _squareGrid;
    private List<Vector3> _vertices;
    private List<int> _triangles;
    private Dictionary<int, List<Triangle>> _triangleDictionary = new Dictionary<int, List<Triangle>>();
    private List<List<int>> _outlines = new List<List<int>>();
    private HashSet<int> _checkedVertices = new HashSet<int>(); // HashSet to have much quicker contain checks than a list

    public void GenerateMesh(int[,] map, float squareSize)
    {
        // Clear the outlines
        _outlines.Clear();
        _checkedVertices.Clear();
        _triangleDictionary.Clear();

        // Clear the collider
        if (_wallCollider != null)
            Destroy(_wallCollider);
        _wallCollider = _wallMesh.gameObject.AddComponent<MeshCollider>();

        _squareGrid = new SquareGrid(map, squareSize);
        _vertices = new List<Vector3>();
        _triangles = new List<int>();

        // Go through all the squares of the square grid and triangulate them
        for (int i = 0; i < _squareGrid.Squares.GetLength(0); i++)
        {
            for (int j = 0; j < _squareGrid.Squares.GetLength(1); j++)
            {
                TriangulateSquare(_squareGrid.Squares[i, j]);
            }
        }

        // Create the mesh
        Mesh mesh = new Mesh();
        _caveMesh.mesh = mesh;
        mesh.vertices = _vertices.ToArray();
        mesh.triangles = _triangles.ToArray();
        mesh.RecalculateNormals();

        CreateWallMesh();
    }

    private void TriangulateSquare(Square square)
    {
        // Look through the 16 configuration cases
        // Define each time the triangle to be produced in ccw order
        switch (square.Configuration)
        {
            // No mesh
            case 0:
                break;

            // 1 points:
            case 1:
                MeshFromPoints(square.CenterLeft, square.CenterBottom, square.BottomLeft);
                break;
            case 2:
                MeshFromPoints(square.BottomRight, square.CenterBottom, square.CenterRight);
                break;
            case 4:
                MeshFromPoints(square.TopRight, square.CenterRight, square.CenterTop);
                break;
            case 8:
                MeshFromPoints(square.TopLeft, square.CenterTop, square.CenterLeft);
                break;

            // 2 points:
            case 3:
                MeshFromPoints(square.CenterRight, square.BottomRight, square.BottomLeft, square.CenterLeft);
                break;
            case 6:
                MeshFromPoints(square.CenterTop, square.TopRight, square.BottomRight, square.CenterBottom);
                break;
            case 9:
                MeshFromPoints(square.TopLeft, square.CenterTop, square.CenterBottom, square.BottomLeft);
                break;
            case 12:
                MeshFromPoints(square.TopLeft, square.TopRight, square.CenterRight, square.CenterLeft);
                break;
            case 5:
                MeshFromPoints(square.CenterTop, square.TopRight, square.CenterRight, square.CenterBottom, square.BottomLeft, square.CenterLeft);
                break;
            case 10:
                MeshFromPoints(square.TopLeft, square.CenterTop, square.CenterRight, square.BottomRight, square.CenterBottom, square.CenterLeft);
                break;

            // 3 point:
            case 7:
                MeshFromPoints(square.CenterTop, square.TopRight, square.BottomRight, square.BottomLeft, square.CenterLeft);
                break;
            case 11:
                MeshFromPoints(square.TopLeft, square.CenterTop, square.CenterRight, square.BottomRight, square.BottomLeft);
                break;
            case 13:
                MeshFromPoints(square.TopLeft, square.TopRight, square.CenterRight, square.CenterBottom, square.BottomLeft);
                break;
            case 14:
                MeshFromPoints(square.TopLeft, square.TopRight, square.BottomRight, square.CenterBottom, square.CenterLeft);
                break;

            // 4 point:
            case 15:
                MeshFromPoints(square.TopLeft, square.TopRight, square.BottomRight, square.BottomLeft);
                // In this case, none of the vertices can be outlines, so we can mark them as checked
                _checkedVertices.Add(square.TopLeft.VertexIndex);
                _checkedVertices.Add(square.TopRight.VertexIndex);
                _checkedVertices.Add(square.BottomRight.VertexIndex);
                _checkedVertices.Add(square.BottomLeft.VertexIndex);
                break;
        }
    }

    // Creates a mesh based on points
    private void MeshFromPoints(params Node[] points)
    {
        AssignVertices(points);

        if (points.Length >= 3) // 3 or more points, we create a triangle
            CreateTriangle(points[0], points[1], points[2]);
        if (points.Length >= 4) // 4 or more points, we create another triangle
            CreateTriangle(points[0], points[2], points[3]);
        if (points.Length >= 5) // 5 or more points, we create another triangle
            CreateTriangle(points[0], points[3], points[4]);
        if (points.Length >= 6) // 6 points, we create another triangle
            CreateTriangle(points[0], points[4], points[5]);
    }

    private void AssignVertices(Node[] points)
    {
        for (int i = 0; i < points.Length; i++)
        {
            if (points[i].VertexIndex == -1) // Point not assigned
            {
                points[i].VertexIndex = _vertices.Count;
                _vertices.Add(points[i].Position);
            }
        }
    }

    private void CreateTriangle(Node a, Node b, Node c)
    {
        _triangles.Add(a.VertexIndex);
        _triangles.Add(b.VertexIndex);
        _triangles.Add(c.VertexIndex);

        Triangle triangle = new Triangle(a.VertexIndex, b.VertexIndex, c.VertexIndex);
        AddTriangleToDictionary(triangle.vertexIndexA, triangle);
        AddTriangleToDictionary(triangle.vertexIndexB, triangle);
        AddTriangleToDictionary(triangle.vertexIndexC, triangle);
    }

    private void AddTriangleToDictionary(int index, Triangle triangle)
    {
        if (_triangleDictionary.ContainsKey(index))
        {
            _triangleDictionary[index].Add(triangle);
        }
        else
        {
            List<Triangle> newTriangleList = new List<Triangle>();
            newTriangleList.Add(triangle);
            _triangleDictionary.Add(index, newTriangleList);
        }
    }

    // If the two vertices only share one triangle, it is an outline edge
    private bool IsOutlineEdge(int vertexA, int vertexB)
    {
        List<Triangle> vertexATriangles = _triangleDictionary[vertexA];
        int sharedTriangleCount = 0;

        for (int i = 0; i < vertexATriangles.Count; i++)
        {
            if (vertexATriangles[i].Contains(vertexB))
            {
                sharedTriangleCount++;
                if (sharedTriangleCount > 1)
                {
                    break;
                }
            }
        }
        return sharedTriangleCount == 1;
    }

    // Find another vertex to form an outline edge
    private int GetConnectedOutlineVertex(int vertexIndex)
    {
        List<Triangle> vertexIndexTriangles = _triangleDictionary[vertexIndex];

        // Loop through all the vertices in thess triangles, check if they are outline, and if so return the index
        for (int i = 0; i < vertexIndexTriangles.Count; i++)
        {
            Triangle triangle = vertexIndexTriangles[i];

            for (int j = 0; j < 3; j++)
            {
                int vertexB = triangle[j];

                if (vertexB != vertexIndex && !_checkedVertices.Contains(vertexB))
                {
                    if (IsOutlineEdge(vertexIndex, vertexB))
                    {
                        return vertexB;
                    }
                }
            }
        }

        return -1; // Outline not found
    }

    // Go through all the vertices in the map and find the outlines
    private void CalculateMeshOutlines()
    {

        for (int vertexIndex = 0; vertexIndex < _vertices.Count; vertexIndex++)
        {
            if (!_checkedVertices.Contains(vertexIndex))
            {
                int newOutlineVertex = GetConnectedOutlineVertex(vertexIndex);

                if (newOutlineVertex != -1) // Outline found
                {
                    _checkedVertices.Add(vertexIndex);
                    List<int> newOutline = new List<int>();
                    newOutline.Add(vertexIndex);
                    _outlines.Add(newOutline);
                    FollowOutline(newOutlineVertex, _outlines.Count - 1);
                    _outlines[_outlines.Count - 1].Add(vertexIndex); // Add the original vertex to close the outline (make it loop)
                }
            }
        }
    }

    private void FollowOutline(int vertexIndex, int outlineIndex)
    {
        _outlines[outlineIndex].Add(vertexIndex);
        _checkedVertices.Add(vertexIndex);
        int nextVertexIndex = GetConnectedOutlineVertex(vertexIndex);

        if (nextVertexIndex != -1)
        {
            FollowOutline(nextVertexIndex, outlineIndex);
        }
    }

    private void CreateWallMesh()
    {
        CalculateMeshOutlines();

        List<Vector3> wallVertices = new List<Vector3>();
        List<int> wallTriangles = new List<int>();
        Mesh wallMesh = new Mesh();

        foreach (List<int> outline in _outlines)
        {
            for (int i = 0; i < outline.Count - 1; i++)
            {
                int startIndex = wallVertices.Count;
                wallVertices.Add(_vertices[outline[i]]); // Left vertex
                wallVertices.Add(_vertices[outline[i + 1]]); // Right vertex
                wallVertices.Add(_vertices[outline[i]] - Vector3.up * _wallHeight); // Bottom Left vertex
                wallVertices.Add(_vertices[outline[i + 1]] - Vector3.up * _wallHeight); // Bottom Right vertex

                // We view the walls from inside, so ccw order
                wallTriangles.Add(startIndex + 0);
                wallTriangles.Add(startIndex + 2);
                wallTriangles.Add(startIndex + 3);

                wallTriangles.Add(startIndex + 3);
                wallTriangles.Add(startIndex + 1);
                wallTriangles.Add(startIndex + 0);
            }
        }
        wallMesh.vertices = wallVertices.ToArray();
        wallMesh.triangles = wallTriangles.ToArray();
        _wallMesh.mesh = wallMesh;

        // Create the wall colider
        _wallCollider.sharedMesh = wallMesh;
    }
}
