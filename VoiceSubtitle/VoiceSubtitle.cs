using Fiddler;
using Grabacr07.KanColleViewer.Composition;
using Grabacr07.KanColleViewer.Models;
using System.ComponentModel.Composition;
using System.Text.RegularExpressions;
using Session = Fiddler.Session;


namespace VoiceSubtitle
{
    [Export(typeof(IPlugin))]
    [ExportMetadata("Guid", "5A87796A-4C1A-4698-A7D6-9F4CF866AADC")]
    [ExportMetadata("Title", "VoiceSubtitle")]
    [ExportMetadata("Description", "KCV字幕插件")]
    [ExportMetadata("Version", "0.9")]
    [ExportMetadata("Author", "kikiwalu")]

    public class VoiceSubtitle : IPlugin
    {
        static bool isInitialized = false;

        public static VoiceData Data = new VoiceData();

        //public static Cache cache;

        public VoiceSubtitle()
        {
            Initialize();
        }

        public void Initialize()
        {
            if (isInitialized) return;
            isInitialized = true;

            StatusService.Current.Notify("加载KCV语音字幕插件");

            //初始化字幕库
            Data.Init();

            _AppendToFiddler();
        }

        static void _AppendToFiddler()
        {
            FiddlerApplication.BeforeRequest += _BeforeRequest;
            FiddlerApplication.BeforeResponse += _BeforeResponse;
            FiddlerApplication.AfterSessionComplete += _AfterComplete;
        }

        static bool _Filter(Session oSession)
        {
            return oSession.PathAndQuery.StartsWith("/kcs/");
        }

        static void _BeforeRequest(Session oSession)
        {
            Regex reg = new Regex("sound/kc(.*?)/(.*?).mp3");
            Match match = reg.Match(oSession.fullUrl);
            if (match.Success && match.Groups.Count == 3)
            {
                oSession.bBufferResponse = true;
            }
        }

        //This event fires when a server response is received by Fiddler
        static void _BeforeResponse(Session oSession)
        {
            //if (!_Filter(oSession)) return;
            try
            {
                Regex reg = new Regex("sound/kc(.*?)/(.*?).mp3");
                Match match = reg.Match(oSession.fullUrl);
                if (match.Success && match.Groups.Count == 3)
                {
                    oSession.oResponse.headers["Pragma"] = "no-cache";
                    oSession.oResponse.headers["Cache-Control"] = "no-cache";
                    //StatusService.Current.Set("shipCode: " + match.Groups[1].Value + " voiceId: " + match.Groups[2].Value);
                    string voice = Data.GetVoice(match.Groups[1].Value, int.Parse(match.Groups[2].Value));
                    //StatusService.Current.Set("voice: " + voice);
                    if (voice != null)
                    {
                        StatusService.Current.Set(voice);
                    }
                }
            }
            catch
            {
                StatusService.Current.Notify("语音字幕加载失败！");
            }
           
        }

        static void _AfterComplete(Session oSession)
        {
            
        }

    }
}
