# Create an Imaging service over AWS S3 with GrapeCity Documents for Imaging

![Create your Imaging service on AWS with GcImaging](/Blog_ Create your Imaging service on AWS with GcImaging.png)

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

### Amazon S3

Amazon S3 can publish events (for example, when an object is created in a bucket) to AWS Lambda and invoke your Lambda function by passing the event data as a parameter. This integration enables you to write Lambda functions that process Amazon S3 events. In Amazon S3, you add a bucket notification configuration that identifies the type of event that you want Amazon S3 to publish and the Lambda function that you want to invoke. This notification system can then be used to manipulate image which is uploaded to a bucket. We can create a Lambda function that this bucket would invoke when an image is uploaded into it. Then this Lambda function can read the image and upload a manipulated image into another bucket. A high-level design of our imaging service would look like the following


The flow of this diagram can be understood in the following fashion:
1.	User uploads an image into a specific S3 bucket (say, Source bucket)
2.	An event (object created) is raised w.r.t the uploaded image.
3.	This event is notified to an AWS Lambda function. This invoked function takes S3 event and its object’s key (image unique id). GcImaging library is used to manipulate this image
4.	S3 service then uploads the image to another bucket (say, Target bucket)

### Pre-requisite:

1.	Visual Studio
2.	Download and Install AWS Toolkit for Visual Studio.

### Setup AWS Services:
1.	Two S3 bucket (“Source Bucket” and “Target Bucket”)
a.	Open AWS Management Console
b.	Select All Services > Storage > S3 
c.	Click on Create bucket
i.	Name: ‘gc-imaging-source-bucket’
d.	Click on Create bucket
i.	Name: ‘gc-imaging-target-bucket’
2.	An Execution role which gives permission to access AWS resources
a.	Open AWS Management Console
b.	Open IAM Console
c.	Click on ‘Create Role’ button
d.	Select ‘AWS Lambda’ and press next to select permission
e.	Select ‘arn:aws:iam::aws:policy/AWSLambdaExecute’ policy and press ‘Create Role’
The AWSLambdaExecute policy has the permissions that the function needs to manage objects in Amazon S3

Next, we will create a Lambda function which will contain the code to fetch, modify and upload the image to an S3 bucket.

### Lambda Function

1.	Open Visual Studio and create new project ‘GCImagingAWSLambdaS3’ by selecting C# > AWS Lambda > AWS Lambda Project (.NET Core)
2.	Select ‘Simple S3 Function’ from ‘Select Blueprint’ dialog.
3.	Open NuGet Package Manager, search GrapeCity.Documents.Imaging, and install the package.
4.	Create a new class GcImagingOperations.cs. GcImagingOperations class would contain static functions that will manipulate your images.
5.	Open Function class. Add the following code in its ‘FunctionHandler’ method:
6.	You can then publish your function to AWS directly by right-clicking on your project and then selecting ‘Publish to AWS Lambda’

## Configure S3 to Publish Events

The event your Lambda function receives is for a single object and it provides information, such as the bucket name and object key name. There are two types of permissions policies that you work with when you set up the end-to-end experience
Permissions for your Lambda function – Regardless of what invokes a Lambda function, AWS Lambda executes the function by assuming the IAM role (execution role) that you specify at the time you create the Lambda function. Using the permissions policy associated with this role, you grant your Lambda function the permissions that it needs. For example, if your Lambda function needs to read an object, you grant permissions for the relevant Amazon S3 actions in the permissions policy. For more information, see Manage Permissions: Using an IAM Role (Execution Role).
Permissions for Amazon S3 to invoke your Lambda function – Amazon S3 cannot invoke your Lambda function without your permission. You grant this permission via the permissions policy associated with the Lambda function.

The remaining configuration is to setup S3 to publish events to the function we have written. Follow these steps:
1.	Open Amazon S3 Console
2.	Select your bucket ‘gc-imaging-source-bucket’.
3.	Select Properties > Advanced settings > Events
4.	Add a notification with following settings
a.	Name: GrayScaleImage
b.	Events: All object create events
c.	Send To: Lambda Function
d.	Lambda: “GCImagingAWSLambdaS3::GCImagingAWSLambdaS3.Function::FunctionHandler” (Lambda Function ARN)
5.	Publish the settings.

Now whenever you upload any image in gc-imaging-source-bucket bucket you will have its grayscale version uploaded into gc-imaging-target-bucket bucket. 
