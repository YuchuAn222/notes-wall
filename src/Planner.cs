using System;
using System.Collections.Generic;
using System.Linq;

[assembly: System.Reflection.AssemblyFileVersion("1.1.0.0")]

public class Step {
    public string Name { get; set; }
    public bool Done { get; set; }
}
public class Material {
    public string Name { get; set; }
    public string Target { get; set; }
}
public class Alarm {
    public int Minutes;
    public DateTime At;
    public bool Acknowledged;
    public Alarm Copy() { return (Alarm)MemberwiseClone(); }
}

static class Planner {
    public static readonly string[] Palette = { "yellow","pink","blue","mint","purple","peach","apricot","lime","aqua","denim","rose","stone" };
    public static readonly string[] Names = { "鹅黄","浅粉","雾蓝","薄荷","淡紫","蜜桃","杏橙","青柠","湖绿","海蓝","玫瑰","暖灰" };
    public static readonly string[] Hex = { "#FFF0B8","#F9E1E8","#DFEBFA","#DDEEE4","#E8E0F5","#FFDCC7","#FFE5AC","#E6EEC4","#CDECE9","#CEDEEF","#F2CFD6","#E7E3DD" };

    public static string Color(Notebook book, string topic, string color) {
        while (color == "auto") {
            var parent = book.Topics.FirstOrDefault(t => t.Id == topic);
            if (parent == null) return "yellow";
            color = parent.Color;
            topic = parent.ParentId;
        }
        return Palette.Contains(color) ? color : "yellow";
    }
    public static string Progress(Note note) {
        if (note.Steps.Count == 0) return "";
        var next = note.Steps.FirstOrDefault(s => !s.Done);
        return note.Steps.Count(s => s.Done) + "/" + note.Steps.Count + " · " +
               (next == null ? "步骤已全部勾选" : "下一步：" + next.Name);
    }
    public static void Upgrade(Note note) {
        if (!note.ReminderOn || note.Alarms.Count != 0 || !note.RemindUtc.HasValue) return;
        var original = note.RemindUtc.Value;
        int lead = note.DueUtc.HasValue ? (int)Math.Round((note.DueUtc.Value-original).TotalMinutes) : 0;
        note.Alarms.Add(new Alarm { Minutes=lead, At=original });
    }
    public static void Schedule(Note edited, Note old, IEnumerable<int> leads) {
        edited.Alarms.Clear();
        if (edited.ReminderOn && edited.DueUtc.HasValue) {
            foreach (var lead in leads.Distinct().OrderByDescending(v => v)) {
                var previous = old == null ? null : old.Alarms.FirstOrDefault(a => a.Minutes == lead);
                edited.Alarms.Add(previous != null && old.DueUtc == edited.DueUtc
                    ? previous.Copy() : new Alarm { Minutes=lead, At=edited.DueUtc.Value.AddMinutes(-lead) });
            }
        }
        Sync(edited);
    }
    public static void Sync(Note note) {
        var next = note.Alarms.Where(a => !a.Acknowledged).OrderBy(a => a.At).FirstOrDefault();
        note.RemindUtc = next == null ? (DateTime?)null : next.At;
        note.Notified = next == null;
    }
    public static IEnumerable<Alarm> Pending(Note note, DateTime now) {
        if (note.Deleted || note.Done || !note.ReminderOn) return Enumerable.Empty<Alarm>();
        Upgrade(note);
        return note.Alarms.Where(a => !a.Acknowledged && a.At <= now).OrderBy(a => a.At);
    }
    public static List<Note> Day(IEnumerable<Note> notes, DateTime localDate) {
        return notes.Where(n => !n.Deleted && n.DueUtc.HasValue && n.DueUtc.Value.ToLocalTime().Date == localDate.Date)
            .OrderBy(n => n.DueUtc).ThenBy(n => n.Title).ToList();
    }
    public static DateTime Monday(DateTime date) {
        return date.Date.AddDays(-(((int)date.DayOfWeek + 6) % 7));
    }
}
