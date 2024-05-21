using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Windows.Media.Media3D;
using System.Windows.Threading;
using FoundationR;
using FoundationR.Lib;
using FoundationR.Rew;
using FoundationR.Loader;
using FoundationR.Ext;
using FoundationR.Headers;

namespace FoundationR.Lib
{
   public partial class Foundation
   {
      [DllImport("user32.dll", SetLastError = true)]
      static extern IntPtr FindWindow(string lpClassName, string lpWindowName);
      [DllImport("user32.dll", EntryPoint = "FindWindow", SetLastError = true)]
      static extern IntPtr FindWindowByCaption(IntPtr ZeroOnly, string lpWindowName);
      [DllImport("gdi32.dll")]
      internal static extern bool DeleteObject(IntPtr hObject);
      [DllImport("user32.dll")]
      static extern IntPtr GetDCEx(IntPtr hWnd, IntPtr hrgnClip, uint flags);
      [DllImport("user32.dll")]
      static extern IntPtr GetWindowDC(IntPtr hWnd);
      [DllImport(".\\Direct2D_Render.dll")]
      static extern void Direct2D_ShowWindow();
      [DllImport(".\\Direct2D_Render.dll")]
      static extern void Direct2D_Init(uint width, uint height);
      [DllImport(".\\Direct2D_Render.dll")]
      static extern void Direct2D_InitEx(IntPtr hWnd, uint width, uint height);
      [DllImport("user32.dll", SetLastError = true)]
      public static extern IntPtr CreateWindowEx(
         uint dwExStyle,
         string lpClassName,
         string lpWindowName,
         uint dwStyle,
         int x,
         int y,
         int nWidth,
         int nHeight,
         IntPtr hWndParent,
         IntPtr hMenu,
         IntPtr hInstance,
         IntPtr lpParam);
      [DllImport("user32.dll")]
      [return: MarshalAs(UnmanagedType.Bool)]
      public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

      bool flag = true, flag2 = true, init, init2, running = true;
      public static int offX, offY;
      public static Rectangle bounds;
      internal static Viewport viewport = new Viewport();
      protected static RewBatch _rewBatch;
      public static IntPtr HDC, HWND, Handle;
      public Stopwatch watch = new Stopwatch();
      internal static bool MouseLeft;
      static IntPtr handle;

      internal class SurfaceForm : Form
      {
         internal SurfaceForm(Surface surface)
         {
               //form.TransparencyKey = System.Drawing.Color.CornflowerBlue;
               BackColor = System.Drawing.Color.CornflowerBlue;
               FormBorderStyle = FormBorderStyle.FixedSingle;
               Width = surface.Width;
               Height = surface.Height;
               Location = new Point(surface.X, surface.Y);
               Text = surface.Title;
               Name = surface.Title;
               DoubleBuffered = true;
               UseWaitCursor = false;
               BringToFront();
         }
      }

      public virtual void RegisterHooks(Form form)
      {
      }
      public virtual void ClearInput()
      { 
      }
      public void Run(Surface window)
        {
            window.form = new SurfaceForm(window);
            _rewBatch = new RewBatch(window.Width, window.Height, window.BitsPerPixel);
            if (RewBatch.renderOption != RenderOption.None)
            {
                START:
                if (window.form.Handle == IntPtr.Zero)
                {
                    Task.WaitAll(Task.Delay(100));
                    window.form.Refresh();
                    goto START;
                }
                if (RewBatch.renderOption == RenderOption.Direct2D)
                { 
                    Direct2D_InitEx(handle = window.form.Handle, (uint)window.Width, (uint)window.Height);
                }
            }
            this.RegisterHooks(window.form);
            LoadResourcesEvent?.Invoke();
            InitializeEvent?.Invoke(new InitializeArgs());
            Thread t = new Thread(() => Loop(ref running));
            Thread t2 = new Thread(() => draw(ref flag, window));
         t.SetApartmentState(ApartmentState.STA);
         t2.SetApartmentState(ApartmentState.STA);
         t.Start();
         t2.Start();

         void Loop(ref bool running)
         {
               watch.Start();
               double deltaTime = 0;
               double accumulator = 0;
               double targetFrameTime = 1.0 / 120.0;
               double oldTime = 0;

               while (running)
               {
                  InputEvent?.Invoke(new InputArgs() 
                  { 
                     windowBounds = WindowUtils.GetWindowRectangleWithoutShadows(handle)
                  });

                  double currentTime = watch.Elapsed.TotalSeconds;
                  watch.Restart();
                  deltaTime = currentTime - oldTime;
                  oldTime = currentTime;

                  accumulator += deltaTime;

                  if (accumulator < 0)
                  {
                     accumulator = 0d;
                  }
                  ViewportEvent?.Invoke(new ViewportArgs() { viewport = viewport });
                  if ((bool)ExitEvent?.Invoke(new ExitArgs()))
                  {
                     Application.Exit();
                  }
                  if (accumulator >= targetFrameTime)
                  {
                     update(ref flag2);
                     accumulator -= targetFrameTime;
                     ClearInput();
                  }
               }
         }

         void draw(ref bool taskDone, Surface surface)
         {
               int width = (int)surface.Width;
               int height = (int)surface.Height;
               while (running)
               { 
                  if (taskDone)
                  {
                     taskDone = false;
                     InternalBegin(window);
                     if ((bool)ResizeEvent?.Invoke(new ResizeArgs()))
                     {
                           _rewBatch = new RewBatch(width, height, window.BitsPerPixel);
                     }
                     MainMenuEvent?.Invoke(new DrawingArgs() { rewBatch = _rewBatch });
                     PreDrawEvent?.Invoke(new PreDrawArgs() { rewBatch = _rewBatch });
                     DrawEvent?.Invoke(new DrawingArgs() { rewBatch = _rewBatch });
                     InternalEnd(GetDCEx(FindWindowByCaption(IntPtr.Zero, window.Title), IntPtr.Zero, 0x403));
                     taskDone = true;
                  }
               }
         }
         void update(ref bool taskDone)
         {
               if (taskDone)
               {
                  taskDone = false;
                  UpdateEvent?.Invoke(new UpdateArgs());
                  taskDone = true;
               }
         }
         if (RewBatch.renderOption != RenderOption.None)
         { 
               window.form.ShowDialog();
         }
      }
      bool UpdateLimiter(Stopwatch watch1)
      {
         double deltaTime = 0; // Initialize the time accumulator
         double accumulator = 0; // Accumulated time
         double targetFrameTime = 1.0 / 60.0; // Target frame time (1/60 seconds)
         double oldTime = 0;

         double currentTime = watch1.Elapsed.Milliseconds; // Get current time
         deltaTime = currentTime - oldTime; // Calculate time since last frame
         oldTime = currentTime; // Update old time

         accumulator += deltaTime; // Accumulate time

         // Update when the accumulated time exceeds the target frame time
         while (accumulator >= targetFrameTime)
         {
               watch1.Restart();
               accumulator -= targetFrameTime; // Subtract the frame time
               return true;
         }
         return false;
      }

