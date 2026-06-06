using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using Unosquare.FFME;

namespace VideoSpike;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        // Must be set before FFME initializes
        Library.FFmpegDirectory = @"c:\ffmpeg\bin";
        
        InitializeComponent();
    }

    private async void Play_Click(object sender, RoutedEventArgs e)
    {
        await Media.Play();
    }

    private async void Pause_Click(object sender, RoutedEventArgs e)
    {
        await Media.Pause();
    }

    private async void Seek_Click(object sender, RoutedEventArgs e)
    {
        if (Media.IsOpen)
        {
            await Media.Seek(Media.Position + TimeSpan.FromSeconds(5));
        }
    }

    private async void Open_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            // Simulate an RTSP load
            await Media.Open(new Uri("rtsp://test-stream/"));
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message);
        }
    }
}