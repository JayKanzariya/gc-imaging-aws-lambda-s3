using System;
using System.IO;
using System.Drawing;
using GrapeCity.Documents.Drawing;
using GrapeCity.Documents.Text;
using GrapeCity.Documents.Imaging;

namespace GCImagingAWSLambdaS3
{
    public class GcImagingOperations
    {
        public static string GetGrayScale(Stream stream)
        {
            using (var bmp = new GcBitmap())
            {
                bmp.Load(stream);
                bmp.ApplyEffect(GrayscaleEffect.Get(GrayscaleStandard.BT601));
                return GetBase64(bmp);
            }
        }

        public static string GetThumbnail(Stream stream)
        {
            using (var origBmp = new GcBitmap())
            {
                origBmp.Load(stream);
                var bmp = origBmp.Resize(100, 100, InterpolationMode.NearestNeighbor);
                return GetBase64(bmp);
            }
        }

        public static string GetEnlarged(Stream stream)
        {
            using (var origBmp = new GcBitmap())
            {
                origBmp.Load(stream);
                var bmp = origBmp.Resize(
                    origBmp.PixelWidth * 2, 
                    origBmp.PixelHeight * 2,
                    InterpolationMode.NearestNeighbor);
                return GetBase64(bmp);
            }
        }

        public static string GetWaterMarked(Stream stream)
        {
            using (var bmp = new GcBitmap())
            {
                bmp.Load(stream);
                using (var g = bmp.CreateGraphics(Color.White))
                {
                    g.DrawString("Watermark", new TextFormat
                    {
                        FontSize = 96,
                        ForeColor = Color.FromArgb(128, Color.Yellow),
                        Font = FontCollection.SystemFonts.DefaultFont
                    },
                    new RectangleF(0, 0, bmp.Width, bmp.Height),
                    TextAlignment.Center, ParagraphAlignment.Center, false);
                }
                return GetBase64(bmp);
            }
        }

        public static string GetCustomImage(Stream stream)
        {
            using (var bmp = new GcBitmap())
            {
                bmp.Load(stream);
                // Perform custom operations
                using (var g = bmp.CreateGraphics(Color.White))
                {
                    // draw a solid red border on the image
                    g.DrawRectangle(
                        new RectangleF(0, 0, bmp.Width, bmp.Height),
                        Color.Red,
                        2,
                        DashStyle.Solid);
                    // Add a yellow watermark at bottom right corner
                    var wmRect = g.MeasureString(
                        "Created using GCImagingAWSLambdaS3 Lambda", 
                        new TextFormat
                        {
                            FontSize = 18,
                            ForeColor = Color.FromArgb(128, Color.Yellow),
                            Font = FontCollection.SystemFonts.DefaultFont
                        });
                    g.DrawString(
                        "Created using GCImagingAWSLambdaS3 Lambda",
                        new TextFormat
                        {
                            FontSize = 18,
                            ForeColor = Color.FromArgb(128, Color.Yellow),
                            Font = FontCollection.SystemFonts.DefaultFont
                        },
                        new RectangleF(
                            bmp.Width - wmRect.Width,
                            bmp.Height - wmRect.Height,
                            wmRect.Width,
                            wmRect.Height),
                        TextAlignment.Center, 
                        ParagraphAlignment.Center,
                        false);
                }
                return GetBase64(bmp);
            }
        }

        #region helper
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
        #endregion
    }
}
