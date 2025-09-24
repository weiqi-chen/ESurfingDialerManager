using System;
using System.Collections.Generic;
using System.Diagnostics.Eventing.Reader;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;

namespace ESurfingDialerManager;

internal class WindowsEventListener
{
    public bool Enabled => watcher.Enabled;
    public WindowsEventListener()
    {
        string query = "*[System[(EventID=10000 or EventID=10001)]]";
        logQuery = new EventLogQuery("Microsoft-Windows-NetworkProfile/Operational", PathType.LogName, query);
        watcher = new EventLogWatcher(logQuery);
        watcher.EventRecordWritten += Watcher_EventRecordWritten;
    }
    public void StartListen()
    {
        watcher.Enabled = true;
    }
    public void Stop()
    {
        watcher.Enabled = false;
    }
    public class WifiEventArgs : EventArgs
    {
        public string ProfileName { get; set; }
        public string Action { get; set; }
    }

    public event EventHandler<WifiEventArgs> WifiEventOccurred;


    protected void Watcher_EventRecordWritten(object sender, EventRecordWrittenEventArgs e)
    {
        XmlDocument doc = new XmlDocument();
        doc.LoadXml(e.EventRecord.ToXml());

        XmlNamespaceManager nsmgr = new XmlNamespaceManager(doc.NameTable);
        nsmgr.AddNamespace("e", "http://schemas.microsoft.com/win/2004/08/events/event");

        string eventId = doc.SelectSingleNode("//e:System/e:EventID", nsmgr).InnerText;
        string name = doc.SelectSingleNode("//e:EventData/e:Data[@Name='Name']", nsmgr).InnerText;

        if (eventId == "10000")
        {
            WifiEventOccurred?.Invoke(this, new WifiEventArgs { ProfileName = name, Action = "Connected" });
        }
        else if (eventId == "10001")
        {
            WifiEventOccurred?.Invoke(this, new WifiEventArgs { ProfileName = name, Action = "Disconnected" });
        }
    }

    protected EventLogQuery logQuery;
    protected EventLogWatcher watcher;
}

