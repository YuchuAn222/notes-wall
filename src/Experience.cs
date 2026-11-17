using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

partial class Wall {
    public ComboBox SortBox, TypeFilter;
    public CheckBox OpenOnly;
    bool fitting;
    DateTime renderedDay=DateTime.Today;

    FrameworkElement Brand() {
        var row=new StackPanel{Orientation=Orientation.Horizontal};
        var mark=new Grid{Width=28,Height=30,Margin=new Thickness(0,0,10,0)};
        mark.Children.Add(new Border{Background=UI.B("#CADBD1"),CornerRadius=new CornerRadius(3),Margin=new Thickness(0,0,6,6)});
        mark.Children.Add(new Border{Background=UI.B("#E7CE82"),CornerRadius=new CornerRadius(3),Margin=new Thickness(6,6,0,0)});
        row.Children.Add(mark);
        var name=UI.Text("便签墙",23,UI.Ink);name.FontWeight=FontWeights.SemiBold;row.Children.Add(name);
        return row;
    }
    Button NavButton(string id,string name,int count,Action action=null) {
        var button=UI.Button(name,delegate{if(action==null)Select(id);else action();},false);
        button.HorizontalContentAlignment=HorizontalAlignment.Stretch;
        button.BorderThickness=new Thickness(0);button.MinHeight=37;
        button.Background=Current==id?UI.B("#E5E7E1"):Brushes.Transparent;
        button.Margin=new Thickness(0,0,0,3);
        var row=new Grid();row.ColumnDefinitions.Add(new ColumnDefinition());row.ColumnDefinitions.Add(new ColumnDefinition{Width=GridLength.Auto});
        var text=UI.Text(name,13,Current==id?UI.Ink:UI.Muted);
        text.FontWeight=Current==id?FontWeights.SemiBold:FontWeights.Normal;row.Children.Add(text);
        if(count>=0){var number=UI.Text(count.ToString(),12,UI.Muted);number.Margin=new Thickness(8,0,0,0);Grid.SetColumn(number,1);row.Children.Add(number);}
        button.Content=row;button.ToolTip=name;AutomationProperties.SetName(button,name);return button;
    }
    void BuildWallTools() {
        Loaded+=delegate{ResizeShell();};SizeChanged+=delegate{ResizeShell();};
        var bar=new WrapPanel{Margin=new Thickness(0,0,0,16)};Grid.SetRow(bar,2);main.Children.Add(bar);
        OpenOnly=new CheckBox{Content="只看未完成",IsChecked=Data.OnlyOpen,VerticalAlignment=VerticalAlignment.Center,Margin=new Thickness(0,0,16,0)};
        OpenOnly.Click+=delegate{Data.OnlyOpen=OpenOnly.IsChecked==true;Save();RenderCards();};bar.Children.Add(OpenOnly);
        TypeFilter=WallUI.Combo(new[]{"全部类型"}.Concat(Model.Kinds).Cast<object>(),0);TypeFilter.SelectedItem=Model.Kinds.Contains(Data.KindFilter)?Data.KindFilter:"全部类型";
        TypeFilter.MinWidth=90;TypeFilter.Margin=new Thickness(0,0,8,0);bar.Children.Add(TypeFilter);AutomationProperties.SetName(TypeFilter,"筛选便签类型");
        TypeFilter.SelectionChanged+=delegate{Data.KindFilter=TypeFilter.SelectedItem as string??"全部类型";Save();RenderCards();};
        SortBox=WallUI.Combo(new object[]{new Choice("smart","置顶优先"),new Choice("due","截止时间优先"),new Choice("new","最新创建优先")},0);
        WallUI.Choose(SortBox,Data.SortOrder);SortBox.MinWidth=118;SortBox.Margin=new Thickness(0,0,8,0);bar.Children.Add(SortBox);AutomationProperties.SetName(SortBox,"便签排序");
        SortBox.SelectionChanged+=delegate{Data.SortOrder=WallUI.Selected(SortBox);Save();RenderCards();};
        bar.Children.Add(UI.Button("＋ 详细添加",delegate{EditNote(null,false);},false));
        PreviewKeyDown+=delegate(object s,KeyEventArgs e){
            if(e.Key==Key.Escape && Search.IsKeyboardFocusWithin){Search.Clear();e.Handled=true;}
            if(Keyboard.Modifiers==ModifierKeys.Control){
                var keys=new[]{Key.D1,Key.D2,Key.D3,Key.D4};int i=Array.IndexOf(keys,e.Key);
                if(i>=0){Select(new[]{"$today","$all","$week","$month"}[i]);e.Handled=true;}
            }
        };
    }
    void ResizeShell(){bool compact=ActualWidth<1000;root.ColumnDefinitions[0].Width=new GridLength(compact?176:224);main.Margin=new Thickness(compact?16:24,24,compact?16:24,18);}
    void RefreshCalendarDay(){if(renderedDay!=DateTime.Today){renderedDay=DateTime.Today;RenderAll();}}
    void UpdateWallTools(){OpenOnly.IsEnabled=Current!="$done"&&Current!="$trash"&&Current!="$today";foreach(UIElement child in main.Children)if(Grid.GetRow(child)==1)child.Visibility=Current=="$today"?Visibility.Collapsed:Visibility.Visible;}
    internal List<Note> TodayNotes(string search) {
        return Model.Visible(Data,"$all",search).Where(n=>!n.Done && (n.Pinned || n.DueUtc.HasValue && n.DueUtc.Value.ToLocalTime().Date<=DateTime.Today)).ToList();
    }
    internal List<Note> WallNotes() {
        IEnumerable<Note> notes=Current=="$today"?TodayNotes(Search.Text):Model.Visible(Data,Current,Search.Text);
        if(Data.OnlyOpen && Current!="$done" && Current!="$trash")notes=notes.Where(n=>!n.Done);
        if(Model.Kinds.Contains(Data.KindFilter))notes=notes.Where(n=>n.Kind==Data.KindFilter);
        if(Data.SortOrder=="due")notes=notes.OrderBy(n=>n.Done).ThenBy(n=>n.DueUtc??DateTime.MaxValue).ThenByDescending(n=>n.Pinned).ThenByDescending(n=>n.CreatedUtc);
        else if(Data.SortOrder=="new")notes=notes.OrderByDescending(n=>n.CreatedUtc);
        return notes.ToList();
    }
    void Duplicate(Note note) {
        var copy=note.Clone();copy.Id=Guid.NewGuid().ToString("N");copy.Title+="（副本）";
        copy.CreatedUtc=DateTime.UtcNow;copy.Done=false;copy.Deleted=false;copy.Pinned=false;
        foreach(var step in copy.Steps)step.Done=false;
        // A copied assignment must not replay the old task's alarms.
        copy.DueUtc=null;copy.ReminderOn=false;copy.Alarms.Clear();copy.RemindUtc=null;copy.Notified=false;
        Data.Notes.Add(copy);Current=copy.TopicId;Save();RenderAll();
        Status.Text="已复制 · 日期和提醒待重新安排";
    }
    internal static string DueLabel(Note n) {
        if(!n.DueUtc.HasValue)return "";
        var local=n.DueUtc.Value.ToLocalTime();int days=(local.Date-DateTime.Today).Days;
        string day=days==0?"今天":days==1?"明天":local.ToString("M/d");
        return (!n.Done&&n.DueUtc.Value<DateTime.UtcNow?"已逾期 · ":"")+day+" "+local.ToString("HH:mm");
    }
    FrameworkElement PaperCard(Note n) {
        var color=WallUI.Paper(Planner.Color(Data,n.TopicId,n.Color));
        var paper=new Border{Background=color,CornerRadius=new CornerRadius(16),Margin=new Thickness(0,0,16,16),Padding=new Thickness(18,16,18,14),MinHeight=238,Tag=n.Id};
        var layout=new DockPanel{LastChildFill=true};paper.Child=layout;
        var top=new Grid{Margin=new Thickness(0,0,0,12)};top.ColumnDefinitions.Add(new ColumnDefinition());top.ColumnDefinitions.Add(new ColumnDefinition{Width=GridLength.Auto});
        var topic=UI.Text(n.Kind+" · "+Model.TopicPath(Data,n.TopicId),11,UI.Muted);topic.TextWrapping=TextWrapping.NoWrap;topic.TextTrimming=TextTrimming.CharacterEllipsis;topic.ToolTip=Model.TopicPath(Data,n.TopicId);topic.VerticalAlignment=VerticalAlignment.Center;top.Children.Add(topic);
        var menu=UI.Button("···",delegate(object s,RoutedEventArgs e){var b=(Button)s;b.ContextMenu=CardMenu(n);b.ContextMenu.IsOpen=true;},false);
        menu.MinHeight=24;menu.Padding=new Thickness(5,0,5,0);menu.Background=Brushes.Transparent;menu.BorderThickness=new Thickness(0);menu.ToolTip="置顶、复制、移动或删除";AutomationProperties.SetName(menu,"更多操作："+n.Title);Grid.SetColumn(menu,1);top.Children.Add(menu);
        DockPanel.SetDock(top,Dock.Top);layout.Children.Add(top);
        Point start=new Point();bool dragging=false;
        topic.PreviewMouseLeftButtonDown+=delegate(object sender,MouseButtonEventArgs e){start=e.GetPosition(topic);dragging=true;};
        topic.MouseMove+=delegate(object sender,MouseEventArgs e){if(dragging&&e.LeftButton==MouseButtonState.Pressed&&(e.GetPosition(topic)-start).Length>8){dragging=false;DragDrop.DoDragDrop(paper,new DataObject("NotesWall.Note",n.Id),DragDropEffects.Move);}};
        var footer=new Grid{Margin=new Thickness(0,14,0,0)};footer.ColumnDefinitions.Add(new ColumnDefinition());footer.ColumnDefinitions.Add(new ColumnDefinition{Width=GridLength.Auto});
        // The task checkbox is separate from the checklist: checking all steps never silently completes it.
        if(!n.Deleted){var done=new CheckBox{Content=n.Done?"已完成":"完成这件事",IsChecked=n.Done,FontSize=12,VerticalAlignment=VerticalAlignment.Center};AutomationProperties.SetName(done,"完成："+n.Title);done.Click+=delegate{n.Done=done.IsChecked==true;if(popups.ContainsKey(n.Id))popups[n.Id].Close();Save();RenderAll();};footer.Children.Add(done);}
        var edit=UI.Button(n.Deleted?"恢复":"编辑",delegate{if(n.Deleted){n.Deleted=false;Save();RenderAll();}else EditNote(n,false);},false);edit.Background=Brushes.Transparent;edit.BorderThickness=new Thickness(0);edit.Padding=new Thickness(6,2,6,2);edit.MinHeight=26;Grid.SetColumn(edit,1);footer.Children.Add(edit);
        DockPanel.SetDock(footer,Dock.Bottom);layout.Children.Add(footer);
        var body=new StackPanel();layout.Children.Add(body);
        var title=UI.Text((n.Pinned?"置顶 · ":"")+n.Title,18,UI.Ink);title.FontWeight=FontWeights.SemiBold;title.MaxHeight=52;title.TextTrimming=TextTrimming.CharacterEllipsis;
        if(n.Done)title.TextDecorations=TextDecorations.Strikethrough;
        var open=UI.Button(n.Title,delegate{if(!n.Deleted)EditNote(n,false);},false);open.Content=title;open.Background=Brushes.Transparent;open.BorderThickness=new Thickness(0);open.Padding=new Thickness(0);open.HorizontalContentAlignment=HorizontalAlignment.Stretch;open.MinHeight=25;body.Children.Add(open);
        if(n.Body!=""){var content=UI.Text(n.Body,13,UI.Ink);content.MaxHeight=42;content.Margin=new Thickness(0,7,0,0);content.TextTrimming=TextTrimming.CharacterEllipsis;content.Cursor=Cursors.Hand;content.MouseLeftButtonUp+=delegate{if(!n.Deleted)EditNote(n,false);};body.Children.Add(content);}
        if(n.DueUtc.HasValue){var due=UI.Text(DueLabel(n),12,!n.Done&&n.DueUtc<DateTime.UtcNow?UI.Red:UI.Ink);due.Margin=new Thickness(0,12,0,0);body.Children.Add(due);}
        if(n.ReminderOn&&!n.Done){var count=n.Alarms.Count(a=>!a.Acknowledged);var reminder=UI.Text(count==0?"提醒已处理":"待提醒 "+count+" 次",11,UI.Muted);reminder.Margin=new Thickness(0,4,0,0);body.Children.Add(reminder);}
        AddProgressAndMaterials(body,n);
        return paper;
    }
    void AddProgressAndMaterials(StackPanel panel,Note note) {
        if(note.Deleted)return;
        if(note.Steps.Count>0){
            int done=note.Steps.Count(s=>s.Done);
            var heading=UI.Button("步骤  "+done+" / "+note.Steps.Count,delegate{EditNote(note,false,null,2);},false);heading.HorizontalContentAlignment=HorizontalAlignment.Left;heading.Background=Brushes.Transparent;heading.BorderThickness=new Thickness(0);heading.Padding=new Thickness(0);heading.MinHeight=28;heading.Margin=new Thickness(0,9,0,2);heading.ToolTip=Planner.Progress(note)+" · 点击编辑全部步骤";panel.Children.Add(heading);
            var track=new Grid{Height=3,Background=UI.B("#20000000"),Margin=new Thickness(0,0,0,8)};
            track.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(done,GridUnitType.Star)});track.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(note.Steps.Count-done,GridUnitType.Star)});
            if(done>0)track.Children.Add(new Border{Background=UI.Blue});panel.Children.Add(track);
            int next=note.Steps.FindIndex(s=>!s.Done);int skip=Math.Max(0,next-1);
            foreach(var step in note.Steps.Skip(skip).Take(3)){
                var text=UI.Text(step.Name,12,UI.Ink);text.TextWrapping=TextWrapping.NoWrap;text.TextTrimming=TextTrimming.CharacterEllipsis;if(step.Done)text.TextDecorations=TextDecorations.Strikethrough;
                var box=new CheckBox{Content=text,IsChecked=step.Done,Margin=new Thickness(0,2,0,3),IsEnabled=!note.Done,ToolTip=step.Name,HorizontalContentAlignment=HorizontalAlignment.Stretch};
                AutomationProperties.SetName(box,"步骤："+step.Name);box.Click+=delegate{step.Done=box.IsChecked==true;Save();RenderAll();};panel.Children.Add(box);
            }
            if(done==note.Steps.Count&&!note.Done){var finish=UI.Button("完成整张便签",delegate{note.Done=true;Save();RenderAll();},false);finish.Margin=new Thickness(0,6,0,0);panel.Children.Add(finish);}
        }
        if(note.Materials.Count>0){
            var row=new WrapPanel{Margin=new Thickness(0,10,0,0)};panel.Children.Add(row);
            foreach(var entry in note.Materials.Take(2)){
                var button=UI.Button(entry.Name,delegate{DetailControls.Open(entry);},false);button.Content=new TextBlock{Text="↗ "+entry.Name,TextTrimming=TextTrimming.CharacterEllipsis};button.MaxWidth=124;button.FontSize=12;button.MinHeight=28;button.Padding=new Thickness(7,3,7,3);button.Margin=new Thickness(0,0,5,4);button.Background=UI.B("#90FFFFFF");button.BorderThickness=new Thickness(0);button.ToolTip=entry.Target;row.Children.Add(button);
            }
            if(note.Materials.Count>2)row.Children.Add(UI.Button("全部资料",delegate{EditNote(note,false,null,2);},false));
        }
    }
    void FitPaperCards() {
        if(fitting||cardScroll==null||Cards==null||Cards.Children.Count==0)return;
        fitting=true;try{
            double available=Math.Max(250,cardScroll.ActualWidth-18);int columns=Math.Max(1,(int)(available/254));
            double width=Math.Floor(available/columns);Cards.ItemWidth=width;
            var cards=Cards.Children.OfType<Border>().ToList();
            for(int i=0;i<cards.Count;i+=columns){
                var row=cards.Skip(i).Take(columns).ToList();double height=0;
                foreach(var card in row){card.Height=double.NaN;card.Measure(new Size(width,double.PositiveInfinity));height=Math.Max(height,card.DesiredSize.Height-card.Margin.Top-card.Margin.Bottom);}
                foreach(var card in row)card.Height=height;
            }
        }finally{fitting=false;}
    }
}

