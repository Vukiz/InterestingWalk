using System;
using System.IO;
using System.Linq;
using UnityEngine;

namespace Assets.Scripts  
{
  internal static class GraphExporter
  {
    public static string ToJson(float spawnRate, _Vertex[] vertices, _Edge[] edges)
    {
      MapWrapper mapWrapper = new MapWrapper {SpawnRate =  spawnRate, Vertices = vertices, Edges = edges };
      return JsonUtility.ToJson(mapWrapper);
    }
    public static MapWrapper FromJson(string json)
    {
      MapWrapper mapWrapper = null;
      try
      {
        mapWrapper = JsonUtility.FromJson<MapWrapper>(json);
      }
      catch (Exception e)
      {
        Debug.Log(e.Message);
      }
      return mapWrapper;
    }
    public static void LoadGraph(MapContent mapContent, string loadPath)
    {
      string json = File.ReadAllText(loadPath);
      MapWrapper mapWrapper = FromJson(json);
      mapContent.InitMapFromJson(mapWrapper);
    }

    public static bool SaveGraph(Map mapToSave)
    {
      try
      {
        string json = ToJson(
          mapToSave.SpawnRate,
          mapToSave.Vertices.Select(v => v.Vertex).ToArray(),
          mapToSave.Edges.Select(e => e.Edge).ToArray()
        );
        File.WriteAllText("Map" + DateTime.Now.ToString().Replace(" ","").Replace(":",".") , json);
      }
      catch(Exception exception)
      {
        Debug.Log(exception.Message);
        return false;
      }
      return true;
    }
  }
}
