using Chinatsuservices_localAPI_GUI;
using Microsoft.Data.SqlClient;
using Microsoft.VisualBasic.ApplicationServices;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Diagnostics;
using System.Net;
using System.Net.Mail;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Xml.Linq;
using System.Data;
using System.Data.SqlClient;

internal class Program
{
    [DllImport("User32.dll", CharSet = CharSet.Unicode)]
    public static extern int MessageBox(IntPtr h, string text, string caption, int type);

    public async Task Main(string[] args)
    {
        
    }
}

public class MangaAPI
{
    private readonly string APIRootPath = "C:\\chinatsuservices_API";
    public static readonly string configPath = "C:\\chinatsuservices_API\\config.json";
    public static readonly string sqlConfigPath = "C:\\chinatsuservices_API\\sql.json";

    private string mangaRootStoragePath = "";

    public ProgressBar proccessBar;
    public Button Run_API_Button;
    public Color defaultColor;

    private string accountToLibraryPath = "";
    private string librariesPath = "";
    private string accountsPath = "";
    private string coverRootPath = "";
    private string cachePath = "";
    private int rateLimit;
    private string host;
    private string port;
    private int rateLimitTimeout;
    private int apiRefreshTime;
    private int cacheExpireTime;

    public static string CurrentLogPath;
    public static string cachePathStatic;
    public static string mangaStoragePath;
    public static string backupStoragePath;

    private DateTime lastPullFromMangaAPI;
    private DateTime lastAPIRefershTime;

    private List<CachedManga> oldCachedManga;

    private string currentLogFile = "C:\\chinatsuservices_API\\log.txt";
    private string disposableLogFile = "C:\\chinatsuservices_API\\log.txt";

    public async Task Run()
    {
        Log(LogLevel.info, "Attempting API run");
        //every set time run api controller
        try
        {
            Main.PauseTimer();
            Main.runningAPICount++;

            SetupLogData();
            DeleteExpiredCache();
            await UpdateCache();
            //CheckForUpdatedChapeters();
            BackupDir();
            lastAPIRefershTime = DateTime.Now;

            if (oldCachedManga.Count > 0)
            {
                CheckForUpdatedChapeters();
            }

            Main.runningAPICount--;
            Main.ResumeTimer();
        }
        catch (Exception e)
        {
            Log(LogLevel.error, $"Failed to run API error: {e.Message}");
            Log(LogLevel.error, $"Inner Exception: {e.InnerException}");
            Log(LogLevel.error, $"Stack Trace: {e.StackTrace}");
        }
        Run_API_Button.ForeColor = defaultColor;
    }

    //returns API refresh time +1 so that it prevents any time collisions with api
    public int GetAPISleepAmount()
    {
        return (apiRefreshTime) * 60000;
    }

    public void DeleteAllCache()
    {
        Log(LogLevel.info, $"Started Delete of All cached files");

        if (File.Exists(cachePath))
        {
            Log(LogLevel.info, $"Deleting: {cachePath}");
            File.Delete(cachePath);
        }

        if (!Directory.Exists(coverRootPath))
            return;

        var files = Directory.GetFiles(coverRootPath);

        proccessBar.Maximum += files.Length;

        foreach (var file in files)
        {
            try
            {
                Log(LogLevel.info, $"Deleting: {file}");
                File.Delete(file);
            }
            catch (Exception ex)
            {
                Log(LogLevel.error, $"Failed to delete {file}: {ex.Message}");
            }
            if (proccessBar.Value >= proccessBar.Maximum)
            {
                if (proccessBar.Value < proccessBar.Minimum)
                {
                    proccessBar.Value++;
                }
            }
            else
            {
                proccessBar.Value = proccessBar.Maximum;
            }
        }
    }

    public void DeleteExpiredCache()
    {
        Log(LogLevel.info, "Starting Expired Cache removal proccess");

        oldCachedManga = new List<CachedManga>();

        var cacheCollection = JsonHandler.DeserializeJsonFile<CachedMangaCollection>(cachePath);
        var cache = cacheCollection.cache;

        List<string> IdsToRem = new List<string>();

        int processBarMaxBefore = proccessBar.Maximum;

        proccessBar.Maximum += cache.Values.Count;

        int count = 0;

        foreach (var manga in cache.Values)
        {
            if ((DateTime.Now - manga.dateAdded).TotalDays >= cacheExpireTime)
            {
                IdsToRem.Add(manga.managaId);
                oldCachedManga.Add(manga);
                var path = Path.Combine(coverRootPath, Path.GetFileName(new Uri(manga.coverPhotoPath).LocalPath));
                Log(LogLevel.info, $"Deleting: {path}");
                File.Delete(path);
                if (proccessBar.Value >= proccessBar.Maximum)
                {
                    if (proccessBar.Value < proccessBar.Minimum)
                    {
                        proccessBar.Value++;
                    }
                }
                else
                {
                    proccessBar.Value = proccessBar.Maximum;
                }
                count++;
            }
        }

        proccessBar.Maximum = processBarMaxBefore + (count * 2);

        foreach (var id in IdsToRem)
        {
            count++;
            Log(LogLevel.info, $"Removing: {id}, from Cache");
            cache.Remove(id);
        }

        JsonHandler.SerializeJsonFile<CachedMangaCollection>(cachePath, cacheCollection);

        Log(LogLevel.info, "Completed Expired Cache removal proccess");
    }

    public async Task UpdateCache()
    {
        Log(LogLevel.info, "Starting Update Cache Proccess");

        var Libraries = JsonHandler.DeserializeJsonFile<LibraryFromID>(librariesPath).collection.Values;

        int count = 0;

        SQLConfig sqlConfig = JsonHandler.DeserializeJsonFile<SQLConfig>(sqlConfigPath);

        if (sqlConfig.useDB)
        {
            Log(LogLevel.info, "Using DB for Cache Update");

            List<MangaEntry> allMangaEntries = new List<MangaEntry>();

            string connectionString = $"Server={sqlConfig.IP};Database={sqlConfig.DB_Name};User Id={sqlConfig.username};Password={sqlConfig.password};TrustServerCertificate=True;";
            using (SqlConnection conn = new(connectionString))
            {
                conn.Open();

                string query = "SELECT * FROM Manga";
                using (SqlCommand cmd = new(query, conn))
                {
                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            allMangaEntries.Add(new MangaEntry
                            {
                                Title = reader["Title"].ToString(),
                                Link = reader["Link"].ToString(),
                                Status = reader["Status"].ToString(),
                                chaptersRead = float.Parse(reader["chaptersRead"].ToString()),
                                favourite = bool.Parse(reader["favourite"].ToString()),
                            });
                        }
                    }
                }
            }

