using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Win32;
using System.Windows.Controls.Primitives;

partial class Wall {
    DateTime anchor=DateTime.Today;
    string scheduleTopic="";
    ScrollViewer plannerView;
    DateTime lastCheck=DateTime.MinValue;
    readonly HashSet<string> announced=new HashSet<string>();
    string trayState="";
    Window digest;
    const string RunKey=@"Software\Microsoft\Windows\CurrentVersion\Run";
    const string RunName="YuChuanNotesWall";

    void AddStartupSetting(ContextMenu menu) {
        string command="\""+System.Reflection.Assembly.GetExecutingAssembly().Location+"\" --tray";
        var item=new MenuItem{Header="登录 Windows 后在托盘启动",IsCheckable=true};
        using(var key=Registry.CurrentUser.OpenSubKey(RunKey)) item.IsChecked=key!=null && (string)key.GetValue(RunName)==command;
        item.Click+=delegate {
            try {
                using(var key=Registry.CurrentUser.CreateSubKey(RunKey)) {
                    if(item.IsChecked)key.SetValue(RunName,command);else key.DeleteValue(RunName,false);
                }
            } catch(Exception ex){MessageBox.Show(this,ex.Message,"启动设置");}
        };
        menu.Items.Add(item);
    }
    void PlannerNavigation() {
        foreach(var id in new[]{"$week","$month","$pending"}) {
            string label=id=="$week"?"本周看板":id=="$month"?"月历":"待查看提醒    "+Model.Due(Data,DateTime.UtcNow).Count;
            var button=UI.Button(label,delegate {
                if(!Current.StartsWith("$"))scheduleTopic=Current;
                anchor=DateTime.Today;Select(id);
            },false);
            button.BorderThickness=new Thickness(0);
            button.Background=Current==id?UI.B("#E7E7E4"):Brushes.Transparent;
            nav.Children.Add(button);
        }
    }
    void AddProgressAndMaterials(StackPanel panel,Note note) {
        if(note.Deleted)return;
        if(note.Steps.Count>0) {
            var next=note.Steps.FirstOrDefault(x=>!x.Done);
            var action=UI.Button(next==null && !note.Done?"完成整张便签":Planner.Progress(note),delegate {
                if(note.Done)return;
                if(next==null)note.Done=true;else next.Done=true;
                Save();RenderAll();
            },false);
            action.ToolTip=next==null?"确认整张任务完成":"点击勾选下一步";
            action.MinHeight=24;action.Padding=new Thickness(3);panel.Children.Add(action);
        }
        if(note.Materials.Count>0) {
            var button=UI.Button("资料入口  "+note.Materials.Count,delegate(object sender,RoutedEventArgs e){
                var menu=new ContextMenu();
                foreach(var link in note.Materials)AddMenu(menu,link.Name,delegate{DetailControls.Open(link);});
                ((Button)sender).ContextMenu=menu;menu.IsOpen=true;
            },false);
            button.MinHeight=22;button.Padding=new Thickness(3);panel.Children.Add(button);
        }
    }
    bool RenderPlanner() {
        bool active=Current=="$week" || Current=="$month" || Current=="$pending";
        if(plannerView!=null){main.Children.Remove(plannerView);plannerView=null;}
        foreach(UIElement child in main.Children)
            if(Grid.GetRow(child)>0)child.Visibility=active?Visibility.Collapsed:Visibility.Visible;
        if(!active)return false;
        var panel=new StackPanel();
        plannerView=new ScrollViewer{Content=panel,VerticalScrollBarVisibility=ScrollBarVisibility.Auto};
        Grid.SetRow(plannerView,3);main.Children.Add(plannerView);
        if(Current=="$pending"){RenderPending(panel);return true;}
        var all=Model.Visible(Data,"$all",Search.Text).Where(n=>n.DueUtc.HasValue);
        if(scheduleTopic!="" && Data.Topics.Any(t=>t.Id==scheduleTopic)){
            var ids=Model.Descendants(Data,scheduleTopic);all=all.Where(n=>ids.Contains(n.TopicId));
        }else scheduleTopic="";
        var notes=all.ToList();
        bool month=Current=="$month";
        Heading.Text=month?"月历":"周看板";
        var monday=Planner.Monday(anchor);
        Summary.Text=month?anchor.ToString("yyyy 年 M 月"):monday.ToString("yyyy/MM/dd")+" — "+monday.AddDays(6).ToString("MM/dd");
        var bar=new WrapPanel{Margin=new Thickness(0,0,0,12)};panel.Children.Add(bar);
        foreach(var delta in new[]{-1,0,1}){
            var label=delta==0?"今天":(delta<0?"上一":"下一")+(month?"月":"周");
            bar.Children.Add(UI.Button(label,delegate{anchor=delta==0?DateTime.Today:month?anchor.AddMonths(delta):anchor.AddDays(delta*7);RenderCards();},false));
        }
        var choices=new List<Choice>{new Choice("","全部主题")};choices.AddRange(WallUI.Choices(Data));
        var filter=WallUI.Combo(choices.Cast<object>(),0);WallUI.Choose(filter,scheduleTopic);
        filter.SelectionChanged+=delegate{scheduleTopic=WallUI.Selected(filter);RenderCards();};bar.Children.Add(filter);

        if(!month){
            var late=notes.Where(n=>!n.Done && n.DueUtc.Value.ToLocalTime().Date<DateTime.Today).OrderBy(n=>n.DueUtc).ToList();
            if(late.Count>0){
                panel.Children.Add(UI.Text("此前逾期 · "+late.Count,16,UI.Red));
                foreach(var note in late)panel.Children.Add(EventButton(note));
            }
            for(int day=0;day<7;day++){
                var date=monday.AddDays(day);var section=new StackPanel{Margin=new Thickness(8)};
                var block=new Border{Child=section,Background=date==DateTime.Today?UI.B("#EAF4F0"):Brushes.White,Margin=new Thickness(0,3,0,3)};
                panel.Children.Add(block);
                section.Children.Add(UI.Text(date.ToString("MM/dd")+"  "+"周"+"一二三四五六日"[day]+(date==DateTime.Today?" · 今天":""),15,UI.Ink));
                var events=Planner.Day(notes,date);
                if(events.Count==0)section.Children.Add(UI.Text("没有已安排的事项",12,UI.Muted));
                foreach(var note in events)section.Children.Add(EventButton(note));
            }
            return true;
        }
        var weekdays=new UniformGrid{Columns=7,Height=25};panel.Children.Add(weekdays);
        foreach(char day in "一二三四五六日")weekdays.Children.Add(UI.Text("周"+day,13,UI.Muted));
        var grid=new UniformGrid{Columns=7};panel.Children.Add(grid);
        var first=new DateTime(anchor.Year,anchor.Month,1);var start=Planner.Monday(first);
        for(var d=start; d<first.AddMonths(1) || d.DayOfWeek!=DayOfWeek.Monday;d=d.AddDays(1)){
            DateTime date=d;
            var cell=new StackPanel{Margin=new Thickness(4)};
            var box=new Border{Child=cell,Background=date==DateTime.Today?UI.B("#EAF4F0"):Brushes.White,BorderBrush=UI.Line,BorderThickness=new Thickness(.5),MinHeight=64};
            grid.Children.Add(box);
            cell.Children.Add(UI.Text(date.Day+(date==DateTime.Today?" 今天":""),13,date.Month==anchor.Month?UI.Ink:UI.Muted));
            var events=Planner.Day(notes,date);
            foreach(var note in events.Take(3))cell.Children.Add(EventButton(note,true));
            if(events.Count>3)cell.Children.Add(UI.Button("还有 "+(events.Count-3)+" 项",delegate{DayList(date,events);},false));
            box.MouseLeftButtonDown+=delegate(object sender,MouseButtonEventArgs e){if(e.ClickCount==2){e.Handled=true;EditNote(null,true,date);}};
        }
        return true;
    }
    Button EventButton(Note note,bool compact=false){
        string path=Model.TopicPath(Data,note.TopicId);
        string caption=note.DueUtc.Value.ToLocalTime().ToString("HH:mm")+"  "+note.Title+" · "+path;
        string progress=Planner.Progress(note);
        if(!compact && progress!="")caption+="  "+progress;
        var button=UI.Button(caption,delegate{EditNote(note,false);},false);
        button.Content=new TextBlock{Text=compact?note.Title:caption,TextTrimming=TextTrimming.CharacterEllipsis};
        if(compact){var label=new StackPanel();var name=(TextBlock)button.Content;button.Content=null;label.Children.Add(name);button.Content=label;label.Children.Add(new TextBlock{Text=path,FontSize=10,Foreground=UI.Muted,TextTrimming=TextTrimming.CharacterEllipsis});}
        button.Background=WallUI.Paper(Planner.Color(Data,note.TopicId,note.Color));button.BorderThickness=new Thickness(0);
        button.MinHeight=compact?22:30;button.Padding=new Thickness(4);button.Margin=new Thickness(0,2,2,1);
        button.ToolTip=caption;return button;
    }
    void DayList(DateTime day,List<Note> notes){
        var dialog=new Window{Owner=this,Title=day.ToString("yyyy/MM/dd"),Width=480,Height=420};
        UI.WindowStyle(dialog);var list=new StackPanel{Margin=new Thickness(16)};
        dialog.Content=new ScrollViewer{Content=list,VerticalScrollBarVisibility=ScrollBarVisibility.Auto};
        foreach(var note in notes){
            var button=UI.Button(note.DueUtc.Value.ToLocalTime().ToString("HH:mm")+"  "+note.Title+" · "+Model.TopicPath(Data,note.TopicId),delegate{dialog.Close();EditNote(note,false);},false);
            button.Background=WallUI.Paper(Planner.Color(Data,note.TopicId,note.Color));list.Children.Add(button);
        }
        dialog.ShowDialog();
    }
    internal FrameworkElement PlannerContent { get {return plannerView;} }
    void RenderPending(StackPanel panel){
        var now=DateTime.UtcNow;
        var notes=Model.Visible(Data,"$all",Search.Text).Where(n=>Planner.Pending(n,now).Any()).ToList();
        Heading.Text="待查看提醒";Summary.Text=notes.Count+" 件事项";
        if(notes.Count==0)panel.Children.Add(UI.Text("提醒都已处理。",16,UI.Muted));
        foreach(var n in notes){
            var group=new StackPanel{Margin=new Thickness(14)};
            panel.Children.Add(new Border{Background=WallUI.Paper(Planner.Color(Data,n.TopicId,n.Color)),Child=group,Margin=new Thickness(0,0,0,8)});
            group.Children.Add(UI.Text(n.Title+" · "+Model.TopicPath(Data,n.TopicId),16,UI.Ink));
            group.Children.Add(UI.Text("事项时间 "+(n.DueUtc.HasValue?n.DueUtc.Value.ToLocalTime().ToString("yyyy/MM/dd HH:mm"):"未设置"),12,UI.Muted));
            foreach(var alarm in Planner.Pending(n,now)){
                var row=new WrapPanel();group.Children.Add(row);
                var age=now-alarm.At;
                var elapsed=age.TotalHours>=1?(int)age.TotalHours+" 小时":(int)age.TotalMinutes+" 分钟";
                row.Children.Add(UI.Text(alarm.At.ToLocalTime().ToString("MM/dd HH:mm")+" · 已过 "+elapsed+"  ",13,UI.Muted));
                row.Children.Add(UI.Button("我知道了",delegate{alarm.Acknowledged=true;Planner.Sync(n);Save();RenderAll();},false));
                row.Children.Add(UI.Button("稍后 10 分钟",delegate{alarm.At=DateTime.UtcNow.AddMinutes(10);Planner.Sync(n);Save();RenderAll();},false));
            }
            group.Children.Add(UI.Button("完成整张便签",delegate{n.Done=true;Save();RenderAll();},false));
        }
    }
    internal void RefreshAlarms(bool resume=false){
        if(modal)return;
        DateTime now=DateTime.UtcNow;
        bool catchup=resume || !Testing && now-lastCheck>TimeSpan.FromSeconds(15);
        lastCheck=now;
        if(catchup)announced.Clear();
        var notes=Model.Due(Data,now);
        string state=string.Join("|",notes.SelectMany(n=>Planner.Pending(n,now).Select(a=>AlarmKey(n,a))).ToArray());
        if(state!=trayState){
            trayState=state;
            if(tray!=null){
                tray.Text=notes.Count==0?"便签墙":"便签墙 · "+notes.Count+" 件待查看";
                tray.Icon=notes.Count==0?normalIcon:System.Drawing.SystemIcons.Exclamation;
            }
            RenderTree();if(Current=="$pending")RenderCards();
        }
        var fresh=notes.SelectMany(n=>Planner.Pending(n,now).Select(a=>new{Note=n,Alarm=a,Key=AlarmKey(n,a)}))
            .Where(pair=>!announced.Contains(pair.Key)).ToList();
        if(fresh.Count==0)return;
        foreach(var pair in fresh)announced.Add(pair.Key);
        if(catchup || fresh.Count>1 || popups.ContainsKey(fresh[0].Note.Id)){
            if(digest!=null)digest.Close();
            digest=new Window{Title="便签墙提醒",Width=365,SizeToContent=SizeToContent.Height,Topmost=true,WindowStartupLocation=WindowStartupLocation.CenterScreen};
            UI.WindowStyle(digest);var content=new StackPanel{Margin=new Thickness(22)};digest.Content=content;
            content.Children.Add(UI.Text("你有 "+notes.Count+" 件事项的提醒待查看",19,UI.Ink));
            content.Children.Add(UI.Button("查看提醒",delegate{digest.Close();Reveal();Select("$pending");},true));
            digest.Closed+=delegate{digest=null;};digest.Show();
        }else{
            var pair=fresh[0];
            var popup=new WallReminder(pair.Note,pair.Alarm,Data,delegate{Save();RenderAll();});
            popups[pair.Note.Id]=popup;popup.Closed+=delegate{popups.Remove(pair.Note.Id);};
            popup.WindowStartupLocation=WindowStartupLocation.CenterScreen;popup.Show();
        }
        if(Data.Sound&&!Testing)System.Media.SystemSounds.Asterisk.Play();
    }
    static string AlarmKey(Note n,Alarm alarm){return n.Id+":"+alarm.Minutes+":"+alarm.At.Ticks;}
    System.Drawing.Icon normalIcon;
}

