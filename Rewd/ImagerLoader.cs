using System;
using System.Collections;
using System.Data.SqlTypes;
using System.Diagnostics.Eventing.Reader;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;                     
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Security.Policy;
using System.Text;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using static System.Net.Mime.MediaTypeNames;
using Color = System.Drawing.Color;
using MessageBox = System.Windows.Forms.MessageBox;
using MessageBoxButtons = System.Windows.Forms.MessageBoxButtons;
using Path = System.IO.Path;
using PixelFormat = System.Windows.Media.PixelFormat;
using Rectangle = System.Drawing.Rectangle;
using FoundationR.Lib;
using FoundationR.Rew;
using FoundationR.Loader;
using FoundationR.Ext;
using FoundationR.Headers;
using System.Threading.Tasks;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.TrayNotify;

namespace FoundationR.Headers
{
   public struct BitmapFile
   {
      public string Name;
      public Bitmap Value;
      static byte[] GetDIBHeader(REW image)
      {
         byte[] array = new byte[0];
         switch (image.Header)
         {
               default:
               case BitmapHeader.BITMAPINFOHEADER:
                  array = array.Concat(BITMAPINFOHEADER.CreateDIBHeader(image, out _)).ToArray();
                  break;
               case BitmapHeader.BITMAPV2INFOHEADER:
                  array = array.Concat(BitmapV2InfoHeader.CreateDIBHeader(image, out _))
                              .Concat(AppendChunkToDIB(false))
                              .ToArray();
                  break;
               case BitmapHeader.BITMAPV3INFOHEADER:
                  array = array.Concat(BITMAPV3INFOHEADER.CreateDIBHeader(image, out _))
                              .Concat(AppendChunkToDIB(true))
                              .ToArray();
                  break;
         }
         return array;
      }
      static byte[] AppendChunkToDIB(bool alphaAppend)
      {
         byte[] array = new byte[]
         {
               0,   0,   0,   0,
               0,   0,   0,   0,
               0,   0,   0,   0,
               0,   0,
               255, 0,   0,   0,
               0,   255, 0,   0,
               0,   0,   255, 0
         };
         if (alphaAppend)
         {
               return array.Concat(new byte[]
               {
                  0,   0,   0,   255
               }).ToArray();
         }
         else return array;
      }
      static byte[] GetDataBuffer(REW image)
      {
         return image.GetPixels();
      }
      static byte[] BmpHeader(REW image, int arrayOffset)
      {
         byte[] fileSize = BitConverter.GetBytes(image.RealLength);
         byte[] offset = BitConverter.GetBytes(arrayOffset);
         //  B     M   , Total file size                                      , N/a       , Index offset of where pixel array is
         return new byte[] { 0x42, 0x4D, fileSize[0], fileSize[1], fileSize[2], fileSize[3], 0, 0, 0, 0, offset[0], offset[1], offset[2], offset[3] };
      }
      public static byte[] Create(REW image)
      {
         int headerSize = 14;
         byte[] dib = GetDIBHeader(image);
         byte[] header = BmpHeader(image, dib.Length + headerSize);
         var result = header.Concat(dib);
         byte[] data = GetDataBuffer(image);
         return result.Concat(data).ToArray();
      }
      public static byte[] Create(int width, int height, byte[] arrayPixel, short bpp)
      {
         REW image = REW.Create(width, height, arrayPixel, bpp);
         int headerSize = 14;
         byte[] dib = GetDIBHeader(image);
         byte[] header = BmpHeader(image, dib.Length + headerSize);
         var result = header.Concat(dib);
         byte[] data = GetDataBuffer(image);
         image = null;
         return result.Concat(data).ToArray();
      }
   }
}
namespace FoundationR.Rew
{
   public class RewBatch
   {
      [DllImport("gdi32.dll", EntryPoint = "CreateDIBSection", SetLastError = true)]
      static extern IntPtr CreateDIBSection(IntPtr hdc, [In] ref BitmapInfo pbmi, uint pila, out IntPtr ppbBits, IntPtr hSection, uint dwOffset);
      [DllImport("gdi32.dll")]
      static extern IntPtr CreateBitmap(int nWidth, int nHeight, uint cPlanes, uint cBitsPerPel, IntPtr lpvBits);
      [DllImport("gdi32.dll")]
      static extern IntPtr CreateBitmap(int nWidth, int nHeight, uint cPlanes, uint cBitsPerPel, byte[] lpBits);
      [DllImport("user32.dll")]
      static extern IntPtr GetDC(IntPtr hWnd);
      [DllImport("user32.dll")]
      static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);
      [DllImport("gdi32.dll")]
      static extern int SetDIBitsToDevice(IntPtr hdc, int xDest, int yDest, int w, int h, int xSrc, int ySrc, int startScan, int scanLines, IntPtr bits, IntPtr bmih, uint colorUse);
      [DllImport("gdi32.dll")]
      static extern int SetDIBitsToDevice(IntPtr hdc, int xDest, int yDest, int w, int h, int xSrc, int ySrc, int startScan, int scanLines, byte[] bits, BitmapInfoHeader bmih, uint colorUse);
      [DllImport("gdi32.dll")]
      static extern IntPtr SelectObject(IntPtr hdc, IntPtr hbdiobj);
      [DllImport(".\\Direct2D_Render.dll")]
      static extern void Direct2D_Init(IntPtr hwnd, uint width, uint height);
      [DllImport(".\\Direct2D_Render.dll")]
      static extern void Direct2D_Render(byte[] buffer, uint width, uint height);
      [DllImport(".\\Direct2D_Render.dll")]
      static extern void Direct2D_Dispose();
      [DllImport(".\\Direct2D_Render.dll")]
      static extern void Direct2D_Draw(byte[] buffer, uint x, uint y, uint width, uint height);
      [DllImport(".\\Direct2D_Render.dll")]
      static extern void Direct2D_Clear();
      [DllImport(".\\Direct2D_Render.dll")]
      static extern void Direct2D_Begin();
      [DllImport(".\\Direct2D_Render.dll")]
      static extern void Direct2D_End();


