using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.Generic;

namespace noteBackupX.Models;

public partial class NoteData : ObservableObject
{
    [ObservableProperty]
    public partial string Status { get; set; } = string.Empty;
    [ObservableProperty]
    public partial string Title { get; set; } = string.Empty;
    [ObservableProperty]
    public partial string PubDateTime { get; set; } = string.Empty;
    public string Id = string.Empty;
    public string Url = string.Empty;
    public string HtmlPageData = string.Empty;

    [ObservableProperty]
    public partial bool IsSaved { get; set; }
}

