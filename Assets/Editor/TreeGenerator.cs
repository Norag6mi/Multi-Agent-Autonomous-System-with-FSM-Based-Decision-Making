using UnityEngine;
using UnityEditor;
using System.IO;
using System.Text;
using System.Linq;

public class TreeGenerator : EditorWindow
{
[MenuItem("Tools/Copy Specific Scripts Tree")]
public static void CopyScriptsTree()
{
    // Points specifically to your subfolder
    string scriptsPath = Path.Combine(Application.dataPath, "New Multi Agent Version/Scripts");

    if (!Directory.Exists(scriptsPath))
    {
        Debug.LogError($"Folder not found at: {scriptsPath}");
        return;
    }

    StringBuilder sb = new StringBuilder();
    sb.AppendLine("New Multi Agent Version/Scripts/");
    
    // We pass an empty string for the initial indent
    Traverse(new DirectoryInfo(scriptsPath), sb, "");
    
    GUIUtility.systemCopyBuffer = sb.ToString();
    Debug.Log("Specific scripts tree copied to clipboard!");
}


    private static void Traverse(DirectoryInfo dir, StringBuilder sb, string indent)
    {
        // Get relevant files (.cs, .unity, .prefab)
        var files = dir.GetFiles().Where(f => !f.Name.EndsWith(".meta") && 
            (f.Extension == ".cs" || f.Extension == ".unity" || f.Extension == ".prefab")).ToList();
        var subDirs = dir.GetDirectories().Where(d => !d.Name.StartsWith(".")).ToList();

        var children = subDirs.Cast<FileSystemInfo>().Concat(files.Cast<FileSystemInfo>()).ToList();

        for (int i = 0; i < children.Count; i++)
        {
            bool isLast = i == children.Count - 1;
            string marker = isLast ? "└── " : "├── ";
            
            if (children[i] is DirectoryInfo subDir)
            {
                sb.AppendLine($"{indent}{marker}{subDir.Name}/");
                Traverse(subDir, sb, indent + (isLast ? "    " : "│   "));
            }
            else
            {
                sb.AppendLine($"{indent}{marker}{children[i].Name}");
            }
        }
    }
}