      public static RenderOption renderOption = RenderOption.GDI;
      public virtual int stride => width * ((BitsPerPixel + 7) / 8);
      internal static int width, height;
      private static int oldWidth, oldHeight;
      public virtual short BitsPerPixel { get; protected set; }
      internal static byte[] backBuffer;
      IntPtr hdc;
      public static Viewport Viewport
      { 
         get { return FoundationR.Lib.Foundation.viewport; }
         set { FoundationR.Lib.Foundation.viewport = value; }
      }
      public RewBatch(int width, int height, int bitsPerPixel = 32)
      {
         Initialize(width, height);
         BitsPerPixel = (short)bitsPerPixel;
      }
      void Initialize(int width, int height)
      {
         Viewport.bounds = new Rectangle(0, 0, width, height);
         RewBatch.width = width;
         RewBatch.height = height;
         if (renderOption == RenderOption.Direct2D)
               Direct2D_Init(IntPtr.Zero, (uint)width, (uint)height);
         else 
         if (renderOption == RenderOption.Both)
         {
               Direct2D_Init(IntPtr.Zero, (uint)width, (uint)height);
               backBuffer = new byte[width * height * (BitsPerPixel / 8)];
         }
         else
               backBuffer = new byte[width * height * (BitsPerPixel / 8)];
      }
      public bool Resize(int width, int height)
      {
         if (oldWidth != width || oldHeight != height)
         {
               RewBatch.width = width;
               RewBatch.oldWidth = width;
               RewBatch.height = height;
               RewBatch.oldHeight = height;
               if (renderOption == RenderOption.Direct2D)
               { 
                  Direct2D_Dispose();
                  Direct2D_Init(IntPtr.Zero, (uint)width, (uint)height);
               }
               else if (renderOption == RenderOption.Both)
               {
                  Direct2D_Dispose();
                  Direct2D_Init(IntPtr.Zero, (uint)width, (uint)height);
                  backBuffer = new byte[width * height * (BitsPerPixel / 8)];
               }
               else
                  backBuffer = new byte[width * height * (BitsPerPixel / 8)];
               return true;
         }
         return false;
      }
      public void Begin(IntPtr hdc)
      {
         if (renderOption == RenderOption.Direct2D)
         { 
               Direct2D_Begin();
               Direct2D_Clear(); 
         }
         else if (renderOption == RenderOption.Both)
         {
               Direct2D_Begin();
               Direct2D_Clear();
               backBuffer = new byte[width * height * (BitsPerPixel / 8)];
         }
         else
         {
               backBuffer = new byte[width * height * (BitsPerPixel / 8)];
         }
      }         

      public virtual void Draw(REW image, Rectangle rectangle)
      {
         int w = rectangle.Width;
         int h = rectangle.Height;
         byte[] result = null;
         if (Culling(rectangle.Width, rectangle.Height, rectangle.X, rectangle.Y))
               result = CropARGBImage(image.GetPixels(), rectangle.Width, rectangle.Height, rectangle.X, rectangle.Y, out w, out h);
         else 
               result = image.GetPixels();
         if (result == null)
               return;
         if (renderOption == RenderOption.Direct2D)
               Direct2D_Draw(result, (uint)rectangle.X, (uint)rectangle.Y, (uint)w, (uint)h);
         else
               CompositeImage(backBuffer, RewBatch.width, RewBatch.height, image.GetPixels(), image.Width, image.Height, rectangle.X - Viewport.X, rectangle.Y - Viewport.Y, rectangle.X, rectangle.Y);
         result = null;
      }
      public virtual void Draw(REW image, Rectangle rectangle, Color color)
      {
         int w = Math.Min(rectangle.Width, Viewport.Width);
         int h = Math.Min(rectangle.Height, Viewport.Height);
         byte[] result = null;
         if (Culling(rectangle.Width, rectangle.Height, rectangle.X, rectangle.Y))
               result = CropARGBImage(image.GetPixels()/*.Recolor(Convert(color))*/, image.Width, image.Height, rectangle.X, rectangle.Y, out w, out h);
         else 
               result = image.GetPixels();//.Recolor(Convert(color));
         if (result == null)
               return;
         if (renderOption == RenderOption.Direct2D)
               Direct2D_Draw(result, (uint)rectangle.X, (uint)rectangle.Y, (uint)w, (uint)h);
         else
               CompositeImage(backBuffer, RewBatch.width, RewBatch.height, image.GetPixels(), image.Width, image.Height, rectangle.X - Viewport.X, rectangle.Y - Viewport.Y, rectangle.X, rectangle.Y);
         result = null;
      }                            
      public virtual void Draw(REW image, int x, int y)
      {
         int w = Math.Min(image.Width, Viewport.Width);
         int h = Math.Min(image.Height, Viewport.Height);
         byte[] result = null;
         if (Culling(image.Width, image.Height, x, y))
               result = CropARGBImage(image.GetPixels(), image.Width, image.Height, x, y, out w, out h);
         else 
               result = image.GetPixels();
         if (result == null)
               return;
         if (renderOption == RenderOption.Direct2D)
               Direct2D_Draw(result, (uint)x, (uint)y, (uint)w, (uint)h);
         else
               CompositeImage(backBuffer, RewBatch.width, RewBatch.height, image.GetPixels(), image.Width, image.Height, x - Viewport.X, y - Viewport.Y, x, y);
         result = null;
      }
      public virtual void Draw(REW image, int x, int y, Color color)
      {
         int w = Math.Min(image.Width, Viewport.Width);
         int h = Math.Min(image.Height, Viewport.Height);
         byte[] result = null;
         if (Culling(image.Width, image.Height, x, y))
               result = CropARGBImage(image.GetPixels()/*.Recolor(Convert(color))*/, image.Width, image.Height, x, y, out w, out h);
         else
               result = image.GetPixels();//.Recolor(Convert(color));
         if (result == null)
               return; 
         if (renderOption == RenderOption.Direct2D)
         {
               Direct2D_Draw(result, (uint)x, (uint)y, (uint)w, (uint)h);
         }
         else
               CompositeImage(backBuffer, RewBatch.width, RewBatch.height, image.GetPixels()/*.Recolor(Convert(color))*/, image.Width, image.Height, x - Viewport.X, y - Viewport.Y, x, y);
         result = null;
      }
      public virtual void Draw(byte[] image, int x, int y, int width, int height)
      {
         int w = width;
         int h = height;
         if (Culling(width, height, x, y))
               image = CropARGBImage(image, width, height, x, y, out w, out h);
         if (image == null)
               return;
         if (renderOption == RenderOption.Direct2D)
               Direct2D_Draw(image, (uint)x, (uint)y, (uint)w, (uint)h);
         else
               CompositeImage(backBuffer, RewBatch.width, RewBatch.height, image, width, height, x - Viewport.X, y - Viewport.Y, x, y);
         image = null;
      }               
        
