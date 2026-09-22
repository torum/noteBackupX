using AngleSharp;
using AngleSharp.Dom;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using noteBackupX.Models;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace noteBackupX.ViewModels;

public partial class MainWindowViewModel : ObservableObject
{
    public static string AppTitleVer => "noteBackupX v0.0.2.0";

    public bool IsWorking
    {
        get;
        set
        {
            if (field == value)
                return;

            field = value;

            OnPropertyChanged();

            Dispatcher.UIThread.Post(() =>
            {
                WorkingStateChanged?.Invoke(this, value);
            }, DispatcherPriority.Default);
        }
    }

    [ObservableProperty]
    public partial string StatusbarProgressMsg { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string StatusbarMsg { get; set; } = string.Empty;

    [ObservableProperty]
    public partial int StatusbarProgress { get; set; }

    [ObservableProperty]
    public partial int StatusbarProgressMax { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(GetNoteCommand))]
    public partial string SearchQuery { get; set; } = string.Empty;

    [ObservableProperty]
    public partial ObservableCollection<NoteData> Entries { get; set; } = [];

    [ObservableProperty]
    public partial NoteData? SelectedItem { get; set; }

    public event EventHandler<bool>? WorkingStateChanged;

    private readonly HttpClient _httpClient;

    private readonly CancellationTokenSource _cts = new();

    private string _totalCount = string.Empty;

