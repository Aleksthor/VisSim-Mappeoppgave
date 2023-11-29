using Codice.Client.BaseCommands;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using AT.BSpline;

namespace AT.HeightMap
{
    public class Heightmap : MonoBehaviour
    {
        [SerializeField] MeshFilter meshFilter;
        [SerializeField] string path = "Assets/DataFiles/big.txt";
        [SerializeField] int resolution = 500;
        [SerializeField] bool generateResultText = false;
        [SerializeField] bool isPointCloud = true;
        List<Vector3> vertex_positions = new List<Vector3>();

        [SerializeField] Mesh mesh;
        [SerializeField] Material material;
        float xmin;
        float ymin;
        float zmin;
        float vertex_width;
        float vertex_height;
        int cachedInstanceCount = -1;
        private ComputeBuffer positionBuffer;
        private ComputeBuffer argsBuffer;
        private uint[] args = new uint[5] { 0, 0, 0, 0, 0 };

        [SerializeField] bool renderAllPoints = false;

        float[] heightmap;
        List<Vector3> vertices;
        public Vector2 min;
        public Vector2 max;
        [SerializeField] BSplineFlate bSplineFlate;


        // Start is called before the first frame update
        void Awake()
        {
            // Get all the vertices from the text file
            vertex_positions = TextReaderWriter.ReadText(path);

            // Create a mesh object and a vertices array
            Mesh mesh = new Mesh();
            vertices = new List<Vector3>();
            //If we have a pointcloud we consruct a mesh based on a set resolution, else we just copy it and presume its already a mesh
            if (isPointCloud)
            {
                vertices = GetVertices(vertex_positions, resolution);
                mesh.vertices = vertices.ToArray();
            }
            else
            {
                mesh.vertices = vertex_positions.ToArray();
            }
            // calculate triangles based on the resolution 
            mesh.triangles = GetTriangles(resolution).ToArray();
            mesh.RecalculateNormals();
            meshFilter.mesh = mesh;

            // We can also save the new constucted mesh to a text file
            if (generateResultText)
            {
                if (isPointCloud)
                {
                    TextReaderWriter.WriteText(AssetDatabase.GenerateUniqueAssetPath("Assets/DataFiles/generated.txt"), vertices);
                }
            }

            // save the heightmap in a float array so we can use it 
            heightmap = new float[resolution * resolution];
            for (int i = 0; i < vertices.Count; i++)
            {
                if (float.IsNaN(vertices[i].y)) Debug.Log("NAN when inserting heights to heightmap");
                heightmap[i] = vertices[i].y;
            }


            argsBuffer = new ComputeBuffer(1, args.Length * sizeof(uint), ComputeBufferType.IndirectArguments);
            UpdateBuffers();

            // Send data to the BSpline
            List<List<Vector3>> vertex_list = new List<List<Vector3>>();

            for (int i = 0; i < resolution; i++)
            {
                if (i % 5 == 0)
                {
                    vertex_list.Add(new List<Vector3>());
                }
                for (int j = 0; j < resolution; j++)
                {
                    if (j % 5 == 0 && i % 5 == 0)
                    {
                        vertex_list[i / 5].Add(vertices[(i * resolution) + j]);
                    }
                }
            }

            bSplineFlate.SetupBSplineMesh(vertex_list);
        }

        private void Update()
        {
            // Input to start the eroision of the map
            //if (Input.GetKeyDown(KeyCode.E))
            //{
            //    start_erosion = true;
            //}

            // Render the whole point cloud using GPU instancing
            if (renderAllPoints)
            {
                Graphics.DrawMeshInstancedIndirect(mesh, 0, material, new Bounds(Vector3.zero, new Vector3(10000.0f, 10000.0f, 10000.0f)), argsBuffer);
            }
        }

