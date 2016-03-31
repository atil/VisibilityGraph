﻿using System;
using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Pathfinding;
using Pathfinding.SelfBalancedTree;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class PathEdge : IEquatable<PathEdge>
{
    public Vertex Vertex1 { get; private set; }
    public Vertex Vertex2 { get; private set; }

    public PathEdge(Vertex v1, Vertex v2)
    {
        Vertex1 = v1;
        Vertex2 = v2;
    }

    public bool Equals(PathEdge other)
    {
        return (Vertex1 == other.Vertex1 && Vertex2 == other.Vertex2)
               || (Vertex1 == other.Vertex2 && Vertex2 == other.Vertex1);
    }
}

public class Test : MonoBehaviour
{
    private readonly List<Polygon> _polygons = new List<Polygon>();
    private readonly AVLTree<Edge> _bst = new AVLTree<Edge>();
    private readonly HashSet<PathEdge> _pathEdges = new HashSet<PathEdge>();
    private List<GameObject> _obstacleGos = new List<GameObject>(); 

    private readonly List<Vertex> _allVertices = new List<Vertex>(); 

    void Start()
    {
        _obstacleGos = GameObject.FindGameObjectsWithTag("Obstacle").ToList();
        foreach (var obstacleGo in _obstacleGos)
        {
            var floor = GetFloor(obstacleGo.GetComponent<Collider>().bounds);
            _polygons.Add(new Polygon(floor));
        }

        foreach (var polygon in _polygons)
        {
            _allVertices.AddRange(polygon.Vertices);
        }
    }

    void Update()
    {
        CalculateVisiblityGraph();
    }

    void CalculateVisiblityGraph()
    {
        
        foreach (var polygon in _polygons)
        {
            foreach (var vertex in polygon.Vertices)
            {
                var visibleVertices = GetVisibilePoints(vertex, polygon, _allVertices);

                //foreach (var visibleVertex in visibleVertices)
                //{
                //    _pathEdges.Add(new PathEdge(vertex, visibleVertex));
                //}
            }
        }

        //foreach (var pathEdge in _pathEdges)
        //{
        //    Debug.DrawLine(pathEdge.Vertex1.Position, pathEdge.Vertex2.Position, Color.red);
        //}
    }

    private bool IsVisible(Vertex from, Polygon ownerPolygon, Vertex to)
    {
        // Non-neighbor vertices of the same ploygon don't see each other
        if (ownerPolygon.Vertices.Contains(to))
        {
            return false;
        }

        // Neighboring vertices are assumed to be seeing each other
        if (from.IsNeighborWith(to))
        {
            return true;
        }

        var nudgedFrom = from.Position + (to.Position - from.Position).normalized * 0.0001f;
        if (ownerPolygon.IntersectsWith(nudgedFrom, to.Position))
        {
            return false;
        }

        Edge leftMostEdge;
        _bst.GetMin(out leftMostEdge);

        if (leftMostEdge != null && leftMostEdge.IntersectsWith(from.Position.x, from.Position.z, to.Position.x, to.Position.z))
        {
            return false;
        }

        return true;
    }

    private List<Vertex> GetVisibilePoints(Vertex v, Polygon ownerPolygon, List<Vertex> allVertices)
    {
        var visiblePoints = new List<Vertex>();
        var sortedEvents = Util.SortClockwise(v.Position, allVertices);

        var ray = new Ray(v.Position, Vector3.right);
        foreach (var polygon in _polygons)
        {
            foreach (var edge in polygon.Edges)
            {
                float t;
                if (edge.IntersectsWith(ray, out t))
                {
                    edge.DistanceToReference = t;
                    _bst.Add(edge);
                }
            }
        }

        foreach (var eventPoint in sortedEvents)
        {
            if (eventPoint == v)
            {
                continue;
            }

            if (IsVisible(v, ownerPolygon, eventPoint))
            {
                visiblePoints.Add(eventPoint);
            }

            // Algorithm adds CW edges, then deletes CCW ones
            // The reverse is done here,
            // Reason is because when an edge is added with a ref distance value "d" already exists in the tree
            // Removing the older "d" also removes the newly added one
            foreach (var edge in eventPoint.GetEdgesOnCCwSide(v))
            {
                if (edge != null)
                {
                    _bst.Delete(edge);
                }
            }
            foreach (var edge in eventPoint.GetEdgesOnCwSide(v))
            {
                if (edge != null)
                {
                    edge.DistanceToReference = edge.DistanceTo(v.Position); // TODO: This line smells
                    _bst.Add(edge);
                }
            }

        }

        _bst.Clear();
        return visiblePoints;
    }

    private Vector3[] GetFloor(Bounds b)
    {
        var bMin = b.min;
        var bMax = b.max;
        var b1 = new Vector3(bMin.x, bMin.y, bMin.z);
        var b2 = new Vector3(bMax.x, bMin.y, bMin.z);
        var b3 = new Vector3(bMax.x, bMin.y, bMax.z);
        var b4 = new Vector3(bMin.x, bMin.y, bMax.z);

        return new[] { b1, b2, b3, b4 };
    }

}