      public virtual void DrawString(string font, string text, Vector2 v2, Color color)
      {
         Bitmap image = new Bitmap(width, height);
         using (Graphics graphics = Graphics.FromImage(image))
         {
               graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
               Font _font = new Font("Arial", 12);
               SolidBrush brush = new SolidBrush(color);
               PointF point = new PointF(0, 0);
               graphics.DrawString(text, _font, brush, point);

               Draw(REW.Extract(image, 32), (int)v2.X, (int)v2.Y);
         }
      }
      public virtual void DrawString(string font, string text, Rectangle rectangle, Color color)
      {
         Bitmap image = new Bitmap(width, height);
         using (Graphics graphics = Graphics.FromImage(image))
         {
               graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
               Font _font = new Font("Arial", 12);
               SolidBrush brush = new SolidBrush(Color.White);
               PointF point = new PointF(0, 0);
               graphics.DrawString(text, _font, brush, point);

               Draw(REW.Extract(image, 32), rectangle.X, rectangle.Y, color);
         }
      }
      public virtual void DrawString(string font, string text, int x, int y, int width, int height, Color color)
      {
         Bitmap image = new Bitmap(width, height);
         using (Graphics graphics = Graphics.FromImage(image))
         {
               graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
               Font _font = new Font("Arial", 12);
               SolidBrush brush = new SolidBrush(Color.White);
               PointF point = new PointF(0, 0);
               graphics.DrawString(text, _font, brush, point);

               Draw(REW.Extract(image, 32), x, y, color);
         }
      }
        
      public void End(RenderOption type, IntPtr handle)
      {
         switch (type)
         {
               case RenderOption.GDI:
                  BitmapInfoHeader bmih = new BitmapInfoHeader()
                  {
                     Size = 40,
                     Width = RewBatch.width,
                     Height = RewBatch.height,
                     Planes = 1,
                     BitCount = 32,
                     Compression = (uint)BitmapCompressionMode.BI_BITFIELDS,
                     SizeImage = (uint)(RewBatch.width * RewBatch.height * (BitsPerPixel / 8)),
                     XPelsPerMeter = 96,
                     YPelsPerMeter = 96,
                     RedMask = 0x00FF0000,
                     GreenMask = 0x0000FF00,
                     BlueMask = 0x000000FF,
                     AlphaMask = 0xFF000000,
                     CSType = BitConverter.ToUInt32(new byte[] { 32, 110, 106, 87 }, 0)
                  };
                  GCHandle h = GCHandle.Alloc(bmih, GCHandleType.Pinned);
                  GCHandle h2 = GCHandle.Alloc(FlipVertically(backBuffer, Viewport.Width, Viewport.Height), GCHandleType.Pinned);
                  SetDIBitsToDevice(handle, 0, 0, RewBatch.width, RewBatch.height, 0, 0, 0, RewBatch.height, h2.AddrOfPinnedObject(), h.AddrOfPinnedObject(), 0);
                  h.Free();
                  h2.Free();
                  ReleaseDC(IntPtr.Zero, handle);
                  goto default;
               case RenderOption.Direct2D:
                  Direct2D_End();
                  goto default;
               case RenderOption.Both:
                  Direct2D_Render(backBuffer, (uint)Viewport.Width, (uint)Viewport.Height);
                  Direct2D_End();
                  goto default;
               case RenderOption.None:
               default:
                  backBuffer = null;
                  break;
         }
      }

      public static (int, int) CalculateCropDimensions(int X, int Y, int imageWidth, int imageHeight, int backBufferWidth, int backBufferHeight)
      {
         if (X + imageWidth > backBufferWidth)
         {
               imageWidth = backBufferWidth - X;
         }
         if (Y + imageHeight > backBufferHeight)
         {
               imageHeight = backBufferHeight - Y;
         }
         return (imageWidth, imageHeight);
      }

      public byte[] CropARGBImage(byte[] image, int width, int height, int x, int y, out int cropWidth, out int cropHeight)
      {
         // usage
         int X = x, Y = y;  // starting point of the image on the backbuffer
         int imageWidth = width, imageHeight = height;  // width and height of the image
         int backBufferWidth = Viewport.Width, backBufferHeight = Viewport.Height;  // width and height of the backbuffer

         var (newWidth, newHeight) = CalculateCropDimensions(X, Y, imageWidth, imageHeight, backBufferWidth, backBufferHeight);
            
         cropWidth = newWidth;
         cropHeight = newHeight;

         if (cropWidth <= 0 || cropHeight <= 0)
         {
               return null;
         }
         byte[] croppedImage = new byte[newWidth * newHeight * 4];
         for (int i = 0; i < cropHeight; i++)
         {
               try
               { 
                  Array.Copy(image, i * width * 4, croppedImage, i * newWidth * 4, newWidth * 4);
               }
               catch
               {
                  newWidth--;
               }
         }
         return croppedImage;
      }

