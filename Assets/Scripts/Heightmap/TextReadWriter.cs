using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace AT.HeightMap
{
    public class TextReaderWriter
    {
        public static List<Vector3> ReadText(string path)
        {
            List<Vector3> vertex_positions = new List<Vector3>();
            StreamReader reader = new StreamReader(path);
            string first_line = reader.ReadLine();

            int vertex_count = int.Parse(first_line);

            for (int i = 0; i < vertex_count; i++)
            {
                string line = reader.ReadLine();
                vertex_positions.Add(StringToVector3(line));
            }

            return vertex_positions;
        }

        public static void WriteText(string path, List<Vector3> vertices)
        {
            StreamWriter writer = new StreamWriter(path);
            string first_line = vertices.Count.ToString();
            writer.WriteLine(first_line);

            for (int i = 0; i < vertices.Count; i++)
            {
                string line = vertices[i].x.ToString() + " " + vertices[i].y.ToString() + " " + vertices[i].z.ToString();
                writer.WriteLine(line);
            }
            writer.Close();
        }

        public static Vector3 StringToVector3(string vector_string)
        {
            if (vector_string == null)
            {
                return Vector3.zero;
            }

            if (vector_string.StartsWith("(") && vector_string.EndsWith(")"))
            {
                vector_string = vector_string.Substring(1, vector_string.Length - 2);
            }
            string[] sArray = vector_string.Split(' ');
            float x, y, z;
            if (!float.TryParse(sArray[0], out x))
            {
                return new Vector3();
            }
            if (!float.TryParse(sArray[1], out y))
            {
                return new Vector3();
            }
            if (!float.TryParse(sArray[2], out z))
            {
                return new Vector3();
            }

            Vector3 result = new Vector3(x, y, z);

            return result;
        }
    }

}
