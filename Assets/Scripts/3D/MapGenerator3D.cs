using UnityEngine;
using System.Collections.Generic;
using System;

public class MapGenerator3D : MonoBehaviour
{
    [Header("Size of the map")]
    [SerializeField] private int _width;
    [SerializeField] private int _height;
    [SerializeField] private int _depth;
    [SerializeField] private int _borderSize;

    [Header("Percentage of walls in the cave")]
    [SerializeField][Range(0,40)] private int _wallPercent;

    // Pseudo random used to have the same results with the same seed: more control
    [Header("Pseudo random")]
    [SerializeField] private string _seed;
    [SerializeField] private bool _useRandomSeed; // Option to use a random seed

    [Header("Smoothing")]
    [SerializeField] private int _smoothIterations;
    [SerializeField] private int _smoothLimit;

    [Header("Marching cubes")]
    [SerializeField] private MeshGenerator3D _meshGen;
    private float _squareSize = 1;

    [Header("Player")]
    [SerializeField] private GameObject _player;
    private GameObject _spawnedPlayer;

    // Two maps are used to apply cellular automata rules in parallel
    private int [,,] _map;
    private int [,,] _smoothedMap;

    public struct Coord
    {
        public int TileX;
        public int TileY;
        public int TileZ;

        public Coord(int x, int y, int z)
        {
            TileX = x;
            TileY = y;
            TileZ = z;
        }
    }

    private void Start()
    {
        GenerateMap();
    }

    private void Update()
    {
        if (Input.GetMouseButtonDown(0))
            GenerateMap();
    }

    private void GenerateMap()
    {
        _map = new int[_width, _height, _depth];
        RandomlyFillMap();

        for (int i = 0; i < _smoothIterations; i++)
        {
            SmoothMap();
            _map = _smoothedMap; // Replace the map by the smoothed map only when an iteration is finished, avoiding bias
        }

        // Remove small regions
        ProcessRegionsAndRooms();

        // Create the borders of the map
        int[,,] borderedMap = new int[_width + _borderSize * 2, _height + _borderSize * 2, _depth + _borderSize * 2];

        for (int i = 0; i < borderedMap.GetLength(0); i++)
        {
            for (int j = 0; j < borderedMap.GetLength(1); j++)
            {
                for (int k = 0; k < borderedMap.GetLength(2); k++)
                {
                    // if we are inside the map
                    if (i >= _borderSize && i < _width + _borderSize && j >= _borderSize && j < _height + _borderSize && k >= _borderSize && k < _depth + _borderSize)
                    {
                        borderedMap[i, j, k] = _map[i - _borderSize, j - _borderSize, k - _borderSize];
                    }
                    // else if we are in the bordered portion
                    else
                    {
                        borderedMap[i, j, k] = 1;
                    }
                }
            }
        }

        // Generate the mesh with the marching cubes method
        if (_meshGen != null)
        {
            _meshGen.GenerateMesh(borderedMap);
        }

        // Place the player in the first empty space we find near the center
        if (_spawnedPlayer)
            Destroy(_spawnedPlayer);
        for (int i = _width / 2; i < borderedMap.GetLength(0); i++)
        {
            for (int j = _height / 2; j < borderedMap.GetLength(1); j++)
            {
                for (int k = _depth / 2; k < borderedMap.GetLength(2); k++)
                {
                    if (borderedMap[i, j, k] == 0)
                    {
                        Vector3 spawnPos = new Vector3(-_width / 2.0f + i * _squareSize + _squareSize / 2.0f, -_depth / 2.0f + k * _squareSize + _squareSize / 2.0f, -_height / 2.0f + j * _squareSize + _squareSize / 2.0f);
                        _spawnedPlayer = Instantiate<GameObject>(_player, spawnPos, Quaternion.identity);
                        return;
                    }
                }
            }
        }
    }

    private void RandomlyFillMap()
    {
        // Override the seed if we chose to use a random one
        if (_useRandomSeed)
            _seed = Time.time.ToString();

        // Pseudo random number generator with the hash code of the seed
        System.Random pseudoRand = new System.Random(_seed.GetHashCode());

        for (int i = 0; i < _width; i++)
        {
            for (int j = 0; j < _height; j++)
            {
                for (int k = 0; k < _depth; k++)
                {
                    if (i == 0 || i == _width - 1 || j == 0 || j == _height - 1 || k == 0 || k == _depth - 1) // Make the boundaries walls
                    {
                        _map[i, j, k] = 1;
                    }
                    else
                    {
                        _map[i, j, k] = pseudoRand.Next(0, 100) < _wallPercent ? 1 : 0;
                    }
                }
            }
        }
    }