      bool Culling(int x, int y, int imageWidth, int imageHeight)
      {
         if (x < 0 || y < 0 || x + imageWidth >= Viewport.Width || y + imageHeight >= Viewport.Height)
               return true;
         return false;
      }
      public virtual void CompositeImage(byte[] buffer, int bufferWidth, int bufferHeight, byte[] image, int imageWidth, int imageHeight, int x, int y, int origX, int origY, bool text = false)
      {
         if (buffer == null || image == null) return;
         Parallel.For(0, imageHeight, i =>
         {
               for (int j = 0; j < imageWidth; j++)
               {
                  int index = Math.Min((i * imageWidth + j) * 4, image.Length - 4);
                  int bufferIndex = ((y + i) * bufferWidth + (x + j)) * 4;

                  if (bufferIndex < 0 || bufferIndex >= buffer.Length - 4)
                     return;
                  Pixel back = new Pixel(
                     buffer[bufferIndex + 3],
                     buffer[bufferIndex + 2],
                     buffer[bufferIndex + 1],
                     buffer[bufferIndex]
                  );
                  Pixel fore = new Pixel(
                     image[index],
                     image[index + 1],
                     image[index + 2],
                     image[index + 3]
                  );
                  if (back.A == 0 && fore.A == 0)
                     continue;
                  
                  if (fore.A < 255 && !text)
                  {
                     Color blend = fore.color.Blend(back.color, 0.15d);
                     buffer[bufferIndex] = blend.B;
                     buffer[bufferIndex + 1] = blend.G;
                     buffer[bufferIndex + 2] = blend.R;

                     if (back.A == 255) buffer[bufferIndex + 3] = 255;
                     else buffer[bufferIndex + 3] = blend.A;
                  }
                  else
                  {
                     buffer[bufferIndex] = fore.color.B;
                     buffer[bufferIndex + 1] = fore.color.G;
                     buffer[bufferIndex + 2] = fore.color.R;
                     buffer[bufferIndex + 3] = 255;
                  }
               }
         });
      }
      public byte[] FlipVertically(byte[] pixels, int width, int height)
      {
         int bytesPerPixel = 4;
         byte[] output = new byte[pixels.Length];

         for (int y = 0; y < height; y++)
         {
               for (int x = 0; x < width; x++)
               {
                  for (int c = 0; c < bytesPerPixel; c++)
                  {
                     int inIndex = (y * width + x) * bytesPerPixel + c;
                     int outIndex = ((height - y - 1) * width + x) * bytesPerPixel + c;

                     output[outIndex] = pixels[inIndex];
                  }
               }
         }
         return output;
      }
      public byte[] BlendFrames(byte[] frame1, byte[] frame2, float alpha)
      {
         // Init property
         if (frame1.Length == 0)
         {
               return frame2;
         }

         // Check that the input frames have the same length
         if (frame1.Length != frame2.Length)
         {
               throw new ArgumentException("Input frames must have the same length.");
         }

         // Create a new byte array for the output frame
         byte[] output = new byte[frame1.Length];

         // Iterate over the pixel data in the input frames
         for (int i = 0; i < frame1.Length; i += 4)
         {
               // Calculate the blended value for each color channel
               for (int j = 0; j < 4; j++)
               {
                  float value1 = frame1[i + j] / 255f;
                  float value2 = frame2[i + j] / 255f;

                  // Blend the color values using the specified alpha value
                  float blendedValue = value1 * (1 - alpha) + value2 * alpha;

                  // Convert the blended value back to a byte and store it in the output frame
                  output[i + j] = (byte)(blendedValue * 255);
               }
         }

         // Return the output frame
         return output;
      }
      public Bitmap BlendFrames(Bitmap frame1, Bitmap frame2, float alpha)
      {
         // Create a new bitmap with the same size as the input frames
         Bitmap output = new Bitmap(frame1.Width, frame1.Height);

         // Create a Graphics object from the output bitmap
         using (Graphics g = Graphics.FromImage(output))
         {
               // Draw the first frame onto the output bitmap
               g.DrawImage(frame1, new Rectangle(0, 0, frame1.Width, frame1.Height));

               // Create a color matrix with the specified alpha value
               ColorMatrix matrix = new ColorMatrix(new float[][]
               {
                  new float[] {1, 0, 0, 0, 0},
                  new float[] {0, 1, 0, 0, 0},
                  new float[] {0, 0, 1, 0, 0},
                  new float[] {0, 0, 0, alpha, 0},
                  new float[] {0, 0, 0, 0, 1}
               });

               // Create a new ImageAttributes object and set its color matrix
               ImageAttributes attributes = new ImageAttributes();
               attributes.SetColorMatrix(matrix);

               // Draw the second frame onto the output bitmap with the specified ImageAttributes
               g.DrawImage(frame2, new Rectangle(0, 0, frame2.Width, frame2.Height), 0, 0, frame2.Width, frame2.Height, GraphicsUnit.Pixel, attributes);
         }

         // Return the output bitmap
         return output;
      }
      ARGB Convert(Color color)
      {
         return new ARGB()
         {
               a = color.A,
               r = color.R,
               g = color.G,
               b = color.B,
         };
      }
   }
   public partial class REW
   {
      public byte[] data;
      public int frameCount;
      public virtual int HeaderOffset => 10;
      public virtual short Width { get; protected set; }
      public virtual short Height { get; protected set; }
      public virtual short BitsPerPixel { get; protected set; }
      public virtual int Count => (data.Length - HeaderOffset) / NumChannels;
      public virtual int RealLength => data.Length - HeaderOffset;
      public virtual int NumChannels => BitsPerPixel / 8;
      public virtual BitmapHeader Header => NumChannels == 4 ? BitmapHeader.BITMAPV2INFOHEADER : BitmapHeader.BITMAPINFOHEADER;
      public static REW Create(int width, int height, Color color, PixelFormat format)
      {
         return new REW(width, height, color, format);
      }
      public static REW Create(int width, int height, in byte[] pixels, short bpp)
      {
         return new REW(width, height, pixels, bpp);
      }
      public static REW CreateEmpty(int width, int height, PixelFormat format)
      {
         return new REW(width, height, new byte[width * height * (format.BitsPerPixel / 8)], (byte)format.BitsPerPixel);
      }
      public static REW Dummy(int width, int height, short bpp)
      {
         return new REW(width, height, bpp);
      }
      public REW() { }
      public REW(int frameCount)
      {
         this.frameCount = frameCount;
      }
      private REW(int width, int height, short bpp)
      {
         this.BitsPerPixel = bpp;
         this.Width = (short)width;
         this.Height = (short)height;
         this.data = new byte[Width * Height * NumChannels + HeaderOffset];
         WriteHeader(this);
         WriteDataChunk(this, default);
      }
      private REW(int width, int height, byte[] pixels, short bpp)
      {
         this.BitsPerPixel = bpp;
         this.Width = (short)width;
         this.Height = (short)height;
         this.data = new byte[HeaderOffset];
         WriteHeader(this);
         this.data = this.data.Concat(pixels).ToArray();
      }
      private REW(int width, int height, Color color, PixelFormat format)
      {
         this.BitsPerPixel = (short)format.BitsPerPixel;
         this.Width = (short)width;
         this.Height = (short)height;
         this.data = new byte[Width * Height * NumChannels + HeaderOffset];
         WriteHeader(this);
         if (color != default)
         {
               WriteDataChunk(this, color);
         }
      }
      public virtual byte[] GetPixels()
      {
         if (NumChannels < 4)
         {
               int padding = (4 - (Width * (BitsPerPixel / 8)) % 4) % 4;
               if (padding > 0)
               {
                  var list = data.Skip(HeaderOffset).ToList();
                  int num = Width;
                  {
                     for (int i = 0; i < Height; i++)
                     {
                           list.InsertRange(num, new byte[padding]);
                           num += Width + padding;
                     }
                  }
                  return list.ToArray();
               }
         }
         return data.Skip(HeaderOffset).ToArray();
      }
      static void WriteHeader(REW rew)
      {
         rew.data.AddHeader(new Point16(rew.Width, rew.Height), rew.Width * rew.Height * rew.NumChannels, rew.BitsPerPixel);
      }
      static void WriteDataChunk(REW rew, Color color)
      {
         int num = 0;
         for (int j = 0; j < rew.Height; j++)
         {
               for (int i = 0; i < rew.Width; i++)
               {
                  Pixel pixel = default;
                  if (rew.NumChannels == 4)
                  {
                     pixel = new Pixel(color.A, color.R, color.G, color.B);
                  }
                  else
                  {
                     pixel = new Pixel(color.R, color.G, color.B);
                  }
                  rew.data.color_AppendPixel(num * rew.NumChannels + rew.HeaderOffset, pixel);
                  pixel = null;
                  num++;
               }
         }
      }
      public virtual void Extract(Bitmap bitmap)
      {
         int num = 0;
         this.Width = (short)bitmap.Width;
         this.Height = (short)bitmap.Height;
         this.data = new byte[bitmap.Width * bitmap.Height * NumChannels + HeaderOffset];
         this.data.AddHeader(new Point16(this.Width, this.Height), bitmap.Width * bitmap.Height * NumChannels + HeaderOffset, BitsPerPixel);
         for (int j = 0; j < bitmap.Height; j++)
         {
               for (int i = 0; i < bitmap.Width; i++)
               {
                  Color c = bitmap.GetPixel(i, j);
                  Pixel pixel = default;
                  if (NumChannels == 4)
                  {
                     pixel = new Pixel(c.A, c.R, c.G, c.B);
                  }
                  else
                  {
                     pixel = new Pixel(c.R, c.G, c.B);
                  }
                  data.AppendPixel(num * NumChannels + HeaderOffset, pixel);
                  pixel = null;
                  num++;
               }
         }
      }
      public static REW Extract(Bitmap bitmap, short bitsPerPixel, int headerOffset = 10, int pixelArrayOffset = 54)
      {
         PixelFormat format = default;
         System.Drawing.Imaging.PixelFormat bmpf = default;
         if (bitsPerPixel == 32)
         {
               format = PixelFormats.Bgr32;
               bmpf = System.Drawing.Imaging.PixelFormat.Format32bppArgb;
         }
         else
         {
               format = PixelFormats.Bgr24;
               bmpf = System.Drawing.Imaging.PixelFormat.Format24bppRgb;
         }

         REW result = REW.CreateEmpty(bitmap.Width, bitmap.Height, format);

         result.Width = (short)bitmap.Width;
         result.Height = (short)bitmap.Height;
         result.data = new byte[bitmap.Width * bitmap.Height * result.NumChannels + headerOffset];
         result.data.AddHeader(new Point16(result.Width, result.Height), bitmap.Width * bitmap.Height * result.NumChannels + headerOffset, result.BitsPerPixel);

         BitmapData data = bitmap.LockBits(new Rectangle(0, 0, bitmap.Width, bitmap.Height), ImageLockMode.ReadOnly, bmpf);
         Marshal.Copy(data.Scan0, result.data, headerOffset, bitmap.Width * bitmap.Height * 4);

         bitmap.UnlockBits(data);
         bitmap.Dispose();
         return result;
      }
      public virtual void Write(BinaryWriter w)
      {
         Point16 point = new Point16(Width, Height);
         w.Write(point);
         w.Write(data.Length);
         w.Write(BitsPerPixel);
         byte[] buffer = this.GetPixels();
         w.Write(buffer, 0, buffer.Length);
         point = default;
         buffer = null;
      }
      public virtual void ReadData(BinaryReader br)
      {
         Point16 size = br.ReadPoint16();
         int len = br.ReadInt32();
         BitsPerPixel = br.ReadInt16();
         data = new byte[len - HeaderOffset];
         data.AddHeader(size, len, BitsPerPixel);
         Width = size.X;
         Height = size.Y;
         for (int i = HeaderOffset; i < data.Length - NumChannels; i += NumChannels)
         {
               Pixel pixel = br.ReadPixel();
               pixel.hasAlpha = NumChannels == 4;
               data.AppendPixel(i, pixel);
               pixel = null;
         }
      }
      public virtual Pixel GetPixel(int x, int y)
      {
         int i = this.Width;
         int whoAmI = y * i + x;
         if (whoAmI < 0)
         {
               return new Pixel();
         }
         if (NumChannels == 4)
         {
               return new Pixel(
                  data[Math.Min(data.Length - 1, whoAmI * 4 + HeaderOffset)],
                  data[Math.Min(data.Length - 1, whoAmI * 4 + HeaderOffset + 1)],
                  data[Math.Min(data.Length - 1, whoAmI * 4 + HeaderOffset + 2)],
                  data[Math.Min(data.Length - 1, whoAmI * 4 + HeaderOffset + 3)]
               );
         }
         else
         {
               return new Pixel(
                  data[Math.Min(data.Length - 1, whoAmI * 3 + HeaderOffset)],
                  data[Math.Min(data.Length - 1, whoAmI * 3 + HeaderOffset + 1)],
                  data[Math.Min(data.Length - 1, whoAmI * 3 + HeaderOffset + 2)]
               );
         }
      }
      public virtual void SetPixel(int x, int y, Color color)
      {
         int i = this.Width;
         int whoAmI = y * i + x;
         if (whoAmI < 0)
         {
               return;
         }
         if (NumChannels == 4)
         {
               data[Math.Min(data.Length - 1, whoAmI * 4 + HeaderOffset)] = color.A;
               data[Math.Min(data.Length - 1, whoAmI * 4 + HeaderOffset + 1)] = color.R;
               data[Math.Min(data.Length - 1, whoAmI * 4 + HeaderOffset + 2)] = color.G;
               data[Math.Min(data.Length - 1, whoAmI * 4 + HeaderOffset + 3)] = color.B;
         }
         else
         {
               data[Math.Min(data.Length - 1, whoAmI * 3 + HeaderOffset)] = color.R;
               data[Math.Min(data.Length - 1, whoAmI * 3 + HeaderOffset + 1)] = color.G;
               data[Math.Min(data.Length - 1, whoAmI * 3 + HeaderOffset + 2)] = color.B;
         }
      }
      public virtual byte[] Animate(int frame, int frameHeight, int width)
      {
         int len = width * frameHeight * 4;
         byte[] bytes = new byte[len];
         byte[] original = GetPixels();
         int x = 0;
         int y = frame * frameHeight * 4;
         int offset = Math.Min(y * width + x, Math.Abs(bytes.Length - original.Length) - 10);
         Array.Copy(original, offset, bytes, 0, len);
         return bytes;
      }
   }
   public class Pixel
   {
      public bool hasAlpha;
      public Pixel() { }
      public Pixel(byte A, byte R, byte G, byte B)
      {
         this.A = A;
         this.R = R;
         this.G = G;
         this.B = B;
         this.hasAlpha = true;
      }
      public Pixel(byte R, byte G, byte B)
      {
         //  Flipped; requires drawing 24bppBGR
         this.R = R;
         this.G = G;
         this.B = B;
         this.hasAlpha = false;
      }
      public static Pixel Extract32bit(byte[] buffer)
      {
         return new Pixel
         (
               buffer[0],
               buffer[1],
               buffer[2],
               buffer[3]
         );
      }
      public static Pixel Extract32bit(byte[] buffer, int offset)
      {
         if (offset > buffer.Length - 4 || offset < 0)
               return new Pixel();
         return new Pixel
         (
               buffer[0 + offset],
               buffer[1 + offset],
               buffer[2 + offset],
               buffer[3 + offset]
         );
      }
      public static Pixel Extract24bit(byte[] buffer)
      {
         return new Pixel
         (
               buffer[0],
               buffer[1],
               buffer[2]
         );
      }
      public virtual void SetColor(Color color)
      {
         A = color.A; //B
         R = color.R; //G
         G = color.G; //R
         B = color.B; //A
      }
      public byte A = 255, R, G, B;
      public virtual byte[] Buffer => hasAlpha ? new byte[] { A, R, G, B } : new byte[] { R, G, B };
      public virtual Color color => Color.FromArgb(A, R, G, B);
      public override string ToString()
      {
         return $"\"ARGB=({A}, {R}, {G}, {B})\"";
      }
   }
   public struct Point16
   {
      public Point16(short x, short y)
      {
         X = x;
         Y = y;
      }
      public short X;
      public short Y;
      public byte[] Buffer()
      {
         var x = BitConverter.GetBytes(X);
         var y = BitConverter.GetBytes(Y);
         return new byte[] { x[0], x[1], y[0], y[1] };
      }
   }
}
namespace FoundationR.Loader
{ 
   public static class ImageLoader
   {
      static int count = 0;
      static bool skip = false;
      public static string WorkingDir;
      public static void Initialize(string path)
      {
         WorkingDir = path;
      }
      public static REW BitmapIngest(BitmapFile bitmap, PixelFormat format, bool skipConvert = true)
      {
         REW instance = REW.CreateEmpty(bitmap.Value.Width, bitmap.Value.Height, format);
         if (!skipConvert)
         {
               string file = Path.Combine(WorkingDir, bitmap.Name);
         BEGIN:
               if (File.Exists(file) && !skip)
               {
                  if (count == 0 || count > 1)
                  {
                     var result = MessageBox.Show($"File:\n\n{file}\n\nAlready exists. Would you like to overwrite it?", "File Overwrite", MessageBoxButtons.YesNoCancel);
                     if (result == DialogResult.Yes)
                     {
                           handleFile(bitmap);
                           count++;
                     }
                     else if (result == DialogResult.No)
                     {
                           SaveFileDialog dialog = new SaveFileDialog();
                           dialog.Title = "Pick a save file";
                           dialog.DefaultExt = "rew";
                           dialog.CheckPathExists = true;
                           dialog.RestoreDirectory = true;
                           dialog.ShowDialog();
                           file = dialog.FileName;
                     }
                     else if (result == DialogResult.Cancel)
                     {
                           return instance;
                     }
                  }
                  else if (count == 1)
                  {
                     var result = MessageBox.Show("There are clearly more files to be processed. Would you like to skip this dialog and overwrite them all?", "Overwrite All", MessageBoxButtons.YesNoCancel);
                     if (result == DialogResult.Yes)
                     {
                           skip = true;
                     }
                     else if (result == DialogResult.Cancel)
                     {
                           return instance;
                     }
                     count++;
                     goto BEGIN;
                  }
               }
               else
               {
                  handleFile(bitmap);
               }
         }
         else
         {
               instance.Extract(bitmap.Value);
         }
         void handleFile(BitmapFile bitmap)
         {
               instance.Extract(bitmap.Value);
               using (FileStream fs = new FileStream(Path.Combine(WorkingDir, bitmap.Name), FileMode.Create))
               {
                  BinaryWriter bw = new BinaryWriter(fs);
                  instance.Write(bw);
               }
         }
         return instance;
      }
      public static void HandleFile(string outpath, string inpath, int bpp)
      {
         Bitmap bitmap = (Bitmap)Bitmap.FromFile(inpath);
         REW result = REW.Extract(bitmap, (short)bpp);
         FileStream fs = default;
         try
         {
               fs = new FileStream(outpath, FileMode.CreateNew);
         }
         catch
         {
               var message = MessageBox.Show($"File\n\n{outpath}\n\nalready exists. Overwrite?", "File Exists", MessageBoxButtons.YesNoCancel);
               if (message == DialogResult.No || message == DialogResult.Cancel)
               {
                  return;
               }
               fs = new FileStream(outpath, FileMode.Create);
         }
         result.Write(new BinaryWriter(fs));
         fs.Dispose();
      }
   }
}
 