            if ((DateTime.Now - lastPullFromMangaAPI).TotalMinutes > rateLimitTimeout)
            {
                if (count <= rateLimit)
                {
                    foreach (var manga in allMangaEntries)
                    {
                        if (!CheckForCache(ExtractMangaIdFromUrl(manga.Link)))
                        {
                            Log(LogLevel.info, $"Caching Manga: {ExtractMangaIdFromUrl(manga.Link)}");

                            var timeout = TimeSpan.FromSeconds(10);

                            var mangaTask = Task.Run(async () =>
                            {
                                string coverFilename = "";

                                //add to cache
                                if (File.Exists(cachePath))
                                {
                                    //work from
                                    bool fail = false;

                                    CachedManga cachedManga = new CachedManga();

                                    coverFilename = await GetMangaCoverUrlAsync(manga);

                                    Log(LogLevel.info, $"Downloading Cover: {$"https://uploads.mangadex.org/covers/{ExtractMangaIdFromUrl(manga.Link)}/{coverFilename}"}");

                                    await DownloadFileAsync($"https://uploads.mangadex.org/covers/{ExtractMangaIdFromUrl(manga.Link)}/{coverFilename}", coverRootPath);

                                    if (!string.IsNullOrEmpty(coverFilename))
                                    {
                                        if (port != "")
                                        {
                                            cachedManga.coverPhotoPath = $"https://{host}:{port}/Libraries/Manga/Covers/{coverFilename}";
                                        }
                                        else
                                        {
                                            cachedManga.coverPhotoPath = $"https://{host}/Libraries/Manga/Covers/{coverFilename}";
                                        }
                                    }
                                    else
                                    {
                                        fail = true;
                                    }
                                    var jsonData = await GetMangaJsonDataAsync(manga.Link);
                                    if (jsonData == null)
                                    {
                                        fail = true;
                                    }
                                    manga.MangaJsonData = jsonData;
                                    cachedManga.altTitle = ExtractAndSetAltTitle(manga);
                                    cachedManga.genres = ExtractGenresFromJson(manga);
                                    cachedManga.publicationStatus = ExtractStatusFromJson(manga);
                                    var publishedCount = await GetAndSetPublishEnglishChapters(manga);
                                    if (publishedCount == -1)
                                    {
                                        fail = true;
                                        publishedCount = 0;
                                    }
                                    cachedManga.pulishedChapterCount = publishedCount;
                                    cachedManga.dateAdded = DateTime.Now;
                                    cachedManga.managaId = ExtractMangaIdFromUrl(manga.Link);

                                    if (!fail)
                                    {
                                        //add to cache
                                        CachedMangaCollection cachedMangaCollection = JsonHandler.DeserializeJsonFile<CachedMangaCollection>(cachePath);
                                        cachedMangaCollection.cache.Add(ExtractMangaIdFromUrl(manga.Link), cachedManga);
                                        JsonHandler.SerializeJsonFile(cachePath, cachedMangaCollection);
                                    }
                                }
                                else
                                {
                                    //create new
                                    bool fail = false;

                                    CachedManga cachedManga = new CachedManga();

                                    coverFilename = await GetMangaCoverUrlAsync(manga);

                                    Log(LogLevel.info, $"Downloading Cover: {$"https://uploads.mangadex.org/covers/{ExtractMangaIdFromUrl(manga.Link)}/{coverFilename}"}");

                                    await DownloadFileAsync($"https://uploads.mangadex.org/covers/{ExtractMangaIdFromUrl(manga.Link)}/{coverFilename}", coverRootPath);

                                    if (!string.IsNullOrEmpty(coverFilename))
                                    {
                                        if (port != "")
                                        {
                                            cachedManga.coverPhotoPath = $"https://{host}:{port}/Libraries/Manga/Covers/{coverFilename}";
                                        }
                                        else
                                        {
                                            cachedManga.coverPhotoPath = $"https://{host}/Libraries/Manga/Covers/{coverFilename}";
                                        }
                                    }
                                    else
                                    {
                                        fail = true;
                                    }
                                    var jsonData = await GetMangaJsonDataAsync(manga.Link);
                                    if (jsonData == null)
                                    {
                                        fail = true;
                                    }
                                    manga.MangaJsonData = jsonData;
                                    cachedManga.altTitle = ExtractAndSetAltTitle(manga);
                                    cachedManga.genres = ExtractGenresFromJson(manga);
                                    cachedManga.publicationStatus = ExtractStatusFromJson(manga);
                                    var publishedCount = await GetAndSetPublishEnglishChapters(manga);
                                    if (publishedCount == -1)
                                    {
                                        fail = true;
                                        publishedCount = 0;
                                    }
                                    cachedManga.pulishedChapterCount = publishedCount;
                                    cachedManga.dateAdded = DateTime.Now;
                                    cachedManga.managaId = ExtractMangaIdFromUrl(manga.Link);

                                    if (!fail)
                                    {
                                        //add to cache
                                        CachedMangaCollection cachedMangaCollection = new CachedMangaCollection();
                                        cachedMangaCollection.cache.Add(ExtractMangaIdFromUrl(manga.Link), cachedManga);
                                        JsonHandler.SerializeJsonFile(cachePath, cachedMangaCollection);
                                    }
                                }
                            });

                            var completed = await Task.WhenAny(mangaTask, Task.Delay(timeout));

                            count++;
                        }

                        if (proccessBar.Value >= proccessBar.Maximum)
                        {
                            if (proccessBar.Value < proccessBar.Minimum)
                            {
                                proccessBar.Value++;
                            }
                        }
                        else
                        {
                            proccessBar.Value = proccessBar.Maximum;
                        }
                    }
                }
            }
        }
        else
        {
            if ((DateTime.Now - lastPullFromMangaAPI).TotalMinutes > rateLimitTimeout)
            {
                int tempCount = 0;

                foreach (var library in Libraries)
                {
                    tempCount += library.entries.Count;
                }

                proccessBar.Maximum += tempCount;

                if (count <= rateLimit)
                {
                    foreach (var library in Libraries)
                    {
                        foreach (var manga in library.entries)
                        {
                            if (!CheckForCache(ExtractMangaIdFromUrl(manga.Link)))
                            {
                                Log(LogLevel.info, $"Caching Manga: {ExtractMangaIdFromUrl(manga.Link)}");

                                var timeout = TimeSpan.FromSeconds(10);

                                var mangaTask = Task.Run(async () =>
                                {
                                    string coverFilename = "";

                                    //add to cache
                                    if (File.Exists(cachePath))
                                    {
                                        //work from
                                        bool fail = false;

                                        CachedManga cachedManga = new CachedManga();

                                        coverFilename = await GetMangaCoverUrlAsync(manga);

                                        Log(LogLevel.info, $"Downloading Cover: {$"https://uploads.mangadex.org/covers/{ExtractMangaIdFromUrl(manga.Link)}/{coverFilename}"}");

                                        await DownloadFileAsync($"https://uploads.mangadex.org/covers/{ExtractMangaIdFromUrl(manga.Link)}/{coverFilename}", coverRootPath);

                                        if (!string.IsNullOrEmpty(coverFilename))
                                        {
                                            if (port != "")
                                            {
                                                cachedManga.coverPhotoPath = $"https://{host}:{port}/Libraries/Manga/Covers/{coverFilename}";
                                            }
                                            else
                                            {
                                                cachedManga.coverPhotoPath = $"https://{host}/Libraries/Manga/Covers/{coverFilename}";
                                            }
                                        }
                                        else
                                        {
                                            fail = true;
                                        }
                                        var jsonData = await GetMangaJsonDataAsync(manga.Link);
                                        if (jsonData == null)
                                        {
                                            fail = true;
                                        }
                                        manga.MangaJsonData = jsonData;
                                        cachedManga.altTitle = ExtractAndSetAltTitle(manga);
                                        cachedManga.genres = ExtractGenresFromJson(manga);
                                        cachedManga.publicationStatus = ExtractStatusFromJson(manga);
                                        var publishedCount = await GetAndSetPublishEnglishChapters(manga);
                                        if (publishedCount == -1)
                                        {
                                            fail = true;
                                            publishedCount = 0;
                                        }
                                        cachedManga.pulishedChapterCount = publishedCount;
                                        cachedManga.dateAdded = DateTime.Now;
                                        cachedManga.managaId = ExtractMangaIdFromUrl(manga.Link);

                                        if (!fail)
                                        {
                                            //add to cache
                                            CachedMangaCollection cachedMangaCollection = JsonHandler.DeserializeJsonFile<CachedMangaCollection>(cachePath);
                                            cachedMangaCollection.cache.Add(ExtractMangaIdFromUrl(manga.Link), cachedManga);
                                            JsonHandler.SerializeJsonFile(cachePath, cachedMangaCollection);
                                        }
                                    }
                                    else
                                    {
                                        //create new
                                        bool fail = false;

                                        CachedManga cachedManga = new CachedManga();

                                        coverFilename = await GetMangaCoverUrlAsync(manga);

                                        Log(LogLevel.info, $"Downloading Cover: {$"https://uploads.mangadex.org/covers/{ExtractMangaIdFromUrl(manga.Link)}/{coverFilename}"}");

                                        await DownloadFileAsync($"https://uploads.mangadex.org/covers/{ExtractMangaIdFromUrl(manga.Link)}/{coverFilename}", coverRootPath);

                                        if (!string.IsNullOrEmpty(coverFilename))
                                        {
                                            if (port != "")
                                            {
                                                cachedManga.coverPhotoPath = $"https://{host}:{port}/Libraries/Manga/Covers/{coverFilename}";
                                            }
                                            else
                                            {
                                                cachedManga.coverPhotoPath = $"https://{host}/Libraries/Manga/Covers/{coverFilename}";
                                            }
                                        }
                                        else
                                        {
                                            fail = true;
                                        }
                                        var jsonData = await GetMangaJsonDataAsync(manga.Link);
                                        if (jsonData == null)
                                        {
                                            fail = true;
                                        }
                                        manga.MangaJsonData = jsonData;
                                        cachedManga.altTitle = ExtractAndSetAltTitle(manga);
                                        cachedManga.genres = ExtractGenresFromJson(manga);
                                        cachedManga.publicationStatus = ExtractStatusFromJson(manga);
                                        var publishedCount = await GetAndSetPublishEnglishChapters(manga);
                                        if (publishedCount == -1)
                                        {
                                            fail = true;
                                            publishedCount = 0;
                                        }
                                        cachedManga.pulishedChapterCount = publishedCount;
                                        cachedManga.dateAdded = DateTime.Now;
                                        cachedManga.managaId = ExtractMangaIdFromUrl(manga.Link);

                                        if (!fail)
                                        {
                                            //add to cache
                                            CachedMangaCollection cachedMangaCollection = new CachedMangaCollection();
                                            cachedMangaCollection.cache.Add(ExtractMangaIdFromUrl(manga.Link), cachedManga);
                                            JsonHandler.SerializeJsonFile(cachePath, cachedMangaCollection);
                                        }
                                    }
                                });

                                var completed = await Task.WhenAny(mangaTask, Task.Delay(timeout));

                                count++;
                            }

                            if (proccessBar.Value >= proccessBar.Maximum)
                            {
                                if (proccessBar.Value < proccessBar.Minimum)
                                {
                                    proccessBar.Value++;
                                }
                            }
                            else
                            {
                                proccessBar.Value = proccessBar.Maximum;
                            }
                        }
                    }
                }
                else
                {
                    Log(LogLevel.warning, "Reached Rate Limit, waiting for timeout to continue");
                    lastPullFromMangaAPI = DateTime.Now;
                }
            }
        }

        Log(LogLevel.info, "Completed Update Cache Proccess");
    }

    public void CheckForUpdatedChapeters()
    {
        Log(LogLevel.info, "Starting Check for Updated Chapters Proccess");

        Config config = JsonHandler.DeserializeJsonFile<Config>(configPath);

        var cache = JsonHandler.DeserializeJsonFile<CachedMangaCollection>(cachePath).cache;
        var Libraries = JsonHandler.DeserializeJsonFile<LibraryFromID>(librariesPath).collection;
        var link = JsonHandler.DeserializeJsonFile<AccountToLibraries>(accountToLibraryPath).link;
        var accounts = JsonHandler.DeserializeJsonFile<List<Account>>(accountsPath);

        var accountMap = accounts.ToDictionary(a => a.ID);
        var libraryEntryMap = Libraries.ToDictionary(lib => lib.Key, lib => lib.Value.entries);
        var libraryToAccountMap = link.GroupBy(kvp => kvp.Value)
                                      .ToDictionary(g => g.Key, g => g.Select(kvp => kvp.Key).ToList());

        Dictionary<string, List<MangaEntry>> updatedMangaMap = new Dictionary<string, List<MangaEntry>>(); //account email, manga titles

        foreach (var old in oldCachedManga)
        {
            //redundant check for testing new sql checking
            if (cache.TryGetValue(old.managaId, out var updated) &&
                updated.pulishedChapterCount > old.pulishedChapterCount)
            {
                foreach (var (libraryID, entries) in libraryEntryMap)
                {

                    foreach (var manga in entries)
                    {
                        if (ExtractMangaIdFromUrl(manga.Link) == updated.managaId)
                        {
                            if (libraryToAccountMap.TryGetValue(libraryID, out var accountIds))
                            {
                                foreach (var accountId in accountIds)
                                {
                                    if (accountMap.TryGetValue(accountId, out var account))
                                    {
                                        if (account.sendUpdates && manga.Status != "Onhold" && manga.Status != "Dropped")
                                        {
                                            if (updatedMangaMap.ContainsKey(account.email))
                                            {
                                                updatedMangaMap[account.email].Add(manga);
                                            }
                                            else
                                            {
                                                updatedMangaMap.Add(account.email, new List<MangaEntry> { manga });
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }

        Log(LogLevel.info, "Completed Check for Updated Chapters Proccess");

        Log(LogLevel.info, "Sending Update Emails");
        foreach (string email in updatedMangaMap.Keys)
        {
            //SendNewChaptersEmail(email, updatedMangaMap[email]);

            LogSeperateForSQLTest(LogLevel.info, $"would send update email to {email} for {updatedMangaMap[email].Count} manga updates, if using json. should be receiving from sql tho");
            //add this string to the ./api.log file on linux.
        }


        //---------------------------------------------------------
        // 🔔 This will be the one used for sql check 🔔
        //---------------------------------------------------------

        cache = JsonHandler.DeserializeJsonFile<CachedMangaCollection>(cachePath).cache;
        Dictionary<string, List<MangaEntry>> updatedMangaMapSql = new Dictionary<string, List<MangaEntry>>();

        Log(LogLevel.info, "Starting SQL Check for Updated Chapters Proccess");

        // 1. Get ALL relevant user-manga data from the database (replacing file lookups)
        var userMangaCollection = GetAllActiveUserMangaFromDatabase();
        proccessBar.Maximum += userMangaCollection.Count;

        // 2. Iterate and compare against the 'cache' and 'oldCachedManga' list
        foreach (var old in oldCachedManga)
        {
            // This is the redundant check for testing, comparing old vs. new chapter counts
            if (cache.TryGetValue(old.managaId, out var updated) &&
                updated.pulishedChapterCount > old.pulishedChapterCount)
            {
                // Now, find all UserMangaEntries that match the updated manga ID
                var updatedMangaId = updated.managaId;

                // Iterate over the DB results to find which users are tracking this manga
                foreach (var userManga in userMangaCollection)
                {
                    if (ExtractMangaIdFromUrl(userManga.Link) == updatedMangaId)
                    {
                        // The SQL query already filters for A.sendUpdates = TRUE and
                        // M.Status NOT IN ('Onhold', 'Dropped'), but we include the checks 
                        // for clarity if the SQL was simpler.
                        // if (userManga.SendUpdates && userManga.Status != "Onhold" && userManga.Status != "Dropped") 

                        // Create the MangaEntry object for the notification email
                        var mangaEntry = new MangaEntry
                        {
                            Title = userManga.Title,
                            Link = userManga.Link,
                            chaptersRead = userManga.ChaptersRead,
                            Status = userManga.Status,
                            publicationStatus = updated.publicationStatus,
                            AltTitle = updated.altTitle
                        };

                        if (updatedMangaMapSql.ContainsKey(userManga.Email))
                        {
                            updatedMangaMapSql[userManga.Email].Add(mangaEntry);
                        }
                        else
                        {
                            updatedMangaMapSql.Add(userManga.Email, new List<MangaEntry> { mangaEntry });
                        }
                    }
                }
            }
        }

        Log(LogLevel.info, "Completed SQL Check for Updated Chapters Proccess");

        Log(LogLevel.info, "Sending Update Emails (via SQL check)");
        foreach (string email in updatedMangaMapSql.Keys)
        {
            LogSeperateForSQLTest(LogLevel.info, $"sending update email to {email} for {updatedMangaMap[email].Count} manga updates using sql. compare with json result");
            SendNewChaptersEmail(email, updatedMangaMapSql[email]);
        }
        Log(LogLevel.info, "Clearing Old Cached Managa Lookup");
        oldCachedManga.Clear(); // <-- Clear the list after the comparison
        Log(LogLevel.info, "Finished Sending Update Emails (via SQL check)");
    }

    public void LogSeperateForSQLTest(LogLevel lv, string message)
    {
        string level = "Info";

        string log = "";

        switch (lv)
        {
            case LogLevel.info:
                level = "Info";
                Output.WriteColored($"[{DateTime.Now}] ", Color.White);
                Output.WriteColored($"[{level.ToUpper()}] ", Color.Green);
                Output.WriteColored($"{message}{Environment.NewLine}", Color.White);

                log += $"[{DateTime.Now}] [{level.ToUpper()}] {message}";
                break;

            case LogLevel.warning:
                level = "Warning";
                Output.WriteColored($"[{DateTime.Now}] ", Color.White);
                Output.WriteColored($"[{level.ToUpper()}] {message}{Environment.NewLine}", Color.Yellow);

                log += $"[{DateTime.Now}] [{level.ToUpper()}] {message}";
                break;

            case LogLevel.error:
                level = "Error";
                Output.WriteColored($"[{DateTime.Now}] ", Color.White);
                Output.WriteColored($"[{level.ToUpper()}] {message}{Environment.NewLine}", Color.Red);

                log += $"[{DateTime.Now}] [{level.ToUpper()}] {message}";
                break;

            case LogLevel.test:
                level = "Test";
                Output.WriteColored($"[{DateTime.Now}] ", Color.White);
                Output.WriteColored($"[{level.ToUpper()}] {message}{Environment.NewLine}", Color.Blue);

                log += $"[{DateTime.Now}] [{level.ToUpper()}] {message}";
                break;
        }

        try
        {
            if (File.Exists("C:\\chinatsuservices_API\\sqlLog.txt"))
            {
                File.AppendAllLines("C:\\chinatsuservices_API\\sqlLog.txt", new[] { log });
            }
            else
            {
                File.CreateText("C:\\chinatsuservices_API\\sqlLog.txt");
                File.AppendAllLines("C:\\chinatsuservices_API\\sqlLog.txt", new[] { log });
            }
        }
        catch (Exception e)
        {
            if (e.InnerException is IOException && e.Message.Contains("because it is being used by another process"))
            {
                Output.WriteLine($"Failed to write log: Due to IOException file is being used by another proccess");
            }
            else
            {
                Output.WriteLine($"Failed to write log: {e.Message}");
            }
        }

        Output.ChangeColor(Color.White);
    }


    private List<UserMangaEntry> GetAllActiveUserMangaFromDatabase()
    {
        // 1. **Define your Connection String**
        // Get this from your configuration file (Config config = ...)
        SQLConfig sqlConfig = JsonHandler.DeserializeJsonFile<SQLConfig>(sqlConfigPath);
        string connectionString = $"Server={sqlConfig.IP};Database={sqlConfig.DB_Name};User Id={sqlConfig.username};Password={sqlConfig.password};TrustServerCertificate=True;";

        // 2. **Define the SQL Query**
        // Fetches all necessary data in one efficient call
        string sqlQuery = @"
        SELECT
            M.Title,
            M.Link,
            M.chaptersRead,
            M.Status,
            A.email,
            A.sendUpdates
        FROM
            Manga M
        JOIN
            Accounts A ON M.UserID = A.UserID
        WHERE
            A.sendUpdates = 1 -- Use 1 for TRUE/Boolean logic in SQL
            AND M.Status NOT IN ('Onhold', 'Dropped');";

        List<UserMangaEntry> userMangaCollection = new List<UserMangaEntry>();

        try
        {
            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                using (SqlCommand command = new SqlCommand(sqlQuery, connection))
                {
                    connection.Open();

                    using (SqlDataReader reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            userMangaCollection.Add(new UserMangaEntry
                            {
                                // Ensure data types and column names match your DB schema
                                Title = reader["Title"].ToString(),
                                Link = reader["Link"].ToString(),
                                ChaptersRead = Convert.ToInt32(reader["chaptersRead"]),
                                Status = reader["Status"].ToString(),
                                Email = reader["email"].ToString(),
                                SendUpdates = Convert.ToBoolean(reader["sendUpdates"])
                            });
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            // Important: Log any database errors that occur
            Log(LogLevel.error, $"Database error fetching user manga: {ex.Message}");
            // Depending on your application, you might rethrow the exception or return an empty list
            return new List<UserMangaEntry>();
        }

        return userMangaCollection;
    }

    public void SendNewChaptersEmail(string recipientEmail, List<MangaEntry> mangaList)
    {
        if (File.Exists(configPath))
        {
            try
            {
                Config confg = JsonHandler.DeserializeJsonFile<Config>(configPath);

                string senderEmail = confg.fromEmail;
                string appPassword = confg.googleAPPpass;

                Log(LogLevel.info, $"Sending new chapters email from {senderEmail} to {recipientEmail}");

                var smtp = new SmtpClient("smtp.gmail.com", 587)
                {
                    Credentials = new NetworkCredential(senderEmail, appPassword),
                    EnableSsl = true
                };

                CachedMangaCollection loadedCache = null;

                if (File.Exists(cachePath))
                {
                    loadedCache = JsonHandler.DeserializeJsonFile<CachedMangaCollection>(cachePath);
                }

                string mangaHtmlList = "";
                foreach (var manga in mangaList)
                {
                    CachedManga cachedManga = null;

                    string newAmountofChapters = "";
                    string publicationStatus = "";
                    string altTitle = "";
                    string readChapters = "";

                    if (loadedCache != null) 
                    { 
                        if (loadedCache.cache.ContainsKey(ExtractMangaIdFromUrl(manga.Link)))
                        {
                            cachedManga = loadedCache.cache[ExtractMangaIdFromUrl(manga.Link)];
                        }
                    }

                    if (cachedManga != null)
                    {
                        newAmountofChapters = cachedManga.pulishedChapterCount.ToString();
                        publicationStatus = cachedManga.publicationStatus;
                        altTitle = cachedManga.altTitle;
                    }
                    else
                    {
                        newAmountofChapters = "Unknowm";
                        publicationStatus = "Unknowm";
                        altTitle = "";
                    }

                    string statusColor = publicationStatus.ToLower() switch
                    {
                        "ongoing" => "green",
                        "completed" => "blue",
                        "hiatus" => "red",
                        _ => "gray"
                    };
                    int oldCachedChapters = 0;
                    CachedManga matchingEntry = oldCachedManga.FirstOrDefault(a => a.managaId == ExtractMangaIdFromUrl(manga.Link));

                    if (matchingEntry != null)
                    {
                        oldCachedChapters = matchingEntry.pulishedChapterCount;
                        mangaHtmlList += $@"
                        <li class='manga-item'>
                            <div class='manga-header'>
                                <div class='manga-details'>
                                    <a href='{WebUtility.HtmlEncode(manga.Link)}' class='manga-title'>
                                        {WebUtility.HtmlEncode(manga.Title)}
                                    </a>
                                    <p class='manga-alt-title'>
                                        {WebUtility.HtmlEncode(altTitle)}
                                    </p>
                                </div>
                                <span class='status' style='color: {statusColor};'>
                                    {WebUtility.HtmlEncode(publicationStatus)}
                                </span>
                            </div>
                            <div class='chapter-info'>
                                Old Chapters -> New Chapters: {WebUtility.HtmlEncode(oldCachedChapters.ToString())} / {WebUtility.HtmlEncode(newAmountofChapters)}
                            </div>
                            <div class='chapter-info'>
                                Read / Published Chapters: {WebUtility.HtmlEncode(manga.chaptersRead.ToString())} / {WebUtility.HtmlEncode(newAmountofChapters)}
                            </div>
                        </li>";
                    }
                    else
                    {
                        mangaHtmlList += $@"
                        <li class='manga-item'>
                            <div class='manga-header'>
                                <div class='manga-details'>
                                    <a href='{WebUtility.HtmlEncode(manga.Link)}' class='manga-title'>
                                        {WebUtility.HtmlEncode(manga.Title)}
                                    </a>
                                    <p class='manga-alt-title'>
                                        {WebUtility.HtmlEncode(altTitle)}
                                    </p>
                                </div>
                                <span class='status' style='color: {statusColor};'>
                                    {WebUtility.HtmlEncode(publicationStatus)}
                                </span>
                            </div>
                            <div class='chapter-info'>
                                Read / Published Chapters: {WebUtility.HtmlEncode(manga.chaptersRead.ToString())} / {WebUtility.HtmlEncode(newAmountofChapters)}
                            </div>
                        </li>";
                    }
                    
                }

                string htmlBody = $@"
                    <html>
                    <head>
                        <title>New Manga Chapters</title>
                        <style>
                            body {{
                                font-family: Arial, sans-serif;
                                background-color: #f4f4f4;
                                padding: 20px;
                                margin: 0;
                            }}
                            .container {{
                                max-width: 600px;
                                margin: auto;
                                background: white;
                                padding: 20px;
                                border-radius: 10px;
                                box-shadow: 0 0 10px rgba(0,0,0,0.1);
                            }}
                            .logo {{
                                max-width: 150px;
                                display: block;
                                margin: 0 auto 20px;
                            }}
                            .main-heading {{
                                color: #333;
                                text-align: center;
                            }}
                            .manga-list {{
                                list-style-type: none;
                                padding: 0;
                                margin: 0;
                            }}
                            .manga-item {{
                                margin-bottom: 15px;
                                border-bottom: 1px solid #eee;
                                padding-bottom: 10px;
                                display: flex;
                                flex-direction: column;
                            }}
                            .manga-header {{
                                display: flex;
                                justify-content: space-between;
                                align-items: center;
                            }}
                            .manga-details {{
                                display: flex;
                                flex-direction: column;
                            }}
                            .manga-title {{
                                text-decoration: none;
                                color: #007BFF;
                                font-size: 1.1em;
                                font-weight: bold;
                            }}
                            .manga-alt-title {{
                                font-size: 0.9em;
                                color: #555;
                                margin: 0;
                                padding: 0;
                            }}
                            .status {{
                                font-size: 0.9em;
                                font-weight: bold;
                            }}
                            .chapter-info {{
                                font-size: 0.8em;
                                color: #666;
                                margin-top: 5px;
                            }}
                            .cta-button {{
                                display: block;
                                text-align: center;
                                margin-top: 20px;
                                padding: 10px;
                                background-color: #007BFF;
                                color: white;
                                text-decoration: none;
                                border-radius: 5px;
                                font-weight: bold;
                            }}
                            .footer-text {{
                                font-size: 0.9em;
                                color: #666;
                                text-align: center;
                                margin-top: 30px;
                            }}
                        </style>
                    </head>
                    <body style='font-family: Arial, sans-serif; background-color: #f4f4f4; padding: 20px;'>
                        <div class='container'>
                            <img class='logo' src='https://chinatsuservices.ddns.net/MainStaticImages/MainLogo.png' alt='Logo' />
                            <h2 class='main-heading'>New Manga Chapters Released!</h2>
                            <p>Hi there! The following manga have new chapters available:</p>
                            <ul class='manga-list'>
                                {mangaHtmlList}
                            </ul>
                            <p><a href='https://chinatsuservices.ddns.net/Tools/Libraries/MangaLibrary' class='cta-button'>Check them out in your library now!</a></p>
                            <p class='footer-text'>You are receiving this email because you subscribed to notifications for manga updates.</p>
                        </div>
                    </body>
                    </html>";

                var mail = new MailMessage
                {
                    From = new MailAddress(senderEmail, "Chinatsuservices"),
                    Subject = "New Manga Chapters Available!",
                    Body = htmlBody,
                    IsBodyHtml = true
                };

                mail.To.Add(recipientEmail);

                smtp.Send(mail);
            }
            catch (Exception ex)
            {
                Log(LogLevel.error ,"Email failed: " + ex.Message);
            }
        }
        else
        {
            Console.WriteLine("Config file not found.");
        }
    }

    public void BackupDir()
    {
        Log(LogLevel.info, "Starting backup proccess");
        Log(LogLevel.info, $"══════════════════════════════════════════════════");

        Config config = JsonHandler.DeserializeJsonFile<Config>(configPath);

        string sourceDir = config.backupSourceDir;
        string destDir = config.backupDestinationDir;

        if (sourceDir is null) throw new ArgumentNullException(nameof(sourceDir));
        if (destDir is null) throw new ArgumentNullException(nameof(destDir));

        if (!Directory.Exists(sourceDir))
            throw new DirectoryNotFoundException($"Source not found: {sourceDir}");

        // Ensure destination root exists (CreateDirectory is idempotent)
        Directory.CreateDirectory(destDir);

        // Normalise for prefix replace (similar to the PS .Replace($source,$destination))
        // Use Path.GetFullPath to eliminate .., and ensure trailing separator.
        var sourceFull = EnsureTrailingSeparator(Path.GetFullPath(sourceDir));
        var destFull = EnsureTrailingSeparator(Path.GetFullPath(destDir));

        proccessBar.Maximum += Directory.EnumerateFileSystemEntries(sourceFull, "*", SearchOption.AllDirectories).Count();

        // Enumerate everything (files + dirs) recursively.
        // This will include hidden/system entries; no filtering is applied.
        foreach (var entryPath in Directory.EnumerateFileSystemEntries(sourceFull, "*", SearchOption.AllDirectories))
        {
            var targetPath = destFull + entryPath.Substring(sourceFull.Length);

            Log(LogLevel.info, $"   | Backing Up: {entryPath} -- To -- {targetPath}");

            if (Directory.Exists(entryPath))
            {
                // Create directory if needed.
                Directory.CreateDirectory(targetPath);
            }
            else
            {
                // Ensure parent directory exists (in case enumeration returns file before parent created)
                string? parent = Path.GetDirectoryName(targetPath);
                if (!string.IsNullOrEmpty(parent))
                    Directory.CreateDirectory(parent);

                // Copy file (overwrite = true)
                File.Copy(entryPath, targetPath, overwrite: true);
            }

            if (proccessBar.Value >= proccessBar.Maximum)
            {
                if (proccessBar.Value < proccessBar.Minimum)
                {
                    proccessBar.Value++;
                }
            }
            else
            {
                proccessBar.Value = proccessBar.Maximum;
            }
        }
        Log(LogLevel.info, $"══════════════════════════════════════════════════");
        Log(LogLevel.info, "Backup Proccess Finished");
    }

    private static string EnsureTrailingSeparator(string path)
    {
        if (string.IsNullOrEmpty(path)) return path;
        char sep = Path.DirectorySeparatorChar;
        return path.EndsWith(sep) ? path : path + sep;
    }

    public void SetInits()
    {
        if (File.Exists(configPath))
        {
            Config config = JsonHandler.DeserializeJsonFile<Config>(configPath);
            string mangaRootPath = config.mangaRootStoragePath;
            if (Directory.Exists(mangaRootPath))
            {
                mangaRootStoragePath = mangaRootPath;

                accountToLibraryPath = Path.Combine(mangaRootStoragePath, "accToLib.json");
                librariesPath = Path.Combine(mangaRootStoragePath, "Libraries.json");
                accountsPath = Path.Combine(mangaRootStoragePath, "Accounts.json");
                coverRootPath = Path.Combine(mangaRootStoragePath, "Covers");
                cachePath = Path.Combine(mangaRootStoragePath, "Cache.json");
            }
            else
            {
                Log(LogLevel.error, "Couldnt not map manga path");
                return;
            }
            rateLimit = config.rateLimit;
            host = config.host;
            port = config.port;
            rateLimitTimeout = config.rateLimitTimeout;
            apiRefreshTime = config.apiRefreshTime;
            cacheExpireTime = config.cacheExpireDays;
            cachePathStatic = cachePath;
            mangaStoragePath = mangaRootStoragePath;
            backupStoragePath = config.backupDestinationDir;
            Log(LogLevel.info, "Config Data:");
            Log(LogLevel.info, $"   Cache Expires after {cacheExpireTime} days");
            Log(LogLevel.info, $"   API Root Path: {APIRootPath}");
            Log(LogLevel.info, $"   Manga Root Storage Path: {mangaRootStoragePath}");
            Log(LogLevel.info, $"   Account to Library Path: {accountToLibraryPath}");
            Log(LogLevel.info, $"   Libraries Path: {librariesPath}");
            Log(LogLevel.info, $"   Accounts Path: {accountsPath}");
            Log(LogLevel.info, $"   Cover Root Path: {coverRootPath}");
            Log(LogLevel.info, $"   Cache Path: {cachePath}");
            Log(LogLevel.info, $"   Sender Email: {config.fromEmail}");
        }
        else
        {
            if (!Directory.Exists(APIRootPath))
            {
                Directory.CreateDirectory(APIRootPath);
            }
            Config config = new Config();
            JsonHandler.SerializeJsonFile<Config>(configPath, config);
            Log(LogLevel.error, "Config was created however has not been setup");
        }
        lastAPIRefershTime = DateTime.MinValue;
    }

    public async Task<string> GetMangaCoverUrlAsync(MangaEntry manga)
    {
        string defaultImageUrl = "/MainStaticImages/Simple_Manga.png";
        string mangaDexUrl = manga.Link;
        string coverFilename = "";

        try
        {
            var uri = new Uri(mangaDexUrl);
            var segments = uri.Segments;
            if (segments.Length < 3)
                return defaultImageUrl;

            string mangaId = segments[2].TrimEnd('/');
            using HttpClient client = new HttpClient();
            client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0");

            var queryParams = new Dictionary<string, string>
            {
                ["limit"] = "10",
                ["manga[]"] = mangaId,
                ["order[createdAt]"] = "asc"
            };

            var query = string.Join("&", queryParams.Select(kvp => $"{Uri.EscapeDataString(kvp.Key)}={Uri.EscapeDataString(kvp.Value)}"));
            var response = await client.GetStringAsync($"https://api.mangadex.org/cover?{query}");

            var json = JObject.Parse(response);
            var first = json["data"]?.FirstOrDefault();
            var fileName = first?["attributes"]?["fileName"]?.ToString();

            if (!string.IsNullOrEmpty(fileName))
            {
                coverFilename = fileName;
            }
            else
            {
                coverFilename = "404";
            }
            return coverFilename;
        }
        catch
        {
            return defaultImageUrl;
        }
    }

    public async Task<JObject?> GetMangaJsonDataAsync(string mangaDexUrl)
    {
        try
        {
            var uri = new Uri(mangaDexUrl);
            var segments = uri.AbsolutePath.Trim('/').Split('/');

            if (segments.Length < 2 || segments[0] != "title")
                return null;

            string mangaId = segments[1];

            using HttpClient client = new HttpClient();
            client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0");

            string url = $"https://api.mangadex.org/manga/{mangaId}";
            var response = await client.GetStringAsync(url);

            return JObject.Parse(response);
        }
        catch
        {
            return null;
        }
    }

    public string ExtractAndSetAltTitle(MangaEntry manga)
    {
        if (manga.MangaJsonData == null)
        {
            return null;
        }

        var attr = manga.MangaJsonData["data"]?["attributes"];
        if (attr == null)
        {
            return null;
        }

        var altTitles = attr["altTitles"] as JArray;
        if (altTitles == null || altTitles.Count == 0)
        {
            return null;
        }

        // Try to get English alt title first
        var enAlt = altTitles.FirstOrDefault(t => t["en"] != null);
        if (enAlt != null)
        {
            return enAlt["en"]?.ToString();
        }

        // Try Japanese romanized alt title next
        var jaRoAlt = altTitles.FirstOrDefault(t => t["ja-ro"] != null);
        if (jaRoAlt != null)
        {
            return jaRoAlt["ja-ro"]?.ToString();
        }

        // Fallback to first alt title available
        return altTitles[0].First?.ToString();
    }

    public List<string> ExtractGenresFromJson(MangaEntry manga)
    {
        if (manga.MangaJsonData == null)
        {
            return null;
        }

        var genres = manga.MangaJsonData["data"]?["attributes"]?["tags"]?
            .Where(t => t["attributes"]?["group"]?.ToString() == "genre")
            .Select(t => t["attributes"]?["name"]?["en"]?.ToString())
            .Where(n => !string.IsNullOrWhiteSpace(n))
            .ToList();
        return genres;
    }

    public string ExtractStatusFromJson(MangaEntry manga)
    {
        if (manga.MangaJsonData == null)
        {
            return "unknown";
        }

        var status = manga.MangaJsonData["data"]?["attributes"]?["status"]?.ToString();

        if (!string.IsNullOrEmpty(status))
        {
            return status;
        }
        else
        {
            return null;
        }
    }

    public string ExtractMangaIdFromUrl(string mangaDexUrl)
    {
        try
        {
            var uri = new Uri(mangaDexUrl);
            var segments = uri.Segments;
            if (segments.Length < 3)
                return string.Empty;

            return segments[2].TrimEnd('/');
        }
        catch
        {
            return string.Empty;
        }
    }

    public bool CheckForCache(string mangaID)
    {
        CachedMangaCollection cacheFile = JsonHandler.DeserializeJsonFile<CachedMangaCollection>(cachePath);
        if (File.Exists(cachePath))
        {
            if (cacheFile.cache.ContainsKey(mangaID))
            {
                return true;
            }
            else
            {
                return false;
            }
        }
        else
        {
            return false;
        }
    }

    public async Task<string?> DownloadFileAsync(string url, string destinationDirectory)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(url) || string.IsNullOrWhiteSpace(destinationDirectory))
                return null;

            Uri uri = new Uri(url);
            string fileName = Path.GetFileName(uri.LocalPath);

            // Ensure directory exists
            Directory.CreateDirectory(destinationDirectory);

            string destinationPath = Path.Combine(destinationDirectory, fileName);

            using HttpClient client = new HttpClient();
            client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0");

            byte[] fileBytes = await client.GetByteArrayAsync(uri);

            await File.WriteAllBytesAsync(destinationPath, fileBytes);

            return destinationPath;
        }
        catch (Exception ex)
        {
            Log(LogLevel.error, $"Failed to download file: {ex.Message}");
            return null;
        }
    }

    public async Task<int> GetAndSetPublishEnglishChapters(MangaEntry manga)
    {
        using var client = new HttpClient();
        client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0");

        // Only need metadata for counting, not full chapter list
        var url = $"https://api.mangadex.org/manga/{ExtractMangaIdFromUrl(manga.Link)}/feed?translatedLanguage[]=en&limit=1";

        try
        {
            var response = await client.GetAsync(url);
            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(content);

            if (doc.RootElement.TryGetProperty("total", out var totalElement))
            {
                return totalElement.GetInt32(); // number of EN chapters
            }
        }
        catch (Exception ex)
        {
            Log(LogLevel.error, $"Failed to fetch chapter count: {ex.Message}");
        }

        return -1; // fallback on error
    }

    public enum LogLevel
    {
        info,
        warning,
        error,
        test,
    }

    public void SetupLogData()
    {
        DateTime date = DateTime.Now;
        int year = date.Year;
        int month = date.Month;
        int day = date.Day;

        var yearPath = Path.Combine(APIRootPath, year.ToString());
        var monthPath = Path.Combine(yearPath, month.ToString());
        var logPath = Path.Combine(monthPath, $"{day}.txt");

        if (!Directory.Exists(yearPath))
        {
            Directory.CreateDirectory(yearPath);
        }

        if (!Directory.Exists(monthPath))
        {
            Directory.CreateDirectory(monthPath);
        }

        if (!File.Exists(logPath))
        {
            File.Create(logPath).Close();
            Log(LogLevel.info, $"Creating {logPath}");
        }

        currentLogFile = logPath;

        CurrentLogPath = logPath;

        try
        {
            if (File.Exists(disposableLogFile))
            {
                File.Delete(disposableLogFile);
                Log(LogLevel.info, $"Deleting {disposableLogFile}");
            }
        }
        catch (Exception e)
        {
            if (e.InnerException is System.IO.IOException && e.Message.Contains("because it is being used by another process"))
            {
                Output.WriteLine($"Failed to delete disposable log: Due to IOException file is being used by another proccess");
            }
            else
            {
                Output.WriteLine($"Failed to delete disposable log: {e.Message}");
            }
        }
    }

    public void Log(LogLevel lv, string message)
    {
        string level = "Info";

        string log = "";

        switch (lv)
        {
            case LogLevel.info:
                level = "Info";
                Output.WriteColored($"[{DateTime.Now}] ", Color.White);
                Output.WriteColored($"[{level.ToUpper()}] ", Color.Green);
                Output.WriteColored($"{message}{Environment.NewLine}", Color.White);

                log += $"[{DateTime.Now}] [{level.ToUpper()}] {message}";
                break;

            case LogLevel.warning:
                level = "Warning";
                Output.WriteColored($"[{DateTime.Now}] ", Color.White);
                Output.WriteColored($"[{level.ToUpper()}] {message}{Environment.NewLine}", Color.Yellow);

                log += $"[{DateTime.Now}] [{level.ToUpper()}] {message}";
                break;

            case LogLevel.error:
                level = "Error";
                Output.WriteColored($"[{DateTime.Now}] ", Color.White);
                Output.WriteColored($"[{level.ToUpper()}] {message}{Environment.NewLine}", Color.Red);

                log += $"[{DateTime.Now}] [{level.ToUpper()}] {message}";
                break;

            case LogLevel.test:
                level = "Test";
                Output.WriteColored($"[{DateTime.Now}] ", Color.White);
                Output.WriteColored($"[{level.ToUpper()}] {message}{Environment.NewLine}", Color.Blue);

                log += $"[{DateTime.Now}] [{level.ToUpper()}] {message}";
                break;
        }

        try
        {
            if (File.Exists(currentLogFile))
            {
                File.AppendAllLines(currentLogFile, new[] { log });
            }
            else
            {
                File.CreateText(currentLogFile);
                File.AppendAllLines(currentLogFile, new[] { log });
            }
        }
        catch (Exception e)
        {
            if (e.InnerException is IOException && e.Message.Contains("because it is being used by another process"))
            {
                Output.WriteLine($"Failed to write log: Due to IOException file is being used by another proccess");
            }
            else
            {
                Output.WriteLine($"Failed to write log: {e.Message}");
            }
        }

        Output.ChangeColor(Color.White);
    }

    public void ResetConfigData()
    {
        if (File.Exists(configPath))
        {
            Config config = new Config();
            JsonHandler.SerializeJsonFile<Config>(configPath, config);
            Log(LogLevel.info, "Config data reset to default");
        }
        else
        {
            Log(LogLevel.error, "Config data does not exist, cannot reset");
        }
    }
}

public class UserMangaEntry
{
    public string Title { get; set; }
    public string Link { get; set; }
    public float ChaptersRead { get; set; }
    public string Status { get; set; }
    public string Email { get; set; }
    public bool SendUpdates { get; set; }
}

[Serializable]
public class CachedManga
{
    public string managaId { get; set; }
    public string altTitle { get; set; }
    public List<string> genres { get; set; } = new List<string>();
    public string publicationStatus { get; set; }
    public string coverPhotoPath { get; set; }
    public int pulishedChapterCount { get; set; }
    public DateTime dateAdded { get; set; } = DateTime.Now;
}

[Serializable]
public class CachedMangaCollection
{
    public Dictionary<string, CachedManga> cache; //mangaID

    public CachedMangaCollection()
    {
        cache = new Dictionary<string, CachedManga>();
    }
}

[Serializable]
public class AccountToLibraries
{
    public Dictionary<string, string> link;
}

[Serializable]
public class LibraryFromID
{
    public Dictionary<string, Library> collection;
}

[Serializable]
public class Library
{
    public List<MangaEntry> entries;
}

[Serializable]
public class MangaEntry
{
    public string Title { get; set; }
    public string Link { get; set; }
    public string Status { get; set; }
    public float chaptersRead { get; set; } = 0;
    public bool favourite { get; set; } = false;
    [Newtonsoft.Json.JsonIgnore]
    public string CoverImageUrl { get; set; }
    [Newtonsoft.Json.JsonIgnore]
    public JObject? MangaJsonData { get; set; }
    [Newtonsoft.Json.JsonIgnore]
    public string? AltTitle { get; set; }
    [Newtonsoft.Json.JsonIgnore]
    public List<string> Genres { get; set; } = new List<string>();
    [Newtonsoft.Json.JsonIgnore]
    public string? publicationStatus { get; set; }
    [Newtonsoft.Json.JsonIgnore]
    public int pulishedChapterCount { get; set; }
}

[Serializable]
public class Account
{
    public string ID;
    public string email;
    public string userName;
    public string password;
    public string salt;
    public bool sendUpdates;
}

[Serializable]
public class Config
{
    public string mangaRootStoragePath = "";
    public int rateLimit = 40; //amount of request to certain endpoint per min
    public string host = "";
    public string port = "";
    public int rateLimitTimeout = 1; //in mins
    public int apiRefreshTime = 20; //in mins
    public string backupSourceDir = "";
    public string backupDestinationDir = "";
    public string fromEmail = "";
    public string googleAPPpass = "";
    public int smtpPort = 25;
    public int cacheExpireDays = 10; // days after which cache expires

    public Config()
    {
        mangaRootStoragePath = "";
        rateLimit = 40;
        host = "";
        port = "";
        rateLimitTimeout = 1;
        apiRefreshTime = 20;
        backupSourceDir = "";
        backupDestinationDir = "";
        fromEmail = "";
        googleAPPpass = "";
        smtpPort = 25;
        cacheExpireDays = 10;
    }

}

[Serializable]
public class SQLConfig
{
    public bool useDB = false;
    public string IP = "";
    public string DB_Name = "";
    public string username = "";
    public string password = "";
}

public static class JsonHandler
{
    public static void SerializeJsonFile<T>(string filePath, T obj, bool append = false)
    {
        using var writer = new StreamWriter(filePath, append);
        writer.Write(JsonConvert.SerializeObject(obj));
    }

    public static T DeserializeJsonFile<T>(string filePath) where T : new()
    {
        if (!System.IO.File.Exists(filePath))
            return new T();

        using var reader = new StreamReader(filePath);
        return JsonConvert.DeserializeObject<T>(reader.ReadToEnd());
    }
}