using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

static class Checks {
 public static int Core(string dir){
  Directory.CreateDirectory(dir);var report=new List<string>();try{
   Action<bool,string> check=delegate(bool ok,string label){if(!ok)throw new Exception(label);report.Add("PASS "+label);};
   var data=new Notebook();Model.Normalize(data);var course=Model.AddTopic(data,"机器学习","courses");var week=Model.AddTopic(data,"实验课",course.Id);
   var task=new Note{Title="神经网络作业",Body="中文备注\n<&>第二行",TopicId=week.Id,Kind="作业",DueUtc=DateTime.UtcNow.AddDays(2)};data.Notes.Add(task);
   check(Model.Visible(data,"courses","").Count==1&&Model.TopicPath(data,week.Id)=="课程 / 机器学习 / 实验课","Nested topics include descendants and preserve path");
   check(Model.Visible(data,"$all","机器学习").Count==1&&Model.Visible(data,"$all","神经网络").Count==1,"Search title, body and topic path");
   bool duplicate=false;try{Model.AddTopic(data,"机器学习","courses");}catch(ArgumentException){duplicate=true;}check(duplicate,"Duplicate sibling topic rejected");
   var now=DateTime.UtcNow;task.ReminderOn=true;task.RemindUtc=now.AddSeconds(-1);check(Model.Due(data,now).Count==1,"Advance reminder can fire before the event date");
   var deadline=task.DueUtc;Model.Snooze(task,now);check(task.DueUtc==deadline&&Model.Due(data,now.AddMinutes(9)).Count==0&&Model.Due(data,now.AddMinutes(11)).Count==1,"Snoozing keeps homework or exam time unchanged");
   task.Deleted=true;check(Model.Visible(data,"$all","").Count==0&&Model.Visible(data,"$trash","").Count==1&&Model.Due(data,now.AddMinutes(11)).Count==0,"Trash hides notes and suppresses reminders without losing data");task.Deleted=false;
   task.Done=true;check(Model.Due(data,now.AddMinutes(11)).Count==0&&Model.Visible(data,"$done","").Count==1,"Completed notes do not remind");task.Done=false;
   var store=new Store(Path.Combine(dir,"model-data"));data.Draft="尚未贴上墙的草稿";store.Save(data);var round=store.Load();check(round.Notes[0].Body==task.Body&&round.Draft==data.Draft&&round.Topics.Count==data.Topics.Count,"XML round trip preserves hierarchy, note fields and draft");
   store.Save(round);check(File.Exists(store.FilePath+".bak"),"Atomic save keeps previous version");File.WriteAllText(store.FilePath,"broken");round=store.Load();check(round.Notes.Count==1&&store.Warning!=""&&store.Load().Notes.Count==1,"Damaged primary recovers backup and survives the next load");store.Save(round);
   Model.DeleteTopic(round,course.Id);check(round.Notes[0].TopicId=="inbox"&&!round.Topics.Any(t=>t.Id==week.Id),"Deleting a parent topic moves all descendant notes to inbox");
   var invalid=new Notebook();invalid.Topics.Add(new Topic{Id="a",Name="A",ParentId="b"});invalid.Topics.Add(new Topic{Id="b",Name="B",ParentId="a"});bool cycle=false;try{Model.Normalize(invalid);}catch(InvalidDataException){cycle=true;}check(cycle,"Cyclic topic imports rejected");
   var incoming=new Notebook();Model.Normalize(incoming);incoming.Notes.Add(new Note{Id=task.Id,Title="旧备份标题"});incoming.Notes.Add(new Note{Id="new-backup-note",Title="备份中的新便签"});var merged=Model.Merged(data,incoming);check(merged.Notes.Count==2&&merged.Notes.First(x=>x.Id==task.Id).Title==task.Title&&data.Notes.Count==1,"Backup import adds missing notes without overwriting current data");
   var baseTree=new Notebook();Model.Normalize(baseTree);baseTree.Topics.Add(new Topic{Id="a",Name="A"});baseTree.Topics.Add(new Topic{Id="b",Name="B",ParentId="a"});var other=new Notebook();Model.Normalize(other);other.Topics.Add(new Topic{Id="a",Name="A",ParentId="c"});other.Topics.Add(new Topic{Id="c",Name="C",ParentId="b"});other.Topics.Add(new Topic{Id="b",Name="B"});Model.Normalize(other);var safeMerged=Model.Merged(baseTree,other);check(safeMerged.Topics.First(x=>x.Id=="a").ParentId==""&&baseTree.Topics.Count+1==safeMerged.Topics.Count,"Backup import preserves existing topic parents");
   var legacy=Path.Combine(dir,"legacy.xml");File.WriteAllText(legacy,"<Notebook><Notes><Note><Id>old-a</Id><Title>同名比赛</Title><Body>原记录</Body><Done>true</Done><CreatedUtc>2026-11-01T00:00:00Z</CreatedUtc></Note><Note><Id>old-b</Id><Title>同名比赛</Title><Done>false</Done></Note></Notes><Scratch>旧版随手记</Scratch></Notebook>",Encoding.UTF8);
   var migrated=new Notebook();Model.Normalize(migrated);int count=Model.ImportLegacy(migrated,legacy);check(count==3&&migrated.Notes.Count(x=>x.Done)==1&&migrated.Notes.Count(x=>x.Title=="同名比赛")==2,"Legacy migration preserves duplicate titles, completion and scratch");check(Model.ImportLegacy(migrated,legacy)==0&&migrated.Notes.Count==3,"Legacy migration runs only once");
   File.WriteAllLines(Path.Combine(dir,"core-test.txt"),report,Encoding.UTF8);return 0;
  }catch(Exception ex){report.Add("FAIL "+ex);File.WriteAllLines(Path.Combine(dir,"core-test.txt"),report,Encoding.UTF8);return 1;}
 }
 static IEnumerable<T> Desc<T>(DependencyObject node)where T:DependencyObject{if(node is T)yield return (T)node;for(int i=0;i<VisualTreeHelper.GetChildrenCount(node);i++)foreach(var child in Desc<T>(VisualTreeHelper.GetChild(node,i)))yield return child;}
 static void VerifyPlanner(Wall wall,string dir,Action<bool,string> check){
   var now=DateTime.UtcNow;
   var contest=Model.AddTopic(wall.Data,"样例比赛","contests");contest.Color="aqua";
   var test=new Note{Title="测试作品提交",TopicId=contest.Id,Color="auto",DueUtc=now.AddDays(2),ReminderOn=true};
   for(int i=1;i<=4;i++)test.Steps.Add(new Step{Name="步骤 "+i,Done=i<4});
   string path=Path.Combine(dir,"materials.txt");File.WriteAllText(path,"source");
   test.Materials.Add(new Material{Name="作品原件",Target=path});
   test.Materials.Add(new Material{Name="报名入口",Target="https://example.org/contest"});
   Planner.Schedule(test,null,new[]{10080,1440,120});
   wall.Data.Notes.Add(test);
   try{
    check(Planner.Color(wall.Data,test.TopicId,test.Color)=="aqua"&&Planner.Progress(test).Contains("3/4"),"Theme inheritance and next incomplete step");
    var editor=new NoteEditor(wall.Data,test,contest.Id,"","比赛","auto"){Owner=wall};
    editor.Loaded+=delegate{editor.DateBox.SelectedDate=test.DueUtc.Value.ToLocalTime().AddDays(4).Date;editor.SaveButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));};
    check(editor.ShowDialog()==true && editor.Result.Alarms.Count==3 && editor.Result.Materials.Count==2 && editor.Result.Steps.Count==4,"Competition editor retains four steps, three alarms and two materials on date change");
    var rescheduled=editor.Result;

