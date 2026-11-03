using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using System.Xml.Serialization;
using F = System.Windows.Forms;

static class Native { [DllImport("user32.dll")] public static extern bool SetProcessDpiAwarenessContext(IntPtr context); [DllImport("dwmapi.dll")] public static extern int DwmSetWindowAttribute(IntPtr w,int attr,ref int value,int size); }
static class UI {
 public static Brush Ink = B("#141414"), Muted = B("#626262"), Line = B("#E6E6E6"), Ground = B("#F8F8F8"), Blue = B("#1D5FBF"), Red = B("#A52626");
 public static Brush B(string s) { return (Brush)new BrushConverter().ConvertFromString(s); }
 public static TextBlock Text(string s, double size, Brush color) { return new TextBlock { Text=s, FontSize=size, Foreground=color, TextWrapping=TextWrapping.Wrap }; }
 public static Button Button(string s, RoutedEventHandler click, bool primary) {
  var b = new Button { Content=s, Padding=new Thickness(11,6,11,6), MinHeight=34, Background=primary?Ink:Brushes.White, Foreground=primary?Brushes.White:Ink, BorderBrush=primary?Ink:Line, BorderThickness=new Thickness(1), Cursor=Cursors.Hand, FontSize=13, Margin=new Thickness(0), VerticalContentAlignment=VerticalAlignment.Center };
  AutomationProperties.SetName(b,s); b.Click += click; return b;
 }
 public static TextBox Input(string text, bool multi) { return new TextBox { Text=text, FontSize=14, Padding=new Thickness(9), BorderBrush=B("#D4D4D4"), BorderThickness=new Thickness(1), Background=Brushes.White, Foreground=Ink, AcceptsReturn=multi, TextWrapping=multi?TextWrapping.Wrap:TextWrapping.NoWrap, VerticalScrollBarVisibility=multi?ScrollBarVisibility.Auto:ScrollBarVisibility.Hidden, MinHeight=38 }; }
 public static void Label(StackPanel p, string s) { var t = Text(s,13,Muted); t.Margin=new Thickness(0,14,0,7); p.Children.Add(t); }
 public static void Capture(FrameworkElement root, string path) {
  root.UpdateLayout(); var bmp = new RenderTargetBitmap((int)Math.Ceiling(root.ActualWidth),(int)Math.Ceiling(root.ActualHeight),96,96,PixelFormats.Pbgra32);
  var background=new DrawingVisual();using(var dc=background.RenderOpen())dc.DrawRectangle(Ground,null,new Rect(0,0,root.ActualWidth,root.ActualHeight));bmp.Render(background);bmp.Render(root);
  using(var fs=File.Create(path)) { var enc=new PngBitmapEncoder(); enc.Frames.Add(BitmapFrame.Create(bmp)); enc.Save(fs); }
 }
 public static void WindowStyle(Window w) {
  w.FontFamily=new FontFamily("Microsoft YaHei UI"); w.FontSize=14; w.Foreground=Ink; w.Background=Ground; w.WindowStartupLocation=WindowStartupLocation.CenterOwner;
  w.Resources.Add(typeof(Button),(Style)XamlReader.Parse(@"<Style xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' TargetType='Button'><Setter Property='Template'><Setter.Value><ControlTemplate TargetType='Button'><Border x:Name='face' xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml' CornerRadius='6' Background='{TemplateBinding Background}' BorderBrush='{TemplateBinding BorderBrush}' BorderThickness='{TemplateBinding BorderThickness}' Padding='{TemplateBinding Padding}'><ContentPresenter HorizontalAlignment='Center' VerticalAlignment='Center'/></Border><ControlTemplate.Triggers><Trigger Property='IsMouseOver' Value='True'><Setter Property='Opacity' Value='0.82'/></Trigger><Trigger Property='IsPressed' Value='True'><Setter Property='Opacity' Value='0.65'/></Trigger><Trigger Property='IsEnabled' Value='False'><Setter Property='Opacity' Value='0.5'/></Trigger></ControlTemplate.Triggers></ControlTemplate></Setter.Value></Setter></Style>"));
  w.SourceInitialized += delegate { try { int corner=2; Native.DwmSetWindowAttribute(new WindowInteropHelper(w).Handle,33,ref corner,4); } catch {} };
 }
}
