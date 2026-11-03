using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using F=System.Windows.Forms;

static class DetailControls {
    public static ComboBox Palette(string selected, Action<string> changed) {
        var box = new ComboBox { MinHeight=32, MinWidth=104, MaxWidth=150 };
        box.Items.Add(new ComboBoxItem { Content="跟随主题", Tag="auto" });
        for (int i=0; i<Planner.Palette.Length; i++) {
            box.Items.Add(new ComboBoxItem { Content=Planner.Names[i], Tag=Planner.Palette[i], Background=UI.B(Planner.Hex[i]) });
        }
        box.SelectedItem=box.Items.Cast<ComboBoxItem>().FirstOrDefault(x=>(string)x.Tag==selected) ?? box.Items[0];
        box.SelectionChanged+=delegate { changed((string)((ComboBoxItem)box.SelectedItem).Tag); };
        return box;
    }
    static DataGrid GridFor(object rows) {
        return new DataGrid { ItemsSource=(System.Collections.IEnumerable)rows, Height=110,
            AutoGenerateColumns=false, CanUserAddRows=false, CanUserDeleteRows=false,
            CanUserSortColumns=false, SelectionMode=DataGridSelectionMode.Single, HeadersVisibility=DataGridHeadersVisibility.Column };
    }
    static DataGridTextColumn Field(string title,string property) {
        return new DataGridTextColumn { Header=title, Binding=new Binding(property){UpdateSourceTrigger=UpdateSourceTrigger.PropertyChanged},
            Width=new DataGridLength(1,DataGridLengthUnitType.Star) };
    }
    public static void Steps(StackPanel panel, ObservableCollection<Step> rows) {
        UI.Label(panel,"步骤清单");
        var grid=GridFor(rows);
        grid.Columns.Add(new DataGridCheckBoxColumn {Header="完成",Binding=new Binding("Done"){UpdateSourceTrigger=UpdateSourceTrigger.PropertyChanged}});
        grid.Columns.Add(Field("步骤名称","Name")); panel.Children.Add(grid);
        var bar=new WrapPanel(); panel.Children.Add(bar);
        bar.Children.Add(UI.Button("＋ 步骤",delegate {var step=new Step(); rows.Add(step); grid.SelectedItem=step; grid.CurrentCell=new DataGridCellInfo(step,grid.Columns[1]); grid.Focus(); grid.BeginEdit();},false));
        foreach (int delta in new[]{-1,1}) {
            bar.Children.Add(UI.Button(delta<0?"上移":"下移",delegate {
                int i=grid.SelectedIndex;
                if (i>=0 && i+delta>=0 && i+delta<rows.Count) rows.Move(i,i+delta);
            },false));
        }
        bar.Children.Add(UI.Button("移除",delegate {if(grid.SelectedIndex>=0)rows.RemoveAt(grid.SelectedIndex);},false));
    }
    public static void Materials(StackPanel panel, ObservableCollection<Material> rows) {
        UI.Label(panel,"资料入口");
        var grid=GridFor(rows); grid.Columns.Add(Field("名称","Name"));grid.Columns.Add(Field("网址或原路径","Target"));panel.Children.Add(grid);
        var bar=new WrapPanel();panel.Children.Add(bar);
        Action<string> add=target=>{var row=new Material{Name=target==""?"网址":Path.GetFileName(target),Target=target};rows.Add(row);grid.SelectedItem=row;};
        bar.Children.Add(UI.Button("＋ 网址",delegate{add("");grid.CurrentCell=new DataGridCellInfo(grid.SelectedItem,grid.Columns[1]);grid.Focus();grid.BeginEdit();},false));
        bar.Children.Add(UI.Button("选文件",delegate{var d=new Microsoft.Win32.OpenFileDialog();if(d.ShowDialog()==true)add(d.FileName);},false));
        bar.Children.Add(UI.Button("选文件夹",delegate{using(var d=new F.FolderBrowserDialog()){if(d.ShowDialog()==F.DialogResult.OK)add(d.SelectedPath);}},false));
        bar.Children.Add(UI.Button("打开",delegate{Open(grid.SelectedItem as Material);},false));
        bar.Children.Add(UI.Button("移除",delegate{if(grid.SelectedIndex>=0)rows.RemoveAt(grid.SelectedIndex);},false));
        panel.Children.Add(UI.Text("双击名称或路径可编辑，包括为移动过的原件重新定位。",12,UI.Muted));
    }
    public static bool ValidTarget(string target) {
        if(string.IsNullOrWhiteSpace(target))return false;
        Uri uri;
        if(Uri.TryCreate(target,UriKind.Absolute,out uri) && (uri.Scheme=="http" || uri.Scheme=="https"))return true;
        return Path.IsPathRooted(target) && (target.StartsWith(@"\\") || (target.Length>2 && target[1]==':' && (target[2]=='\\' || target[2]=='/')));
    }
    public static void Open(Material entry,Action<string> launch=null) {
        if(entry==null)return;
        try {
            var path=entry.Target;
            if(!ValidTarget(path))throw new IOException("请填写完整网址或本地路径。");
            if(Path.IsPathRooted(path) && !File.Exists(path) && !Directory.Exists(path))throw new IOException("找不到原件，请在便签编辑器中更新路径。");
            if(launch==null)Process.Start(new ProcessStartInfo(path){UseShellExecute=true});else launch(path);
        } catch(Exception e){MessageBox.Show(e.Message,"打开资料");}
    }
}

