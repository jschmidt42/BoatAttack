using UnityEngine;

namespace Unity.QuickSearch
{
    struct ColorInfo
    {
        public uint color;
        public double ratio;
    }

    class Histogram
    {
        public const int histogramSize = 256;

        public float[] valuesR;
        public float[] valuesG;
        public float[] valuesB;

        public Histogram()
        {
            valuesR = new float[histogramSize];
            valuesG = new float[histogramSize];
            valuesB = new float[histogramSize];
        }

        public void AddPixel(Color32 pixel)
        {
            ++valuesR[pixel.r];
            ++valuesG[pixel.g];
            ++valuesB[pixel.b];
        }

        public void Normalize(int totalPixels)
        {
            for (var i = 0; i < histogramSize; ++i)
            {
                valuesR[i] /= totalPixels;
                valuesG[i] /= totalPixels;
                valuesB[i] /= totalPixels;
            }
        }
    }

    struct ImageData
    {
        public Hash128 guid;
        public ColorInfo[] bestColors;
        public ColorInfo[] bestShades;
        public Histogram histogram;

        public ImageData(string assetPath)
        {
            guid = Hash128.Compute(assetPath);
            bestColors = new ColorInfo[5];
            bestShades = new ColorInfo[5];
            histogram = new Histogram();
        }

        public ImageData(Hash128 assetGuid)
        {
            guid = assetGuid;
            bestColors = new ColorInfo[5];
            bestShades = new ColorInfo[5];
            histogram = new Histogram();
        }
    }
}
