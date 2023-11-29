using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace AT.BSpline
{
    public class BSpline : MonoBehaviour
    {
        [SerializeField] List<float> knot_vector = new List<float>();
        [SerializeField] List<Vector3> points = new List<Vector3>();


        List<Vector3> spline_vertices = new List<Vector3>();
        [SerializeField] float spline_resolution = 0.1f;
        int d = 0;
        Color color = Color.blue;

        private void Update()
        {
            for (int i = 0; i < spline_vertices.Count - 1; i++)
            {
                Debug.DrawLine(spline_vertices[i], spline_vertices[i + 1], color);
            }
        }

        public void AddVertex(Vector3 pos)
        {
            points.Add(pos);
        }

        public void CalculateKnots(int degree)
        {
            d = degree;
            int knot = 0;
            for (int i = 0; i < points.Count + degree + 1; i++)
            {
                if (i > degree && i <= points.Count) { knot++; }
                knot_vector.Add(knot);
            }
        }
        public void CreateSpline()
        {
            CreateSpline(points, d);
        }
        void CreateSpline(List<Vector3> points, int degree)
        {
            for (int i = 0; i < points.Count - 3; i++)
            {
                for (float j = 0; j < 1f; j += spline_resolution)
                {
                    float pos1x = B(i, degree, (float)i + j) * points[i].x;
                    float pos2x = B(i + 1, degree, (float)i + j) * points[i + 1].x;
                    float pos3x = B(i + 2, degree, (float)i + j) * points[i + 2].x;
                    float pos4x = B(i + 3, degree, (float)i + j) * points[i + 3].x;

                    float pos1y = B(i, degree, (float)i + j) * points[i].y;
                    float pos2y = B(i + 1, degree, (float)i + j) * points[i + 1].y;
                    float pos3y = B(i + 2, degree, (float)i + j) * points[i + 2].y;
                    float pos4y = B(i + 3, degree, (float)i + j) * points[i + 3].y;

                    float pos1z = B(i, degree, (float)i + j) * points[i].z;
                    float pos2z = B(i + 1, degree, (float)i + j) * points[i + 1].z;
                    float pos3z = B(i + 2, degree, (float)i + j) * points[i + 2].z;
                    float pos4z = B(i + 3, degree, (float)i + j) * points[i + 3].z;

                    spline_vertices.Add(new Vector3(pos1x + pos2x + pos3x + pos4x, pos1y + pos2y + pos3y + pos4y, pos1z + pos2z + pos3z + pos4z));
                }
            }
        }

        float B(int i, int d, float t)
        {
            if (d == 0)
            {
                return t >= knot_vector[i] && t < knot_vector[i + 1] ? 1 : 0;
            }

            return (W(i, d, t) * B(i, d - 1, t)) + ((1 - W(i + 1, d, t)) * B(i + 1, d - 1, t));
        }

        float W(int i, int d, float t)
        {
            return knot_vector[i] < knot_vector[i + d] ? (t - knot_vector[i]) / (knot_vector[i + d] - knot_vector[i]) : 0;
        }

        public void SetColor(Color _color)
        {
            color = _color;
        }
    }

}