    check(rescheduled.Alarms.All(a=>a.At==rescheduled.DueUtc.Value.AddMinutes(-a.Minutes))&&Planner.Day(new[]{rescheduled},test.DueUtc.Value.ToLocalTime()).Count==0&&Planner.Day(new[]{rescheduled},rescheduled.DueUtc.Value.ToLocalTime()).Count==1,"Rescheduled exam/contest moves to its new date in both planners");
    test.Steps[3].Done=true;check(!test.Done&&Planner.Progress(test).Contains("4/4"),"All steps checked still requires explicit task completion");
    DetailControls.Open(test.Materials[0],target=>File.AppendAllText(target," edited"));check(File.ReadAllText(path)=="source edited","Material dispatch targets the editable original file");
    wall.Width=1160;wall.Height=800;wall.Select("$week");wall.UpdateLayout();UI.Capture((FrameworkElement)wall.Content,Path.Combine(dir,"week.png"));
    wall.Select("$month");wall.UpdateLayout();UI.Capture((FrameworkElement)wall.Content,Path.Combine(dir,"month.png"));
    check(wall.PlannerContent!=null,"Week and month render at the normal navigation entrypoints");
    foreach(var alarm in test.Alarms)alarm.At=now.AddMinutes(-1-alarm.Minutes);
    wall.RefreshAlarms(true);wall.CheckReminders();
    var digests=Application.Current.Windows.Cast<Window>().Where(w=>w.Title=="便签墙提醒").ToList();
    check(digests.Count==1,"Three missed alarms produce one summary across repeated ticks");digests[0].Close();
    wall.Select("$pending");check(wall.Summary.Text.StartsWith("1 件")&&Planner.Pending(test,now).Count()==3,"Dismissing summary keeps one note with three pending alarm nodes");
    test.Alarms[0].Acknowledged=true;
    test.Alarms[1].At=now.AddMinutes(10);Planner.Sync(test);
    check(Planner.Pending(test,now).Count()==1&&!test.Alarms[2].Acknowledged,"Acknowledging and snoozing individual nodes preserve remaining alarms");
    wall.Save();var restored=wall.Storage.Load().Notes.First(n=>n.Id==test.Id);
    check(restored.Steps.Count==4&&restored.Materials.Count==2&&restored.Materials[0].Target==path&&restored.Alarms.Count==3&&restored.Alarms[0].Acknowledged,"XML round trip restores checklist, source paths and independent reminder states");
   }finally{wall.Data.Notes.Remove(test);wall.Data.Topics.Remove(contest);wall.Select("$all");}
 }
 public static int Visual(string dir){
  Directory.CreateDirectory(dir);var report=new List<string>();int code=0;var app=new Application{ShutdownMode=ShutdownMode.OnExplicitShutdown};app.DispatcherUnhandledException+=delegate(object sender,DispatcherUnhandledExceptionEventArgs e){File.WriteAllText(Path.Combine(dir,"dispatcher-error.txt"),e.Exception.ToString());e.Handled=true;code=1;app.Shutdown();};var store=new Store(Path.Combine(dir,"ui-data"));var wall=new Wall(store,true);wall.Data.Notes.Clear();
  wall.Loaded+=delegate{wall.Dispatcher.BeginInvoke(new Action(delegate{try{
   Action<bool,string> check=delegate(bool ok,string label){if(!ok)throw new Exception(label);report.Add("PASS "+label);File.WriteAllLines(Path.Combine(dir,"ui-progress.txt"),report);};
   wall.RenderAll();wall.UpdateLayout();UI.Capture((FrameworkElement)wall.Content,Path.Combine(dir,"empty.png"));
   var topicDialog=new TopicEditor(wall.Data,null,"courses"){Owner=wall};topicDialog.Loaded+=delegate{topicDialog.NameBox.Text="机器学习";topicDialog.SaveButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));if(topicDialog.IsVisible){topicDialog.UpdateLayout();UI.Capture((FrameworkElement)topicDialog.Content,Path.Combine(dir,"topic-debug.png"));}};check(topicDialog.ShowDialog()==true&&topicDialog.ParentId=="courses","New child topic dialog selects parent");var course=Model.AddTopic(wall.Data,topicDialog.TopicName,topicDialog.ParentId);wall.RenderAll();wall.Select(course.Id);
   wall.QuickKind.SelectedItem="作业";wall.Quick.Text="完成神经网络实验\n整理训练曲线和实验结论。";wall.AddButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));check(wall.Data.Notes.Count==1&&wall.Data.Notes[0].TopicId==course.Id&&wall.Data.Notes[0].Body.Contains("训练曲线"),"Quick add routes multiline homework to selected course");var task=wall.Data.Notes[0];
   var ed=new NoteEditor(wall.Data,null,course.Id,"","考试","blue"){Owner=wall};ed.Loaded+=delegate{
    ed.SaveButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));check(ed.Result==null,"Blank title is rejected without closing editor");ed.TitleBox.Text="期中考试";ed.BodyBox.Text="复习线性模型与神经网络，带学生证。";ed.HasDate.IsChecked=true;ed.Remind.IsChecked=true;ed.DateBox.SelectedDate=DateTime.Today.AddDays(3);ed.TimeBox.Text="bad";ed.SaveButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));check(ed.Result==null,"Invalid date/time is rejected");ed.TimeBox.Text="09:00";WallUI.Choose(ed.LeadBox,"1440");ed.UpdateLayout();UI.Capture((FrameworkElement)ed.Content,Path.Combine(dir,"editor.png"));ed.SaveButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));};check(ed.ShowDialog()==true&&ed.Result.Kind=="考试"&&ed.Result.DueUtc-ed.Result.RemindUtc==TimeSpan.FromDays(1),"Exam event date and one-day-early reminder saved separately");wall.Data.Notes.Add(ed.Result);
   wall.Data.Notes.Add(new Note{Title="比赛材料清单",Body="核对作品说明、图片与报名附件。",TopicId="contests",Kind="比赛",Color="yellow"});wall.Data.Notes.Add(new Note{Title="周末整理",Body="备份本周课程资料。",TopicId="life",Color="mint"});wall.Data.Notes.Add(new Note{Title="课堂疑问",Body="下次讨论时问清楚损失函数的选择。",TopicId=course.Id,Color="purple"});wall.Data.Notes.Add(new Note{Title="记得带充电器",TopicId="life",Color="pink"});
   wall.Select("$all");wall.UpdateLayout();var card=wall.Cards.Children.OfType<Border>().First(x=>(string)x.Tag==task.Id);var done=Desc<CheckBox>(card).First();done.IsChecked=true;done.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));check(task.Done,"Card completion checkbox updates saved task");
   wall.Search.Text="神经网络";check(wall.Cards.Children.Count==2,"Search filters note cards across title and body");wall.Search.Clear();wall.RenderAll();wall.UpdateLayout();UI.Capture((FrameworkElement)wall.Content,Path.Combine(dir,"wall.png"));
   wall.Select(course.Id);wall.UpdateLayout();UI.Capture((FrameworkElement)wall.Content,Path.Combine(dir,"course.png"));wall.Select("$all");wall.Width=960;wall.Height=680;wall.UpdateLayout();UI.Capture((FrameworkElement)wall.Content,Path.Combine(dir,"medium.png"));wall.Width=780;wall.Height=600;wall.UpdateLayout();UI.Capture((FrameworkElement)wall.Content,Path.Combine(dir,"compact.png"));
   card=wall.Cards.Children.OfType<Border>().First(x=>(string)x.Tag==task.Id);var more=Desc<Button>(card).First(x=>x.Content as string=="···");more.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));var trash=more.ContextMenu.Items.OfType<MenuItem>().First(x=>(string)x.Header=="移到回收站");more.ContextMenu.IsOpen=false;trash.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));check(task.Deleted&&Model.Visible(wall.Data,"$trash","").Count==1,"Card menu moves note to recoverable trash");wall.Select("$trash");wall.UpdateLayout();card=wall.Cards.Children.OfType<Border>().First();var restore=Desc<Button>(card).First(x=>x.Content as string=="恢复");restore.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));check(!task.Deleted,"Restore button recovers trashed note");
   var alarm=new Note{Title="提醒窗口验证",TopicId=course.Id,Kind="作业",DueUtc=DateTime.UtcNow.AddHours(1),RemindUtc=DateTime.UtcNow.AddSeconds(-1),ReminderOn=true};wall.Data.Notes.Add(alarm);wall.CheckReminders();var popup=app.Windows.OfType<WallReminder>().First();popup.UpdateLayout();UI.Capture((FrameworkElement)popup.Content,Path.Combine(dir,"reminder.png"));var deadline=alarm.DueUtc;popup.SnoozeButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));check(alarm.DueUtc==deadline&&alarm.RemindUtc>DateTime.UtcNow.AddMinutes(9),"Reminder snooze button preserves original deadline");alarm.Alarms[0].At=DateTime.UtcNow.AddSeconds(-1);Planner.Sync(alarm);wall.CheckReminders();app.Windows.OfType<WallReminder>().First().DoneButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));check(alarm.Done,"Reminder complete button updates task");
   VerifyPlanner(wall,dir,check);wall.Quick.Text="没有贴上墙的临时想法";check(wall.Save()&&store.Load().Draft==wall.Quick.Text,"Unposted draft persists to disk");check(store.Load().Notes.Count==7,"All note and category changes survive reload");
  }catch(Exception ex){code=1;report.Add("FAIL "+ex);}finally{File.WriteAllLines(Path.Combine(dir,"ui-test.txt"),report,Encoding.UTF8);app.Shutdown();}}),DispatcherPriority.ApplicationIdle);};
  app.Run(wall);return code;
 }
}