namespace FoundationR.Ext
{ 
   //  TODO:
   //  shrink the REW back to version 1.0 and then only use extensions on whatever is there
   public static class Ext
   {
      public static void AddHeader(this byte[] buffer, Point16 size, int dataLength, int bpp)
      {
         byte[] sizeb = size.Buffer();
         buffer[0] = sizeb[0];
         buffer[1] = sizeb[1];
         buffer[2] = sizeb[2];
         buffer[3] = sizeb[3];
         byte[] lenb = BitConverter.GetBytes(dataLength);
         buffer[4] = lenb[0];
         buffer[5] = lenb[1];
         buffer[6] = lenb[2];
         buffer[7] = lenb[3];
         byte[] _bpp = BitConverter.GetBytes(bpp);
         buffer[8] = _bpp[0];
         buffer[9] = _bpp[1];
      }

      public static void Write(this BinaryWriter w, Pixel pixel)
      {
         byte[] buffer = pixel.Buffer;
         w.Write(buffer, 0, 4);
      }
      public static void Write(this BinaryWriter w, Point16 point)
      {
         byte[] buffer = point.Buffer();
         w.Write(buffer, 0, 4);
      }
      public static Point16 ReadPoint16(this BinaryReader r)
      {
         Point16 p = new Point16();
         byte x0 = r.ReadByte();
         byte x1 = r.ReadByte();
         byte y0 = r.ReadByte();
         byte y1 = r.ReadByte();
         p.X = BitConverter.ToInt16(new byte[] { x0, x1 }, 0);
         p.Y = BitConverter.ToInt16(new byte[] { y0, y1 }, 0);
         return p;
      }
      public static Pixel ReadPixel(this BinaryReader r)
      {
         try
         {
               Pixel i = new Pixel
               (
                  r.ReadByte(),
                  r.ReadByte(),
                  r.ReadByte(),
                  r.ReadByte()
               );
               return i;
         }
         catch
         {
               return new Pixel();
         }
      }
      public static byte[] color_AppendPixel(this byte[] array, int index, Pixel i)
      {
         if (i.hasAlpha)
         {
               array[index] = i.A;
               array[index + 1] = i.R;
               array[index + 2] = i.G;
               array[index + 3] = i.B;
         }
         else
         {
               array[index] = i.R;
               array[index + 1] = i.G;
               array[index + 2] = i.B;
         }
         return array;
      }
      public static byte[] AppendPixel(this byte[] array, int index, Pixel i)
      {
         if (i.hasAlpha)
         {                           
               switch (RewBatch.renderOption)
               { 
                  case RenderOption.None:
                  case RenderOption.Both:
                  case RenderOption.GDI:      // CPU compositing requires:
                     array[index] = i.B;     // B, 
                     array[index + 1] = i.G; // G, 
                     array[index + 2] = i.R; // R, 
                     array[index + 3] = i.A; // A
                     break;
                  case RenderOption.Direct2D:
                     array[index] = i.A;     // A, 
                     array[index + 1] = i.R; // R, 
                     array[index + 2] = i.G; // G, 
                     array[index + 3] = i.B; // B
                     break;
               }
         }
         else
         {
               switch (RewBatch.renderOption)
               {
                  case RenderOption.None:
                  case RenderOption.Both:
                  case RenderOption.GDI:      // CPU compositing requires:
                     array[index] = i.R;     // B, 
                     array[index + 1] = i.G; // G, 
                     array[index + 2] = i.B; // R, 
                     break;
                  case RenderOption.Direct2D:
                     array[index] = i.R; // R, 
                     array[index + 1] = i.G; // G, 
                     array[index + 2] = i.B; // B
                     break;
               }
         }
         return array;
      }
      public static byte[] AppendPoint16(this byte[] array, int index, Point16 i)
      {
         byte[] buffer = i.Buffer();
         array[index] = buffer[0];   // x
         array[index + 1] = buffer[1];
         array[index + 2] = buffer[3];   // y
         array[index + 3] = buffer[4];
         return array;
      }
      public static void Composite(this byte[] input, byte[] layer, int x, int y, int width, int height)
      {
         int w = width;
         if (w > RewBatch.width)
         {
               w = RewBatch.width;
         }
         Parallel.For(0, height, j =>
         {
               for (int i = 0; i < w; i++)
               {
                  int whoAmI = ((y + j) * w + (x + i)) * 4;
                  if (whoAmI < 0 || whoAmI > input.Length)
                  {
                     continue;
                  }
                  Pixel _one = Pixel.Extract32bit(input, whoAmI);
                  Pixel _two = Pixel.Extract32bit(layer, (j * width + i) * 4);
                  if (_two.A < 255)
                  {
                     Color blend = _two.color.Blend(_one.color, 0.15d);
                     input[whoAmI] = blend.A;     // B
                     input[whoAmI + 1] = blend.R; // G
                     input[whoAmI + 2] = blend.G; // R
                     input[whoAmI + 3] = blend.B; // A
                  }
                  else
                  {
                     input[whoAmI] = 255;
                     input[whoAmI + 1] = _two.color.R;
                     input[whoAmI + 2] = _two.color.G;
                     input[whoAmI + 3] = _two.color.B;
                  }
               }
         });
      }
      public static REW Composite(this byte[] one, REW tex, int x, int y)
      {
         short width = tex.Width;
         short height = tex.Height;
         REW output = REW.Create(RewBatch.width, RewBatch.height, one, 32);
         for (int n = 0; n < height; n++)
         {
               for (int m = 0; m < width; m++)
               {
                  if (n > output.Height || m > output.Width)
                     continue;
                  Pixel _one = output.GetPixel(m + x, n + y);
                  Pixel _two = tex.GetPixel(m, n);
                  if (_two.A < 255)
                  {
                     output.SetPixel(m + x, n + y, _two.color.Blend(_one.color, 0.15d));
                  }
                  else output.SetPixel(m + x, n + y, _two.color);
               }
         }
         return output;
      }
      public static REW Composite(this REW one, REW tex, int x, int y)
      {
         short width = tex.Width;
         short height = tex.Height;
         REW output = REW.CreateEmpty(one.Width, one.Height, PixelFormats.Bgr32);
         for (int n = 0; n < height; n++)
         {
               for (int m = 0; m < width; m++)
               {
                  if (n > one.Height || m > one.Width)
                     continue;
                  Pixel _one = one.GetPixel(m + x, n + y);
                  Pixel _two = tex.GetPixel(m, n);
                  if (_two.A < 255)
                  {
                     output.SetPixel(m + x, n + y, _two.color.Blend(_one.color, 0.15d));
                  }
                  else output.SetPixel(m + x, n + y, _two.color);
               }
         }
         return output;
      }
      public static Pixel Composite(this Pixel one, Pixel two)
      {
         if (two.A < 255)
         {
               one.SetColor(two.color.Blend(one.color, 0.85d));
         }
         else one = two;
         return one;
      }
      public static Pixel PreMultiply(this Pixel pixel)
      {
         byte r = pixel.R;
         byte g = pixel.G;
         byte b = pixel.B;
         byte a = pixel.A;
         pixel.R = (byte)((r * a) / 255);
         pixel.G = (byte)((g * a) / 255);
         pixel.B = (byte)((b * a) / 255);
         return pixel;
      }
      public static int BytesPerPixel(this System.Drawing.Imaging.PixelFormat format)
      {
         int bytesPerPixel = 0;
         switch (format)
         {
               case System.Drawing.Imaging.PixelFormat.Format16bppArgb1555:
               case System.Drawing.Imaging.PixelFormat.Format16bppGrayScale:
               case System.Drawing.Imaging.PixelFormat.Format16bppRgb555:
               case System.Drawing.Imaging.PixelFormat.Format16bppRgb565:
                  bytesPerPixel = 16 / 8;
                  break;
               case System.Drawing.Imaging.PixelFormat.Format1bppIndexed:
               case System.Drawing.Imaging.PixelFormat.Format4bppIndexed:
               case System.Drawing.Imaging.PixelFormat.Format8bppIndexed:
                  bytesPerPixel = 1;
                  break;
               case System.Drawing.Imaging.PixelFormat.Format24bppRgb:
                  bytesPerPixel = 24 / 8;
                  break;
               case System.Drawing.Imaging.PixelFormat.Format32bppArgb:
               case System.Drawing.Imaging.PixelFormat.Format32bppPArgb:
               case System.Drawing.Imaging.PixelFormat.Format32bppRgb:
                  bytesPerPixel = 32 / 8;
                  break;
               case System.Drawing.Imaging.PixelFormat.Format48bppRgb:
                  bytesPerPixel = 48 / 8;
                  break;
               case System.Drawing.Imaging.PixelFormat.Format64bppArgb:
               case System.Drawing.Imaging.PixelFormat.Format64bppPArgb:
                  bytesPerPixel = 64 / 8;
                  break;
         }
         return bytesPerPixel;
      }
      public static PixelFormat GetFormat(int bpp)
      {
         switch (bpp)
         {
               case 1:
                  return PixelFormats.Indexed8;
               case 2:
                  return PixelFormats.Bgr555;
               case 3:
                  return PixelFormats.Bgr24;
               default:
               case 8:
                  return PixelFormats.Bgr32;
               case 12:
                  return PixelFormats.Rgb48;
               case 16:
                  return PixelFormats.Rgba64;
         }
      }
      public static REW ReadREW(this FileStream fs)
      {
         BinaryReader br = new BinaryReader(fs);
         REW result = default;
         (result = new REW()).ReadData(br);
         br.Dispose();
         fs.Dispose();
         return result;
      }
   }
   public class Composite
   {
      public Composite(string layerOneFile, string layerTwoFile, string outputFile)
      {
         // load the files
         var layerOne = new BitmapImage(new Uri(layerOneFile, UriKind.Absolute));
         var layerTwo = new BitmapImage(new Uri(layerTwoFile, UriKind.Absolute));

         // create the destination based upon layer one
         var composite = new WriteableBitmap(layerOne);

         // premultiply the alpha values for layer one
         composite = PremultiplyAlpha(composite);

         // premultiply the alpha values for layer two
         var _layerTwo = PremultiplyAlpha(new WriteableBitmap(layerTwo));

         // copy the pixels from layer two on to the destination
         int[] pixels = new int[(int)layerTwo.Width * (int)layerTwo.Height];
         int stride = (int)(4 * layerTwo.Width);
         _layerTwo.CopyPixels(pixels, stride, 0);
         composite.WritePixels(new Int32Rect(0, 0, (int)layerTwo.Width, (int)layerTwo.Height), pixels, stride, 0);

         // encode the bitmap to the output file
         PngBitmapEncoder encoder = new PngBitmapEncoder();
         encoder.Frames.Add(BitmapFrame.Create(composite));
         using (var stream = new FileStream(outputFile, FileMode.Create))
         {
               encoder.Save(stream);
         }
      }

