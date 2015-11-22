using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Xml;

using Advanced_Combat_Tracker;
using System.Reflection;
using System.IO;
using System.Threading;

namespace PSO2ACT
{
    public class PSO2ACT : IActPluginV1
    {
        config Config;
        Label lblStatus;
        string settingsFile = Path.Combine(ActGlobals.oFormActMain.AppDataFolder.FullName, "Config\\PluginSample.config.xml");
        SettingsSerializer xmlSettings;
        Thread logMonitor, mainThread;
        Queue<string> queueActions = new Queue<string>();

        public void InitPlugin(TabPage pluginScreenSpace, Label pluginStatusText)
        {
            Config = new config();
            lblStatus = pluginStatusText;
            pluginScreenSpace.Controls.Add(Config);
            xmlSettings = new SettingsSerializer(Config);
            LoadSettings();
            Config.selectedFolder = Config.Controls["directory"].Text;
            lblStatus.Text = "Loaded PSO2ACT Plugin";
            logMonitor = new Thread(this.LogMonitor);
            logMonitor.Start();
            mainThread = new Thread(this.MainThread);
            mainThread.Start();
            return;
        }

        struct Action
        {
            public uint timestamp;
            public ushort instanceID;
            public uint sourceID;
            public string sourceName;
            public uint targetID;
            public string targetName;
            public uint attackID;
            public int damage;
            public bool isJA;
            public bool isCrit;
            public bool isMultiHit;
            public bool isMisc;
            public bool isMisc2;
        }

        private void MainThread()
        {
            ushort currInstID = 0xFFFF;

            while (true)
            {
                //timestamp, instanceID, sourceID, sourceName, targetID, targetName, attackID, damage, IsJA, IsCrit, IsMultiHit, IsMisc, IsMisc2
                while (queueActions.Count > 0)
                {
                    string strAction = queueActions.Dequeue();
                    if (strAction == null)
                        continue;
                    Action aAction = new Action();
                    string[] tmp = strAction.Split(',');

                    if (tmp[0].Equals("timestamp"))
                        continue;
                    try
                    {
                        aAction.timestamp = Convert.ToUInt32(tmp[0]);
                        aAction.instanceID = Convert.ToUInt16(tmp[1]);
                        aAction.sourceID = Convert.ToUInt32(tmp[2]);
                        aAction.sourceName = tmp[3];
                        aAction.targetID = Convert.ToUInt32(tmp[4]);
                        aAction.targetName = tmp[5];
                        aAction.attackID = Convert.ToUInt32(tmp[6]);
                        aAction.damage = Convert.ToInt32(tmp[7]);
                        aAction.isJA = (Convert.ToInt32(tmp[8]) == 1);
                        aAction.isCrit = (Convert.ToInt32(tmp[9]) == 1);
                        aAction.isMultiHit = (Convert.ToInt32(tmp[10]) == 1);
                        aAction.isMisc = (Convert.ToInt32(tmp[11]) == 1);
                        aAction.damage = Convert.ToInt32(tmp[7]);
                        aAction.isJA = (Convert.ToInt32(tmp[8]) == 1);
                        aAction.isCrit = (Convert.ToInt32(tmp[9]) == 1);
                        aAction.isMultiHit = (Convert.ToInt32(tmp[10]) == 1);
                        aAction.isMisc = (Convert.ToInt32(tmp[11]) == 1);
                        aAction.isMisc2 = (Convert.ToInt32(tmp[12]) == 1);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show("Error: " + ex.Message + "\n" + strAction);
                        continue;
                    }

                    if (aAction.targetID == 0 ||
                        (aAction.instanceID == 0 && currInstID == 0xFFFF)
                        ) //TODO: deal with when the first thing they do is counter
                        continue;


                    if (aAction.instanceID == 0)
                        aAction.instanceID = currInstID;

                    System.DateTime dateTime = new System.DateTime(1970, 1, 1, 0, 0, 0, 0);
                    dateTime = dateTime.AddSeconds(aAction.timestamp);
                    SwingTypeEnum e;
                    if (aAction.damage < 0 && aAction.isMisc)
                        e = SwingTypeEnum.Healing;
                    else
                        e = SwingTypeEnum.Melee;
                    Dnum dmg = new Dnum(aAction.damage);
                    MasterSwing ms = new MasterSwing(Convert.ToInt32(e), 
                        aAction.isCrit, 
                        dmg, 
                        dateTime, 
                        Advanced_Combat_Tracker.ActGlobals.oFormActMain.GlobalTimeSorter, 
                        aAction.attackID.ToString(), 
                        aAction.sourceName, 
                        aAction.attackID.ToString(), 
                        //aAction.targetID.ToString()
                        aAction.targetName
                        );


                    if (aAction.instanceID != currInstID)
                    {
                        ActGlobals.oFormActMain.EndCombat(true);
                        if(ActGlobals.oFormActMain.SetEncounter(dateTime, aAction.sourceName, aAction.target))
                            ActGlobals.oFormActMain.AddCombatAction(ms);
                        currInstID = aAction.instanceID;
                    } 
                    else if (ActGlobals.oFormActMain.InCombat)
                        ActGlobals.oFormActMain.AddCombatAction(ms);



                }

                if (Config.refreshFlag)
                {
                    queueActions.Clear();
                    refreshLog();
                    Config.refreshFlag = false;
                }
            }
        }

