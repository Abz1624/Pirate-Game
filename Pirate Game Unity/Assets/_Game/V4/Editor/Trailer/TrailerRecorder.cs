using System.IO;
using UnityEditor;
using UnityEditor.Recorder;
using UnityEditor.Recorder.Encoder;
using UnityEditor.Recorder.Input;
using UnityEngine;

namespace RoguePirates.V4.Editor.Trailer
{
    /// <summary>
    /// Records trailer takes from the Game view with Unity Recorder, driven from the command line
    /// (<c>unity command eval</c>): Start a named take in Play mode, Stop it. Takes go to Recordings/Trailer/ (git-ignored),
    /// H.264 at a constant frame rate with game audio.
    /// </summary>
    public static class TrailerRecorder
    {
        static RecorderController _controller;
        static string _current;

        public static string Folder => Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Recordings", "Trailer"));

        public static bool IsRecording => _controller != null && _controller.IsRecording();

        public static string Start(string take, int width = 1920, int height = 1080, int fps = 60)
        {
            if (!EditorApplication.isPlaying) return "enter Play mode first";
            if (IsRecording) Stop();
            Directory.CreateDirectory(Folder);

            var settings = ScriptableObject.CreateInstance<RecorderControllerSettings>();
            var movie = ScriptableObject.CreateInstance<MovieRecorderSettings>();
            movie.name = take;
            movie.Enabled = true;
            movie.EncoderSettings = new CoreEncoderSettings
            {
                Codec = CoreEncoderSettings.OutputCodec.MP4,
                EncodingQuality = CoreEncoderSettings.VideoEncodingQuality.High,
            };
            movie.CaptureAudio = true;
            movie.ImageInputSettings = new GameViewInputSettings { OutputWidth = width, OutputHeight = height };
            movie.OutputFile = Path.Combine(Folder, take);
            settings.AddRecorderSettings(movie);
            settings.SetRecordModeToManual();
            settings.FrameRatePlayback = FrameRatePlayback.Constant;
            settings.FrameRate = fps;
            settings.CapFrameRate = true;

            _controller = new RecorderController(settings);
            _controller.PrepareRecording();
            var ok = _controller.StartRecording();
            _current = take;
            return ok ? $"recording {take} at {width}x{height}@{fps}" : "recorder refused to start";
        }

        public static string Stop()
        {
            if (_controller == null) return "not recording";
            _controller.StopRecording();
            _controller = null;
            var file = Path.Combine(Folder, _current + ".mp4");
            return $"stopped {_current} -> {file}";
        }
    }
}
