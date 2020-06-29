using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using JetBrains.Annotations;
using UnityEditor;
using UnityEngine;

namespace Unity.QuickSearch.Providers
{
    class ImageProvider
    {
        const string k_Type = "img";
        const string k_DisplayName = "Image";

        static List<ImageDatabase> m_ImageIndexes;

        static QueryEngine<ImageData> s_QueryEngine;
        static List<ImageDatabase> indexes
        {
            get
            {
                if (m_ImageIndexes == null)
                {
                    UpdateImageIndexes();
                }

                return m_ImageIndexes;
            }
        }

        [UsedImplicitly, SearchItemProvider]
        internal static SearchProvider CreateProvider()
        {
            return new SearchProvider(k_Type, k_DisplayName)
            {
                filterId = "img:",
                showDetails = true,
                showDetailsOptions = ShowDetailsOptions.Inspector,
                fetchItems = (context, items, provider) => SearchItems(context, provider),
                // toObject = (item, type) => GetItemObject(item),
                isExplicitProvider = true,
                // fetchDescription = FetchDescription,
                fetchThumbnail = (item, context) => GetAssetThumbnailFromPath(item.id),
                fetchPreview = (item, context, size, options) => GetAssetPreviewFromPath(item.id, options),
                // trackSelection = (item, context) => TrackSelection(item),
                // fetchKeywords = FetchKeywords,
                // startDrag = (item, context) => DragItem(item, context),
                onEnable = OnEnable
            };
        }

        public static Texture2D GetAssetThumbnailFromPath(string path)
        {
            Texture2D thumbnail = AssetDatabase.GetCachedIcon(path) as Texture2D;
            return thumbnail ?? UnityEditorInternal.InternalEditorUtility.FindIconForFile(path);
        }

        public static Texture2D GetAssetPreviewFromPath(string path, FetchPreviewOptions previewOptions)
        {
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (tex)
                return tex;

            if (!previewOptions.HasFlag(FetchPreviewOptions.Large))
            {
                var assetType = AssetDatabase.GetMainAssetTypeAtPath(path);
                if (assetType == typeof(AudioClip))
                    return GetAssetThumbnailFromPath(path);
            }

            var obj = AssetDatabase.LoadMainAssetAtPath(path);
            if (obj == null)
                return null;
            return GetAssetPreview(obj, previewOptions);
        }

        public static Texture2D GetAssetPreview(UnityEngine.Object obj, FetchPreviewOptions previewOptions)
        {
            var preview = AssetPreview.GetAssetPreview(obj);
            if (preview == null || previewOptions.HasFlag(FetchPreviewOptions.Large))
            {
                var largePreview = AssetPreview.GetMiniThumbnail(obj);
                if (preview == null || (largePreview != null && largePreview.width > preview.width))
                    preview = largePreview;
            }
            return preview;
        }

        static void OnEnable()
        {
            if (s_QueryEngine == null)
            {
                s_QueryEngine = new QueryEngine<ImageData>();
                s_QueryEngine.AddFilter("color", (imageData, context) => GetColorSimilitude(imageData, context), param => GetColorFromParameter(param));
                s_QueryEngine.AddFilter("hist", (imageData, context) => GetHistogramSimilitude(imageData, context), param => GetHistogramFromParameter(param));
                s_QueryEngine.SetSearchDataCallback(DefaultSearchDataCallback);
            }
        }

        static double GetColorSimilitude(ImageData imageData, Color paramColor)
        {
            var similarities = imageData.bestShades.Select(colorInfo =>
            {
                var color32 = ImageUtils.IntToColor32(colorInfo.color);
                var color = ImageUtils.Color32ToColor(color32);
                var ratio = colorInfo.ratio;
                return ImageUtils.WeightedSimilarity(color, ratio, paramColor);
            });
            return similarities.Sum();
        }

        static Color GetColorFromParameter(string param)
        {
            if (param.StartsWith("#"))
            {
                ColorUtility.TryParseHtmlString(Convert.ToString(param), out var color);
                return color;
            }

            switch (param)
            {
                case "red": return Color.red;
                case "green": return Color.green;
                case "blue": return Color.blue;
                case "black": return Color.black;
                case "white": return Color.white;
                case "yellow": return Color.yellow;
            }

            return Color.black;
        }

