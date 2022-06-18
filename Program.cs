using System.Text.Json;

namespace YTnew
{
   class YTNew
   {
      // --------------------------------------------------------------------------------------------------------------------------------

      private enum cmd { none, add, remove, list, generate, version, usage, };
      private const string version = "1.0.1a";
      private ColorConsoleLog log = new();
      private string databasePath = @".\YTnew.database.json";
      private string shortcutPath = @".\";
      private bool dryRun, writeLink;

      // --------------------------------------------------------------------------------------------------------------------------------

      [STAThread]
      public static int Main(string[] args) => new YTNew().Run(args);

      // --------------------------------------------------------------------------------------------------------------------------------

      private int Run(string[] args)
      {
         var (cmd, param) = GetOptions(args);
         switch (cmd)
         {
            case cmd.add: AddChannel(param); break;
            case cmd.remove: RemoveChannel(param); break;
            case cmd.list: List(); break;
            case cmd.generate: Generate(); break;
            case cmd.version: PrintVersion(); break;
            case cmd.usage: PrintUsage(); break;
            default: break;
         }
         return 0;
      }

      // --------------------------------------------------------------------------------------------------------------------------------

      private (cmd, string) GetOptions(string[] args)
      {
         // check if verbose logging is enabled
         for (var i = 0; i < args.Length; i++) { if (args[i] == "-v" || args[i] == "--verbose") { log.LogVerbose = true; log.Verbose($"Enable verbose logging"); args[i] = ""; break; }; }

         // check command
         var c = cmd.none;
         var idxPrm = -1; string p = "";
         for (var i = 0; i < args.Length; i++)
         {
            if (args[i] == "add") { c = cmd.add; idxPrm = i + 1; }
            if (args[i] == "remove") { c = cmd.remove; idxPrm = i + 1; }
            if (args[i] == "list") c = cmd.list;
            if (args[i] == "generate") c = cmd.generate;
            if (args[i] == "version" || args[i] == "--version" || args[i] == "-v") c = cmd.version;
            if (args[i] == "help" || args[i] == "--help" || args[i] == "-h" || args[i] == "/?") c = cmd.usage;
            if (c != cmd.none) { args[i] = ""; break; }
         }
         if (c == cmd.none) c = cmd.generate; //default
         if (c == cmd.add || c == cmd.remove)      // check paramters for add/remove
         {
            if (idxPrm >= args.Length || args[idxPrm] == "") { log.Error("Missing arguments."); return (cmd.none, ""); } // none?
            p = args[idxPrm]; args[idxPrm] = "";   // clear detected params
            log.Verbose($"Arguments for add/remove is '{p}'.");
         }

         // check database
         if (c == cmd.add || c == cmd.remove || c == cmd.generate || c == cmd.list)
         {
            // check if environment variable is set
            var dbPath = Environment.GetEnvironmentVariable("YTNEW_DATABASE");
            if (dbPath != null) { databasePath = dbPath; log.Verbose($"ENV:YTNEW_DATABASE detected, set database to '{dbPath}'"); }
            // check arg
            for (var i = 0; i < args.Length; i++)
            {
               if (args[i] == "--database" || args[i] == "-d")
               {
                  args[i] = ""; i++;
                  if (i >= args.Length || args[i] == "") { log.Error("Missing arguments."); return (cmd.none, ""); }
                  databasePath = args[i]; args[i] = "";
                  log.Verbose($"Database set to: '{databasePath}'");
                  break;
               }
            }

            // check database exists
            if (c != cmd.add && !File.Exists(databasePath)) { log.Error($"Database: '{databasePath}' does not exist."); return (cmd.none, ""); }
         }

         // check args for dryrun
         if (c == cmd.add || c == cmd.remove || c == cmd.generate)
         {
            for (var i = 0; i < args.Length; i++)
            {
               if (args[i] == "--dryrun")
               {
                  dryRun = true; log.Verbose($"Enable dry run"); args[i] = "";
                  break;
               };
            }
         }

         // check arg for shortcut
         if (c == cmd.generate)
            for (var i = 0; i < args.Length; i++)
            {
               if (args[i] == "--shortcut" || args[i] == "-s")
               {
                  writeLink = true; args[i] = ""; i++;
                  if (i < args.Length && args[i] != "") { shortcutPath = args[i]; args[i] = ""; if (!Directory.Exists(shortcutPath)) { log.Error($"Invalid path: '{shortcutPath}'."); return (cmd.none, ""); } }
                  log.Verbose($"Enable writing shortcuts to '{shortcutPath}'");
                  break;
               }
            }

         // any now unprocessed arguments are invalid
         for (var i = 0; i < args.Length; i++) { if (args[i] != "") { log.Error($"Invalid option: {args[i]}."); return (cmd.none, ""); } }
         return (c, p);
      }

