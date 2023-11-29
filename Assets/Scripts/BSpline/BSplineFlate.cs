using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Unity.VisualScripting.YamlDotNet.Core.Tokens;
using UnityEngine;

namespace AT.BSpline
{
    public class BSplineFlate : MonoBehaviour
    {
        [SerializeField] List<List<Vector3>> points = new List<List<Vector3>>();
        [SerializeField] MeshFilter mesh_filter;
        int resolution_u = 30;
        int resolution_v = 40;

        public void SetupBSplineMesh(List<List<Vector3>> _points)
        {
            points = _points;
            resolution_u = points.Count;
            resolution_v = points[0].Count;
            float increment = (points[0][1] - points[0][0]).magnitude;

            List<List<Vector3>> spline = CreateSpline(points, 3, increment);

            Vector3[] vertices = new Vector3[spline.Count * spline[0].Count];
            for (int i = 0; i < spline.Count; i++)
            {
                for (int j = 0; j < spline[i].Count; j++)
                {
                    vertices[(i * spline.Count) + j] = spline[i][j];
                }
            }

            int[] triangles = GetTriangles(spline.Count).ToArray();

            // Create a mesh object and a vertices array
            Mesh mesh = new Mesh();

            mesh.vertices = vertices;
            // calculate triangles based on the resolution 
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh_filter.mesh = mesh;
        }

        public void FlipNormals()
        {
            Vector3[] normals = mesh_filter.mesh.normals;
            for (int i = 0; i < normals.Length; i++)
            {
                normals[i] *= -1f;
            }
            mesh_filter.mesh.normals = normals;
        }



        public List<List<Vector3>> CreateSpline(List<List<Vector3>> points, int d, float increment)
        {
            List<List<Vector3>> map = new List<List<Vector3>>();

            //Setup the result array
            for (int i = 0; i < resolution_u; i++)
            {
                map.Add(new List<Vector3>());
                for (int j = 0; j < resolution_v; j++)
                {
                    map[i].Add(new Vector3());
                }
            }

            // Calculate knots
            List<float> u = CalculateKnots(d, points.Count);
            List<float> v = CalculateKnots(d, points[0].Count);
            // Step size along the curve
            float increment_u = 1f;
            float increment_v = 1f;

            float interval_u = 0f;
            float interval_v = 0f;
            for (int i = 0; i < resolution_u; i++)
            {
                interval_v = 0;
                for (int j = 0; j < resolution_v; j++)
                {
                    map[i][j] = new Vector3();
                    for (int ki = 0; ki < points.Count; ki++)
                    {
                        for (int kj = 0; kj < points[ki].Count; kj++)
                        {
                            float bi = B(ki, d, interval_u, u);
                            float bj = B(kj, d, interval_v, v);

                            map[i][j] += points[ki][kj] * bi * bj;
                        }
                    }

                    interval_v += increment_v;
                }
                interval_u += increment_u;
            }
            interval_u = 0;
            for (int i = 0; i < resolution_u; i++)
            {
                map[i][resolution_v - 1] = new Vector3();
                for (int ki = 0; ki < points.Count; ki++)
                {
                    float bi = B(ki, d, interval_u, u);
                    map[i][resolution_v - 1] += points[ki][points[ki].Count - 1] * bi;
                }
                interval_u += increment_u;
            }
            interval_v = 0;
            for (int j = 0; j < resolution_v; j++)
            {
                map[resolution_u - 1][j] = new Vector3();
                for (int kj = 0; kj < points[points.Count - 1].Count; kj++)
                {
                    float bj = B(kj, d, interval_v, v);
                    map[resolution_u - 1][j] += points[points.Count - 1][kj] * bj;
                }
                interval_v += increment_v;

            }
            map[resolution_u - 1][resolution_v - 1] = points[points.Count - 1][points[points.Count - 1].Count - 1];

            for (int i = map.Count - 1; i >= 0; i--)
            {
                for (int j = map[i].Count - 1; j >= 0; j--)
                {
                    if (map[i][j] == Vector3.zero)
                    {
                        map[i].RemoveAt(j);
                    }
                    if (map[i].Count == 0)
                    {
                        map.RemoveAt(i);
                    }
                }
            }
            int size = map[0].Count;
            for (int i = map.Count - 1; i >= 0; i--)
            {
                if (size != map[i].Count)
                {
                    Debug.Log("NON SQUARE " + size + "," + map[i].Count + ", " + i);
                }
            }

            return map;
        }
        public List<float> CalculateKnots(int degree, int length)
        {
            List<float> knots = new List<float>();
            int knot = 0;
            for (int i = 0; i < length + degree + 1; i++)
            {
                if (i > degree && i <= length) { knot++; }
                knots.Add(knot);
            }
            return knots;
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

        float B(int i, int d, float t, List<float> knot_vector)
        {
            if (d == 0)
            {
                return t >= knot_vector[i] && t < knot_vector[i + 1] ? 1 : 0;
            }

            return (W(i, d, t, knot_vector) * B(i, d - 1, t, knot_vector)) + ((1 - W(i + 1, d, t, knot_vector)) * B(i + 1, d - 1, t, knot_vector));


        }

        float W(int i, int d, float t, List<float> knot_vector)
        {
            if (knot_vector.Count <= i + d)
            {
                Debug.Log("Knot Vector too small : " + knot_vector.Count + " i + d = " + (i + d));
            }
            return knot_vector[i] < knot_vector[i + d] ? (t - knot_vector[i]) / (knot_vector[i + d] - knot_vector[i]) : 0;
        }

        string ListToString(List<float> list)
        {
            string log = "( ";
            for (int i = 0; i < list.Count; i++)
            {
                log += list[i];
                if (i != list.Count - 1)
                    log += ", ";
            }
            log += ")";
            return log;
        }

    }
}