using System;
using System.IO;
using UnityEditor;

namespace Dyma.SplineLevelToolkit.Editor
{
    // Copy missing payload only. Folder existence is never evidence of a complete sample.
    internal static class RoadToolsSampleContent
    {
        internal static bool Repair(string source, string destination, out string message)
        {
            if (!Directory.Exists(source) || !File.Exists(Path.Combine(source,"Scenes/Road Tools Demo.unity")))
                throw new IOException("The installed package has no Demo scene. Update Road Tools before importing.");
            string root = Path.GetFullPath(RoadToolsWorkspace.ToFullPath(destination));
            string assets = Path.GetFullPath(UnityEngine.Application.dataPath) + Path.DirectorySeparatorChar;
            if (!root.StartsWith(assets,StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("Sample destination must be inside Assets.");

            string[] files = Directory.GetFiles(source,"*",SearchOption.AllDirectories);
            int copied = 0, preserved = 0;
            AssetDatabase.StartAssetEditing();
            try
            {
                foreach (string file in files)
                {
                    if (file.EndsWith(".meta",StringComparison.OrdinalIgnoreCase)) continue;
                    string relative = file.Substring(source.TrimEnd('/','\\').Length).TrimStart('/','\\');
                    string target = Path.Combine(root,relative);
                    string guid = ReadGuid(file + ".meta");
                    string existing = ExistingAsset(guid);
                    if (!string.IsNullOrEmpty(existing)) { preserved++; continue; }
                    if (File.Exists(target))
                    {
                        // Preserve authored content, but do not report a broken GUID as a successful repair.
                        string targetGuid = ReadGuid(target + ".meta");
                        if (!string.IsNullOrEmpty(guid) && targetGuid != guid)
                            throw new IOException("Asset GUID conflict at " + target + ". Move this conflicting asset through Unity before retrying; it was kept.");
                        preserved++;
                        continue;
                    }
                    if (File.Exists(target+".meta") && ReadGuid(target+".meta") != guid)
                        throw new IOException("Metadata conflict at " + target + ". Existing metadata was kept.");
                    Directory.CreateDirectory(Path.GetDirectoryName(target));
                    File.Copy(file,target,false);
                    if (File.Exists(file+".meta") && !File.Exists(target+".meta"))
                        File.Copy(file+".meta",target+".meta",false);
                    copied++;
                }
            }
            finally { AssetDatabase.StopAssetEditing(); AssetDatabase.Refresh(); }

            foreach (string file in files)
            {
                if (file.EndsWith(".meta",StringComparison.OrdinalIgnoreCase)) continue;
                string relative = file.Substring(source.TrimEnd('/','\\').Length).TrimStart('/','\\');
                string guid = ReadGuid(file+".meta");
                if (!File.Exists(Path.Combine(root,relative)) && string.IsNullOrEmpty(ExistingAsset(guid)))
                    throw new IOException("Sample asset is still missing: " + relative);
            }
            message = copied > 0
                ? "Demo imported/repaired at " + destination + ": " + copied + " missing files added, " + preserved + " existing files kept. Open Scenes/Road Tools Demo.unity when ready."
                : "Demo is complete. Existing assets were kept at " + destination + ".";
            return true;
        }

        private static string ExistingAsset(string guid)
        {
            if (string.IsNullOrEmpty(guid)) return null;
            string path = AssetDatabase.GUIDToAssetPath(guid);
            return path.StartsWith("Assets/",StringComparison.Ordinal) &&
                   File.Exists(RoadToolsWorkspace.ToFullPath(path)) ? path : null;
        }

        private static string ReadGuid(string metadata)
        {
            if (!File.Exists(metadata)) return null;
            foreach (string line in File.ReadLines(metadata))
                if (line.StartsWith("guid: ",StringComparison.Ordinal)) return line.Substring(6).Trim();
            return null;
        }
    }
}
