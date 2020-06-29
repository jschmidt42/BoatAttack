using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Unity.QuickSearch
{
    class ImageDatabase : ScriptableObject
    {
        Dictionary<Hash128, string> m_HashToFileMap = new Dictionary<Hash128, string>();
        Dictionary<string, int> m_AssetPathToIndex = new Dictionary<string, int>();

        [SerializeField] public new string name;
        [SerializeField, HideInInspector] public byte[] bytes;

        public List<ImageData> imagesData { get; set; } = new List<ImageData>();

        internal void OnEnable()
        {
            if (bytes == null)
                bytes = new byte[0];
            else
            {
                if (bytes.Length > 0)
                    Load();
            }
        }

        public string GetAssetPath(Hash128 hash)
        {
            if (m_HashToFileMap.TryGetValue(hash, out var path))
                return path;
            return null;
        }

        public bool ContainsAsset(string assetPath)
        {
            return m_AssetPathToIndex.ContainsKey(assetPath);
        }

        public ImageData GetImageDataFromPath(string assetPath)
        {
            if (!m_AssetPathToIndex.TryGetValue(assetPath, out var index))
                throw new Exception("Asset path not found.");
            return imagesData[index];
        }

        public void IndexTexture(string assetPath, Texture2D texture)
        {
            var imageIndexData = new ImageData(assetPath);
            IndexColors(imageIndexData, texture);
            imagesData.Add(imageIndexData);
            m_HashToFileMap.Add(imageIndexData.guid, assetPath);
        }

        public static IEnumerable<ImageDatabase> Enumerate()
        {
            const string imageDataFindAssetQuery = "t:ImageDatabase a:all";
            return AssetDatabase.FindAssets(imageDataFindAssetQuery).Select(AssetDatabase.GUIDToAssetPath)
                .Select(path => AssetDatabase.LoadAssetAtPath<ImageDatabase>(path));
        }

        public void WriteBytes()
        {
            using (var ms = new MemoryStream())
            {
                WriteTo(ms);
                bytes = ms.ToArray();
            }
        }

        void WriteTo(Stream s)
        {
            using (var bw = new BinaryWriter(s))
            {
                // Hashes
                bw.Write(m_HashToFileMap.Count);
                foreach (var kvp in m_HashToFileMap)
                {
                    bw.Write(kvp.Key.ToString());
                    bw.Write(kvp.Value);
                }

                // Images data
                bw.Write(imagesData.Count);
                foreach (var imageData in imagesData)
                {
                    bw.Write(imageData.guid.ToString());

                    bw.Write(imageData.bestColors.Length);
                    foreach (var bestColor in imageData.bestColors)
                    {
                        bw.Write(bestColor.color);
                        bw.Write(bestColor.ratio);
                    }

                    bw.Write(imageData.bestShades.Length);
                    foreach (var bestShade in imageData.bestShades)
                    {
                        bw.Write(bestShade.color);
                        bw.Write(bestShade.ratio);
                    }

                    WriteHistogram(imageData.histogram.valuesR, bw);
                    WriteHistogram(imageData.histogram.valuesG, bw);
                    WriteHistogram(imageData.histogram.valuesB, bw);
                }
            }
        }

        static void WriteHistogram(float[] histogram, BinaryWriter bw)
        {
            foreach (var f in histogram)
            {
                bw.Write(f);
            }
        }

        void Load()
        {
            using (var ms = new MemoryStream(bytes))
            {
                ReadFrom(ms);
            }
        }

        void ReadFrom(Stream s)
        {
            using (var br = new BinaryReader(s))
            {
                // Hashes
                var hashCount = br.ReadInt32();
                m_HashToFileMap = new Dictionary<Hash128, string>();
                for (var i = 0; i < hashCount; ++i)
                {
                    var hash = Hash128.Parse(br.ReadString());
                    var value = br.ReadString();

                    m_HashToFileMap[hash] = value;
                }

                // Images data
                var imageDataCount = br.ReadInt32();
                imagesData = new List<ImageData>(imageDataCount);
                m_AssetPathToIndex = new Dictionary<string, int>();
                for (var i = 0; i < imageDataCount; ++i)
                {
                    var guid = Hash128.Parse(br.ReadString());
                    var imageData = new ImageData(guid);

                    var nbColor = br.ReadInt32();
                    imageData.bestColors = new ColorInfo[nbColor];
                    for (var j = 0; j < nbColor; ++j)
                    {
                        var colorInfo = new ColorInfo();
                        colorInfo.color = br.ReadUInt32();
                        colorInfo.ratio = br.ReadDouble();
                        imageData.bestColors[j] = colorInfo;
                    }

                    var nbShades = br.ReadInt32();
                    imageData.bestShades = new ColorInfo[nbShades];
                    for (var j = 0; j < nbShades; ++j)
                    {
                        var colorInfo = new ColorInfo();
                        colorInfo.color = br.ReadUInt32();
                        colorInfo.ratio = br.ReadDouble();
                        imageData.bestShades[j] = colorInfo;
                    }

                    ReadHistogram(imageData.histogram.valuesR, br);
                    ReadHistogram(imageData.histogram.valuesG, br);
                    ReadHistogram(imageData.histogram.valuesB, br);
                    imagesData.Add(imageData);

                    var assetPath = m_HashToFileMap[guid];
                    m_AssetPathToIndex.Add(assetPath, i);
                }
            }
        }

        static void ReadHistogram(float[] histogram, BinaryReader br)
        {
            for (var i = 0; i < Histogram.histogramSize; ++i)
            {
                histogram[i] = br.ReadSingle();
            }
        }

        static void IndexColors(ImageData imageData, Texture2D texture)
        {
            Color32[] pixels;
            if (texture.isReadable)
                pixels = texture.GetPixels32();
            else
            {
                var copy = TextureUtils.CopyTextureReadable(texture, texture.width, texture.height);
                pixels = copy.GetPixels32();
            }

            ImageUtils.ComputeBestColorsAndHistogram(pixels, imageData.bestColors, imageData.bestShades, imageData.histogram);
        }
    }
}
