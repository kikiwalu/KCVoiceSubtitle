using Grabacr07.KanColleViewer.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using d_f_32.KanColleCacher;

namespace VoiceSubtitle
{
    public class VoiceData
    {
        int[] voiceKey = new int[] { 604825, 607300, 613847, 615318, 624009, 631856, 635451, 637218, 640529, 643036, 652687, 658008, 662481, 669598, 675545, 685034, 687703, 696444, 702593, 703894, 711191, 714166, 720579, 728970, 738675, 740918, 743009, 747240, 750347, 759846, 764051, 770064, 773457, 779858, 786843, 790526, 799973, 803260, 808441, 816028, 825381, 827516, 832463, 837868, 843091, 852548, 858315, 867580, 875771, 879698, 882759, 885564, 888837, 896168 };
        Dictionary<int, Dictionary<int, int>> voiceMap = new Dictionary<int, Dictionary<int, int>>();
        Dictionary<String, String> shipDict = new Dictionary<String, String>();
        Dictionary<String, String> shipNameDict = new Dictionary<String, String>();

        public static bool UseThirdBuffer = false;

        private static string filePath;

        Voice voice = new Voice("简体中文", "http://api.kcwiki.moe/subtitles/diff/");

        Settings set = Settings.Current;

        public void Init()
        {
            filePath = set.CacheFolder;

            //初始化船只编号配置文件
            if (File.Exists(set.CacheFolder + "\\GraphList.txt"))
            {
                StreamReader sr = new StreamReader(set.CacheFolder + "\\GraphList.txt", Encoding.UTF8);
                String str = "";
                int cnt = 0;
                while ((str = sr.ReadLine()) != null)
                {
                    if (cnt > 1)
                    {
                        String[] line = str.Split(',');
                        shipDict.Add(line[3], line[1]);
                        shipNameDict.Add(line[1], line[2]);
                    }
                    cnt++;
                }
            }


            voice.LocalFile = set.CacheFolder + "\\Subtitles.json";

            FileStream fileStream = new FileStream(set.CacheFolder + "\\Subtitles.json", FileMode.Open, FileAccess.Read, FileShare.Read);
            voice.InternalData = new byte[fileStream.Length];
            fileStream.Read(voice.InternalData, 0, voice.InternalData.Length);
            fileStream.Close();
            

            JavaScriptSerializer Serializer = new JavaScriptSerializer();

            //初始化字幕文件
            if (File.Exists(voice.LocalFile))
            {
                var localjson = File.ReadAllText(voice.LocalFile);
                try
                {
                    var localdata = Serializer.DeserializeObject(localjson) as Dictionary<string, object>;
                    if (localdata != null)
                    {
                        voice.VoiceData = localdata;
                    }
                }
                catch
                {
                }
            }

            if (voice.VoiceData == null)
            {
                string s = Encoding.UTF8.GetString(voice.InternalData);
                voice.VoiceData = (Dictionary<string, object>)Serializer.DeserializeObject(s);
            }

            if (voice.CouldUpdate)
            {
                voice.Updating = true;
                Task.Factory.StartNew(UpdateVoice, voice);
            }

            for (int ShipID = 1; ShipID <= 500; ShipID++)
            {
                voiceMap[ShipID] = new Dictionary<int, int>();
                for (int i = 1; i < voiceKey.Length; i++)
                {
                    voiceMap[ShipID][ConvertFilename(ShipID, i)] = i;
                }
            }
        }

        int ConvertFilename(int ShipId, int VoiceId)
        {
            return (ShipId + 7) * 17 * (voiceKey[VoiceId] - voiceKey[VoiceId - 1]) % 99173 + 100000;
        }

        void CheckUpdate()
        {
            if (!voice.Updating && voice.CouldUpdate)
            {
                voice.Updating = true;
                if (System.DateTime.Now > voice.LastUpdateTime.AddHours(12))
                {
                    Task.Factory.StartNew(UpdateVoice, voice);
                }
            }
        }