    private readonly string _envMyDocFolder = System.Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);

    private readonly string _backupFolder;

    public MainWindowViewModel()
    {
        _httpClient = new HttpClient();

        StatusbarProgressMax = 100;
        StatusbarProgress = 0;

        _backupFolder = System.IO.Path.Combine(_envMyDocFolder, "noteBackupX");
        System.IO.Directory.CreateDirectory(_backupFolder);
    }

    public void OnWindowClosing(object? sender, CancelEventArgs e)
    {
        _cts.Cancel();

        _cts.Dispose();
    }

    #region == Methods ==

    private async Task SaveImagesToFilesAsync(AngleSharp.Dom.IElement e, string id)
    {
        var imgs = e.QuerySelectorAll("img");
        if (imgs is null)
        {
            return;
        }

        foreach (var img in imgs)
        {
            var srcUri = img.GetAttribute("src");
            if (srcUri is null)
            {
                continue;
            }

            if (string.IsNullOrEmpty(srcUri))
            {
                continue;
            }

            //Debug.WriteLine($"Src = {src}");

            try
            {
                Uri uri = new(srcUri);
                var result = await DownloadImageAsync(uri.AbsoluteUri,id);

                if (!string.IsNullOrEmpty(result))
                {
                    img.SetAttribute("src", result);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
                continue;
            }

        }
    }

    private async Task<string?> DownloadImageAsync(string url, string id)
    {
        if (string.IsNullOrEmpty(url))
        {
            return null;
        }

        if (string.IsNullOrEmpty(id))
        {
            return null;
        }

        using HttpResponseMessage response = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
        response.EnsureSuccessStatusCode();

        Uri uri = new Uri(url);
        string extension = Path.GetExtension(uri.AbsolutePath);

        if (string.IsNullOrEmpty(extension))
        {
            string? mimeType = response.Content.Headers.ContentType?.MediaType;
            extension = mimeType switch
            {
                "image/jpeg" => ".jpg",
                "image/png" => ".png",
                "image/gif" => ".gif",
                "image/webp" => ".webp",
                _ => ".jpg" // default fallback
            };
        }

        byte[] imageBytes = await response.Content.ReadAsByteArrayAsync();

        if (imageBytes.Length <= 0)
        {
            return null;
        }

        string filename = $"{Guid.NewGuid()}{extension}";
        var newFileRelativePath = System.IO.Path.Combine(_backupFolder, id);
        System.IO.Directory.CreateDirectory(newFileRelativePath);
        newFileRelativePath = System.IO.Path.Combine(newFileRelativePath, filename);
        var fullPath = System.IO.Path.Combine(_backupFolder, newFileRelativePath); 

        await File.WriteAllBytesAsync(fullPath, imageBytes, _cts.Token);
        //if (File.Exists())
        return newFileRelativePath;
    }

    private async Task SaveNoteToFileAsync(NoteData ndata)
    {
        try
        {
            var destFilename = System.IO.Path.Combine(_backupFolder, ndata.Id + ".html");

            await System.IO.File.WriteAllTextAsync(destFilename, WrapHtmlContent(ndata.HtmlPageData), _cts.Token);

            if (_cts.IsCancellationRequested)
            {
                return;
            }

            ndata.Status = "保存済み";
            ndata.IsSaved = true;
        }
        catch (Exception ex)
        {
            StatusbarMsg = $"ファイル保存でエラー：{ex.Message}";
            ndata.Status = $"保存エラー";
            ndata.IsSaved = false;
            Debug.WriteLine(ex);
            return;
        }
    }

    private static string WrapHtmlContent(string source, string? styles = null)
    {
        styles ??= @"
/*
::-webkit-scrollbar { width: 17px; height: 3px;}
::-webkit-scrollbar-button {  background-color: #666; }
::-webkit-scrollbar-track {  background-color: #646464; box-shadow: 0 0 4px #aaa inset;}
::-webkit-scrollbar-track-piece { background-color: #212121;}
::-webkit-scrollbar-thumb { height: 50px; background-color: #666;}
::-webkit-scrollbar-corner { background-color: #646464;}}
::-webkit-resizer { background-color: #666;}
*/
body {
	
	line-height: 1.75em;
	font-size: 12px;
	background-color: #fff;
	color: #000;
}

p {
	font-size: 12px;
}

h1 {
	font-size: 30px;
	line-height: 34px;
}

h2 {
	font-size: 20px;
	line-height: 25px;
}

h3 {
	font-size: 16px;
	line-height: 27px;
	padding-top: 15px;
	padding-bottom: 15px;
	border-bottom: 1px solid #D8D8D8;
	border-top: 1px solid #D8D8D8;
}

hr {
	height: 1px;
	background-color: #d8d8d8;
	border: none;
	width: 100%;
	margin: 0px;
}

/*
a[href] {
	color: #1e8ad6;
}

a[href]:hover {
	color: #3ba0e6;
}


img {
    width: 160;
    height: auto;
    float: left;
    margin: 6px 12px 12px 6px;
}
*/

li {
	line-height: 1.5em;
}
                ";

        return String.Format(
            @"<html>
                    <head>
                        <meta http-equiv='Content-Type' content='text/html; charset=utf-8' />

                        <!-- saved from url=(0014)about:internet -->

                        <style type='text/css'>
                            body {{ font: 10pt verdana; color: #000; background: #fff; }}
                            table, td, th, tr {{ border: 1px solid black; border-collapse: collapse; }}
                        </style>

                        <!-- Custom style sheet -->
                        <style type='text/css'>{1}</style>
                    </head>
                    <body>{0}</body>
                </html>",
            source, styles);
    }

    private async Task GetNotePageAsync(NoteData ndata)
    {
        StatusbarMsg = ndata.Url;

        var HTTPResponse = await _httpClient.GetAsync(ndata.Url,_cts.Token);
        if (_cts.IsCancellationRequested)
        {
            return;
        }

        //Use the default configuration for AngleSharp
        var config = AngleSharp.Configuration.Default;

        //Create a new context for evaluating webpages with the given config
        var context = AngleSharp.BrowsingContext.New(config);

        //Source
        var source = await HTTPResponse.Content.ReadAsStreamAsync(_cts.Token);
        if (_cts.IsCancellationRequested)
        {
            return;
        }

        //Create a virtual request to specify the document to load (here from our fixed string)
        var document = await context.OpenAsync(req => req.Content(source),_cts.Token);
        if (_cts.IsCancellationRequested)
        {
            return;
        }

        var elements = document.QuerySelectorAll("div");
        foreach (var e in elements)
        {
            var re = e.GetAttribute("data-name");
            if (!string.IsNullOrEmpty(re))
            {
                if (re?.ToUpper() == "BODY")
                {
                    //Debug.WriteLine(ndata.HtmlPageData);

                    // Clear previously saved images.
                    var entryFolderPath = System.IO.Path.Combine(_backupFolder, ndata.Id);
                    if (Directory.Exists(entryFolderPath))
                    {
                        //Debug.WriteLine($"Deleting folder: {entryFolderPath}");
                        Directory.Delete(entryFolderPath, true);
                    }

                    await SaveImagesToFilesAsync(e, ndata.Id);
                    if (_cts.IsCancellationRequested)
                    {
                        IsWorking = false;
                        return;
                    }

                    ndata.HtmlPageData = e.InnerHtml;

                    await SaveNoteToFileAsync(ndata);
                    if (_cts.IsCancellationRequested)
                    {
                        IsWorking = false;
                        return;
                    }
                }
            }
        }

        await Task.Delay(1000);
    }

    private async Task<ObservableCollection<NoteData>?> GetNoteListAsync(string noteaddr)
    {
        var nlist = new ObservableCollection<NoteData>();

        return await GetNoteListRecursiveAsync(noteaddr, nlist, 1);
    }

    private async Task<ObservableCollection<NoteData>?> GetNoteListRecursiveAsync(string noteaddr, ObservableCollection<NoteData> nlist, int pageNum)
    {
        var HTTPResponse = await _httpClient.GetAsync(noteaddr + "&page=" + pageNum.ToString(), _cts.Token);

        if (_cts.IsCancellationRequested)
        {
            return null;
        }

        if (!HTTPResponse.IsSuccessStatusCode)
        {
            Debug.WriteLine("FailedStatusCode" + HTTPResponse.StatusCode.ToString());
            StatusbarMsg = "記事リストが見つかりませんでした。note IDをご確認ください。";
            return null;
        }

        //Debug.WriteLine("SuccessStatusCode");

        if (HTTPResponse.Content == null)
        {
            Debug.WriteLine("HTTPResponse.Content == null");
            StatusbarMsg = "なんらかの原因で記事リストを取得出来ませんでした。";
            return null;
        }

        var contenTypeString = HTTPResponse.Content.Headers.GetValues("Content-Type").FirstOrDefault();

        if (string.IsNullOrEmpty(contenTypeString))
        {
            Debug.WriteLine("- Content-Type header is null.");
            StatusbarMsg = "なんらかの原因で記事リストを取得出来ませんでした。";
            return null;
        }

        //Debug.WriteLine(string.Format("- Content-Type header is {0}", contenTypeString));

        if (!contenTypeString.StartsWith("application/json"))
        {
            Debug.WriteLine($"- Content-Type header is not json but {contenTypeString}");
            StatusbarMsg = "なんらかの原因で記事リストを取得出来ませんでした。";
            return null;
        }

        var source = await HTTPResponse.Content.ReadAsStreamAsync();
        //var notes = new ObservableCollection<NoteData>();
        var unknownObject = JsonDocument.Parse(source);

        var data = unknownObject.RootElement.GetProperty("data");
        var totalCount = data.GetProperty("totalCount");
        _totalCount = totalCount.ToString();
        var isLastPage = data.GetProperty("isLastPage");

        //Debug.WriteLine($"Total count: {totalCount}");

        var contents = data.GetProperty("contents");

        foreach (var content in contents.EnumerateArray())
        {
            NoteData ndata = new()
            {
                Id = content.GetProperty("id").ToString(),
                Title = content.GetProperty("name").ToString(),
                PubDateTime = content.GetProperty("publishAt").ToString(),
                Url = content.GetProperty("noteUrl").ToString(),
                Status = "未取得"
            };

            try
            {
                ndata.PubDateTime = DateTime.Parse(ndata.PubDateTime, null, System.Globalization.DateTimeStyles.RoundtripKind).ToString();

            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
            }

            if (!string.IsNullOrEmpty(ndata.Id))
            {
                nlist.Add(ndata);
            }
        }

        //Debug.WriteLine(isLastPage.ToString());
        if (isLastPage.ToString().Equals("false", StringComparison.CurrentCultureIgnoreCase))
        {
            pageNum++;
            var result = await GetNoteListRecursiveAsync(noteaddr + "&page=" + pageNum.ToString(), nlist, pageNum);
            
            if (_cts.IsCancellationRequested)
            {
                return null;
            }

            if (result is not null)
            {
                nlist = result;
            }
        }

        if (nlist.Count <= 0)
        {
            StatusbarMsg = "記事数が0で返りました。";
        }

        return nlist;
    }

    #endregion

    #region == Commands ==

    [RelayCommand(CanExecute = nameof(GetNoteCommand_CanExecute))]
    private async Task GetNote()
    {
        //"https://note.com/api/v2/creators/torum/contents?kind=note"
        const string jsonaddr1 = "https://note.com/api/v2/creators/";
        const string jsonaddr2 = "/contents?kind=note";

        if (string.IsNullOrEmpty(SearchQuery.Trim()))
        {
            return;
        }

        Entries.Clear();
        StatusbarProgressMsg = "";
        StatusbarMsg = "";

        IsWorking = true;

        var addr = jsonaddr1 + SearchQuery.Trim() + jsonaddr2;

        StatusbarMsg = $"一覧取得中・・・";
        var ndataList = await GetNoteListAsync(addr);

        if (_cts.IsCancellationRequested)
        {
            IsWorking = false;
            return;
        }

        if (ndataList is null)
        {
            IsWorking = false;
            return;
        }

        Entries = ndataList;

        StatusbarProgressMax = Entries.Count;
        StatusbarProgressMsg = "0/" + _totalCount;
        StatusbarProgress = 0;
        StatusbarMsg = "";
        
        int nnn = 0;
        foreach (var ndata in Entries)
        {
            await GetNotePageAsync(ndata);
            
            if (_cts.IsCancellationRequested)
            {
                IsWorking = false;
                return;
            }

            nnn++;
            var currCount = nnn.ToString();

            StatusbarProgressMsg = currCount + "/" + _totalCount;

            StatusbarMsg = "";
            StatusbarProgress = nnn;
        }

        IsWorking = false;

        StatusbarMsg = $"すべて完了 {_backupFolder}";
    }
    private bool GetNoteCommand_CanExecute()
    {
        if (SearchQuery is null)
            return false;

        if (string.IsNullOrEmpty(SearchQuery.Trim()))
            return false;
        else
            return true;
    }

    [RelayCommand(CanExecute = nameof(OpenPageCommand_CanExecute))]
    private async Task OpenPage(NoteData entry)
    {
        if (entry is null)
        {
            Debug.WriteLine("entry param is null. @OpenPage()");
            return;
        }

        if (string.IsNullOrEmpty(entry.Id))
        {
            Debug.WriteLine("entry ID is null. @OpenPage()");
            return;
        }

        if (!entry.IsSaved)
        {
            Debug.WriteLine("entry is not saved. @OpenPage()");
            return;
        }

        if (App.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop)
        {
            Debug.WriteLine("not IClassicDesktopStyleApplicationLifetime. @OpenPage()");
            return;
        }

        var destFilename = System.IO.Path.Combine(_backupFolder, entry.Id + ".html");

        var topLevel = TopLevel.GetTopLevel(desktop.MainWindow);
        if (topLevel is null)
        {
            Debug.WriteLine("topLevel is null. @OpenPage()");
            return;
        }
        var launcher = topLevel?.Launcher;
        if (launcher is null)
        {
            Debug.WriteLine("launcher is null. @OpenPage()");
            return;
        }

        if (topLevel is null)
        {
            Debug.WriteLine("topLevel is null. @OpenPage()");
            return;
        }
        IStorageFile? storageFile = await topLevel.StorageProvider.TryGetFileFromPathAsync(destFilename);
        if (storageFile is null)
        {
            Debug.WriteLine("storageFile is null. @OpenPage()");
            return;
        }

        await launcher.LaunchFileAsync(storageFile);
    }
    private static bool OpenPageCommand_CanExecute(NoteData entry)
    {
        if (entry is null)
            return false;

        if (entry.IsSaved)
            return true;
        else
            return false;
    }

    [RelayCommand(CanExecute = nameof(OpenFolderCommand_CanExecute))]
    private async Task OpenFolder(NoteData entry)
    {
        if (entry is null)
        {
            return;
        }

        if (string.IsNullOrEmpty(entry.Id))
        {
            return;
        }

        if (App.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop)
        {
            return;
        }

        var launcher = TopLevel.GetTopLevel(desktop.MainWindow)?.Launcher;
        if (launcher is null)
        {
            return;
        }

        var destFilename = System.IO.Path.Combine(_backupFolder, entry.Id + ".html");

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            // Avalonia's Launcher does not support selecting file in explorer on Windows. So roll our own.

            // This works currectly (select intended file) only if execute twice. Not sure why.
            Process.Start("explorer.exe", $"/select,\"{destFilename}\"");
            //Process.Start(new ProcessStartInfo(destFilename) { UseShellExecute = true });
            //

        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            // macOS uses 'open -R' to reveal in Finder
            Process.Start("open", $"-R \"{destFilename}\"");
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            // On Linux, there is no way to support selecting file in file manager.
            // So, just use Avalonia's Launcher to open the directory.
            if (await launcher.LaunchDirectoryInfoAsync(new DirectoryInfo(_backupFolder)))
            {
                // ok
            }
            else
            {
                // TODO: show error message?
                // This failes in Debug session when the app is attached VSCode from Snap packages on Linux. 
            }

            // Or this. (not tested)
            // Process.Start("xdg-open", dir);
        }

    }
    private static bool OpenFolderCommand_CanExecute(NoteData entry)
    {
        return true;
    }


    #endregion
}