      // --------------------------------------------------------------------------------------------------------------------------------

      // Add (try to) given Url to the database
      private void AddChannel(string url)
      {
         log.Verbose($"Add channel: {url}");
         var c = ProcessUrl(url);
         if (c == null) return;
         var yt = LoadDatabase();
         if (yt.FirstOrDefault(c2 => c2.channel == c.channel) != null || yt.FirstOrDefault(c2 => c2.url == c.url) != null) { log.Error($"Channel '{c.channel}' is already in the database."); return; }
         Console.WriteLine($"Added '{c.channel}' to database");
         SaveDatabase(yt.Concat(new[] { c }).ToArray());
      }

      // --------------------------------------------------------------------------------------------------------------------------------

      // Check if Url is to youtube, download about page of channel, or download video page, and get channels title and link
      private yt? ProcessUrl(string url)
      {
         // strip leading designator
         if (url.StartsWith("http://", StringComparison.InvariantCultureIgnoreCase)) url = url[7..];
         if (url.StartsWith("https://", StringComparison.InvariantCultureIgnoreCase)) url = url[8..];
         // convert direct video link to full url
         if (url.StartsWith("youtu.be/", StringComparison.InvariantCultureIgnoreCase)) url = "www.youtube.com/watch?v=" + url[9..];
         // is it a youtube url?
         if (!url.StartsWith("www.youtube.com", StringComparison.InvariantCultureIgnoreCase) && !url.StartsWith("youtube.com", StringComparison.InvariantCultureIgnoreCase)) { log.Error("Expected URL to youtube.com"); return null; }
         url = url.Replace("www.", "", StringComparison.InvariantCultureIgnoreCase).Replace("youtube.com", "", StringComparison.InvariantCultureIgnoreCase);
         // detect link type
         if (url.StartsWith("/c/") || url.StartsWith("/channel/") || url.StartsWith("/user/"))
         {
            var split = url.Split('/');
            if (string.IsNullOrWhiteSpace(split[2])) { log.Error("Expected URL to a youtube video or channel."); return null; }
            log.Verbose($"Detected channel '{split[2]}'");
            return DownloadChannelAbout(split[1] + '/' + split[2]);
         }
         if (url.StartsWith("/watch?v="))
         {
            var split = url.Split('=');
            log.Verbose($"Detected video '{split[1]}'");
            return DownloadChannelVideo(split[1]);
         }

         log.Error("Expected URL to a youtube video or channel.");
         return null;
      }

      // --------------------------------------------------------------------------------------------------------------------------------

      // Download the channels about page and parse to find Title, return a yt-instance on succes
      private yt? DownloadChannelAbout(string url)
      {
         try
         {
            log.Verbose($"Download channel about page '{url}'");
            using var client = new HttpClient();
            client.DefaultRequestHeaders.Add("Accept-Language", "en");
            var response = client.GetAsync($"https://www.youtube.com/{url}/about").Result;
            var html = response.Content.ReadAsStringAsync().Result;

            const string tokenUrl = "\"canonicalChannelUrl\":";
            const string tokenTitle = "\"title\":{\"simpleText\":\"";

            var start = html.IndexOf(tokenUrl, 0);
            if (start == -1) { log.Error("token not found."); return null; }
            start = html.IndexOf(tokenTitle, start);
            if (start == -1) { log.Error("token not found."); return null; }
            start += tokenTitle.Length;
            var end = html.IndexOf("\"", start + 1);
            if (end == -1) { log.Error("token not found."); return null; }
            var title = html.Substring(start, end - start);
            log.Verbose($"Channel Title: {title}");
            return new yt { url = url, channel = title, last = DateTimeOffset.Now.Date };
         }
         catch (Exception ex)
         {
            log.Error($"Failed to process url '{url}': {ex.Message}");
            return null;
         }
      }

      // --------------------------------------------------------------------------------------------------------------------------------