    // Used to smooth the randomly generated map by updating the map depending on the walls around
    private void SmoothMap()
    {
        int surroundingWallCount;
        _smoothedMap = (int[,,])_map.Clone();
        for (int i = 0; i < _width; i++)
        {
            for (int j = 0; j < _height; j++)
            {
                for (int k = 0; k < _depth; k++)
                {
                    surroundingWallCount = GetSurroundingWallCount(i, j, k);
                    if (surroundingWallCount > _smoothLimit)
                        _smoothedMap[i, j, k] = 1;
                    else if (surroundingWallCount < _smoothLimit)
                        _smoothedMap[i, j, k] = 0;
                }
            }
        }
    }

    private int GetSurroundingWallCount(int x, int y, int z)
    {
        int wallCount = 0;

        // Go through all the neighbours inside the map
        for (int i = x - 1; i <= x + 1; i++)
        {
            for (int j = y - 1; j <= y + 1; j++)
            {
                for (int k = z - 1; k <= z + 1; k++)
                {
                    if (i >= 0 && i < _width && j >= 0 && j < _height && k >= 0 && k < _depth) // Check that we are inside the boundaries of the map
                    {
                        if (i != x || j != y || k != z) // Get others than the original tile
                        {
                            wallCount += _map[i, j, k];
                        }
                    } // Otherwise, increase the wallcount to produce more walls near the boundaries
                    else
                    {
                        wallCount++;
                    }
                }
            }
        }

        return wallCount;
    }

    #region Regions handling
    // Finds all the tiles of the region around the given coordinates
    List<Coord> GetRegionTiles(int startX, int startY, int startZ)
    {
        List<Coord> tiles = new List<Coord>();
        int[,,] mapFlags = new int[_width, _height, _depth];
        int tileType = _map[startX, startY, startZ];

        Queue<Coord> queue = new Queue<Coord>();
        queue.Enqueue(new Coord(startX, startY, startZ));
        mapFlags[startX, startY, startZ] = 1; // Mark it as processed

        while (queue.Count > 0)
        {
            Coord tile = queue.Dequeue();
            tiles.Add(tile);

            for (int x = tile.TileX - 1; x <= tile.TileX + 1; x++)
            {
                for (int y = tile.TileY - 1; y <= tile.TileY + 1; y++)
                {
                    for (int z = tile.TileZ - 1; z <= tile.TileZ + 1; z++)
                    {
                        if (IsInMapRange(x, y, z) && (y == tile.TileY || x == tile.TileX || z == tile.TileZ)) // In the map
                        {
                            if (mapFlags[x, y, z] == 0 && _map[x, y, z] == tileType) // Not already checked and of the correct type
                            {
                                mapFlags[x, y, z] = 1;
                                queue.Enqueue(new Coord(x, y, z));
                            }
                        }
                    }
                }
            }
        }

        return tiles;
    }

    // Returns all the regions of the given type
    List<List<Coord>> GetRegions(int tileType)
    {
        List<List<Coord>> regions = new List<List<Coord>>();
        int[,,] mapFlags = new int[_width, _height, _depth];

        for (int x = 0; x < _width; x++)
        {
            for (int y = 0; y < _height; y++)
            {
                for (int z = 0; z < _depth; z++)
                {
                    if (mapFlags[x, y, z] == 0 && _map[x, y, z] == tileType) // Not already checked and of the correct type
                    {
                        List<Coord> newRegion = GetRegionTiles(x, y, z);
                        regions.Add(newRegion);

                        // Mark everything in the new region as checked
                        foreach (Coord tile in newRegion)
                        {
                            mapFlags[tile.TileX, tile.TileY, tile.TileZ] = 1;
                        }
                    }
                }
            }
        }

        return regions;
    }

