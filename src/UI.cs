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
 public static Brush Ink = B("#282D29"), Muted = B("#5C625D"), Line = B("#DDDFD8"), Ground = B("#F3F3EF"), Blue = B("#1B6E66"), Red = B("#A52626");
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
  var surface=new DrawingVisual();using(var dc=surface.RenderOpen())dc.DrawRectangle(new VisualBrush(root){Stretch=Stretch.Fill,ViewboxUnits=BrushMappingMode.Absolute,Viewbox=new Rect(VisualTreeHelper.GetOffset(root).X,VisualTreeHelper.GetOffset(root).Y,root.ActualWidth,root.ActualHeight)},null,new Rect(0,0,root.ActualWidth,root.ActualHeight));bmp.Clear();bmp.Render(background);bmp.Render(surface);
  using(var fs=File.Create(path)) { var enc=new PngBitmapEncoder(); enc.Frames.Add(BitmapFrame.Create(bmp)); enc.Save(fs); }
 }
 public static void WindowStyle(Window w) {
  w.FontFamily=new FontFamily("Microsoft YaHei UI"); w.FontSize=14; w.Foreground=Ink; w.Background=Ground; w.WindowStartupLocation=WindowStartupLocation.CenterOwner;
  w.Resources.Add(typeof(Button),(Style)XamlReader.Parse(@"<Style xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' TargetType='Button'><Setter Property='Template'><Setter.Value><ControlTemplate TargetType='Button'><Border x:Name='face' xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml' CornerRadius='8' Background='{TemplateBinding Background}' BorderBrush='{TemplateBinding BorderBrush}' BorderThickness='{TemplateBinding BorderThickness}' Padding='{TemplateBinding Padding}'><ContentPresenter HorizontalAlignment='{TemplateBinding HorizontalContentAlignment}' VerticalAlignment='Center'/></Border><ControlTemplate.Triggers><Trigger Property='IsMouseOver' Value='True'><Setter Property='Opacity' Value='0.8'/></Trigger><Trigger Property='IsKeyboardFocused' Value='True'><Setter TargetName='face' Property='BorderBrush' Value='#1B6E66'/><Setter TargetName='face' Property='BorderThickness' Value='2'/></Trigger><Trigger Property='IsPressed' Value='True'><Setter Property='Opacity' Value='0.65'/></Trigger><Trigger Property='IsEnabled' Value='False'><Setter Property='Opacity' Value='0.5'/></Trigger></ControlTemplate.Triggers></ControlTemplate></Setter.Value></Setter></Style>"));
  w.Resources.Add(typeof(TextBox),(Style)XamlReader.Parse(@"<Style xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml' TargetType='TextBox'><Setter Property='Template'><Setter.Value><ControlTemplate TargetType='TextBox'><Border x:Name='face' CornerRadius='8' BorderThickness='{TemplateBinding BorderThickness}' BorderBrush='{TemplateBinding BorderBrush}' Background='{TemplateBinding Background}'><ScrollViewer x:Name='PART_ContentHost' Margin='{TemplateBinding Padding}'/></Border><ControlTemplate.Triggers><Trigger Property='IsKeyboardFocused' Value='True'><Setter TargetName='face' Property='BorderBrush' Value='#1B6E66'/></Trigger></ControlTemplate.Triggers></ControlTemplate></Setter.Value></Setter></Style>"));
  w.Resources.Add(typeof(ComboBox),(Style)XamlReader.Parse(@"<Style xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml' TargetType='ComboBox'>
   <Setter Property='Background' Value='White'/><Setter Property='BorderBrush' Value='#D4D7D0'/><Setter Property='BorderThickness' Value='1'/><Setter Property='Padding' Value='9,4'/>
   <Setter Property='ScrollViewer.HorizontalScrollBarVisibility' Value='Disabled'/><Setter Property='ScrollViewer.VerticalScrollBarVisibility' Value='Auto'/>
   <Setter Property='Template'><Setter.Value><ControlTemplate TargetType='ComboBox'><Grid>
    <ToggleButton Focusable='False' IsChecked='{Binding IsDropDownOpen, RelativeSource={RelativeSource TemplatedParent}, Mode=TwoWay}' ClickMode='Press'>
     <ToggleButton.Template><ControlTemplate TargetType='ToggleButton'><Border x:Name='toggleFace' Background='{Binding Background, RelativeSource={RelativeSource AncestorType=ComboBox}}' BorderBrush='#D4D7D0' BorderThickness='1' CornerRadius='8'><Path Data='M 0 0 L 4 4 L 8 0' Stroke='#5C625D' StrokeThickness='1.2' HorizontalAlignment='Right' VerticalAlignment='Center' Margin='0,0,10,0'/></Border><ControlTemplate.Triggers><Trigger Property='IsMouseOver' Value='True'><Setter TargetName='toggleFace' Property='BorderBrush' Value='#8D978D'/></Trigger></ControlTemplate.Triggers></ControlTemplate></ToggleButton.Template>
    </ToggleButton>
    <ContentPresenter Margin='9,4,27,4' Content='{TemplateBinding SelectionBoxItem}' ContentTemplate='{TemplateBinding SelectionBoxItemTemplate}' VerticalAlignment='Center' IsHitTestVisible='False'/>
    <Popup x:Name='PART_Popup' Placement='Bottom' IsOpen='{TemplateBinding IsDropDownOpen}' AllowsTransparency='True' Focusable='False'><Border Background='White' BorderBrush='#D4D7D0' BorderThickness='1' CornerRadius='8' Padding='4' MinWidth='{Binding ActualWidth, RelativeSource={RelativeSource TemplatedParent}}' MaxHeight='320'><ScrollViewer CanContentScroll='True'><ItemsPresenter KeyboardNavigation.DirectionalNavigation='Contained'/></ScrollViewer></Border></Popup>
   </Grid><ControlTemplate.Triggers><Trigger Property='IsEnabled' Value='False'><Setter Property='Opacity' Value='.55'/></Trigger></ControlTemplate.Triggers></ControlTemplate></Setter.Value></Setter>
  </Style>"));
  w.Resources.Add(typeof(TabItem),(Style)XamlReader.Parse(@"<Style xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml' TargetType='TabItem'><Setter Property='Template'><Setter.Value><ControlTemplate TargetType='TabItem'><Border x:Name='face' Padding='{TemplateBinding Padding}' Background='Transparent' BorderBrush='Transparent' BorderThickness='0,0,0,2'><ContentPresenter ContentSource='Header'/></Border><ControlTemplate.Triggers><Trigger Property='IsSelected' Value='True'><Setter TargetName='face' Property='BorderBrush' Value='#1B6E66'/></Trigger><Trigger Property='IsMouseOver' Value='True'><Setter TargetName='face' Property='Background' Value='#E7E9E2'/></Trigger><Trigger Property='IsKeyboardFocused' Value='True'><Setter TargetName='face' Property='Background' Value='#E7E9E2'/></Trigger></ControlTemplate.Triggers></ControlTemplate></Setter.Value></Setter></Style>"));
  w.SourceInitialized += delegate { try { int corner=2; Native.DwmSetWindowAttribute(new WindowInteropHelper(w).Handle,33,ref corner,4); } catch {} };
 }
}
