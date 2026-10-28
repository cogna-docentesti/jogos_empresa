using System;
using System.IO;
using UnityEngine;

/// <summary>Allows final QA to reuse the production interaction suites without overwriting their historical evidence.</summary>
public static class UiValidationContext
{
    public static string OutputRoot;
    public static Vector2Int[] Viewports;
    public static Action<string, string> OnCapture;
    public static Vector2Int[] Resolutions => Viewports ?? new[] { new Vector2Int(1920, 1080), new Vector2Int(1366, 768), new Vector2Int(1280, 720) };
    public static string DirectoryFor(string task) => OutputRoot ?? "output/ui-update/" + task;
    public static string FileFor(string task, string file) => Path.Combine(DirectoryFor(task), file);
    public static string CaptureName(string task, string name) => Path.GetRelativePath("output/ui-update/UI-01", Path.Combine(DirectoryFor(task), name)).Replace('\\', '/');
}
