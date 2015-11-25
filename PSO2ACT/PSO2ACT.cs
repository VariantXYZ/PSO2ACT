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
        string settingsFile = Path.Combine(ActGlobals.oFormActMain.AppDataFolder.FullName, "Config\\PluginPSO2.config.xml");
        SettingsSerializer xmlSettings;
        Queue<string> queueActions = new Queue<string>();
        static ushort currInstID = 0xFFFF;
        Thread logThread;

        struct Skill
        {
            public string Name;
            public string Type;
            public string Comment;
        }

        Dictionary<uint, Skill> skillDict = new Dictionary<uint, Skill>();

        public void DeInitPlugin()
        {
            SaveSettings();
            logThread.Abort();
            ActGlobals.oFormActMain.BeforeLogLineRead -= oFormActMain_BeforeLogLineRead;
            ActGlobals.oFormActMain.OnCombatEnd -= oFormActMain_OnCombatEnd;
            lblStatus.Text = "Plugin Exited";
        }

        public void InitPlugin(TabPage pluginScreenSpace, Label pluginStatusText)
        {
            Config = new config();
            lblStatus = pluginStatusText;
            pluginScreenSpace.Controls.Add(Config);
            xmlSettings = new SettingsSerializer(Config);
            LoadSettings();
            Config.selectedFolder = Config.Controls["directory"].Text;
            lblStatus.Text = "Loaded PSO2ACT Plugin";

            //if (ActGlobals.oFormActMain.GetAutomaticUpdatesAllowed())
            //    new Thread(new ThreadStart(oFormActMain_UpdateCheckClicked)).Start();

            ActGlobals.oFormActMain.GetDateTimeFromLog = ParseDateTime;
            ActGlobals.oFormActMain.BeforeLogLineRead += new LogLineEventDelegate(oFormActMain_BeforeLogLineRead);
            ActGlobals.oFormActMain.OnCombatEnd += new CombatToggleEventDelegate(oFormActMain_OnCombatEnd);

            Config.refreshFlag = true;
            logThread = new Thread(this.LogThread);
            logThread.Start();

            return;
        }

        private void LogThread()
        {
            while (true)
            {
                if (!Config.refreshFlag)
                    continue;
                Config.refreshFlag = false;
                string dir = String.Format(@"{0}\damagelogs", Config.selectedFolder);
                DirectoryInfo dirInfo = new DirectoryInfo(dir);
                if (!dirInfo.Exists)
                {
                    Config.Controls["lblLogFile"].Text = "damagelogs folder not found";
                    continue;
                }

                try
                {
                    FileStream f = File.Open(String.Format(@"{0}\skills.csv", Config.selectedFolder), FileMode.Open);
                    using (StreamReader sr = new StreamReader(f))
                    {
                        string line;
                        while ((line = sr.ReadLine()) != null)
                        {
                            string[] tmp = line.Split(',');
                            Skill s;
                            s.Name = tmp[0];
                            s.Type = tmp[2];
                            s.Comment = tmp[3];
                            if (skillDict.ContainsKey(Convert.ToUInt32(tmp[1])))
                            {
                                MessageBox.Show("Duplicate ID:  " + line);
                            }
                            skillDict.Add(Convert.ToUInt32(tmp[1]), s);
                        }
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message);
                }

                FileInfo file = (from f in dirInfo.GetFiles("*.csv") orderby f.LastWriteTime descending select f).FirstOrDefault();
                Config.Controls["lblLogFile"].Text = String.Format("Reading {0}", file.Name ?? "<NULL>");

                if (!file.Exists)
                {
                    Config.Controls["lblLogFile"].Text = "No logs";
                    continue;
                }

                ActGlobals.oFormActMain.LogFilePath = file.FullName;
                ActGlobals.oFormActMain.OpenLog(false, false);
            }
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



        void oFormActMain_OnCombatEnd(bool isImport, CombatToggleEventArgs encounterInfo)
        {
            currInstID = 0xFFFF;
        }

        void oFormActMain_BeforeLogLineRead(bool isImport, LogLineEventArgs logInfo)
        {
            Action aAction = new Action();
            string logLine = logInfo.logLine;
            string[] tmp = logInfo.logLine.Split(',');

            if (tmp[0].Equals("timestamp"))
                return;
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
                aAction.isMisc2 = (Convert.ToInt32(tmp[12]) == 1);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error: " + ex.Message + "\n" + logLine);
                return;
            }

            //TODO: deal with when the first thing they do is counter
            if (aAction.targetID == 0 ||
                (aAction.instanceID == 0 && currInstID == 0xFFFF))
                return;
            DateTime time = ActGlobals.oFormActMain.LastKnownTime;
            int gts = ActGlobals.oFormActMain.GlobalTimeSorter;
            if (aAction.instanceID == 0)
                aAction.instanceID = currInstID;
            SwingTypeEnum e;



            string sourceName = aAction.sourceName + "_" + aAction.sourceID.ToString();
            string targetName = aAction.targetName + "_" + aAction.targetID.ToString();


            string actionType = aAction.attackID.ToString();
            string damageType = aAction.attackID.ToString();

            if (aAction.damage < 0 && aAction.isMisc)
                e = SwingTypeEnum.Healing;
            else
                e = SwingTypeEnum.Melee;

            Dnum dmg = new Dnum(aAction.damage) * ((e == SwingTypeEnum.Healing) ? -1:1);

            if(skillDict.ContainsKey(aAction.attackID))
            {
                actionType = skillDict[aAction.attackID].Name;
                damageType = skillDict[aAction.attackID].Type;
            }


            MasterSwing ms = new MasterSwing(
                Convert.ToInt32(e),
                aAction.isCrit,
                "",
                dmg,
                time,
                gts,
                actionType,
                sourceName,
                damageType,
                targetName
                );

            if (aAction.instanceID != currInstID)
            {
                currInstID = aAction.instanceID;
                ActGlobals.oFormActMain.ChangeZone(aAction.instanceID.ToString());
            }

            if (ActGlobals.oFormActMain.SetEncounter(time, aAction.sourceName, aAction.targetName))
                ActGlobals.oFormActMain.AddCombatAction(ms);

        }


        DateTime ParseDateTime(string logLine)
        {
            string[] tmp = logLine.Split(',');
            if (tmp[0] == "timestamp")
                return DateTime.MinValue;
            System.DateTime dateTime = new System.DateTime(1970, 1, 1, 0, 0, 0, 0);
            dateTime = dateTime.AddSeconds(Convert.ToUInt32(tmp[0]));
            return dateTime;
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

        //void oFormActMain_UpdateCheckClicked()
        //{
        //    int pluginId = 64;
        //    try
        //    {
        //        DateTime localDate = ActGlobals.oFormActMain.PluginGetSelfDateUtc(this);
        //        DateTime remoteDate = ActGlobals.oFormActMain.PluginGetRemoteDateUtc(pluginId);
        //        if (localDate.AddHours(2) < remoteDate)
        //        {
        //            DialogResult result = MessageBox.Show("There is an updated version of the EQ1 Parsing Plugin.  Update it now?\n\n(If there is an update to ACT, you should click No and update ACT first.)", "New Version", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
        //            if (result == DialogResult.Yes)
        //            {
        //                FileInfo updatedFile = ActGlobals.oFormActMain.PluginDownload(pluginId);
        //                ActPluginData pluginData = ActGlobals.oFormActMain.PluginGetSelfData(this);
        //                pluginData.pluginFile.Delete();
        //                updatedFile.MoveTo(pluginData.pluginFile.FullName);
        //                ThreadInvokes.CheckboxSetChecked(ActGlobals.oFormActMain, pluginData.cbEnabled, false);
        //                Application.DoEvents();
        //                ThreadInvokes.CheckboxSetChecked(ActGlobals.oFormActMain, pluginData.cbEnabled, true);
        //            }
        //        }
        //    }
        //    catch (Exception ex)
        //    {
        //        ActGlobals.oFormActMain.WriteExceptionLog(ex, "Plugin Update Check");
        //    }
        //}

    }
}
