//#define DEBUG_INDEXING
using System;
using System.IO;
using System.Linq;
using UnityEditor;

using UnityEngine;

#if UNITY_2020_2_OR_NEWER
using UnityEditor.AssetImporters;
using UnityEditor.Experimental.AssetImporters;
#else
using UnityEditor.Experimental.AssetImporters;
#endif

namespace Unity.QuickSearch
{
    [ExcludeFromPreset, ScriptedImporter(version: 1, importQueueOffset: int.MaxValue, ext: "idb")]
    class ImageDatabaseImporter : ScriptedImporter
    {
        public override void OnImportAsset(AssetImportContext ctx)
        {
            var filePath = ctx.assetPath;
            var fileName = Path.GetFileNameWithoutExtension(filePath);

            hideFlags |= HideFlags.HideInInspector;

            #if DEBUG_INDEXING
            using (new DebugTimer($"Importing image index {fileName}"))
            #endif
            {
                var db = ScriptableObject.CreateInstance<ImageDatabase>();
                db.name = fileName;
                db.hideFlags = HideFlags.NotEditable;

                BuildIndex(db);

                ctx.AddObjectToAsset(fileName, db);
                ctx.SetMainObject(db);
            }
        }

        static void BuildIndex(ImageDatabase idb)
        {
            try
            {
                var assetPaths = AssetDatabase.FindAssets("t:texture2d").Select(AssetDatabase.GUIDToAssetPath);
                var textures = assetPaths.Select(path => AssetDatabase.LoadMainAssetAtPath(path) as Texture2D).Where(t => t);

                var current = 1;
                var total = textures.Count();
                foreach (var assetPath in assetPaths)
                {
                    var texture = AssetDatabase.LoadMainAssetAtPath(assetPath) as Texture2D;
                    if (texture == null)
                        continue;

                    ReportProgress(texture.name, current / (float)total, false, idb);
                    idb.IndexTexture(assetPath, texture);
                    ++current;
                }
                idb.WriteBytes();

                ReportProgress("Indexing Finished", 1.0f, true, idb);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                ReportProgress("Indexing failed", 1.0f, true, idb);
            }
        }

        static void ReportProgress(string description, float progress, bool finished, ImageDatabase idb)
        {
            EditorUtility.DisplayProgressBar($"Building {idb.name} index...", description, progress);
            if (finished)
                EditorUtility.ClearProgressBar();
        }

        public static void CreateIndex(string path)
        {
            var dirPath = path;

            var indexPath = AssetDatabase.GenerateUniqueAssetPath(Path.Combine(dirPath, $"{Path.GetFileNameWithoutExtension(path)}.idb")).Replace("\\", "/");
            File.WriteAllText(indexPath, "");
            AssetDatabase.ImportAsset(indexPath, ImportAssetOptions.ForceSynchronousImport);
            Debug.LogFormat(LogType.Log, LogOption.NoStacktrace, null, $"Generated image index at {indexPath}");
        }

        [MenuItem("Assets/Create/Quick Search/Image Index")]
        internal static void CreateIndexProject()
        {
            var folderPath = AssetDatabase.GetAssetPath(Selection.activeObject);
            CreateIndex(folderPath);
        }
    }
}