        void UpdateVoice(object oi)
        {
            Voice voice = (Voice)oi;
            string LocalVer = voice.VoiceData["version"].ToString();
            
            StatusService.Current.Notify("开始更新字幕数据！");
            string Url = voice.UpdateUrl + LocalVer;
            WebRequest wReq = System.Net.WebRequest.Create(Url);
            // Get the response instance.
            WebResponse wResp = wReq.GetResponse();
            Stream respStream = wResp.GetResponseStream();
            var JavaScriptSerializer = new JavaScriptSerializer();
            int count = 0;
            using (System.IO.StreamReader reader = new System.IO.StreamReader(respStream))
            {
                var diffstring = reader.ReadToEnd();
                try
                {
                    var diff = JavaScriptSerializer.DeserializeObject(diffstring) as Dictionary<string, object>;
                    if (diff != null && diff.Count > 0)
                    {
                        lock (voice.VoiceData)
                        {
                            foreach (var shipdata in diff)
                            {
                                if (shipdata.Value is Dictionary<string, object>)
                                {
                                    var shipvoice = shipdata.Value as Dictionary<string, object>;
                                    count += shipvoice.Count;
                                    if (!voice.VoiceData.ContainsKey(shipdata.Key))
                                    {
                                        voice.VoiceData[shipdata.Key] = shipvoice;
                                    }
                                    else
                                    {
                                        var LocalVoices = voice.VoiceData[shipdata.Key] as Dictionary<string, object>;
                                        foreach (var Singlevoice in shipvoice)
                                        {
                                            LocalVoices[Singlevoice.Key] = Singlevoice.Value;
                                            //File.AppendAllText(@"d:\1.txt", voice.Key + ":" + voice.Value);
                                        }
                                    }
                                }
                                else
                                {
                                    voice.VoiceData[shipdata.Key] = shipdata.Value;
                                    //File.AppendAllText(@"d:\1.txt", shipdata.Key + ":" + shipdata.Value);
                                }
                            }
                        }
                    }
                }
                catch { count = -1; }
            }
            voice.LastUpdateTime = System.DateTime.Now;
            if (count == -1)
            {
                StatusService.Current.Notify(string.Format("{0}字幕数据更新出现错误,稍后会再次检查更新", voice.Language));
               
            }
            else if (count == 0)
            {
                StatusService.Current.Notify(string.Format("{0}字幕数据检查更新完成,无需更新", voice.Language));
                
            }
            else
            {
                StatusService.Current.Notify(string.Format("{2}字幕数据更新完成({0}),更新了({1})条语音", voice.VoiceData["version"], count, voice.Language));
                
                File.WriteAllText(voice.LocalFile, JavaScriptSerializer.Serialize(voice.VoiceData));
            }
            voice.Updating = false;
        }

        public string GetVoice(int shipid, int voiceID)
        {
            CheckUpdate();
            StringBuilder builder = new StringBuilder();
            var ShipName = shipNameDict["" + shipid];
            builder.Append("[" + ShipName + "]: ");
            string chsSubtitle;
            chsSubtitle = GetVoice(shipid, voiceID, voice);
            if (string.IsNullOrWhiteSpace(chsSubtitle))
                return null;
            builder.Append(chsSubtitle); ;
            return builder.ToString();
        }

        public string GetVoice(int shipid, int voiceID, Voice voice)
        {
            lock (voice)
            {
                if (voiceMap.ContainsKey(shipid) && voice.VoiceData.ContainsKey(shipid.ToString()))
                {
                    string voiceid = voiceID.ToString();

                    var voices = (Dictionary<string, object>)voice.VoiceData[shipid.ToString()];
                    if (voices.ContainsKey(voiceid))
                    {
                        string text = voices[voiceid].ToString();
                        return text;
                    }
                }
            }
            return null;
        }


        public string GetVoice(string ShipCode, int FileName)
        {
            if (!shipDict.ContainsKey(ShipCode))
                return null;
            int shipid =  int.Parse(shipDict[ShipCode]);
            //StatusService.Current.Set("shipId: " + shipid);
            if (voiceMap.ContainsKey(shipid) && voiceMap[shipid].ContainsKey(FileName))
                return GetVoice(shipid, voiceMap[shipid][FileName]);
            return null;
        }
    }

    public class Voice
    {
        public string Language { get; set; }
        public string UpdateUrl { get; set; }
        public Dictionary<string, object> VoiceData { get; set; }
        public bool Updating = false;
        public bool CouldUpdate = true;
        public System.DateTime LastUpdateTime { get; set; }
        public string LocalFile { get; set; }
        public byte[] InternalData;

        public Voice(string language, string updateUrl)
        {
            Language = language;
            UpdateUrl = updateUrl;
        }
    }
}
