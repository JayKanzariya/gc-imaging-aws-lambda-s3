# Create an Imaging service over AWS S3 with GrapeCity Documents for Imaging

![Create your Imaging service on AWS with GcImaging](https://github.com/iwannabebot/gc-imaging-aws-lambda-s3/raw/master/Blog_%20Create%20your%20Imaging%20service%20on%20AWS%20with%20GcImaging.png)

GrapeCity Document for Imaging (or GcImaging) is a high-speed, feature-rich API for .NET Standard 2.0 under GrapeCity Documents providing support for image manipulation. You can create, load, modify, crop, resize, convert, and draw images in your .NET Standard 2.0 apps.  GrapeCity Documents for Imaging assemblies are built for .NET Standard 2.0 and can be used with any target that supports it. 

## Designing the service
Earlier we discussed how GrapeCity Documents can be used to generate documents (PDF and Excel) over AWS Lambda. In this article we will be discussing how we can leverage this compatibility of GcImaging and Lambda function to create an imaging service over AWS S3 events.

### GcImaging and Image operations
GcImaging is distributed as standalone NuGet packages, available directly from NuGet.org. GcImaging does not depend on any specific hardware or any third-party libraries. Usage of GcImaging revolves around GcBitmap class, which can be created directly (new GcBitmap()). The functionalities of this class are:

1.	Creating a new image
2.	Loading an existing Image
3.	Saving an image
4.	Changing image size, resolution, format
5.	Bitmap specific operations such as BitBit and resizing with various interpolation modes.
6.	Creating an instance of GcBitmapGraphics, that allows drawing.
7.	License checking.

We are designing a service which will perform manipulations on the image. Following are some of the manipulation we have decided to walk-through in this article.

|Effect|Code|Description/Remarks|
|---|---|---|
|Grayscale|```var __gcBitmap = new GcBitmap();```<br>```_gcBitmap.Load(imageStream);```<br>```_gcBitmap.ApplyEffect(```<br>```     GrayscaleEffect.Get(```<br>```         GrayscaleStandard.BT601));```|GrayscaleStandard enum specifies the standard used for converting full-color image to monochromatic gray. Its values are:<br>•	BT601<br>•	BT709<br>•	BT2100|
|Resize|```var __gcBitmap = new GcBitmap();```<br>```_gcBitmap.Load(imageStream);```<br>```_gcBitmap.Resize(```<br>```     100,```<br>```     100,```<br>```      InterpolationMode.NearestNeighbor);```|InterpolationMode enum specifies the sampling or filtering mode to use when scaling an image. Its values are:<br>•	NearestNeighbor<br>•	Linear<br>•	Cubic<br>•	Downscale|
|Watermark|```var __gcBitmap = new GcBitmap();```<br>```_gcBitmap.Load(imageStream);```<br>```using (var g = bmp.CreateGraphics(Color.White))```<br>```{```<br>```     g.DrawString(```<br>```         "Watermark",```<br>```         new TextFormat```<br>```         {```<br>```             FontSize = 96,```<br>```             ForeColor = Color.FromArgb(128, Color.Yellow),```<br>```             Font = FontCollection.SystemFonts.DefaultFont```<br>```         },```<br>```         new RectangleF(0, 0, gcBitmap.Width, gcBitmap.Height),```<br>```         TextAlignment.Center,```<br>```         ParagraphAlignment.Center,```<br>```         false```<br>```     );```<br>```}```|We use GcBitmapGraphics class to draw watermark on an image. An instance of GcBitmapGraphics can be created on a GcBitmap using the method GcBitmap.CreateGraphics(). GcBitmapGraphics derives from GcGraphics and provides drawing, filling, clipping and other normal graphics operations, like GcPdfGraphics. Text can be drawn on a GcBitmapGraphics using the same methods as in GcPdf - e.g. DrawTextLayout.|

[For more imaging operations refer this](https://demos.componentone.com/gcdocs/gcimaging/).

### Amazon S3

Amazon S3 can publish events (for example, when an object is created in a bucket) to AWS Lambda and invoke your Lambda function by passing the event data as a parameter. This integration enables you to write Lambda functions that process Amazon S3 events. In Amazon S3, you add a bucket notification configuration that identifies the type of event that you want Amazon S3 to publish and the Lambda function that you want to invoke. This notification system can then be used to manipulate image which is uploaded to a bucket. We can create a Lambda function that this bucket would invoke when an image is uploaded into it. Then this Lambda function can read the image and upload a manipulated image into another bucket. A high-level design of our imaging service would look like the following

![High-level-design](https://github.com/iwannabebot/gc-imaging-aws-lambda-s3/raw/master/GCImagingAWSLambdaS3/GCImagingS3Lambda%20(6)%20(1).png)

The flow of this diagram can be understood in the following fashion:
1.	User uploads an image into a specific S3 bucket (say, Source bucket)
2.	An event (object created) is raised w.r.t the uploaded image.
3.	This event is notified to an AWS Lambda function. This invoked function takes S3 event and its object’s key (image unique id). GcImaging library is used to manipulate this image
4.	S3 service then uploads the image to another bucket (say, Target bucket)

### Pre-requisite:

1.	Visual Studio
2.	Download and Install [AWS Toolkit for Visual Studio](https://aws.amazon.com/visualstudio/).

### Setup AWS Services:
1.	Two S3 bucket (“Source Bucket” and “Target Bucket”)
    1.	Open AWS Management Console
    2.	Select All Services > Storage > S3 
    3.	Click on Create bucket > Name: ‘gc-imaging-source-bucket’
    4.	Click on Create bucket > Name: ‘gc-imaging-target-bucket’
2.	An Execution role which gives permission to access AWS resources
    1.	Open AWS Management Console
    2.	Open IAM Console
    3.	Click on ‘Create Role’ button
    5.	Select ‘AWS Lambda’ and press next to select permission
    6.	Select ‘arn:aws:iam::aws:policy/AWSLambdaExecute’ policy and press ‘Create Role’
The AWSLambdaExecute policy has the permissions that the function needs to manage objects in Amazon S3

Next, we will create a Lambda function which will contain the code to fetch, modify and upload the image to an S3 bucket.

### Lambda Function

<aside class="warning">
You can skip this step by cloning this repository.
</aside>

1.	Open Visual Studio and create new project ‘GCImagingAWSLambdaS3’ by selecting C# > AWS Lambda > AWS Lambda Project (.NET Core)
2.	Select ‘Simple S3 Function’ from ‘Select Blueprint’ dialog.
3.	Open NuGet Package Manager, search GrapeCity.Documents.Imaging, and install the package.
4.	Create a new class GcImagingOperations.cs. GcImagingOperations class would contain static functions that will manipulate your images.
```cs
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
```
5.	Open Function class. Add the following code in its ‘FunctionHandler’ method:
```cs
public async Task<string> FunctionHandler(S3Event evnt, ILambdaContext context)
{
    var s3Event = evnt.Records?[0].S3;
    if(s3Event == null)
    {
        return null;
    }

    try
    {
        var rs = await this.S3Client.GetObjectMetadataAsync(
            s3Event.Bucket.Name,
            s3Event.Object.Key);
                
        if (rs.Headers.ContentType.StartsWith("image/"))
        {
            using (GetObjectResponse response = await S3Client.GetObjectAsync(
                s3Event.Bucket.Name,
                s3Event.Object.Key))
            {
                using (Stream responseStream = response.ResponseStream)
                {
                    using (StreamReader reader = new StreamReader(responseStream))
                    {
                        using (var memstream = new MemoryStream())
                        {
                            var buffer = new byte[512];
                            var bytesRead = default(int);
                            while ((bytesRead = reader.BaseStream.Read(buffer, 0, buffer.Length)) > 0)
                                memstream.Write(buffer, 0, bytesRead);
                        // Perform image manipulation
                        // We are choosing to grayscale
                            var transformedImage = GcImagingOperations.GetGrayScale(memstream);
                            PutObjectRequest putRequest = new PutObjectRequest()
                            {
                                BucketName = DestBucket,
                                Key = $"grayscale-{s3Event.Object.Key}",
                                ContentType = rs.Headers.ContentType,
                                ContentBody = transformedImage
                            };
                            await S3Client.PutObjectAsync(putRequest);
                        }
                    }
                }
            }
        }
        return rs.Headers.ContentType;
    }
    catch (Exception e)
    {
        throw;
    }
}
```
6.	You can then publish your function to AWS directly by right-clicking on your project and then selecting ‘Publish to AWS Lambda’

## Configure S3 to Publish Events

The event your Lambda function receives is for a single object and it provides information, such as the bucket name and object key name. There are two types of permissions policies that you work with when you set up the end-to-end experience
Permissions for your Lambda function – Regardless of what invokes a Lambda function, AWS Lambda executes the function by assuming the IAM role (execution role) that you specify at the time you create the Lambda function. Using the permissions policy associated with this role, you grant your Lambda function the permissions that it needs. For example, if your Lambda function needs to read an object, you grant permissions for the relevant Amazon S3 actions in the permissions policy. For more information, see Manage Permissions: Using an IAM Role (Execution Role).
Permissions for Amazon S3 to invoke your Lambda function – Amazon S3 cannot invoke your Lambda function without your permission. You grant this permission via the permissions policy associated with the Lambda function.

The remaining configuration is to setup S3 to publish events to the function we have written. Follow these steps:
1.	[Open Amazon S3 Console](https://console.aws.amazon.com/s3/home)
2.	Select your bucket ‘gc-imaging-source-bucket’.
3.	Select Properties > Advanced settings > Events
4.	Add a notification with following settings
    1.	Name: GrayScaleImage
    2.	Events: All object create events
    3.	Send To: Lambda Function
    4.	Lambda: “GCImagingAWSLambdaS3::GCImagingAWSLambdaS3.Function::FunctionHandler” (Lambda Function ARN, [see your project's configuration file](https://github.com/iwannabebot/gc-imaging-aws-lambda-s3/blob/master/GCImagingAWSLambdaS3/aws-lambda-tools-defaults.json#L18))
5.	Publish the settings.

Now whenever you upload any image in gc-imaging-source-bucket bucket you will have its grayscale version uploaded into gc-imaging-target-bucket bucket. 
