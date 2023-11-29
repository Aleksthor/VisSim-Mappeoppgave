using AT.BSpline;
using AT.HeightMap;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace AT.Raindrop
{

    public class DropletManager : MonoBehaviour
    {
        [SerializeField] GameObject spline_prefab;
        Heightmap map;
        List<GameObject> splines = new List<GameObject>();
        [SerializeField] float friction = 0.5f;
        float update_spline_timer = 0f;

        // Update is called once per frame
        void Update()
        {
            if(map == null)
            {
                map = GameObject.Find("Heightmap").GetComponent<Heightmap>();
            }
            Vector2 min = map.min;
            Vector2 max = map.max;
            Vector2 pos = new Vector3(Random.Range(min.x + 5f, max.x - 5f), Random.Range(min.y + 5f, max.y - 5f));
            int2 xy = map.FindMapPos(pos.x, pos.y);
            float y = 450f;
            if (xy.x > 0)
            {
                y = map.GetHeight(xy);
            }

            Vector3 position = SimulateDroplet(new Vector3(pos.x, y, pos.y));
            int2 end_xy = map.FindMapPos(position.x, position.z);
            map.RainFall(end_xy);


            update_spline_timer += Time.deltaTime;
            if (update_spline_timer > 30f)
            {
                update_spline_timer = 0f;
                map.RecalcSpline();
            }
        }

        public Vector3 SimulateDroplet(Vector3 position)
        {
            Vector3 pos = position;
            Vector3 vel = Vector3.zero;
            Heightmap hm = GameObject.Find("Heightmap").GetComponent<Heightmap>();
            int2 xy = hm.FindMapPos(pos.x, pos.z);
            if (xy.x > 0)
                pos = new Vector3(pos.x, hm.GetHeight(xy) + 1f, pos.z);

            float dt = 0.1f;
            float time = 0f;
            float time_lived = 0f;
            float spline_interval = 0.1f;
            float mass = 0.1f;
            AT.BSpline.BSpline spline = null;
            for (int iteration = 0; iteration < 1000; iteration++)
            {
                time += dt;
                time_lived += dt;
                if (time > spline_interval && spline != null)
                {
                    spline.AddVertex(pos + new Vector3(0, 1f, 0));
                }

                // Add Forces
                Vector3 gravity_force = new Vector3(0, -9.81f * mass, 0);
                Vector3 normal_force = new Vector3();
                Vector3 friction_force = new Vector3();
                Vector3 normal_unit_vector;
                if (xy.x >= 0 && xy.y >= 0 && xy.x < hm.GetResolution() - 1 && xy.y < hm.GetResolution() - 1)
                {
                    normal_unit_vector = CheckCollission(pos, vel, hm, xy);


                    if (normal_unit_vector != Vector3.one * -1f)
                    {
                        if (spline == null)
                        {
                            GameObject go = Instantiate(spline_prefab, pos, Quaternion.identity);
                            spline = go.AddComponent<AT.BSpline.BSpline>();
                            splines.Add(go);
                            if (splines.Count > 100)
                            {
                                GameObject dest = splines[0];
                                splines.RemoveAt(0);
                                Destroy(dest);
                            }
                            spline.AddVertex(pos);
                            time = 0f;
                        }

                        // Normal Force
                        normal_force = -Vector3.Dot(normal_unit_vector, gravity_force) * normal_unit_vector;
                        Vector3 Vnormal = Vector3.Dot(vel, normal_unit_vector) * normal_unit_vector;
                        vel = vel - Vnormal;

                        // Friction Force
                        float normal = normal_force.magnitude;
                        float length = normal * friction;
                        Vector3 dir = vel.normalized * -1f;
                        friction_force = dir * length;

                    }
                }
                else
                {
                    break;
                }

                Vector3 acc = AddForce(gravity_force + normal_force + friction_force, mass);

                vel += acc * dt;
                if (vel.magnitude < 1f && time_lived > 5f)
                {
                    spline.SetColor(Color.green);
                    break;
                }
                pos += vel * dt;
                xy = hm.FindMapPosFast(xy, pos.x, pos.z);
            }

            if (spline != null)
            {
                spline.CalculateKnots(2);
                spline.CreateSpline();
            }
            return pos;
        }

        Vector3 CheckCollission(Vector3 pos, Vector3 vel, Heightmap hm, int2 xy)
        {
            Vector2 X = new Vector2(pos.x, pos.z);
            List<Vector3> ALL = hm.GetSquare(xy);
            Vector2 P = new Vector2(ALL[0].x, ALL[0].z);
            Vector2 Q = new Vector2(ALL[2].x, ALL[2].z);
            Vector2 R = new Vector2(ALL[1].x, ALL[1].z);


            Vector3 bary1 = Bary(X, Q, R, P);

            P = new Vector2(ALL[1].x, ALL[1].z);
            Q = new Vector2(ALL[2].x, ALL[2].z);
            R = new Vector2(ALL[3].x, ALL[3].z);

            Vector3 bary2 = Bary(X, Q, R, P);

            if (bary1.x >= 0f && bary1.x <= 1f && bary1.y >= 0f && bary1.y <= 1f && bary1.z >= 0f && bary1.z <= 1f)
            {
                float y = bary1.x * ALL[0].y + bary1.y * ALL[2].y + bary1.z * ALL[1].y;

                if (pos.y - 1f + vel.y < y)
                {
                    Vector3 PQ = ALL[2] - ALL[0];
                    Vector3 PR = ALL[1] - ALL[0];

                    Vector3 normal_unit_vector = Vector3.Cross(PQ, PR);
                    normal_unit_vector = normal_unit_vector.normalized;

                    return normal_unit_vector;
                }
            }
            else if (bary2.x >= 0f && bary2.x <= 1f && bary2.y >= 0f && bary2.y <= 1f && bary2.z >= 0f && bary2.z <= 1f)
            {
                float y = (bary2.x * ALL[1].y) + (bary2.y * ALL[2].y) + (bary2.z * ALL[3].y);
                if (pos.y - 1f + vel.y < y)
                {
                    Vector3 PQ = ALL[2] - ALL[1];
                    Vector3 PR = ALL[3] - ALL[1];

                    Vector3 normal_unit_vector = Vector3.Cross(PQ, PR);
                    normal_unit_vector = normal_unit_vector.normalized;

                    return normal_unit_vector;
                }
            }
            return Vector3.one * -1f;

        }
        Vector3 Bary(Vector2 playerxz, Vector2 P, Vector2 Q, Vector2 R)
        {
            Vector2 PQ = Q - P;
            Vector2 PR = R - P;

            Vector3 PQR = Cross2D(PQ, PR);
            float normal = PQR.magnitude;


            Vector2 XP = P - playerxz;
            Vector2 XQ = Q - playerxz;
            Vector2 XR = R - playerxz;

            float x = Cross2D(XP, XQ).z / normal;
            float y = Cross2D(XQ, XR).z / normal;
            float z = Cross2D(XR, XP).z / normal;

            return new Vector3(x, y, z);
        }
        Vector3 Cross2D(Vector2 A, Vector2 B)
        {
            Vector3 cross = new Vector3();

            cross.z = (A.x * B.y) - (A.y * B.x);

            return cross;
        }
        static Vector3 AddForce(Vector3 force, float mass)
        {
            return force / mass;
        }
    }

}