      private void InternalBegin(Surface window)
      {
         _rewBatch.Begin(IntPtr.Zero);
      }
      private void InternalBegin(IntPtr hdc)
      {
         _rewBatch.Begin(hdc);
      }
      private void InternalEnd(IntPtr handle)
      {
         _rewBatch.End(RewBatch.renderOption, handle);
      }
      #region events
      public delegate void Event<T>(T e);
      public delegate void Event();
      public delegate bool _Resize<T>(T e);
      public delegate bool _Exit<T>(T e);
      public static event _Exit<ExitArgs> ExitEvent;
      public static event _Resize<ResizeArgs> ResizeEvent;
      public static event Event<InitializeArgs> InitializeEvent;
      public static event Event<InputArgs> InputEvent;
      public static event Event LoadResourcesEvent;
      public static event Event<DrawingArgs> MainMenuEvent;
      public static event Event<PreDrawArgs> PreDrawEvent;
      public static event Event<DrawingArgs> DrawEvent;
      public static event Event<UpdateArgs> UpdateEvent;
      public static event Event<ViewportArgs> ViewportEvent;
      public interface IArgs
      {
      }
      public class ResizeArgs : IArgs
      {
      }
      public class DrawingArgs : IArgs
      {
         public RewBatch rewBatch;
      }
      public class PreDrawArgs : IArgs
      {
         public RewBatch rewBatch;
      }
      public class UpdateArgs : IArgs
      {
      }
      public class ViewportArgs : IArgs
      {
         public Viewport viewport;
      }
      public class InitializeArgs : IArgs
      {
      }
      public class InputArgs : IArgs
      {
         public RECT windowBounds;
      }
      public class ExitArgs : IArgs
      {
      }
      #endregion
   }
   public struct Surface
   {
      public Surface(int x, int y, int width, int height, string windowTitle, int bitsPerPixel)
      {
         this.X = x;
         this.Y = y;
         this.Width = width;
         this.Height = height;
         this.Title = windowTitle;
         this.BitsPerPixel = bitsPerPixel;
         form = default;
      }
      public string Title;
      public int Width, Height;
      public int X, Y;
      public int BitsPerPixel;
      public Form form;
   }
   public class Viewport
   {
      public Vector2 oldPosition;
      public Vector2 position;
      public Vector2 velocity;
      public Rectangle bounds;
      public int Right => X + bounds.Width;
      public int Bottom => Y + bounds.Height;
      public int Width => bounds.Width;
      public int Height => bounds.Height;
      public int X => (int)position.X;
      public int Y => (int)position.Y;
      public virtual bool isMoving => velocity != Vector2.Zero || oldPosition != position;
      public bool follow = false;
      public bool active = false;
   }
   public struct RECT
   {
      public int Left;
      public int Top;
      public int Right;
      public int Bottom;
   }
   public static class WindowUtils
   {
      [DllImport("dwmapi.dll")]
      static extern int DwmGetWindowAttribute(IntPtr hwnd, int dwAttribute, out RECT pvAttribute, int cbAttribute);

      public static RECT GetWindowRectangleWithoutShadows(IntPtr handle)
      {
         RECT rect;
         DwmGetWindowAttribute(handle, 9 /* DWMWA_EXTENDED_FRAME_BOUNDS */, out rect, Marshal.SizeOf(typeof(RECT)));
         return rect;
      }
   }
}