      // premultiply the alpha values for a bitmap
      public WriteableBitmap PremultiplyAlpha(WriteableBitmap bitmap)
      {
         int[] pixels = new int[(int)bitmap.Width * (int)bitmap.Height];
         int stride = (int)(4 * bitmap.Width);
         bitmap.CopyPixels(pixels, stride, 0);

         for (int i = 0; i < pixels.Length; i++)
         {
               byte a = (byte)(pixels[i] >> 24);
               byte r = (byte)(pixels[i] >> 16);
               byte g = (byte)(pixels[i] >> 8);
               byte b = (byte)(pixels[i] >> 0);

               r = (byte)((r * a) / 255);
               g = (byte)((g * a) / 255);
               b = (byte)((b * a) / 255);

               pixels[i] = (a << 24) | (r << 16) | (g << 8) | (b << 0);
         }

         var result = new WriteableBitmap(bitmap);
         result.WritePixels(new Int32Rect(0, 0, (int)bitmap.Width, (int)bitmap.Height), pixels, stride, 0);

         return result;
      }
   }
   public static class ColorExtensions
   {
      public static Color Blend(this Color color, Color backColor, double amount)
      {
         byte a = (byte)((color.A + backColor.A) / 2); // unknown
         byte r = (byte)(color.R * amount + backColor.R * (1 - amount));
         byte g = (byte)(color.G * amount + backColor.G * (1 - amount));
         byte b = (byte)(color.B * amount + backColor.B * (1 - amount));
         return Color.FromArgb(a, r, g, b);
      }
      [Obsolete("Transforms the textures into something chaotic.")]
      public static Color AlphaBlend(this Color argb, Color blend)
      {
         if (argb.A == 0)
               return blend;
         if (blend.A == 0)
               return argb;
         if (argb.A == 255)
               return argb;

         int alpha = argb.A + 1;
         int r = (alpha * argb.R + (255 - alpha) * blend.R) >> 8;
         int g = (alpha * argb.G + (255 - alpha) * blend.G) >> 8;
         int b = (alpha * argb.B + (255 - alpha) * blend.B) >> 8;

         return Color.FromArgb(Math.Abs(argb.A), Math.Abs(r), Math.Abs(g), Math.Abs(b));
      }
      [Obsolete("Literally does nothing.")]
      public static REW AlphaBlend(this REW surface, REW image)
      {
         // Load your images and get pixel data
         //Bitmap bitmap = new Bitmap("path/to/your/opaque_bitmap.bmp");
         //Bitmap pngImage = new Bitmap("path/to/your/translucent_png.png");

         int w = 0, h = 0;
         // Ensure both images have the same dimensions
         if (surface.Width != image.Width || surface.Height != image.Height)
         {
               // Handle dimension mismatch
               // (e.g., resize one of the images to match the other)
               if (surface.Width > image.Width)
               {
                  w = image.Width;
               }
               else w = surface.Width;
               if (surface.Height > image.Height)
               {
                  h = image.Height;
               }
               else h = surface.Height;
         }

         // Iterate through pixels
         for (int y = 0; y < h; y++)
         {
               for (int x = 0; x < w; x++)
               {
                  Pixel pngPixel = image.GetPixel(x, y);
                  Pixel bmpPixel = surface.GetPixel(x, y);

                  // Blend based on alpha (transparency)
                  int blendedR = (int)(pngPixel.A * pngPixel.R / 255.0 + (1 - pngPixel.A / 255.0) * bmpPixel.R);
                  int blendedG = (int)(pngPixel.A * pngPixel.G / 255.0 + (1 - pngPixel.A / 255.0) * bmpPixel.G);
                  int blendedB = (int)(pngPixel.A * pngPixel.B / 255.0 + (1 - pngPixel.A / 255.0) * bmpPixel.B);

                  // Set the blended pixel in the Bitmap
                  surface.SetPixel(x, y, Color.FromArgb(bmpPixel.A, blendedR, blendedG, blendedB));
               }
         }
         // Save or use the composited Bitmap
         //bitmap.Save("path/to/output/composited_image.bmp");
         return surface;
      }
   }
   public enum RenderOption : byte
   {
      None = 0,
      GDI = 1,
      Direct2D = 2,
      Both = 3
   }
}