partial class NoteEditor {
    public TabControl Tabs;
    void ArrangeEditor(StackPanel old,TextBlock error,TextBlock hint) {
        var items=old.Children.Cast<UIElement>().ToList();int date=items.IndexOf(HasDate);
        int steps=items.FindIndex(x=>x is TextBlock && ((TextBlock)x).Text=="步骤清单");int footer=items.IndexOf(hint);
        old.Children.Clear();Content=null;
        var frame=new DockPanel{Margin=new Thickness(24)};Content=frame;
        var head=UI.Text(Title,24,UI.Ink);head.FontWeight=FontWeights.SemiBold;head.Margin=new Thickness(0,0,0,18);DockPanel.SetDock(head,Dock.Top);frame.Children.Add(head);
        var actions=new StackPanel{Margin=new Thickness(0,12,0,0)};DockPanel.SetDock(actions,Dock.Bottom);frame.Children.Add(actions);actions.Children.Add(error);
        var bar=new Grid();bar.ColumnDefinitions.Add(new ColumnDefinition());bar.ColumnDefinitions.Add(new ColumnDefinition{Width=GridLength.Auto});bar.ColumnDefinitions.Add(new ColumnDefinition{Width=GridLength.Auto});
        var shortcut=UI.Text("Ctrl+Enter 保存 · Esc 取消",11,UI.Muted);shortcut.VerticalAlignment=VerticalAlignment.Center;bar.Children.Add(shortcut);
        var cancel=UI.Button("取消",delegate{Close();},false);cancel.Margin=new Thickness(8,0,8,0);Grid.SetColumn(cancel,1);bar.Children.Add(cancel);SaveButton.Margin=new Thickness(0);Grid.SetColumn(SaveButton,2);bar.Children.Add(SaveButton);actions.Children.Add(bar);
        Tabs=new TabControl{Background=Brushes.Transparent,BorderThickness=new Thickness(0)};frame.Children.Add(Tabs);
        var names=new[]{"内容","时间与提醒","步骤与资料"};var panels=new StackPanel[3];
        for(int i=0;i<3;i++){panels[i]=new StackPanel{Margin=new Thickness(0,4,4,12)};Tabs.Items.Add(new TabItem{Header=names[i],Padding=new Thickness(16,9,16,9),Content=new ScrollViewer{Content=panels[i],VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled}});}
        for(int i=1;i<date;i++)panels[0].Children.Add(items[i]);
        for(int i=date;i<steps;i++)panels[1].Children.Add(items[i]);
        for(int i=steps;i<footer;i++)panels[2].Children.Add(items[i]);
        hint.Text="关机期间错过的提醒，会在下次启动时汇总。";panels[1].Children.Add(hint);
        var days=new WrapPanel{Margin=new Thickness(0,10,0,0)};panels[1].Children.Insert(1,days);
        foreach(var offset in new[]{0,1,7}){int value=offset;var b=UI.Button(offset==0?"今天":offset==1?"明天":"一周后",delegate{HasDate.IsChecked=true;DateBox.SelectedDate=DateTime.Today.AddDays(value);},false);b.Margin=new Thickness(0,0,8,8);days.Children.Add(b);}
        HasDate.Checked+=delegate{Tabs.SelectedIndex=1;};
        SaveButton.Click+=delegate{if(Result==null){if(string.IsNullOrWhiteSpace(TitleBox.Text))Tabs.SelectedIndex=0;else if(error.Text.Contains("日期"))Tabs.SelectedIndex=1;else Tabs.SelectedIndex=2;}};
    }
}
