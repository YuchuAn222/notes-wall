using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using F=System.Windows.Forms;

class Choice {
 public string Id,Name;public override string ToString(){return Name;}
 public Choice(string id,string name){Id=id;Name=name;}
}
static class WallUI {
 public static string[] ColorNames={"鹅黄","浅粉","雾蓝","薄荷","淡紫"};
 public static Brush Paper(string color){switch(color){case "pink":return UI.B("#F9E1E8");case "blue":return UI.B("#DFEBFA");case "mint":return UI.B("#DDEEE4");case "purple":return UI.B("#E8E0F5");default:return UI.B("#FFF0B8");}}
 public static ComboBox Combo(IEnumerable<object> options,int index){var c=new ComboBox{FontSize=13,MinHeight=34,Padding=new Thickness(6,3,6,3),VerticalContentAlignment=VerticalAlignment.Center};foreach(var x in options)c.Items.Add(x);c.SelectedIndex=index;return c;}
 public static List<Choice> Choices(Notebook n){return n.Topics.OrderBy(t=>Model.TopicPath(n,t.Id)).Select(t=>new Choice(t.Id,Model.TopicPath(n,t.Id))).ToList();}
 public static void Choose(ComboBox box,string id){foreach(var x in box.Items.OfType<Choice>())if(x.Id==id){box.SelectedItem=x;return;}if(box.Items.Count>0)box.SelectedIndex=0;}
 public static string Selected(ComboBox box){return box.SelectedItem is Choice?((Choice)box.SelectedItem).Id:"inbox";}
 public static void Space(StackPanel p,int height){p.Children.Add(new Border{Height=height});}
}
class NoteEditor:Window {
 public TextBox TitleBox,BodyBox,TimeBox;public ComboBox TopicBox,KindBox,LeadBox;public CheckBox HasDate,Remind;public DatePicker DateBox;public Button SaveButton;public Note Result;public string Color;
 public NoteEditor(Notebook data,Note original,string topic,string seed,string kind,string color){
  UI.WindowStyle(this);Title=original==null?"新建便签":"编辑便签";Width=520;Height=750;MinWidth=410;MinHeight=540;MaxHeight=Math.Max(540,SystemParameters.WorkArea.Height-40);ShowInTaskbar=false;Color=original==null?color:original.Color;
  var scroll=new ScrollViewer{VerticalScrollBarVisibility=ScrollBarVisibility.Auto};Content=scroll;var p=new StackPanel{Margin=new Thickness(26,18,26,24)};scroll.Content=p;
  p.Children.Add(UI.Text(original==null?"写一张便签":"编辑便签",24,UI.Ink));
  UI.Label(p,"主题");TopicBox=WallUI.Combo(WallUI.Choices(data).Cast<object>(),0);WallUI.Choose(TopicBox,original==null?topic:original.TopicId);AutomationProperties.SetName(TopicBox,"便签所属主题");p.Children.Add(TopicBox);
  UI.Label(p,"标题");TitleBox=UI.Input(original==null?seed:original.Title,false);TitleBox.MaxLength=500;AutomationProperties.SetName(TitleBox,"便签标题");p.Children.Add(TitleBox);
  UI.Label(p,"内容");BodyBox=UI.Input(original==null?"":original.Body,true);BodyBox.Height=132;AutomationProperties.SetName(BodyBox,"便签内容");p.Children.Add(BodyBox);
  var line=new Grid{Margin=new Thickness(0,16,0,0)};line.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(115)});line.ColumnDefinitions.Add(new ColumnDefinition());
  KindBox=WallUI.Combo(Model.Kinds.Cast<object>(),Array.IndexOf(Model.Kinds,original==null?kind:original.Kind));line.Children.Add(KindBox);AutomationProperties.SetName(KindBox,"便签类型");
  var colors=new StackPanel{Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Right};Grid.SetColumn(colors,1);line.Children.Add(colors);p.Children.Add(line);
  Action renderColors=null;renderColors=delegate{colors.Children.Clear();for(int j=0;j<Model.Colors.Length;j++){string c=Model.Colors[j];var b=UI.Button("",delegate{Color=c;renderColors();},false);b.Background=WallUI.Paper(c);b.Width=29;b.Height=29;b.MinHeight=29;b.Padding=new Thickness(0);b.Margin=new Thickness(5,0,0,0);b.BorderBrush=c==Color?UI.Ink:UI.Line;b.BorderThickness=new Thickness(c==Color?2:1);b.ToolTip=WallUI.ColorNames[j];AutomationProperties.SetName(b,WallUI.ColorNames[j]+"便签");colors.Children.Add(b);}};renderColors();
  HasDate=new CheckBox{Content="记录日期 / 截止时间",Margin=new Thickness(0,20,0,9),IsChecked=original!=null&&original.DueUtc.HasValue};p.Children.Add(HasDate);
  var due=original!=null&&original.DueUtc.HasValue?original.DueUtc.Value.ToLocalTime():DateTime.Today.AddDays(1).AddHours(18);
  var dates=new Grid();dates.ColumnDefinitions.Add(new ColumnDefinition());dates.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(96)});
  DateBox=new DatePicker{SelectedDate=due.Date,SelectedDateFormat=DatePickerFormat.Short,MinHeight=36,Margin=new Thickness(0,0,8,0)};dates.Children.Add(DateBox);AutomationProperties.SetName(DateBox,"事项日期");
  TimeBox=UI.Input(due.ToString("HH:mm"),false);TimeBox.Padding=new Thickness(7);Grid.SetColumn(TimeBox,1);dates.Children.Add(TimeBox);AutomationProperties.SetName(TimeBox,"事项时间，24小时制");p.Children.Add(dates);
  var reminderRow=new Grid{Margin=new Thickness(0,12,0,0)};reminderRow.ColumnDefinitions.Add(new ColumnDefinition());reminderRow.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(150)});
  Remind=new CheckBox{Content="开启提醒",VerticalAlignment=VerticalAlignment.Center,IsChecked=original!=null&&original.ReminderOn};reminderRow.Children.Add(Remind);
  var leads=new[]{new Choice("0","到时间提醒"),new Choice("10","提前 10 分钟"),new Choice("60","提前 1 小时"),new Choice("1440","提前 1 天")};LeadBox=WallUI.Combo(leads.Cast<object>(),0);
  if(original!=null&&original.DueUtc.HasValue&&original.RemindUtc.HasValue){int lead=(int)Math.Round((original.DueUtc.Value-original.RemindUtc.Value).TotalMinutes);var match=leads.FirstOrDefault(x=>x.Id==lead.ToString());if(match!=null)LeadBox.SelectedItem=match;}
  Grid.SetColumn(LeadBox,1);reminderRow.Children.Add(LeadBox);p.Children.Add(reminderRow);
  Action enabled=delegate{dates.IsEnabled=HasDate.IsChecked==true;reminderRow.IsEnabled=HasDate.IsChecked==true;LeadBox.IsEnabled=Remind.IsChecked==true;};HasDate.Checked+=delegate{enabled();};HasDate.Unchecked+=delegate{enabled();};Remind.Checked+=delegate{enabled();};Remind.Unchecked+=delegate{enabled();};enabled();
  var hint=UI.Text("使用本机时间；提醒运行时需要电脑保持开机。",12,UI.Muted);hint.Margin=new Thickness(0,10,0,0);p.Children.Add(hint);
  var error=UI.Text("",12,UI.Red);error.Margin=new Thickness(0,10,0,0);p.Children.Add(error);TitleBox.TextChanged+=delegate{error.Text="";};TimeBox.TextChanged+=delegate{error.Text="";};DateBox.SelectedDateChanged+=delegate{error.Text="";};
  SaveButton=UI.Button("保存便签",delegate{
   if(string.IsNullOrWhiteSpace(TitleBox.Text)){error.Text="请写下便签标题。";TitleBox.Focus();return;}
   DateTime? date=null,remind=null;bool remindOn=HasDate.IsChecked==true&&Remind.IsChecked==true;
   if(HasDate.IsChecked==true){DateTime time;if(!DateBox.SelectedDate.HasValue||!DateTime.TryParseExact(TimeBox.Text.Trim(),new[]{"H:mm","HH:mm"},CultureInfo.InvariantCulture,DateTimeStyles.None,out time)){error.Text="请检查日期和时间，例如 18:30。";return;}date=DateBox.SelectedDate.Value.Date.Add(time.TimeOfDay).ToUniversalTime();if(remindOn){remind=date.Value.AddMinutes(-int.Parse(WallUI.Selected(LeadBox)));bool unchanged=original!=null&&original.ReminderOn&&original.DueUtc==date&&original.RemindUtc==remind;if(remind<=DateTime.UtcNow&&!unchanged){error.Text="提醒时间已经过去，可以取消提醒或选择未来的时间。";return;}}}
   Result=original==null?new Note():original.Clone();Result.Title=TitleBox.Text.Trim();Result.Body=BodyBox.Text.Trim();Result.TopicId=WallUI.Selected(TopicBox);Result.Kind=KindBox.SelectedItem.ToString();Result.Color=Color;Result.DueUtc=date;Result.ReminderOn=remindOn;
   if(original!=null&&original.DueUtc==date&&original.ReminderOn==remindOn&&original.RemindUtc.HasValue&&remindOn&&original.RemindUtc.Value>original.DueUtc.Value){Result.RemindUtc=original.RemindUtc;}else Result.RemindUtc=remind;
   if(original==null||original.RemindUtc!=Result.RemindUtc||original.ReminderOn!=remindOn)Result.Notified=false;
   DialogResult=true;
  },true);SaveButton.Margin=new Thickness(0,6,0,0);p.Children.Add(SaveButton);
  Loaded+=delegate{TitleBox.Focus();};PreviewKeyDown+=delegate(object s,KeyEventArgs e){if(e.Key==Key.Escape)Close();if(e.Key==Key.Enter&&(Keyboard.Modifiers&ModifierKeys.Control)!=0){SaveButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));e.Handled=true;}};
 }
}
class TopicEditor:Window {
 public TextBox NameBox;public ComboBox ParentBox;public Button SaveButton;public string TopicName,ParentId;
 public TopicEditor(Notebook n,Topic existing,string parent){
  UI.WindowStyle(this);Title=existing==null?"新建主题":"重命名主题";Width=400;SizeToContent=SizeToContent.Height;ResizeMode=ResizeMode.NoResize;ShowInTaskbar=false;
  var p=new StackPanel{Margin=new Thickness(24)};Content=p;p.Children.Add(UI.Text(Title,22,UI.Ink));UI.Label(p,"主题名称");NameBox=UI.Input(existing==null?"":existing.Name,false);NameBox.MaxLength=60;p.Children.Add(NameBox);AutomationProperties.SetName(NameBox,"主题名称");
  UI.Label(p,"上级主题");var choices=new List<Choice>{new Choice("","顶层主题")};choices.AddRange(WallUI.Choices(n));ParentBox=WallUI.Combo(choices.Cast<object>(),0);WallUI.Choose(ParentBox,existing==null?parent:existing.ParentId);ParentBox.IsEnabled=existing==null;p.Children.Add(ParentBox);
  var error=UI.Text("",12,UI.Red);error.Margin=new Thickness(0,10,0,0);p.Children.Add(error);
  SaveButton=UI.Button("保存主题",delegate{string value=NameBox.Text.Trim();string pid=WallUI.Selected(ParentBox);if(value==""){error.Text="写一个主题名称，例如“机器学习”。";return;}if(n.Topics.Any(t=>t.Id!=(existing==null?"":existing.Id)&&t.ParentId==pid&&t.Name==value)){error.Text="同一层已经有这个名称。";return;}TopicName=value;ParentId=pid;DialogResult=true;},true);SaveButton.Margin=new Thickness(0,12,0,0);p.Children.Add(SaveButton);Loaded+=delegate{NameBox.Focus();};
 }
}
class WallReminder:Window {
 public Note Note;public Button SnoozeButton,DoneButton;
 public WallReminder(Note n,Notebook data,Action save){
  Note=n;UI.WindowStyle(this);Title="便签墙提醒";Width=380;SizeToContent=SizeToContent.Height;ResizeMode=ResizeMode.NoResize;Topmost=true;ShowInTaskbar=true;
  var p=new StackPanel{Margin=new Thickness(26)};Content=p;p.Children.Add(UI.Text(n.Kind+"提醒",18,UI.Ink));var title=UI.Text(n.Title,22,UI.Ink);title.MaxHeight=140;title.Margin=new Thickness(0,16,0,8);p.Children.Add(title);
  p.Children.Add(UI.Text(Model.TopicPath(data,n.TopicId),12,UI.Muted));if(n.Body!=""){var body=UI.Text(n.Body,14,UI.Muted);body.MaxHeight=100;body.Margin=new Thickness(0,12,0,0);p.Children.Add(body);}
  var date=UI.Text(n.DueUtc.HasValue?"事项时间 · "+n.DueUtc.Value.ToLocalTime().ToString("MM/dd HH:mm"):"提醒已到时间",13,UI.Muted);date.Margin=new Thickness(0,16,0,16);p.Children.Add(date);
  var buttons=new Grid();buttons.ColumnDefinitions.Add(new ColumnDefinition());buttons.ColumnDefinitions.Add(new ColumnDefinition());
  SnoozeButton=UI.Button("10 分钟后提醒",delegate{Model.Snooze(n,DateTime.UtcNow);save();Close();},false);SnoozeButton.Margin=new Thickness(0,0,8,0);buttons.Children.Add(SnoozeButton);
  DoneButton=UI.Button("标记完成",delegate{n.Done=true;save();Close();},true);Grid.SetColumn(DoneButton,1);buttons.Children.Add(DoneButton);p.Children.Add(buttons);
 }
}
class Wall:Window {
 public Notebook Data;public Store Storage;public TextBox Quick,Search;public ComboBox QuickKind,QuickTopic;public Button AddButton;public WrapPanel Cards;public TreeView Tree;
 public string Current="$all",CurrentColor="yellow";public TextBlock Status,Heading,Summary;public bool Testing;
 Grid root,main;StackPanel nav;ScrollViewer cardScroll;DispatcherTimer ticker,draftTimer;F.NotifyIcon tray;EventWaitHandle showSignal,exitSignal;bool quitting,buildingTree,loaded,modal;Dictionary<string,WallReminder> popups=new Dictionary<string,WallReminder>();
 public Wall(Store store,bool test){
  Storage=store;Data=store.Load();Testing=test;Current=Data.SelectedTopic;if(!Current.StartsWith("$")&&!Data.Topics.Any(x=>x.Id==Current))Current="$all";
  UI.WindowStyle(this);Title="便签墙";Width=Math.Min(Math.Max(820,Data.Width),SystemParameters.WorkArea.Width-32);Height=Math.Min(Math.Max(600,Data.Height),SystemParameters.WorkArea.Height-32);MinWidth=780;MinHeight=560;WindowStartupLocation=WindowStartupLocation.CenterScreen;Background=Brushes.White;
  root=new Grid();root.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(220)});root.ColumnDefinitions.Add(new ColumnDefinition());Content=root;
  var sidebar=new Border{Background=UI.B("#F4F4F2"),Padding=new Thickness(16,24,14,18)};root.Children.Add(sidebar);
  var side=new DockPanel();sidebar.Child=side;var brand=new StackPanel{Margin=new Thickness(8,0,0,24)};brand.Children.Add(UI.Text("便签墙",24,UI.Ink));var brandHint=UI.Text("把事情放好，慢慢完成。",12,UI.Muted);brandHint.Margin=new Thickness(0,7,0,0);brand.Children.Add(brandHint);DockPanel.SetDock(brand,Dock.Top);side.Children.Add(brand);
  var bottom=new StackPanel();DockPanel.SetDock(bottom,Dock.Bottom);side.Children.Add(bottom);Status=UI.Text("保存在本机",11,UI.Muted);Status.Margin=new Thickness(8,12,0,0);
  var settings=UI.Button("备份与设置",delegate(object sender,RoutedEventArgs e){var b=(Button)sender;b.ContextMenu=SettingsMenu();b.ContextMenu.IsOpen=true;},false);settings.Background=Brushes.Transparent;settings.HorizontalContentAlignment=HorizontalAlignment.Left;settings.BorderThickness=new Thickness(0);bottom.Children.Add(settings);bottom.Children.Add(Status);
  var sections=new StackPanel();nav=new StackPanel();sections.Children.Add(nav);
  var topicBar=new Grid{Margin=new Thickness(8,22,0,10)};topicBar.ColumnDefinitions.Add(new ColumnDefinition());topicBar.ColumnDefinitions.Add(new ColumnDefinition{Width=GridLength.Auto});topicBar.Children.Add(UI.Text("我的主题",12,UI.Muted));var topicAdd=UI.Button("＋",delegate{EditTopic(null,"");},false);topicAdd.ToolTip="新建顶层主题";AutomationProperties.SetName(topicAdd,"新建主题");topicAdd.MinHeight=24;topicAdd.Padding=new Thickness(6,0,6,0);topicAdd.Background=Brushes.Transparent;topicAdd.BorderThickness=new Thickness(0);Grid.SetColumn(topicAdd,1);topicBar.Children.Add(topicAdd);sections.Children.Add(topicBar);
  Tree=new TreeView{Background=Brushes.Transparent,BorderThickness=new Thickness(0),FontSize=14,Padding=new Thickness(0)};Tree.SelectedItemChanged+=delegate{if(buildingTree)return;var selected=Tree.SelectedItem as TreeViewItem;if(selected!=null&&Current!=(string)selected.Tag)Select((string)selected.Tag);};sections.Children.Add(Tree);
  var newChild=UI.Button("＋ 添加子主题",delegate{EditTopic(null,Current.StartsWith("$")?"":Current);},false);newChild.Margin=new Thickness(6,14,0,0);newChild.BorderThickness=new Thickness(0);newChild.Background=Brushes.Transparent;sections.Children.Add(newChild);
  var sideScroll=new ScrollViewer{Content=sections,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled};side.Children.Add(sideScroll);
  // Native window: 220 px sidebar + 32 px gutter + flexible paper wall + 32 px gutter.
  main=new Grid{Margin=new Thickness(32,26,32,18)};Grid.SetColumn(main,1);root.Children.Add(main);main.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});main.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});main.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});main.RowDefinitions.Add(new RowDefinition());
  var header=new Grid{Margin=new Thickness(0,0,0,20)};header.ColumnDefinitions.Add(new ColumnDefinition());header.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(208)});var titles=new StackPanel();Heading=UI.Text("全部便签",24,UI.Ink);titles.Children.Add(Heading);Summary=UI.Text("",12,UI.Muted);Summary.Margin=new Thickness(0,7,0,0);titles.Children.Add(Summary);header.Children.Add(titles);
  var find=new StackPanel{VerticalAlignment=VerticalAlignment.Center};find.Children.Add(UI.Text("搜索当前便签",11,UI.Muted));Search=UI.Input("",false);Search.MinHeight=33;Search.Padding=new Thickness(8,5,8,5);Search.Margin=new Thickness(0,5,0,0);Search.TextChanged+=delegate{RenderCards();};AutomationProperties.SetName(Search,"搜索便签");find.Children.Add(Search);Grid.SetColumn(find,1);header.Children.Add(find);main.Children.Add(header);
  var composer=new Border{Background=WallUI.Paper(CurrentColor),CornerRadius=new CornerRadius(8),Padding=new Thickness(18,14,18,14),Margin=new Thickness(0,0,0,18)};Grid.SetRow(composer,1);main.Children.Add(composer);var compose=new StackPanel();composer.Child=compose;
  compose.Children.Add(UI.Text("写一张便签",13,UI.Ink));Quick=UI.Input(Data.Draft,true);Quick.Background=Brushes.Transparent;Quick.BorderThickness=new Thickness(0);Quick.Padding=new Thickness(0,8,0,8);Quick.Height=65;Quick.ToolTip="第一行作为标题，后面可以继续写内容；Ctrl+Enter 贴到墙上";AutomationProperties.SetName(Quick,"新便签内容");compose.Children.Add(Quick);
  var composeBar=new Grid();composeBar.ColumnDefinitions.Add(new ColumnDefinition());composeBar.ColumnDefinitions.Add(new ColumnDefinition{Width=GridLength.Auto});
  var controls=new WrapPanel{VerticalAlignment=VerticalAlignment.Center};composeBar.Children.Add(controls);
  var swatches=new StackPanel{Orientation=Orientation.Horizontal,Margin=new Thickness(0,0,10,0)};controls.Children.Add(swatches);Action swatch=null;swatch=delegate{swatches.Children.Clear();for(int j=0;j<Model.Colors.Length;j++){string c=Model.Colors[j];var b=UI.Button("",delegate{CurrentColor=c;composer.Background=WallUI.Paper(c);swatch();},false);b.Width=22;b.Height=22;b.MinHeight=22;b.Padding=new Thickness(0);b.Margin=new Thickness(0,0,5,0);b.BorderBrush=c==CurrentColor?UI.Ink:UI.B("#9B9587");b.BorderThickness=new Thickness(c==CurrentColor?2:1);b.Background=WallUI.Paper(c);b.ToolTip=WallUI.ColorNames[j];AutomationProperties.SetName(b,WallUI.ColorNames[j]+"便签");swatches.Children.Add(b);}};swatch();
  QuickKind=WallUI.Combo(Model.Kinds.Cast<object>(),0);QuickKind.MinWidth=70;QuickKind.Margin=new Thickness(0,0,6,0);QuickKind.MinHeight=30;controls.Children.Add(QuickKind);AutomationProperties.SetName(QuickKind,"新便签类型");
  QuickTopic=WallUI.Combo(WallUI.Choices(Data).Cast<object>(),0);QuickTopic.MaxWidth=150;QuickTopic.MinWidth=100;QuickTopic.MinHeight=30;controls.Children.Add(QuickTopic);AutomationProperties.SetName(QuickTopic,"新便签主题");WallUI.Choose(QuickTopic,"inbox");
  var actions=new StackPanel{Orientation=Orientation.Horizontal,Margin=new Thickness(8,0,0,0),VerticalAlignment=VerticalAlignment.Center};Grid.SetColumn(actions,1);composeBar.Children.Add(actions);
  var time=UI.Button("时间 / 提醒",delegate{EditNote(null,true);},false);time.MinHeight=32;time.Padding=new Thickness(9,5,9,5);time.Background=Brushes.Transparent;time.BorderThickness=new Thickness(0);actions.Children.Add(time);
  AddButton=UI.Button("贴到墙上",delegate{QuickAdd();},true);AddButton.MinHeight=32;actions.Children.Add(AddButton);compose.Children.Add(composeBar);
  var wallBar=new Grid{Margin=new Thickness(0,0,0,12)};wallBar.ColumnDefinitions.Add(new ColumnDefinition());wallBar.ColumnDefinitions.Add(new ColumnDefinition{Width=GridLength.Auto});var guide=UI.Text("点击编辑便签；拖到左侧主题可移动分类。",11,UI.Muted);wallBar.Children.Add(guide);var addDetailed=UI.Button("＋ 详细添加",delegate{EditNote(null,false);},false);addDetailed.MinHeight=28;addDetailed.Padding=new Thickness(8,3,8,3);Grid.SetColumn(addDetailed,1);wallBar.Children.Add(addDetailed);Grid.SetRow(wallBar,2);main.Children.Add(wallBar);
  Cards=new WrapPanel{Orientation=Orientation.Horizontal};cardScroll=new ScrollViewer{Content=Cards,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled};Grid.SetRow(cardScroll,3);main.Children.Add(cardScroll);cardScroll.SizeChanged+=delegate{FitCards();};
  draftTimer=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(500)};draftTimer.Tick+=delegate{draftTimer.Stop();Save();};Quick.TextChanged+=delegate{Data.Draft=Quick.Text;if(loaded){Status.Text="保存中…";draftTimer.Stop();draftTimer.Start();}};
  Quick.PreviewKeyDown+=delegate(object s,KeyEventArgs e){if(e.Key==Key.Enter&&(Keyboard.Modifiers&ModifierKeys.Control)!=0){QuickAdd();e.Handled=true;}};
  ticker=new DispatcherTimer{Interval=TimeSpan.FromSeconds(1)};ticker.Tick+=delegate{if(showSignal!=null&&showSignal.WaitOne(0))Reveal();if(exitSignal!=null&&exitSignal.WaitOne(0)){Quit();return;}CheckReminders();};
  if(!Testing){showSignal=new EventWaitHandle(false,EventResetMode.AutoReset,"Local\\YuChuanNotesWallShow");exitSignal=new EventWaitHandle(false,EventResetMode.AutoReset,"Local\\YuChuanNotesWallExit");SetupTray();}
  Loaded+=delegate{loaded=true;RenderAll();if(!Testing){ticker.Start();if(Storage.Warning!="")MessageBox.Show(this,Storage.Warning,"备份恢复");}};
  Closing+=delegate(object s,System.ComponentModel.CancelEventArgs e){if(quitting)return;if(!Save()){e.Cancel=true;MessageBox.Show(this,"数据保存失败，请先解决或导出备份，再关闭。","未能保存");return;}if(!Testing){e.Cancel=true;Hide();tray.BalloonTipTitle="便签墙已收到托盘";tray.BalloonTipText="时间提醒继续运行，点击托盘图标可以打开。";tray.ShowBalloonTip(3000);}};
  SizeChanged+=delegate{if(loaded&&WindowState==WindowState.Normal){Data.Width=Width;Data.Height=Height;}};
  PreviewKeyDown+=delegate(object s,KeyEventArgs e){if((Keyboard.Modifiers&ModifierKeys.Control)!=0&&e.Key==Key.N){EditNote(null,false);e.Handled=true;}if((Keyboard.Modifiers&ModifierKeys.Control)!=0&&e.Key==Key.F){Search.Focus();e.Handled=true;}};
 }
 void SetupTray(){tray=new F.NotifyIcon{Icon=new System.Drawing.Icon(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"wall.ico")),Text="便签墙 · 主题与提醒",Visible=true};tray.MouseClick+=delegate(object s,F.MouseEventArgs e){if(e.Button==F.MouseButtons.Left)Reveal();};var m=new F.ContextMenuStrip();m.Items.Add("打开便签墙",null,delegate{Reveal();});m.Items.Add("退出（停止提醒）",null,delegate{Quit();});tray.ContextMenuStrip=m;}
 public bool Save(){Data.SelectedTopic=Current;try{Storage.Save(Data);if(Status!=null){Status.Text="已保存 "+DateTime.Now.ToString("HH:mm");Status.Foreground=UI.Muted;Status.ToolTip=Storage.FilePath;}return true;}catch(Exception ex){if(Status!=null){Status.Text="保存失败 · 请导出备份";Status.Foreground=UI.Red;Status.ToolTip=ex.Message;}return false;}}
 public void Reveal(){Show();if(WindowState==WindowState.Minimized)WindowState=WindowState.Normal;Activate();}
 public void Quit(){if(!Save()){Reveal();MessageBox.Show(this,"内容未能保存，程序将保留运行。请先导出备份。","保存失败");return;}quitting=true;ticker.Stop();draftTimer.Stop();foreach(var p in popups.Values.ToList())p.Close();if(tray!=null)tray.Dispose();if(showSignal!=null)showSignal.Dispose();if(exitSignal!=null)exitSignal.Dispose();Close();Application.Current.Shutdown();}
 public void Select(string id){Current=id;Search.Clear();if(!id.StartsWith("$"))WallUI.Choose(QuickTopic,id);RenderAll();Save();}
 public void RenderAll(){RenderTree();RenderCards();}
 void RenderTree(){
  buildingTree=true;try{nav.Children.Clear();string[] ids={"$all","$soon","$done","$trash"};string[] names={"全部便签","近期与逾期","已完成","回收站"};for(int k=0;k<ids.Length;k++){string id=ids[k];int count=Model.Visible(Data,id,"").Count;var b=UI.Button(names[k]+"    "+count,delegate{Select(id);},false);b.HorizontalContentAlignment=HorizontalAlignment.Left;b.BorderThickness=new Thickness(0);b.Background=Current==id?UI.B("#E7E7E4"):Brushes.Transparent;b.Margin=new Thickness(0,0,0,4);nav.Children.Add(b);}
   Tree.Items.Clear();foreach(var t in Data.Topics.Where(x=>x.ParentId==""))Tree.Items.Add(TopicItem(t,0));
   var topicId=WallUI.Selected(QuickTopic);QuickTopic.Items.Clear();foreach(var c in WallUI.Choices(Data))QuickTopic.Items.Add(c);WallUI.Choose(QuickTopic,Current.StartsWith("$")?topicId:Current);
  }finally{buildingTree=false;}
 }
 TreeViewItem TopicItem(Topic t,int depth){
  var item=new TreeViewItem{Tag=t.Id,IsExpanded=true,Padding=new Thickness(2,5,0,5)};var header=new StackPanel{Orientation=Orientation.Horizontal};var text=UI.Text(t.Name,14,UI.Ink);text.MaxWidth=Math.Max(75,140-depth*12);text.TextTrimming=TextTrimming.CharacterEllipsis;text.TextWrapping=TextWrapping.NoWrap;header.Children.Add(text);var ids=Model.Descendants(Data,t.Id);int count=Data.Notes.Count(x=>!x.Deleted&&ids.Contains(x.TopicId));var number=UI.Text(count.ToString(),11,UI.Muted);number.Margin=new Thickness(9,2,0,0);header.Children.Add(number);item.Header=header;item.ToolTip=Model.TopicPath(Data,t.Id);item.IsSelected=Current==t.Id;
  var cm=new ContextMenu();AddMenu(cm,"添加子主题",delegate{EditTopic(null,t.Id);});AddMenu(cm,"重命名",delegate{EditTopic(t,t.ParentId);});if(t.Id!="inbox")AddMenu(cm,"删除主题，便签移到未分类",delegate{if(MessageBox.Show(this,"删除“"+t.Name+"”及其子主题？其中便签会移到未分类。","删除主题",MessageBoxButton.YesNo)==MessageBoxResult.Yes){Model.DeleteTopic(Data,t.Id);Current=Data.SelectedTopic;Save();RenderAll();}});item.ContextMenu=cm;
  item.AllowDrop=true;item.DragOver+=delegate(object s,DragEventArgs e){e.Effects=e.Data.GetDataPresent("NotesWall.Note")?DragDropEffects.Move:DragDropEffects.None;e.Handled=true;};item.Drop+=delegate(object s,DragEventArgs e){string id=e.Data.GetData("NotesWall.Note") as string;var note=Data.Notes.FirstOrDefault(x=>x.Id==id);if(note!=null){note.TopicId=t.Id;Save();RenderAll();}e.Handled=true;};
  foreach(var child in Data.Topics.Where(x=>x.ParentId==t.Id))item.Items.Add(TopicItem(child,depth+1));return item;
 }
 static void AddMenu(ContextMenu m,string name,Action action){var i=new MenuItem{Header=name};i.Click+=delegate{action();};m.Items.Add(i);}
 public void EditTopic(Topic original,string parent){modal=true;try{var dialog=new TopicEditor(Data,original,parent){Owner=this};if(dialog.ShowDialog()==true){if(original==null){var t=Model.AddTopic(Data,dialog.TopicName,dialog.ParentId);Current=t.Id;}else original.Name=dialog.TopicName;Save();RenderAll();}}finally{modal=false;}}
 public void QuickAdd(){if(string.IsNullOrWhiteSpace(Quick.Text)){Quick.Focus();return;}string[] lines=Quick.Text.Trim().Split(new[]{'\n'},2);var note=new Note{Title=lines[0].TrimEnd('\r'),Body=lines.Length>1?lines[1].Trim():"",TopicId=WallUI.Selected(QuickTopic),Kind=QuickKind.SelectedItem.ToString(),Color=CurrentColor};Data.Notes.Add(note);Quick.Clear();if(Current=="$done"||Current=="$trash"||Current=="$soon")Current=note.TopicId;Save();RenderAll();}
 public void EditNote(Note note,bool withDate){modal=true;try{string[] lines=Quick.Text.Trim().Split(new[]{'\n'},2);var d=new NoteEditor(Data,note,WallUI.Selected(QuickTopic),note==null?lines[0]:"",QuickKind.SelectedItem.ToString(),CurrentColor){Owner=this};if(note==null){if(lines.Length>1)d.BodyBox.Text=lines[1];if(withDate)d.HasDate.IsChecked=true;}
  if(d.ShowDialog()==true){if(note==null){Data.Notes.Add(d.Result);Quick.Clear();if(Current=="$done"||Current=="$trash"||Current=="$soon"||(!Current.StartsWith("$")&&!Model.Descendants(Data,Current).Contains(d.Result.TopicId)))Current=d.Result.TopicId;}else{int index=Data.Notes.IndexOf(note);Data.Notes[index]=d.Result;if(popups.ContainsKey(note.Id))popups[note.Id].Close();}Save();RenderAll();}
 }finally{modal=false;}}
 public void RenderCards(){
  if(Cards==null)return;Cards.Children.Clear();var visible=Model.Visible(Data,Current,Search.Text.Trim());Heading.Text=Current=="$all"?"全部便签":Current=="$soon"?"近期与逾期":Current=="$done"?"已完成":Current=="$trash"?"回收站":Data.Topics.First(x=>x.Id==Current).Name;
  int pending=visible.Count(x=>!x.Done);Summary.Text=(Current.StartsWith("$")?"":Model.TopicPath(Data,Current)+"  ·  ")+visible.Count+" 张便签 · "+pending+" 件未完成"+(Current=="$soon"?" · 未来 7 天及已逾期":"");
  if(visible.Count==0){var empty=new StackPanel{Margin=new Thickness(2,24,0,20)};empty.Children.Add(UI.Text(Search.Text.Trim()!=""?"没有找到符合的便签":Current=="$trash"?"回收站是空的":"给这面墙贴上第一张便签",20,UI.Ink));var hint=UI.Text(Search.Text.Trim()!=""?"试试其他关键词，或切换到全部便签。":"左侧添加主题或课程，再写下任务、作业和考试时间。",13,UI.Muted);hint.Margin=new Thickness(0,10,0,0);empty.Children.Add(hint);Cards.Children.Add(empty);return;}
  foreach(var n in visible)Cards.Children.Add(Card(n));FitCards();
 }
 FrameworkElement Card(Note n){
  var paper=new Border{Background=WallUI.Paper(n.Color),CornerRadius=new CornerRadius(8),Margin=new Thickness(0,0,12,12),Padding=new Thickness(17,13,17,13),Height=246,Opacity=n.Done ? 0.76 : 1,Tag=n.Id};
  var grid=new Grid();paper.Child=grid;grid.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});grid.RowDefinitions.Add(new RowDefinition());grid.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});
  var top=new Grid();top.ColumnDefinitions.Add(new ColumnDefinition());top.ColumnDefinitions.Add(new ColumnDefinition{Width=GridLength.Auto});var topic=UI.Text(n.Kind+" · "+Model.TopicPath(Data,n.TopicId),11,UI.Muted);topic.MaxHeight=32;topic.ToolTip=Model.TopicPath(Data,n.TopicId);top.Children.Add(topic);var menu=UI.Button("···",delegate(object s,RoutedEventArgs e){var b=(Button)s;b.ContextMenu=CardMenu(n);b.ContextMenu.IsOpen=true;},false);menu.MinHeight=22;menu.Padding=new Thickness(3,0,3,0);menu.Background=Brushes.Transparent;menu.BorderThickness=new Thickness(0);Grid.SetColumn(menu,1);top.Children.Add(menu);grid.Children.Add(top);
  Point dragStart=new Point();top.PreviewMouseLeftButtonDown+=delegate(object s,MouseButtonEventArgs e){dragStart=e.GetPosition(top);};top.MouseMove+=delegate(object s,MouseEventArgs e){if(e.LeftButton==MouseButtonState.Pressed&&Math.Abs((e.GetPosition(top)-dragStart).Length)>8)DragDrop.DoDragDrop(paper,new DataObject("NotesWall.Note",n.Id),DragDropEffects.Move);};
  var body=new StackPanel{Margin=new Thickness(0,12,0,8)};var title=UI.Text((n.Pinned?"置顶 · ":"")+n.Title,18,UI.Ink);title.MaxHeight=54;title.TextTrimming=TextTrimming.CharacterEllipsis;if(n.Done)title.TextDecorations=TextDecorations.Strikethrough;body.Children.Add(title);if(n.Body!=""){var content=UI.Text(n.Body,13,UI.Ink);content.MaxHeight=62;content.Margin=new Thickness(0,8,0,0);content.TextTrimming=TextTrimming.CharacterEllipsis;body.Children.Add(content);}body.Cursor=Cursors.Hand;body.MouseLeftButtonUp+=delegate{if(!n.Deleted)EditNote(n,false);};Grid.SetRow(body,1);grid.Children.Add(body);
  var foot=new StackPanel();if(n.DueUtc.HasValue){bool overdue=n.DueUtc.Value<DateTime.UtcNow&&!n.Done;var d=UI.Text((overdue?"已过时间 · ":"时间 · ")+n.DueUtc.Value.ToLocalTime().ToString("MM/dd HH:mm"),12,overdue?UI.Red:UI.Ink);d.Margin=new Thickness(0,0,0,6);foot.Children.Add(d);}
  if(n.ReminderOn&&!n.Done){var r=UI.Text(n.Notified?"本次已提醒":"提醒 · "+(n.RemindUtc.HasValue?n.RemindUtc.Value.ToLocalTime().ToString("MM/dd HH:mm"):"未设置"),11,UI.Muted);r.Margin=new Thickness(0,0,0,8);foot.Children.Add(r);}
  var bottom=new Grid();bottom.ColumnDefinitions.Add(new ColumnDefinition());bottom.ColumnDefinitions.Add(new ColumnDefinition{Width=GridLength.Auto});
  if(!n.Deleted){var done=new CheckBox{Content=n.Done?"已完成":"标记完成",IsChecked=n.Done,FontSize=12,VerticalAlignment=VerticalAlignment.Center};AutomationProperties.SetName(done,"完成："+n.Title);done.Click+=delegate{n.Done=done.IsChecked==true;if(popups.ContainsKey(n.Id))popups[n.Id].Close();Save();RenderAll();};bottom.Children.Add(done);}
  var edit=UI.Button(n.Deleted?"恢复":"编辑",delegate{if(n.Deleted){n.Deleted=false;Save();RenderAll();}else EditNote(n,false);},false);edit.MinHeight=23;edit.Padding=new Thickness(5,1,5,1);edit.BorderThickness=new Thickness(0);edit.Background=Brushes.Transparent;Grid.SetColumn(edit,1);bottom.Children.Add(edit);foot.Children.Add(bottom);Grid.SetRow(foot,2);grid.Children.Add(foot);return paper;
 }
 ContextMenu CardMenu(Note n){var cm=new ContextMenu();if(n.Deleted){AddMenu(cm,"恢复便签",delegate{n.Deleted=false;Save();RenderAll();});return cm;}AddMenu(cm,"编辑",delegate{EditNote(n,false);});AddMenu(cm,n.Pinned?"取消置顶":"置顶",delegate{n.Pinned=!n.Pinned;Save();RenderCards();});var move=new MenuItem{Header="移动到主题"};foreach(var t in WallUI.Choices(Data)){var target=t;var i=new MenuItem{Header=t.Name,IsChecked=n.TopicId==t.Id};i.Click+=delegate{n.TopicId=target.Id;Save();RenderAll();};move.Items.Add(i);}cm.Items.Add(move);AddMenu(cm,"移到回收站",delegate{n.Deleted=true;if(popups.ContainsKey(n.Id))popups[n.Id].Close();Save();RenderAll();});return cm;}
 void FitCards(){if(cardScroll==null||Cards==null||Cards.Children.Count==0)return;double available=Math.Max(250,cardScroll.ActualWidth-18);int columns=Math.Max(1,(int)(available/258));Cards.ItemWidth=Math.Floor(available/columns);}
 ContextMenu SettingsMenu(){var cm=new ContextMenu();AddMenu(cm,"导出完整备份…",Export);AddMenu(cm,"导入备份（追加缺少的内容）…",Import);AddMenu(cm,"打开数据文件夹",delegate{Process.Start(new ProcessStartInfo(Path.GetDirectoryName(Storage.FilePath)){UseShellExecute=true});});var sound=new MenuItem{Header="提醒时播放声音",IsCheckable=true,IsChecked=Data.Sound};sound.Click+=delegate{Data.Sound=sound.IsChecked;Save();};cm.Items.Add(sound);AddMenu(cm,"退出软件（停止提醒）",Quit);return cm;}
 void Export(){modal=true;try{var dialog=new Microsoft.Win32.SaveFileDialog{Filter="便签墙备份 (*.xml)|*.xml",FileName="便签墙备份_"+DateTime.Now.ToString("yyyyMMdd")+".xml"};if(dialog.ShowDialog(this)==true){if(Path.GetFullPath(dialog.FileName).Equals(Path.GetFullPath(Storage.FilePath),StringComparison.OrdinalIgnoreCase))return;using(var f=File.Create(dialog.FileName))new System.Xml.Serialization.XmlSerializer(typeof(Notebook)).Serialize(f,Data);Status.Text="备份已导出";}}catch(Exception ex){MessageBox.Show(this,"导出失败："+ex.Message);}finally{modal=false;}}
 void Import(){modal=true;try{var d=new Microsoft.Win32.OpenFileDialog{Filter="便签墙备份 (*.xml)|*.xml"};if(d.ShowDialog(this)==true){var incoming=Store.Read(d.FileName);int count=Merge(incoming);Save();RenderAll();MessageBox.Show(this,"已追加 "+count+" 张便签；已有同一张便签保持当前版本。","导入完成");}}catch(Exception ex){MessageBox.Show(this,"无法导入："+ex.Message);}finally{modal=false;}}
 public int Merge(Notebook incoming){var merged=Model.Merged(Data,incoming);int count=merged.Notes.Count-Data.Notes.Count;Data=merged;return count;}
 public void CheckReminders(){if(modal)return;foreach(var n in Model.Due(Data,DateTime.UtcNow).Take(Math.Max(0,3-popups.Count))){n.Notified=true;if(!Save()){n.Notified=false;return;}var popup=new WallReminder(n,Data,delegate{Save();RenderAll();});popups[n.Id]=popup;popup.Closed+=delegate{popups.Remove(n.Id);};popup.WindowStartupLocation=WindowStartupLocation.CenterScreen;popup.Show();if(Data.Sound&&!Testing)System.Media.SystemSounds.Asterisk.Play();RenderCards();}}
}
static class WallProgram {
 [STAThread]public static int Main(string[] args){
  try{Native.SetProcessDpiAwarenessContext(new IntPtr(-4));}catch{}
  if(args.Length>=2&&args[0]=="--self-test")return Checks.Core(args[1]);if(args.Length>=2&&args[0]=="--ui-test")return Checks.Visual(args[1]);
  if(args.Contains("--exit")){try{using(var e=EventWaitHandle.OpenExisting("Local\\YuChuanNotesWallExit"))e.Set();}catch{}return 0;}
  bool created;using(var mutex=new Mutex(true,"Local\\YuChuanNotesWall",out created)){
   if(!created){try{using(var e=EventWaitHandle.OpenExisting("Local\\YuChuanNotesWallShow"))e.Set();}catch{}return 0;}
   try{
    var dir=AppDomain.CurrentDomain.BaseDirectory;var store=new Store(Path.Combine(dir,"data"));var data=store.Load();
    if(!data.LegacyImported){var legacy=Path.GetFullPath(Path.Combine(dir,"..","GPT便利贴","data","notes.xml"));var backup=Path.Combine(dir,"data","旧版便签_迁移前.xml");if(File.Exists(legacy)&&!File.Exists(backup))File.Copy(legacy,backup,false);Model.ImportLegacy(data,legacy);store.Save(data);}
    var app=new Application{ShutdownMode=ShutdownMode.OnExplicitShutdown};app.DispatcherUnhandledException+=delegate(object s,DispatcherUnhandledExceptionEventArgs e){MessageBox.Show("操作未完成："+e.Exception.Message,"便签墙");e.Handled=true;};app.Run(new Wall(store,false));return 0;
   }catch(Exception ex){MessageBox.Show(ex.Message,"便签墙未能打开");return 1;}
  }
 }
}
