//using ILGPU;
//using ILGPU.Runtime;
//using System;
//using System.Drawing;
//using System.Drawing.Imaging;

//namespace ILGPU_Example
//{
//    internal class Program
//    {
//        static void Main(string[] args)
//        {
//            Console.WriteLine("Started.");
//            try
//            {
//                GPU_Acceleration_Example();
//            }
//            catch (Exception ex)
//            {
//                Console.WriteLine($"An error occurred: {ex.Message}");
//            }
//            Console.WriteLine("Finished.");
//        }

//        public static void GPU_Acceleration_Example()
//        {
//            // Initialize ILGPU
//            Context context = Context.CreateDefault();
//            Accelerator accelerator = context.GetPreferredDevice(preferCPU: false).CreateAccelerator(context);

//            // Set source and destination paths
//            string imagePath = @"C:\Users\DTPC\Desktop\example_image.png";
//            string newImagePath = $@"C:\Users\DTPC\Desktop\example_modified_image_{DateTime.Now:HH-mm-ss}.png";

//            // Load the image
//            Bitmap image = new Bitmap(imagePath);

//            // Get single-dimensional ARGB pixel array from the Bitmap object
//            int[] pixels = Image.ToARGBArray(image);

//            // Create a container (memory pointer) for the data passed/received from the GPU
//            var pixelBuffer = accelerator.Allocate2D<int>(pixels.Length);
//            pixelBuffer.CopyFromCPU(pixels);

//            // Load/precompile the kernel
//            var loadedKernel = accelerator.LoadAutoGroupedStreamKernel<Index1D, ArrayView<int>, int>(ImageProcessingAcceleratedKernel);

//            // Execute the kernel
//            loadedKernel(pixelBuffer.Length, pixelBuffer);

//            // Copy the modified pixels back to the CPU
//            pixelBuffer.CopyToCPU(pixels);

//            // Create a new Bitmap from the modified pixels
//            Bitmap newImage = Image.FromARGBArray(pixels, image.Width, image.Height);

//            // Save the modified image
//            newImage.Save(newImagePath, ImageFormat.Png);
//        }

//        // Define the GPU kernel for image processing
//        private static void ImageProcessingAcceleratedKernel(
//            Index1D index,
//            ArrayView<int> pixelBuffer)
//        {
//            // Get the ARGB pixel value at the current index
//            int pixel = pixelBuffer[index];

//            // Extract individual components (Alpha, Red, Green, Blue)
//            int alpha = (pixel >> 24) & 0xFF;
//            int red = (pixel >> 16) & 0xFF;
//            int green = (pixel >> 8) & 0xFF;
//            int blue = pixel & 0xFF;

//            // Invert the RGB components
//            red = 255 - red;
//            green = 255 - green;
//            blue = 255 - blue;

//            // Recombine the modified components
//            pixel = (alpha << 24) | (red << 16) | (green << 8) | blue;

//            // Store the modified pixel back to the buffer
//            pixelBuffer[index] = pixel;
//        }
//    }
//}
