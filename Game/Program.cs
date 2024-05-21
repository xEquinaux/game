using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Net.Http.Headers;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Threading;                      
using System.Threading.Tasks;
using System.Windows.Diagnostics;
using System.Windows.Forms;
using System.Windows.Input;
using FoundationR;
using FoundationR.Lib;
using FoundationR.Rew;
using FoundationR.Loader;
using FoundationR.Ext;
using FoundationR.Headers;
using Microsoft.Win32;


namespace game
{
   internal class Program
   {
      static int StartX => 0;
      static int StartY => 0;
      static int Width => 640;
      static int Height => 480;
      static int BitsPerPixel => 32;
      static string Title = "Foundation_GameTemplate";
        
      static void Main(string[] args)
      {
         game.Main m = null;
         Thread t = new Thread(() => { (m = new Main()).Run(new Surface(StartX, StartY, Width, Height, Title, BitsPerPixel)); });
         t.SetApartmentState(ApartmentState.STA);
         t.Start();
         while (Console.ReadLine() != "exit");
         t.Abort();
         Environment.Exit(0);
      }
   }
   public class Main : Foundation
   {
      Point mouse;
      RECT window_frame;
      REW pane;
      REW tile;
      REW cans;
      REW solidColor;
      Form form;
      IList<Keys> keyboard = new List<Keys>();
      bool handled = false;
        
      internal Main()
      {
      }

      public override void RegisterHooks(Form form)
      {
         Foundation.UpdateEvent += Update;
         Foundation.ResizeEvent += Resize;
         Foundation.InputEvent += Input;
         Foundation.DrawEvent += Draw;
         Foundation.InitializeEvent += Initialize;
         Foundation.LoadResourcesEvent += LoadResources;
         Foundation.MainMenuEvent += MainMenu;
         Foundation.PreDrawEvent += PreDraw;
         Foundation.ViewportEvent += Viewport;
         Foundation.ExitEvent += Exit;
         this.form = form;
      }

      protected bool Exit(ExitArgs e)
      {
         return false;
      }

      public override void ClearInput()
      {
         keyboard.Clear();
         handled = false;
      }

      protected void Input(InputArgs e)
      {
         form.Invoke(new Action(() =>
         {
               var _mouse = form.PointToClient(System.Windows.Forms.Cursor.Position);
               int x = _mouse.X;
               int y = _mouse.Y;
               this.mouse = new Point(x + 8, y + 31);
         }));
      }

      protected void Viewport(ViewportArgs e)
      {
         if (KeyDown(Key.W))
         {
               e.viewport.position.Y--;
         }
         if (KeyDown(Key.A))
         {
               e.viewport.position.X--;
         }
         if (KeyDown(Key.S))
         {
               e.viewport.position.Y++;
         }
         if (KeyDown(Key.D))
         { 
               e.viewport.position.X++;
         }
      }

      protected void PreDraw(PreDrawArgs e)
      {
      }

      protected void MainMenu(DrawingArgs e)
      {
      }

      protected void LoadResources()
      {
         Asset.LoadFromFile(@".\Textures\bluepane.rew", out pane);
         Asset.LoadFromFile(@".\Textures\background.rew", out tile);
         Asset.LoadFromFile(@".\Textures\cans.rew", out cans);
      }

      protected void Initialize(InitializeArgs e)
      {
      }

      protected void Draw(DrawingArgs e)
      {
         e.rewBatch.Draw(cans, RewBatch.Viewport.X, RewBatch.Viewport.X);
         //e.rewBatch.Draw(pane, 0, 0);
         if (mouse.X + 50 >= 640 || mouse.Y + 50 >= 480 || mouse.X <= 0 || mouse.Y <= 0)
               goto COLORS;
         e.rewBatch.Draw(tile.GetPixels(), mouse.X, mouse.Y, 50, 50);
         COLORS:
         e.rewBatch.Draw(REW.Create(50, 50, Color.White, Ext.GetFormat(4)), 0, 0);
         e.rewBatch.Draw(REW.Create(50, 50, Color.Red, Ext.GetFormat(4)), 50, 0);
         e.rewBatch.Draw(REW.Create(50, 50, Color.Green, Ext.GetFormat(4)), 100, 0);
         e.rewBatch.Draw(REW.Create(50, 50, Color.Blue, Ext.GetFormat(4)), 150, 0);
         e.rewBatch.Draw(REW.Create(50, 50, Color.Gray, Ext.GetFormat(4)), 200, 0);
         e.rewBatch.Draw(REW.Create(50, 50, Color.Black, Ext.GetFormat(4)), 250, 0);
         e.rewBatch.Draw(REW.Create(50, 50, Color.White, Ext.GetFormat(4)), 640, 50);
         e.rewBatch.DrawString("Arial", "Test_value_01", 50, 50, 200, 100, Color.White);
      }

      protected void Update(UpdateArgs e)
      {
      }
        
      protected bool Resize(ResizeArgs e)
      {
         return false;
      }

      private bool KeyDown(Key k)
      {
         return Keyboard.PrimaryDevice.IsKeyDown(k);
      }
      private bool KeyUp(Key k)
      {
         return Keyboard.PrimaryDevice.IsKeyUp(k);
      }
   }
}