        void UpdateBuffers()
        {

            // positions
            if (positionBuffer != null)
                positionBuffer.Release();
            positionBuffer = new ComputeBuffer(vertex_positions.Count, 12);
            Vector3[] positions = new Vector3[vertex_positions.Count];
            for (int i = 0; i < vertex_positions.Count; i++)
            {
                positions[i] = new Vector3(vertex_positions[i].x - xmin, vertex_positions[i].z - zmin, vertex_positions[i].y - ymin);
            }
            positionBuffer.SetData(positions);
            material.SetBuffer("positionBuffer", positionBuffer);

            // indirect args
            uint numIndices = (mesh != null) ? (uint)mesh.GetIndexCount(0) : 0;
            args[0] = numIndices;
            args[1] = (uint)vertex_positions.Count;
            argsBuffer.SetData(args);

            cachedInstanceCount = vertex_positions.Count;
        }
        public void RainFall(int2 xy)
        {
            if (xy.x < 0 || xy.x >= resolution || xy.y < 0 || xy.y >= resolution)
            {
                return;
            }
            bool not_inserted = true;
            while (not_inserted)
            {
                if (xy.y % 5 != 0)
                {
                    xy.y++;
                }
                if (xy.x % 5 != 0)
                {
                    xy.x++;
                }
                if (xy.x % 5 == 0 && xy.y % 5 == 0)
                {
                    not_inserted = false;
                    if (vertices.Count <= xy.y + (xy.x * resolution))
                    {
                        Debug.Log("Cannot rain here " + xy.x + "," + xy.y);
                    }
                    else
                    {
                        vertices[xy.y + (xy.x * resolution)] += new Vector3(0, 2f, 0);
                    }

                }
            }



        }

        List<Vector3> GetVertices(List<Vector3> vertex_positions, int resolution)
        {
            // Important to remember that z is the up direction in this array
            // Set the min and max to the first element and compare from there
            xmin = vertex_positions[0].x;
            float xmax = vertex_positions[0].x;
            ymin = vertex_positions[0].y;
            float ymax = vertex_positions[0].y;
            zmin = vertex_positions[0].z;
            // Search for x/y min-max
            for (int i = 0; i < vertex_positions.Count; i++)
            {
                if (vertex_positions[i] == Vector3.zero) continue;
                if (vertex_positions[i].x < xmin) xmin = vertex_positions[i].x;
                if (vertex_positions[i].x > xmax) xmax = vertex_positions[i].x;
                if (vertex_positions[i].y < ymin) ymin = vertex_positions[i].y;
                if (vertex_positions[i].y > ymax) ymax = vertex_positions[i].y;
                if (vertex_positions[i].z < zmin) zmin = vertex_positions[i].z;
            }
            float width = xmax - xmin;
            float height = ymax - ymin;
            vertex_width = width / resolution;
            vertex_height = height / resolution;

            // Create a 2D Array that will "sort" all points to their corresponding Vertex
            List<List<List<Vector3>>> areas = new List<List<List<Vector3>>>();
            for (int x = 0; x < resolution; x++)
            {
                areas.Add(new List<List<Vector3>>());
                for (int y = 0; y < resolution; y++)
                {
                    areas[x].Add(new List<Vector3>());
                }
            }
            // The Actual Sorting
            for (int i = 0; i < vertex_positions.Count; i++)
            {
                int x = (int)Map(vertex_positions[i].x, xmin, xmax + 1, 0, resolution);
                int y = (int)Map(vertex_positions[i].y, ymin, ymax + 1, 0, resolution);
                if (x >= areas.Count || x < 0)
                {
                    continue;
                }
                if (y >= areas[x].Count || y < 0)
                {
                    continue;
                }
                if (vertex_positions[i] != Vector3.zero)
                {
                    areas[x][y].Add(vertex_positions[i]);
                }
            }



            // Create that mesh
            List<Vector3> result = new List<Vector3>();
            for (int x = 0; x < resolution; x++)
            {
                for (int y = 0; y < resolution; y++)
                {
                    float z = 0f;
                    for (int i = 0; i < areas[x][y].Count; i++)
                    {
                        z += areas[x][y][i].z;
                    }
                    if (areas[x][y].Count > 0)
                    {
                        result.Add(new Vector3((vertex_width / 2f) + (vertex_width * x), z / areas[x][y].Count - zmin, (vertex_height / 2f) + (vertex_height * y)));
                    }
                    else
                    {
                        result.Add(new Vector3((vertex_width / 2f) + (vertex_width * x), 0f, (vertex_height / 2f) + (vertex_height * y)));
                    }
                }
            }
            min = new Vector2((vertex_width / 2f), (vertex_width / 2f));
            max = new Vector2((vertex_width / 2f) + (vertex_width * resolution), (vertex_width / 2f) + (vertex_width * resolution));
            return result;
        }

