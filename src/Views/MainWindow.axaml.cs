using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;
using noteBackupX.Models;
using noteBackupX.ViewModels;
using System;
using System.Diagnostics;
using System.IO;
using System.Xml;
using System.Xml.Linq;

namespace noteBackupX.Views;

public partial class MainWindow : Window
{
    public const string AppName = "noteBackupX";
    private const string AppDeveloper = "torum";
    private readonly string _envDataFolder = System.Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
    private readonly string _appDataFolder;
    private readonly string _appConfigFilePath;

    public MainWindow()
    {
        InitializeComponent();

        _appDataFolder = System.IO.Path.Combine(System.IO.Path.Combine(_envDataFolder, AppDeveloper), AppName);
        System.IO.Directory.CreateDirectory(_appDataFolder);
        _appConfigFilePath = System.IO.Path.Combine(_appDataFolder, AppName + ".config");

        this.Loaded += MainWindow_Loaded;
        this.KeyDown += Window_KeyDown;
    }

    private void MainWindow_Loaded(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (SearchQueryTextBox.Focusable)
        {
            this.SearchQueryTextBox.Focus();
        }

        if (this.DataContext is MainWindowViewModel vm)
        {
            this.Closing += vm.OnWindowClosing;
        }
    }

    private void Window_KeyDown(object? sender, Avalonia.Input.KeyEventArgs e)
    {
        if (e.Key == Avalonia.Input.Key.Escape)
        {
            // TODO: Cancel?
        }
    }

    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);

        if (this.DataContext is MainWindowViewModel vm)
        {
            vm.WorkingStateChanged += OnWorkingStateChanged;
        }

        LoadSettings();
    }

    private void LoadSettings()
    {
        if (!System.IO.File.Exists(_appConfigFilePath))
        {
            return;
        }

        var windowTop = 0;
        var windowLeft = 0;
        double windowHeight = 300;
        double windowWidth = 300;
        //var windowState = WindowState.Normal;

        try
        {
            XDocument xdoc = XDocument.Load(_appConfigFilePath);

            // Main Window element
            var mainWindow = xdoc.Root?.Element("MainWindow");
            if (mainWindow != null)
            {
                var hoge = mainWindow.Attribute("top");
                if (hoge != null)
                {
                    if (Int32.TryParse(hoge.Value, out var wY))
                    {
                        windowTop = wY;
                    }
                }

                hoge = mainWindow.Attribute("left");
                if (hoge != null)
                {
                    if (int.TryParse(hoge.Value, out var wX))
                    {
                        windowLeft = wX;
                    }
                }

                hoge = mainWindow.Attribute("height");
                if (hoge != null)
                {
                    if (!string.IsNullOrEmpty(hoge.Value))
                    {
                        windowHeight = double.Parse(hoge.Value);
                    }
                }

                hoge = mainWindow.Attribute("width");
                if (hoge != null)
                {
                    if (!string.IsNullOrEmpty(hoge.Value))
                    {
                        windowWidth = double.Parse(hoge.Value);
                    }
                }
                /*
                hoge = mainWindow.Attribute("state");
                if (hoge is not null)
                {
                    switch (hoge.Value)
                    {
                        case "FullScreen":
                            windowState = WindowState.FullScreen;
                            break;
                        case "Maximized":
                            // Since there is no restorebounds in AvaloniaUI, .....
                            windowState = WindowState.Maximized;
                            break;
                        case "Normal":
                        // Ignore minimized.
                        case "Minimized":
                            windowState = WindowState.Normal;
                            break;
                    }
                }
                this.WindowState = windowState;
                */
                this.WindowState = WindowState.Normal;

                if (windowWidth >= 300)
                {
                    this.Width = windowWidth;
                }
                if (windowHeight >= 300)
                {
                    this.Height = windowHeight;
                }

                // TODO: Consider multi-monitor setups. Validate if the position is actually visible on any of the screens.
                // Needed negative number (-9 for now) for some reason.
                // https://github.com/AvaloniaUI/Avalonia/discussions/21103
                if ((windowLeft >= -9) && (windowTop >= 0))
                {
                    this.Position = new PixelPoint(windowLeft, windowTop);
                }
                else
                {
                    Debug.WriteLine("Oops. !(windowLeft >= -9) && (windowTop >= 0)");
                    this.Position = new PixelPoint(0, 0);
                }
            }
        }
        catch (System.IO.FileNotFoundException)
        {
            System.Diagnostics.Debug.WriteLine("Error FileNotFoundException : " + _appConfigFilePath);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine("Error : " + ex + " while opening : " + _appConfigFilePath);
        }


    }

    protected override void OnClosing(WindowClosingEventArgs e)
    {
        if (this.DataContext is MainWindowViewModel vm)
        {
            vm.WorkingStateChanged -= OnWorkingStateChanged;
        }

        SaveSettings();

        base.OnClosing(e);
    }

    private void SaveSettings()
    {
        if (this.DataContext is not ViewModels.MainWindowViewModel)
        {
            return;
        }

        // Config xml file
        XmlDocument doc = new();
        var xmlDeclaration = doc.CreateXmlDeclaration("1.0", "UTF-8", null);
        doc.InsertBefore(xmlDeclaration, doc.DocumentElement);

        // Root Document Element
        var root = doc.CreateElement(string.Empty, "App", string.Empty);
        doc.AppendChild(root);

        XmlAttribute attrs;

        if (this.WindowState == WindowState.Normal)
        {
            // Main Window element
            var mainWindow = doc.CreateElement(string.Empty, "MainWindow", string.Empty);

            // Main Window attributes
            attrs = doc.CreateAttribute("height");
            attrs.Value = this.Height.ToString();
            mainWindow.SetAttributeNode(attrs);

            attrs = doc.CreateAttribute("width");
            attrs.Value = this.Width.ToString();
            mainWindow.SetAttributeNode(attrs);

            attrs = doc.CreateAttribute("top");
            attrs.Value = this.Position.Y.ToString();
            mainWindow.SetAttributeNode(attrs);

            attrs = doc.CreateAttribute("left");
            attrs.Value = this.Position.X.ToString();
            mainWindow.SetAttributeNode(attrs);

            attrs = doc.CreateAttribute("state");
            attrs.Value = this.WindowState switch
            {
                WindowState.FullScreen => "FullScreen",
                WindowState.Maximized => "Maximized",
                WindowState.Normal => "Normal",
                WindowState.Minimized => "Minimized",
                _ => attrs.Value
            };
            mainWindow.SetAttributeNode(attrs);

            // set MainWindow element to root.
            root.AppendChild(mainWindow);
        }

        try
        {
            if (!Directory.Exists(_appDataFolder))
            {
                Directory.CreateDirectory(_appDataFolder);
            }

            doc.Save(_appConfigFilePath);
        }
        catch (Exception ex)
        {
            Debug.WriteLine("Exception @SaveSettings: " + ex);
        }

    }

    private void OnWorkingStateChanged(object? sender, bool e)
    {
        if (e)
        {
            this.Cursor = new Cursor(StandardCursorType.AppStarting);
        }
        else
        {
            this.Cursor = new Cursor(StandardCursorType.Arrow);
        }
    }

    private void ListBox_DoubleTapped(object? sender, TappedEventArgs e)
    {
        if (e.Source is Visual visual && visual.FindAncestorOfType<ListBoxItem>() is { } item)
        {
            item.IsSelected = true;

            if (DataContext is not MainWindowViewModel vm) return;

            if (item.DataContext is not NoteData note) return;

            if (!vm.OpenPageCommand.CanExecute(note)) return;

            vm.OpenPageCommand.Execute(note);
        }
    }

}