        public void DeInitPlugin()
        {
            logMonitor.Abort();
            mainThread.Abort();
            SaveSettings();
            lblStatus.Text = "Unloaded PSO2ACT Plugin";
            return;
        }

        private void LogMonitor()
        {
            while (true)
            {

                string dir = String.Format(@"{0}\damagelogs", Config.selectedFolder);
                Config.Controls["lblLogFile"].Text = String.Format("Reading {0}", dir);
                if (!Directory.Exists(dir))
                {
                    continue;
                }

                DirectoryInfo dirInfo = new DirectoryInfo(dir);
                FileInfo file = (from f in dirInfo.GetFiles("*.csv") orderby f.LastWriteTime descending select f).First();
                Config.Controls["lblLogFile"].Text = String.Format("Reading {0}", file.Name);

                FileStream fs = new FileStream(file.FullName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                var wh = new AutoResetEvent(false);
                var fsw = new FileSystemWatcher(dir);
                fsw.Filter = file.FullName;
                fsw.Changed += (s, e) => wh.Set();
                using (var sr = new StreamReader(fs, Encoding.UTF8))
                {
                    string s = string.Empty;
                    while (true)
                    {
                        s = sr.ReadLine();
                        uint result;
                        if (s != null)
                        {
                            if (!uint.TryParse(s[0].ToString(), out result))
                                continue;
                            queueActions.Enqueue(s);
                        }
                        else
                        {
                            wh.WaitOne(1000);
                        }
                    }
                }

            }
        }

        public void refreshLog()
        {
            logMonitor.Abort();
            logMonitor = new Thread(LogMonitor);
            logMonitor.Start();
        }

        void LoadSettings()
        {
            xmlSettings.AddControlSetting("directory", Config.Controls["directory"]);

            if (File.Exists(settingsFile))
            {
                FileStream fs = new FileStream(settingsFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                XmlTextReader xReader = new XmlTextReader(fs);

                try
                {
                    while (xReader.Read())
                    {
                        if (xReader.NodeType == XmlNodeType.Element)
                        {
                            if (xReader.LocalName == "SettingsSerializer")
                            {
                                xmlSettings.ImportFromXml(xReader);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    lblStatus.Text = "Error loading settings: " + ex.Message;
                }
                xReader.Close();
            }
        }

        void SaveSettings()
        {
            FileStream fs = new FileStream(settingsFile, FileMode.Create, FileAccess.Write, FileShare.ReadWrite);
            XmlTextWriter xWriter = new XmlTextWriter(fs, Encoding.UTF8);
            xWriter.Formatting = Formatting.Indented;
            xWriter.Indentation = 1;
            xWriter.IndentChar = '\t';
            xWriter.WriteStartDocument(true);
            xWriter.WriteStartElement("Config");	// <Config>
            xWriter.WriteStartElement("SettingsSerializer");	// <Config><SettingsSerializer>
            xmlSettings.ExportToXml(xWriter);	// Fill the SettingsSerializer XML
            xWriter.WriteEndElement();	// </SettingsSerializer>
            xWriter.WriteEndElement();	// </Config>
            xWriter.WriteEndDocument();	// Tie up loose ends (shouldn't be any)
            xWriter.Flush();	// Flush the file buffer to disk
            xWriter.Close();
        }

    }
}
