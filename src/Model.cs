using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Serialization;

[Serializable] public class Topic {
 public string Id=Guid.NewGuid().ToString("N"), Name="", ParentId="";
 public string Color="auto";
 public override string ToString(){return Name;}
}
[Serializable] public class Note {
 public string Id=Guid.NewGuid().ToString("N"), Title="", Body="", TopicId="inbox", Kind="便签", Color="yellow";
 public bool Done, Notified, Pinned, Deleted, ReminderOn;
 public DateTime? DueUtc, RemindUtc;
 public DateTime CreatedUtc=DateTime.UtcNow;
 public List<Step> Steps=new List<Step>();
 public List<Material> Materials=new List<Material>();
 public List<Alarm> Alarms=new List<Alarm>();
 public Note Clone(){
  var copy=(Note)MemberwiseClone();
  copy.Steps=Steps.Select(s=>new Step{Name=s.Name,Done=s.Done}).ToList();
  copy.Materials=Materials.Select(m=>new Material{Name=m.Name,Target=m.Target}).ToList();
  copy.Alarms=Alarms.Select(a=>a.Copy()).ToList();
  return copy;
 }
}
[Serializable] public class Notebook {
 public List<Note> Notes=new List<Note>();
 public List<Topic> Topics=new List<Topic>();
 public string Draft="", SelectedTopic="$all";
 public int SchemaVersion;
 public bool Sound=true, LegacyImported;
 public double Width=1160, Height=800;
}
public class Store {
 public string FilePath, Warning="";
 public Store(string dir){Directory.CreateDirectory(dir);FilePath=Path.Combine(dir,"wall.xml");}
 public static Notebook Read(string path){
  var settings=new XmlReaderSettings{DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null};
  using(var f=XmlReader.Create(path,settings)){var n=(Notebook)new XmlSerializer(typeof(Notebook)).Deserialize(f);Model.Normalize(n);return n;}
 }
 public Notebook Load(){
  if(!File.Exists(FilePath)){var n=new Notebook();Model.Normalize(n);return n;}
  try{return Read(FilePath);}catch(Exception original){
   string copy=FilePath+".unreadable-"+DateTime.UtcNow.ToString("yyyyMMddHHmmssfff");File.Copy(FilePath,copy,false);
   if(File.Exists(FilePath+".bak"))try{var n=Read(FilePath+".bak");File.Delete(FilePath);Save(n);Warning="已从上一次备份恢复。无法读取的原文件已另存保留。";return n;}catch{}
   throw new IOException("便签数据无法读取，原文件已经保留。请先备份 data 文件夹，再恢复导出的备份。\n"+original.Message);
  }
 }
 public void Save(Notebook n){
  string temp=FilePath+".tmp";
  using(var f=new FileStream(temp,FileMode.Create,FileAccess.Write,FileShare.None)){new XmlSerializer(typeof(Notebook)).Serialize(f,n);f.Flush(true);}
  if(File.Exists(FilePath))File.Replace(temp,FilePath,FilePath+".bak",true);else File.Move(temp,FilePath);
 }
}
static class Model {
 public static readonly string[] Colors=Planner.Palette;
 public static readonly string[] Kinds={"便签","作业","考试","比赛","提醒"};
 public static void Normalize(Notebook n){
  if(n.Notes==null)n.Notes=new List<Note>();if(n.Topics==null)n.Topics=new List<Topic>();
  if(n.Topics.Count==0)n.Topics.AddRange(new[]{new Topic{Id="contests",Name="比赛"},new Topic{Id="courses",Name="课程"},new Topic{Id="life",Name="生活"}});
  if(!n.Topics.Any(x=>x.Id=="inbox"))n.Topics.Add(new Topic{Id="inbox",Name="未分类"});
  if(n.Topics.Any(x=>string.IsNullOrWhiteSpace(x.Id)||string.IsNullOrWhiteSpace(x.Name))||n.Topics.GroupBy(x=>x.Id).Any(g=>g.Count()>1))throw new InvalidDataException("主题数据重复或不完整。");
  foreach(var t in n.Topics){if(t.ParentId==null)t.ParentId="";if(t.ParentId!=""&&!n.Topics.Any(x=>x.Id==t.ParentId))t.ParentId="";}
  foreach(var t in n.Topics){var seen=new HashSet<string>{t.Id};var parent=t.ParentId;while(parent!=""){if(!seen.Add(parent))throw new InvalidDataException("主题之间形成循环。");parent=n.Topics.First(x=>x.Id==parent).ParentId;}}
  if(n.Notes.GroupBy(x=>x.Id).Any(g=>g.Count()>1))throw new InvalidDataException("便签标识重复。");
  foreach(var x in n.Notes){if(string.IsNullOrEmpty(x.Id))x.Id=Guid.NewGuid().ToString("N");x.Title=x.Title??"";x.Body=x.Body??"";if(!n.Topics.Any(t=>t.Id==x.TopicId))x.TopicId="inbox";Planner.Upgrade(x);if(x.Color!="auto"&&!Colors.Contains(x.Color))x.Color="yellow";if(!Kinds.Contains(x.Kind))x.Kind="便签";}
 }
 public static HashSet<string> Descendants(Notebook n,string id){var ids=new HashSet<string>{id};bool more;do{more=false;foreach(var t in n.Topics)if(ids.Contains(t.ParentId)&&ids.Add(t.Id))more=true;}while(more);return ids;}
 public static string TopicPath(Notebook n,string id){var names=new List<string>();var seen=new HashSet<string>();while(!string.IsNullOrEmpty(id)&&seen.Add(id)){var t=n.Topics.FirstOrDefault(x=>x.Id==id);if(t==null)break;names.Add(t.Name);id=t.ParentId;}names.Reverse();return string.Join(" / ",names);}
 public static Topic AddTopic(Notebook n,string name,string parent){name=(name??"").Trim();if(name=="")throw new ArgumentException("请填写主题名称。");if(name.Length>60)throw new ArgumentException("主题名称最多 60 个字。");if(parent!=""&&!n.Topics.Any(x=>x.Id==parent))throw new ArgumentException("上级主题不存在。");if(n.Topics.Any(x=>x.ParentId==parent&&x.Name==name))throw new ArgumentException("同一层已经有这个主题。");var t=new Topic{Name=name,ParentId=parent};n.Topics.Add(t);return t;}
 public static void DeleteTopic(Notebook n,string id){if(id=="inbox")throw new ArgumentException("未分类需要保留。");var ids=Descendants(n,id);foreach(var note in n.Notes.Where(x=>ids.Contains(x.TopicId)))note.TopicId="inbox";n.Topics.RemoveAll(x=>ids.Contains(x.Id));if(ids.Contains(n.SelectedTopic))n.SelectedTopic="inbox";}
 public static List<Note> Visible(Notebook n,string view,string search){
  var now=DateTime.UtcNow;IEnumerable<Note> q=n.Notes;
  if(view=="$trash")q=q.Where(x=>x.Deleted);else{q=q.Where(x=>!x.Deleted);if(view=="$done")q=q.Where(x=>x.Done);else if(view=="$soon")q=q.Where(x=>!x.Done&&x.DueUtc.HasValue&&x.DueUtc.Value<=now.AddDays(7));else if(!view.StartsWith("$")){var ids=Descendants(n,view);q=q.Where(x=>ids.Contains(x.TopicId));}}
  if(!string.IsNullOrWhiteSpace(search))q=q.Where(x=>(x.Title+"\n"+x.Body+"\n"+x.Kind+"\n"+TopicPath(n,x.TopicId)).IndexOf(search,StringComparison.OrdinalIgnoreCase)>=0);
  return q.OrderByDescending(x=>x.Pinned).ThenBy(x=>x.Done).ThenBy(x=>x.DueUtc??DateTime.MaxValue).ThenByDescending(x=>x.CreatedUtc).ToList();
 }
 public static List<Note> Due(Notebook n,DateTime now){return n.Notes.Where(x=>Planner.Pending(x,now).Any()).ToList();}
 public static void Snooze(Note n,DateTime now){Planner.Upgrade(n);var alarm=Planner.Pending(n,now).First();alarm.At=now.AddMinutes(10);Planner.Sync(n);}
 public static Notebook Merged(Notebook current,Notebook incoming){
  var candidate=new Notebook{Notes=current.Notes.Select(x=>x.Clone()).ToList(),Topics=current.Topics.Select(x=>new Topic{Id=x.Id,Name=x.Name,ParentId=x.ParentId,Color=x.Color}).ToList(),Draft=current.Draft,SelectedTopic=current.SelectedTopic,SchemaVersion=current.SchemaVersion,Sound=current.Sound,LegacyImported=current.LegacyImported,Width=current.Width,Height=current.Height};
  foreach(var t in incoming.Topics)if(!candidate.Topics.Any(x=>x.Id==t.Id))candidate.Topics.Add(new Topic{Id=t.Id,Name=t.Name,ParentId=t.ParentId,Color=t.Color});
  foreach(var note in incoming.Notes)if(!candidate.Notes.Any(x=>x.Id==note.Id))candidate.Notes.Add(note.Clone());
  Normalize(candidate);return candidate;
 }
 public static int ImportLegacy(Notebook n,string path){
  if(n.LegacyImported)return 0;int added=0;
  if(File.Exists(path)){
   var doc=new XmlDocument{XmlResolver=null};using(var reader=XmlReader.Create(path,new XmlReaderSettings{DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null}))doc.Load(reader);
   foreach(XmlNode old in doc.SelectNodes("/Notebook/Notes/Note")){
    Func<string,string> v=key=>old.SelectSingleNode(key)==null?"":old.SelectSingleNode(key).InnerText;
    string id=v("Id");if(n.Notes.Any(x=>x.Id==id))continue;DateTime created,due;bool done,notified;
    var note=new Note{Id=id==""?Guid.NewGuid().ToString("N"):id,Title=v("Title"),Body=v("Body"),Done=bool.TryParse(v("Done"),out done)&&done,Notified=bool.TryParse(v("Notified"),out notified)&&notified,TopicId="inbox"};
    if(DateTime.TryParse(v("CreatedUtc"),null,System.Globalization.DateTimeStyles.RoundtripKind,out created))note.CreatedUtc=created.ToUniversalTime();
    if(DateTime.TryParse(v("DueUtc"),null,System.Globalization.DateTimeStyles.RoundtripKind,out due)){note.DueUtc=due.ToUniversalTime();note.RemindUtc=note.DueUtc;note.ReminderOn=true;}
    n.Notes.Add(note);added++;
   }
   var scratch=doc.SelectSingleNode("/Notebook/Scratch");if(scratch!=null&&!string.IsNullOrWhiteSpace(scratch.InnerText)){n.Notes.Add(new Note{Title="旧版随手记",Body=scratch.InnerText});added++;}
  }
  n.LegacyImported=true;return added;
 }
}