        List<int> GetTriangles(int resolution)
        {
            List<int> indexes = new List<int>();

            for (int x = 0; x < resolution - 1; x++)
            {
                for (int y = 0; y < resolution - 1; y++)
                {
                    indexes.Add(x + (y * resolution));
                    indexes.Add(x + (y * resolution) + 1);
                    indexes.Add(x + (y * resolution) + resolution);

                    indexes.Add(x + (y * resolution) + 1);
                    indexes.Add(x + (y * resolution) + resolution + 1);
                    indexes.Add(x + (y * resolution) + resolution);
                }
            }


            return indexes;
        }
        public void RecalcSpline()
        {
            // Send data to the BSpline
            List<List<Vector3>> vertex_list = new List<List<Vector3>>();

            for (int i = 0; i < resolution; i++)
            {
                if (i % 5 == 0)
                {
                    vertex_list.Add(new List<Vector3>());
                }
                for (int j = 0; j < resolution; j++)
                {
                    if (j % 5 == 0 && i % 5 == 0)
                    {
                        vertex_list[i / 5].Add(vertices[(i * resolution) + j]);
                    }
                }
            }

            bSplineFlate.SetupBSplineMesh(vertex_list);

        }

        public static float Map(float value, float from1, float to1, float from2, float to2)
        {
            return (value - from1) / (to1 - from1) * (to2 - from2) + from2;
        }

        public List<Vector3> GetMap()
        {
            return vertices;
        }
        public float GetVertexWidth()
        {
            return vertex_width;
        }
        public float GetVertexHeigth()
        {
            return vertex_height;
        }
        public int GetResolution()
        {
            return resolution;
        }

        public int2 FindMapPos(float x, float z)
        {
            for (int i = 0; i < vertices.Count; i++)
            {
                if (vertices[i].x <= x && vertices[i].x + vertex_width > x && vertices[i].z <= z && vertices[i].z + vertex_height > z)
                {
                    return new int2() { x = (i / resolution), y = i % resolution };
                }
            }

            return new int2() { x = -1, y = -1 };
        }