        static float GetHistogramSimilitude(ImageData imageData, Histogram context)
        {
            if (context == null)
                return 0.0f;
            return 1.0f - ImageUtils.HistogramDistance(imageData.histogram, context, HistogramDistance.Bhattacharyya);
        }

        static Histogram GetHistogramFromParameter(string param)
        {
            var sanitizedParam = param.Replace("\\", "/");
            var idb = indexes.FirstOrDefault(db => db.ContainsAsset(sanitizedParam));
            if (idb != null)
            {
                var imageData = idb.GetImageDataFromPath(sanitizedParam);
                return imageData.histogram;
            }

            if (!File.Exists(sanitizedParam))
                return null;

            var texture = AssetDatabase.LoadMainAssetAtPath(sanitizedParam) as Texture2D;
            if (texture == null)
                return null;

            Color32[] pixels;
            if (texture.isReadable)
                pixels = texture.GetPixels32();
            else
            {
                var copy = TextureUtils.CopyTextureReadable(texture, texture.width, texture.height);
                pixels = copy.GetPixels32();
            }
            var histogram = new Histogram();
            ImageUtils.ComputeHistogram(pixels, histogram);
            return histogram;
        }

        static string[] DefaultSearchDataCallback(ImageData data)
        {
            var assetPath = indexes.Select(db => db.GetAssetPath(data.guid)).FirstOrDefault(path => path != null);
            return new[] { assetPath };
        }

        static void UpdateImageIndexes()
        {
            m_ImageIndexes = ImageDatabase.Enumerate().ToList();
        }

        static IEnumerator SearchItems(SearchContext context, SearchProvider provider)
        {
            var searchQuery = context.searchQuery;

            if (searchQuery.Length > 0)
                yield return indexes.Select(db => SearchIndexes(searchQuery, context, provider, db));
        }

        static IEnumerator SearchIndexes(string searchQuery, SearchContext context, SearchProvider provider, ImageDatabase db)
        {
            var query = s_QueryEngine.Parse(searchQuery);

//             var filterNodes = GetFilterNodes(query.evaluationGraph);

            var scoreModifiers = new List<Func<ImageData, int, ImageDatabase, int>>();
//             foreach (var filterNode in filterNodes)
//             {
//                 switch (filterNode.filterId)
//                 {
//                     case "color":
//                     {
//                         var paramColor = GetColorFromParameter(filterNode.paramValue);
//                         scoreModifiers.Add(GetColorScoreModifier(paramColor));
//                         break;
//                     }
//                     case "hist":
//                     {
//                         var paramHist = GetHistogramFromParameter(filterNode.paramValue);
//                         scoreModifiers.Add(GetHistogramScoreModifier(paramHist));
//                         break;
//                     }
//                 }
//             }

            var filteredImageData = query.Apply(db.imagesData);
            foreach (var data in filteredImageData)
            {
                var score = 0;
                foreach (var scoreModifier in scoreModifiers)
                {
                    score = scoreModifier(data, score, db);
                }

                var assetPath = db.GetAssetPath(data.guid);
                var name = Path.GetFileNameWithoutExtension(assetPath);

                yield return provider.CreateItem(context, assetPath, score, name, null, null, null);
            }
        }

//         static IEnumerable<IFilterNode> GetFilterNodes(QueryGraph graph)
//         {
//             return graph.EnumerateNodes().Where(node => node.type == QueryNodeType.Filter).Select(node => node as IFilterNode);
//         }

        static Func<ImageData, int, ImageDatabase, int> GetColorScoreModifier(Color searchedColor)
        {
            return (imageData, inputScore, db) =>
            {
                // var assetPath = db.GetAssetPath(imageData.guid);
                // var color32 = ImageUtils.IntToColor32(imageData.bestShades[0].color);
                // var color = ImageUtils.Color32ToColor(color32);
                // Debug.Log($"{assetPath}: {ImageUtils.CIELabDistance(color, searchedColor)}");
                var similitude = GetColorSimilitude(imageData, searchedColor);
                return inputScore - (int)(similitude * 100);
            };
        }

        static Func<ImageData, int, ImageDatabase, int> GetHistogramScoreModifier(Histogram searchedHistogram)
        {
            return (imageData, inputScore, db) =>
            {
                var similitude = GetHistogramSimilitude(imageData, searchedHistogram);
                return inputScore - (int)(similitude * 100);
            };
        }
    }
}
