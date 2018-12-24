using System;
using System.IO;
using System.Drawing;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using GrapeCity.Documents.Drawing;
using GrapeCity.Documents.Text;
using GrapeCity.Documents.Imaging;

namespace GCImagingAWSLambdaS3
{
    public class GcImagingOperations
    {
        public static string GetGrayScale(Stream stream)
        {
            var bmp = new GcBitmap();
            using (var origBmp = new GcBitmap())
            {
                origBmp.Load(stream);
                bmp = new GcBitmap(
                    origBmp.PixelWidth,
                    origBmp.PixelHeight,
                    origBmp.Opaque,
                    origBmp.DpiX,
                    origBmp.DpiY);
                bmp.ApplyEffect(GrayscaleEffect.Get(GrayscaleStandard.BT601));
            }
            return GetBase64(bmp);
        }

        private static string GetBase64(GcBitmap bmp)
        {
            using (Image image = Image.FromGcBitmap(bmp, true))
            {
                using (MemoryStream m = new MemoryStream())
                {
                    bmp.SaveAsPng(m);
                    return Convert.ToBase64String(m.ToArray());
                }
            }
        }
    }
}