        public int2 FindMapPosFast(int2 xy, float x, float z)
        {
            if (xy.x < 0 || xy.x >= resolution || xy.y < 0 || xy.y >= resolution)
            {
                return new int2() { x = -1, y = -1 };
            }


            if (vertices.Count > xy.y + (xy.x * resolution) + 1)
            {
                if (vertices[xy.y + (xy.x * resolution) + 1].x <= x &&
                vertices[xy.y + (xy.x * resolution) + 1].x + vertex_width > x &&
                vertices[xy.y + (xy.x * resolution) + 1].z <= z &&
                vertices[xy.y + (xy.x * resolution) + 1].z + vertex_height > z)
                {
                    return new int2()
                    {
                        x = (xy.y + (xy.x * resolution) + 1) / resolution,
                        y = (xy.y + (xy.x * resolution) + 1) % resolution
                    };
                }
            }
            if (vertices.Count > xy.y + (xy.x * resolution) - 1)
            {
                if (vertices[xy.y + (xy.x * resolution) - 1].x <= x &&
                vertices[xy.y + (xy.x * resolution) - 1].x + vertex_width > x &&
                vertices[xy.y + (xy.x * resolution) - 1].z <= z &&
                vertices[xy.y + (xy.x * resolution) - 1].z + vertex_height > z)
                {
                    return new int2()
                    {
                        x = (xy.y + (xy.x * resolution) - 1) / resolution,
                        y = (xy.y + (xy.x * resolution) - 1) % resolution
                    };
                }
            }
            if (vertices.Count > xy.y + (xy.x * resolution) - 1 - resolution && xy.y + (xy.x * resolution) - 1 - resolution >= 0)
            {
                if (vertices[xy.y + (xy.x * resolution) - 1 - resolution].x <= x &&
                vertices[xy.y + (xy.x * resolution) - 1 - resolution].x + vertex_width > x &&
                vertices[xy.y + (xy.x * resolution) - 1 - resolution].z <= z &&
                vertices[xy.y + (xy.x * resolution) - 1 - resolution].z + vertex_height > z)
                {
                    return new int2()
                    {
                        x = (xy.y + (xy.x * resolution) - 1 - resolution) / resolution,
                        y = (xy.y + (xy.x * resolution) - 1 - resolution) % resolution
                    };

                }
            }
            if (vertices.Count > xy.y + (xy.x * resolution) - resolution && xy.y + (xy.x * resolution) - resolution >= 0)
            {
                if (vertices[xy.y + (xy.x * resolution) - resolution].x <= x &&
                vertices[xy.y + (xy.x * resolution) - resolution].x + vertex_width > x &&
                vertices[xy.y + (xy.x * resolution) - resolution].z <= z &&
                vertices[xy.y + (xy.x * resolution) - resolution].z + vertex_height > z)
                {
                    return new int2()
                    {
                        x = (xy.y + (xy.x * resolution) - resolution) / resolution,
                        y = (xy.y + (xy.x * resolution) - resolution) % resolution
                    };

                }
            }
            if (vertices.Count > xy.y + (xy.x * resolution) - resolution + 1 && xy.y + (xy.x * resolution) - resolution + 1 >= 0)
            {
                if (vertices[xy.y + (xy.x * resolution) - resolution + 1].x <= x &&
                vertices[xy.y + (xy.x * resolution) - resolution + 1].x + vertex_width > x &&
                vertices[xy.y + (xy.x * resolution) - resolution + 1].z <= z &&
                vertices[xy.y + (xy.x * resolution) - resolution + 1].z + vertex_height > z)
                {
                    return new int2()
                    {
                        x = (xy.y + (xy.x * resolution) - resolution + 1) / resolution,
                        y = (xy.y + (xy.x * resolution) - resolution + 1) % resolution
                    };

                }
            }
            if (vertices.Count > xy.y + (xy.x * resolution) - 1 - resolution && xy.y + (xy.x * resolution) - 1 - resolution >= 0)
            {
                if (vertices[xy.y + (xy.x * resolution) - 1 - resolution].x <= x &&
                vertices[xy.y + (xy.x * resolution) - 1 - resolution].x + vertex_width > x &&
                vertices[xy.y + (xy.x * resolution) - 1 - resolution].z <= z &&
                vertices[xy.y + (xy.x * resolution) - 1 - resolution].z + vertex_height > z)
                {
                    return new int2()
                    {
                        x = (xy.y + (xy.x * resolution) - 1 - resolution) / resolution,
                        y = (xy.y + (xy.x * resolution) - 1 - resolution) % resolution
                    };

                }
            }
            if (vertices.Count > xy.y + (xy.x * resolution) - resolution && xy.y + (xy.x * resolution) - resolution >= 0)
            {
                if (vertices[xy.y + (xy.x * resolution) - resolution].x <= x &&
                vertices[xy.y + (xy.x * resolution) - resolution].x + vertex_width > x &&
                vertices[xy.y + (xy.x * resolution) - resolution].z <= z &&
                vertices[xy.y + (xy.x * resolution) - resolution].z + vertex_height > z)
                {
                    return new int2()
                    {
                        x = (xy.y + (xy.x * resolution) - resolution) / resolution,
                        y = (xy.y + (xy.x * resolution) - resolution) % resolution
                    };

                }
            }
            if (vertices.Count > xy.y + (xy.x * resolution) - resolution + 1 && xy.y + (xy.x * resolution) - resolution + 1 >= 0)
            {
                if (vertices[xy.y + (xy.x * resolution) - resolution + 1].x <= x &&
                vertices[xy.y + (xy.x * resolution) - resolution + 1].x + vertex_width > x &&
                vertices[xy.y + (xy.x * resolution) - resolution + 1].z <= z &&
                vertices[xy.y + (xy.x * resolution) - resolution + 1].z + vertex_height > z)
                {
                    return new int2()
                    {
                        x = (xy.y + (xy.x * resolution) - resolution + 1) / resolution,
                        y = (xy.y + (xy.x * resolution) - resolution + 1) % resolution
                    };

                }
            }


            if (vertices.Count > xy.y + (xy.x * resolution) - 1 + resolution && xy.y + (xy.x * resolution) - 1 + resolution >= 0)
            {
                if (vertices[xy.y + (xy.x * resolution) - 1 + resolution].x <= x &&
                vertices[xy.y + (xy.x * resolution) - 1 + resolution].x + vertex_width > x &&
                vertices[xy.y + (xy.x * resolution) - 1 + resolution].z <= z &&
                vertices[xy.y + (xy.x * resolution) - 1 + resolution].z + vertex_height > z)
                {
                    return new int2()
                    {
                        x = (xy.y + (xy.x * resolution) - 1 + resolution) / resolution,
                        y = (xy.y + (xy.x * resolution) - 1 + resolution) % resolution
                    };

                }
            }
            if (vertices.Count > xy.y + (xy.x * resolution) + resolution && xy.y + (xy.x * resolution) + resolution >= 0)
            {
                if (vertices[xy.y + (xy.x * resolution) + resolution].x <= x &&
                vertices[xy.y + (xy.x * resolution) + resolution].x + vertex_width > x &&
                vertices[xy.y + (xy.x * resolution) + resolution].z <= z &&
                vertices[xy.y + (xy.x * resolution) + resolution].z + vertex_height > z)
                {
                    return new int2()
                    {
                        x = (xy.y + (xy.x * resolution) + resolution) / resolution,
                        y = (xy.y + (xy.x * resolution) + resolution) % resolution
                    };

                }
            }
            if (vertices.Count > xy.y + (xy.x * resolution) + resolution + 1 && xy.y + (xy.x * resolution) + resolution + 1 >= 0)
            {
                if (vertices[xy.y + (xy.x * resolution) + resolution + 1].x <= x &&
                vertices[xy.y + (xy.x * resolution) + resolution + 1].x + vertex_width > x &&
                vertices[xy.y + (xy.x * resolution) + resolution + 1].z <= z &&
                vertices[xy.y + (xy.x * resolution) + resolution + 1].z + vertex_height > z)
                {
                    return new int2()
                    {
                        x = (xy.y + (xy.x * resolution) + resolution + 1) / resolution,
                        y = (xy.y + (xy.x * resolution) + resolution + 1) % resolution
                    };

                }
            }

            return FindMapPos(x, z);
        }

        public List<Vector3> GetSquare(int2 xy)
        {
            List<Vector3> result = new List<Vector3>()
            {
                vertices[xy.y + (xy.x * resolution)],
                vertices[xy.y + (xy.x * resolution) + 1],
                vertices[xy.y + (xy.x * resolution) + resolution],
                vertices[xy.y + (xy.x * resolution) + resolution + 1]
            };


            return result;
        }

        public float GetHeight(int2 xy)
        {
            if (vertices.Count > xy.y + (xy.x * resolution))
                return vertices[xy.y + (xy.x * resolution)].y;
            else
                return 400;
        }


    }
    public struct int2
    {
        public int x;
        public int y;
    }
}
