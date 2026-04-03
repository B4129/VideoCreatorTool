using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text.Json;
using VideoCreatorWPF.Models;

namespace VideoCreatorWPF.Core
{
    /// <summary>
    /// プロジェクト情報管理的クラス
    /// </summary>
    public class Project : INotifyPropertyChanged
    {
        private string _projectName = "";
        private int _width = 1920;
        private int _height = 1080;
        private double _frameRate = 30;
        private int _totalFrames = 900; // Default 30 seconds
        private ObservableCollection<Character> _characters = new();
        private ObservableCollection<TimelineTrack> _tracks = new();
        private ObservableCollection<Scene> _scenes = new();
        private string _projectPath = "";
        private DateTime _createdAt;
        private DateTime _modifiedAt;

        public event PropertyChangedEventHandler? PropertyChanged;

        protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (Equals(field, value)) return false;
            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }

        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        /// <summary>
        /// プロジェクト名
        /// </summary>
        public string ProjectName
        {
            get => _projectName;
            set => SetProperty(ref _projectName, value);
        }

        /// <summary>
        /// 動画幅（ピクセル）
        /// </summary>
        public int Width
        {
            get => _width;
            set => SetProperty(ref _width, value);
        }

        /// <summary>
        /// 動画高さ（ピクセル）
        /// </summary>
        public int Height
        {
            get => _height;
            set => SetProperty(ref _height, value);
        }

        /// <summary>
        /// フレームレート（fps）
        /// </summary>
        public double FrameRate
        {
            get => _frameRate;
            set => SetProperty(ref _frameRate, value);
        }

        /// <summary>
        /// 総フレーム数
        /// </summary>
        public int TotalFrames
        {
            get => _totalFrames;
            set => SetProperty(ref _totalFrames, value);
        }

        /// <summary>
        /// キャラクター一覧
        /// </summary>
        public ObservableCollection<Character> Characters => _characters;

        /// <summary>
        /// タイムライントラック一覧
        /// </summary>
        public ObservableCollection<TimelineTrack> Tracks => _tracks;

        /// <summary>
        /// シーン一覧
        /// </summary>
        public ObservableCollection<Scene> Scenes => _scenes;

        /// <summary>
        /// プロジェクトファイルパス
        /// </summary>
        public string ProjectPath
        {
            get => _projectPath;
            set => SetProperty(ref _projectPath, value);
        }

        /// <summary>
        /// 作成日時
        /// </summary>
        public DateTime CreatedAt
        {
            get => _createdAt;
            set => SetProperty(ref _createdAt, value);
        }

        /// <summary>
        /// 更新日時
        /// </summary>
        public DateTime ModifiedAt
        {
            get => _modifiedAt;
            set => SetProperty(ref _modifiedAt, value);
        }

        /// <summary>
        /// 新規プロジェクト作成
        /// </summary>
        public static Project CreateNew(string projectName = "新規プロジェクト")
        {
            return new Project
            {
                ProjectName = projectName,
                Width = 1920,
                Height = 1080,
                FrameRate = 30,
                TotalFrames = 450, // 15 seconds at 30fps
                CreatedAt = DateTime.Now,
                ModifiedAt = DateTime.Now
            };
        }

        /// <summary>
        /// プロジェクトをJSONファイルに保存
        /// </summary>
        public void SaveToJson(string filePath)
        {
            var options = new JsonSerializerOptions { WriteIndented = true };
            var jsonData = JsonSerializer.Serialize(this, options);
            File.WriteAllText(filePath, jsonData, System.Text.Encoding.UTF8);
            ProjectPath = filePath;
            ModifiedAt = DateTime.Now;
        }

        /// <summary>
        /// JSONファイルからプロジェクトを読み込み
        /// </summary>
        public static Project? LoadFromJson(string filePath)
        {
            if (!File.Exists(filePath)) return null;

            var jsonData = File.ReadAllText(filePath, System.Text.Encoding.UTF8);
            var project = JsonSerializer.Deserialize<Project>(jsonData);

            if (project != null)
            {
                project.ProjectPath = filePath;
            }

            return project;
        }

        /// <summary>
        /// 現在時刻を更新時刻として設定
        /// </summary>
        public void UpdateModifiedTime()
        {
            ModifiedAt = DateTime.Now;
        }
    }
}