    private void ProcessRegionsAndRooms()
    {
        // Remove wall regions with less than wallThresholdSize tiles
        List<List<Coord>> wallRegions = GetRegions(1);
        int wallThresholdSize = 50;

        foreach (List<Coord> wallRegion in wallRegions)
        {
            if (wallRegion.Count < wallThresholdSize)
            {
                foreach (Coord tile in wallRegion)
                {
                    _map[tile.TileX, tile.TileY, tile.TileZ] = 0;
                }
            }
        }

        // Remove empty regions with less than emptyThresholdSize tiles
        // The one not removed are added to the rooms
        List<List<Coord>> roomRegions = GetRegions(0);
        int roomThresholdSize = 50;
        List<Room> survivingRooms = new List<Room>();

        foreach (List<Coord> roomRegion in roomRegions)
        {
            if (roomRegion.Count < roomThresholdSize)
            {
                foreach (Coord tile in roomRegion)
                {
                    _map[tile.TileX, tile.TileY, tile.TileZ] = 1;
                }
            }
            else
            {
                survivingRooms.Add(new Room(roomRegion, _map));
            }
        }

        // Get the largest room and set it as the main room
        if (survivingRooms.Count > 0)
        {
            survivingRooms.Sort();
            survivingRooms[0].isMainRoom = true;
            survivingRooms[0].isAccessibleFromMainRoom = true;
        }

        ConnectClosestRooms(survivingRooms);
    }

#endregion

    #region Rooms and connections between them
    public class Room : IComparable<Room>
    {
        public List<Coord> tiles;
        public List<Coord> edgeTiles;
        public List<Room> connectedRooms;
        public int roomSize;
        public bool isAccessibleFromMainRoom;
        public bool isMainRoom;

        public Room() { }

        public Room(List<Coord> roomTiles, int[,,] map)
        {
            tiles = roomTiles;
            roomSize = tiles.Count;
            connectedRooms = new List<Room>();
            edgeTiles = new List<Coord>();

            foreach (Coord tile in tiles)
            {
                for (int i = tile.TileX - 1; i <= tile.TileX + 1; i++)
                {
                    for (int j = tile.TileY - 1; j <= tile.TileY + 1; j++)
                    {
                        for (int k = tile.TileZ - 1; k <= tile.TileZ + 1; k++)
                        {
                            if (i == tile.TileX || j == tile.TileY || k == tile.TileZ) // Excluding diagonal neighbours
                            {
                                if (map[i, j, k] == 1) // Wall tile
                                {
                                    edgeTiles.Add(tile);
                                }
                            }
                        }

                    }
                }
            }
        }

        // Mark a room accessible from the biggest room
        public void SetAccessibleFromMainRoom()
        {
            if (!isAccessibleFromMainRoom)
            {
                isAccessibleFromMainRoom = true;
                foreach (Room connectedRoom in connectedRooms)
                {
                    connectedRoom.SetAccessibleFromMainRoom();
                }
            }
        }

        // Connect two rooms with each other
        public static void ConnectRooms(Room roomA, Room roomB)
        {
            if (roomA.isAccessibleFromMainRoom)
            {
                roomB.SetAccessibleFromMainRoom();
            }
            else if (roomB.isAccessibleFromMainRoom)
            {
                roomA.SetAccessibleFromMainRoom();
            }
            roomA.connectedRooms.Add(roomB);
            roomB.connectedRooms.Add(roomA);
        }

        // Check if a room is connected to another
        public bool IsConnected(Room otherRoom)
        {
            return connectedRooms.Contains(otherRoom);
        }

        public int CompareTo(Room otherRoom)
        {
            return otherRoom.roomSize.CompareTo(roomSize);
        }
    }

    // Connect rooms with their closest one, we can force accessibility from the main room to ensure everything is connected
    private void ConnectClosestRooms(List<Room> allRooms, bool forceAccessibilityFromMainRoom = false)
    {

        List<Room> roomListA = new List<Room>();
        List<Room> roomListB = new List<Room>();

        if (forceAccessibilityFromMainRoom)
        {
            foreach (Room room in allRooms)
            {
                if (room.isAccessibleFromMainRoom)
                {
                    roomListB.Add(room);
                }
                else
                {
                    roomListA.Add(room);
                }
            }
        }
        else
        {
            roomListA = allRooms;
            roomListB = allRooms;
        }

        int bestDistance = 0;
        Coord bestTileA = new Coord();
        Coord bestTileB = new Coord();
        Room bestRoomA = new Room();
        Room bestRoomB = new Room();
        bool possibleConnectionFound = false;

        foreach (Room roomA in roomListA)
        {
            if (!forceAccessibilityFromMainRoom)
            {
                possibleConnectionFound = false;
                if (roomA.connectedRooms.Count > 0)
                {
                    continue;
                }
            }

            foreach (Room roomB in roomListB)
            {
                if (roomA == roomB || roomA.IsConnected(roomB))
                {
                    continue;
                }

                for (int tileIndexA = 0; tileIndexA < roomA.edgeTiles.Count; tileIndexA++)
                {
                    for (int tileIndexB = 0; tileIndexB < roomB.edgeTiles.Count; tileIndexB++)
                    {
                        Coord tileA = roomA.edgeTiles[tileIndexA];
                        Coord tileB = roomB.edgeTiles[tileIndexB];
                        int distanceBetweenRooms = (int)(Mathf.Pow(tileA.TileX - tileB.TileX, 2) + Mathf.Pow(tileA.TileY - tileB.TileY, 2) + Mathf.Pow(tileA.TileZ - tileB.TileZ, 2));

                        if (distanceBetweenRooms < bestDistance || !possibleConnectionFound)
                        {
                            bestDistance = distanceBetweenRooms;
                            possibleConnectionFound = true;
                            bestTileA = tileA;
                            bestTileB = tileB;
                            bestRoomA = roomA;
                            bestRoomB = roomB;
                        }
                    }
                }
            }
            if (possibleConnectionFound && !forceAccessibilityFromMainRoom)
            {
                CreatePassage(bestRoomA, bestRoomB, bestTileA, bestTileB);
            }
        }

        if (possibleConnectionFound && forceAccessibilityFromMainRoom)
        {
            CreatePassage(bestRoomA, bestRoomB, bestTileA, bestTileB);
            ConnectClosestRooms(allRooms, true);
        }

        if (!forceAccessibilityFromMainRoom)
        {
            ConnectClosestRooms(allRooms, true);
        }
    }