      // Download a video page and parse to find Title and Url, return a yt-instance on succes
      private yt? DownloadChannelVideo(string video)
      {
         try
         {
            log.Verbose($"Download channel video '{video}'");
            using var client = new HttpClient();
            client.DefaultRequestHeaders.Add("Accept-Language", "en");
            var response = client.GetAsync($"https://www.youtube.com/watch?v={video}").Result;
            var html = response.Content.ReadAsStringAsync().Result;

            const string tokenUrl = "\"canonicalBaseUrl\":";

            var start = html.IndexOf(tokenUrl, 0);
            if (start == -1) { log.Error("token not found."); return null; }
            start += tokenUrl.Length + 2;
            var end = html.IndexOf("\"", start);
            if (end == -1) { log.Error("token not found."); return null; }
            var url = html.Substring(start, end - start);

            log.Verbose($"Channel Url: {url}");
            return DownloadChannelAbout(url);
         }
         catch (Exception ex)
         {
            log.Error($"Failed to process url '{video}': {ex.Message}");
            return null;
         }
      }

      // --------------------------------------------------------------------------------------------------------------------------------


      // Remove a channel from the users database
      private void RemoveChannel(string channel)
      {
         var yt = LoadDatabase();
         log.Verbose($"Remove channel: {channel}");
         var ch = yt.FirstOrDefault(c => c.channel.Equals(channel, StringComparison.InvariantCultureIgnoreCase));
         if (ch == null) { log.Warning($"Channel {channel} not in database."); return; }
         var yt2 = yt.Except(new[] { ch }).ToArray();
         Console.WriteLine($"Removed {channel} from database");
         SaveDatabase(yt2);
      }

      // --------------------------------------------------------------------------------------------------------------------------------

      // List all channels in the users database
      private void List()
      {
         var yt = LoadDatabase();
         if (!yt.Any()) { Console.WriteLine("Empty"); return; }
         var width1 = yt.Max(c => c.channel.Length);
         var width2 = yt.Max(c => c.url.Length);
         foreach (var c in yt.OrderBy(c => c.channel))
            Console.WriteLine($"Channel: {c.channel.PadRight(width1)}  URL: {c.url.PadRight(width2)}  Checked: {c.last.ToLocalTime():d}");
      }

      // --------------------------------------------------------------------------------------------------------------------------------

      // Generate link-urls for all new videos on the channels in the users database
      private void Generate()
      {
         var yt = LoadDatabase();
         if (!yt.Any()) { Console.WriteLine("Empty"); return; }
         foreach (var c in yt)
         {
            try
            {
               using var client = new HttpClient();
               client.DefaultRequestHeaders.Add("Accept-Language", "en");
               var response = client.GetAsync($"https://www.youtube.com/{c.url}/videos").Result;
               ParseVideo(c, response.Content.ReadAsStringAsync().Result);
            }
            catch (Exception ex)
            {
               log.Error($"Failed to process channel '{c.channel}': {ex.Message}");
               return;
            }
         }

         log.Verbose("Done");
         SaveDatabase(yt);
      }

      // --------------------------------------------------------------------------------------------------------------------------------

      private void PrintVersion() => Console.WriteLine($"YTnew version v{version}");

      // --------------------------------------------------------------------------------------------------------------------------------

      private void PrintUsage()
      {
         Console.WriteLine("usage: YTnew <command> [options]");
         Console.WriteLine();
         Console.WriteLine("available commands:");
         Console.WriteLine("   add <URL>                    add channel to database");
         Console.WriteLine("   remove <channel>             remove channel from database");
         Console.WriteLine("   list                         list all channels in database");
         Console.WriteLine("   [generate]                   check for new videos");
         Console.WriteLine("   version | --version | -v     print version information");
         Console.WriteLine("   help | --help | -h | /?      print usage information");
         Console.WriteLine();
         Console.WriteLine("available options:");
         Console.WriteLine("   [-d=<path>]                specify database, default=.\\YTnew.json");
         Console.WriteLine("   [--verbose]                enable verbose logging to console");
         Console.WriteLine("   [--dryrun]                 do not update database");
         Console.WriteLine("   [--shortcut [path]]        write URL shortcut files");
      }

      // --------------------------------------------------------------------------------------------------------------------------------

      private yt[] LoadDatabase()
      {
         if (!File.Exists(databasePath)) { log.Warning($"Database: '{databasePath}' does not exist."); return Array.Empty<yt>(); }
         log.Verbose($"Loading database from '{databasePath}'");
         try
         {
            var yt = JsonSerializer.Deserialize<yt[]>(File.ReadAllText(databasePath));
            if (yt == null || yt.Length == 0) { log.Warning($"Database empty."); return Array.Empty<yt>(); }
            log.Verbose($"Load database: {yt.Length} entries");
            return yt;
         }
         catch (Exception ex)
         {
            log.Error($"Failed to load database: {ex.Message}");
            return Array.Empty<yt>();
         }
      }