    private void CreatePassage(Room roomA, Room roomB, Coord tileA, Coord tileB)
    {
        Room.ConnectRooms(roomA, roomB);

        List<Coord> line = GetLine(tileA, tileB);
        foreach (Coord c in line)
        {
            DrawCircle(c, 5);
        }
    }

    private void DrawCircle(Coord c, int r)
    {
        for (int x = -r; x <= r; x++)
        {
            for (int y = -r; y <= r; y++)
            {
                for (int z = -r; z <= r; z++)
                {
                    if (x * x + y * y <= r * r)
                    {
                        int drawX = c.TileX + x;
                        int drawY = c.TileY + y;
                        int drawZ = c.TileZ + z;
                        if (IsInMapRange(drawX, drawY, drawZ))
                        {
                            _map[drawX, drawY, drawZ] = 0;
                        }
                    }
                }
            }
        }
    }

    // Draw line between 2 Coords (their connection, used to create a passage)
    private List<Coord> GetLine(Coord from, Coord to)
    {
        List<Coord> line = new List<Coord>();

        int x = from.TileX;
        int y = from.TileY;
        int z = from.TileZ;

        int dx = to.TileX - from.TileX;
        int dy = to.TileY - from.TileY;
        int dz = to.TileZ - from.TileZ;

        bool inverted = false;
        int step = Math.Sign(dx);
        int gradientStep = Math.Sign(dy);

        int longest = Mathf.Abs(dx);
        int shortest = Mathf.Abs(dy);

        if (longest < shortest)
        {
            inverted = true;
            longest = Mathf.Abs(dy);
            shortest = Mathf.Abs(dx);

            step = Math.Sign(dy);
            gradientStep = Math.Sign(dx);
        }

        int gradientAccumulation = longest / 2;
        for (int i = 0; i < longest; i++)
        {
            line.Add(new Coord(x, y, z));

            if (inverted)
            {
                y += step;
            }
            else
            {
                x += step;
            }

            gradientAccumulation += shortest;
            if (gradientAccumulation >= longest)
            {
                if (inverted)
                {
                    x += gradientStep;
                }
                else
                {
                    y += gradientStep;
                }
                gradientAccumulation -= longest;
            }
        }

        return line;
    }

    #endregion

    private bool IsInMapRange(int x, int y, int z)
    {
        return x >= 0 && x < _width && y >= 0 && y < _height && z >= 0 && z < _depth;
    }

    //// Gizmo to check that everything is working
    //private void OnDrawGizmos()
    //{
    //    Vector3 pos = Vector3.zero;
    //    if (_map != null)
    //    {
    //        for (int i = 0; i < _width; i++)
    //        {
    //            for (int j = 0; j < _height; j++)
    //            {
    //                for (int k = 0; k < _depth; k++)
    //                {
    //                    Gizmos.color = _map[i, j, k] == 1 ? Color.black : Color.white;
    //                    pos.x = -_width / 2 + i + 0.5f;
    //                    pos.y = -_depth / 2 + k + 0.5f;
    //                    pos.z = -_height / 2 + j + 0.5f;
    //                    Gizmos.DrawCube(pos, Vector3.one);
    //                }
    //            }
    //        }
    //    }
    //}
}