      // --------------------------------------------------------------------------------------------------------------------------------

      private void SaveDatabase(yt[] yt)
      {
         if (dryRun) return;
         try
         {
            log.Verbose($"Saving database to '{databasePath}'");
            File.WriteAllText(databasePath, JsonSerializer.Serialize(yt));
         }
         catch (Exception ex)
         {
            log.Error($"Failed to save database: {ex.Message}");
         }
      }

      // --------------------------------------------------------------------------------------------------------------------------------

      // Parse the html text of the channels videos page, detect new videos by upload time (xx ago) and last saved video
      private void ParseVideo(yt c, string html)
      {
         const string tokenTitle = "\"title\":{\"runs\":[{\"text\":";
         const string tokenTime = "\"publishedTimeText\":{\"simpleText\":";
         const string tokenUrl = "\"webCommandMetadata\":{\"url\":\"";

         var dt = DateTimeOffset.Now;
         var idx = 0;
         var newVideo = "";
         while (true)
         {
            idx = html.IndexOf(tokenTitle, idx + 1);
            if (idx == -1) break;
            var start = idx + tokenTitle.Length + 1;
            idx = html.IndexOf("\"", start + 1);
            if (idx == -1) break;
            var title = html.Substring(start, idx - start);

            if (title.StartsWith("YouTube TV")) break;

            idx = html.IndexOf(tokenTime, idx + 1);
            if (idx == -1) break;
            start = idx + tokenTime.Length + 1;
            idx = html.IndexOf("\"", start + 1);
            if (idx == -1) break;
            var time = html.Substring(start, idx - start);
            time = time.Replace("Streamed ", "");

            var videoTime = dt - StringToOffset(time);
            if (videoTime <= c.last) break;

            idx = html.IndexOf(tokenUrl, idx + 1);
            if (idx == -1) break;
            start = idx + tokenUrl.Length + 1;
            idx = html.IndexOf("\"", start + 1);
            if (idx == -1) break;
            var video = html.Substring(start, idx - start);
            video = video.Replace("watch?v=", "");

            if (c.video == video) break;

            log.Verbose($"{c.channel} released '{title}' {time}");
            Console.WriteLine($"https://www.youtube.com/watch?v={video}");
            if (writeLink) WriteShortcut(c.channel, video, videoTime);
            if (newVideo.Length == 0) newVideo = video;
         }

         c.last = dt;
         if (newVideo.Length > 0) c.video = newVideo;
      }

      // --------------------------------------------------------------------------------------------------------------------------------

      // Write a shortcur url file in the current directory
      private void WriteShortcut(string channel, string video, DateTimeOffset dt)
      {
         try
         {
            if (video.StartsWith("shorts/")) video = video.Substring(8);
            var file = $"{channel} - video[{video}]";
            var path = Path.ChangeExtension(Path.Combine(shortcutPath, file), ".url");
            File.WriteAllText(path, $"[InternetShortcut]\r\nURL=https://www.youtube.com/watch?v=" + video);
            File.SetCreationTime(path, dt.ToLocalTime().DateTime);
         }
         catch (Exception ex)
         {
            log.Error($"Failed to write link: {ex.Message}");
         }
      }

      // --------------------------------------------------------------------------------------------------------------------------------

      // convert 'xx ago' text into (approx) timespan
      private TimeSpan StringToOffset(string time)
      {
         var s = time.Split(' ');
         var n = int.Parse(s[0]);
         if (s[1].StartsWith("minute")) return TimeSpan.FromMinutes(n);
         if (s[1].StartsWith("hour")) return TimeSpan.FromHours(n);
         if (s[1].StartsWith("day")) return TimeSpan.FromDays(n);
         if (s[1].StartsWith("week")) return 7 * TimeSpan.FromDays(n);
         if (s[1].StartsWith("month")) return 30 * TimeSpan.FromDays(n);
         if (s[1].StartsWith("year")) return 365 * TimeSpan.FromDays(n);
         return TimeSpan.FromDays(0);
      }

      // --------------------------------------------------------------------------------------------------------------------------------

      // database record type
      public class yt
      {
         public yt() => channel = url = video = string.Empty;
         public string channel { get; init; }
         public string url { get; init; }
         public DateTimeOffset last { get; set; }
         public string video { get; set; }
      }

      // --------------------------------------------------------------------------------------------------------------------------------

   }
}