using System;
using System.IO;
using VideoCreatorWPF.Models;
using VideoCreatorWPF.ViewModels;
using Xunit;

namespace VideoCreator.Tests
{
    public class PreviewViewModelTests
    {
        [Fact]
        public void 初期状態でCurrentVideoPathがnullであること()
        {
            var project = new ProjectViewModel();
            var previewVm = new PreviewViewModel(project);
            Assert.Null(previewVm.CurrentVideoPath);
        }

        [Fact]
        public void UpdateFrameで動画ブロックが見つかった場合CurrentVideoPathが設定されること()
        {
            var project = new ProjectViewModel();
            var track = new TimelineTrack { Name = "トラック1" };
            var videoBlock = new TimelineBlock
            {
                CharacterId = Guid.NewGuid(),
                StartFrame = 0,
                Duration = 90,
                Type = BlockType.Video,
                AudioPath = "C:\\test\\video.mp4"
            };
            track.Items.Add(videoBlock);
            project.Tracks.Add(track);
            var previewVm = new PreviewViewModel(project);
            previewVm.UpdateFrame(30);
            Assert.Equal("C:\\test\\video.mp4", previewVm.CurrentVideoPath);
            Assert.Equal(0, previewVm.CurrentVideoStartFrame);
        }

        [Fact]
        public void UpdateFrameで動画ブロックの範囲外ではCurrentVideoPathがnullになること()
        {
            var project = new ProjectViewModel();
            var track = new TimelineTrack { Name = "トラック1" };
            var videoBlock = new TimelineBlock
            {
                CharacterId = Guid.NewGuid(),
                StartFrame = 0,
                Duration = 90,
                Type = BlockType.Video,
                AudioPath = "C:\\test\\video.mp4"
            };
            track.Items.Add(videoBlock);
            project.Tracks.Add(track);
            var previewVm = new PreviewViewModel(project);
            previewVm.UpdateFrame(100);
            Assert.Null(previewVm.CurrentVideoPath);
        }

        [Fact]
        public void CurrentTextBlocksに音声ブロックが含まれないこと()
        {
            var project = new ProjectViewModel();
            var track = new TimelineTrack { Name = "トラック1" };

            var textBlock = new TimelineBlock
            {
                CharacterId = Guid.NewGuid(),
                StartFrame = 0,
                Duration = 90,
                Text = "テスト",
                Type = BlockType.Text
            };
            track.Items.Add(textBlock);

            var audioBlock = new TimelineBlock
            {
                CharacterId = Guid.NewGuid(),
                StartFrame = 0,
                Duration = 90,
                Text = "音声",
                Type = BlockType.Audio,
                AudioPath = "C:\\test\\audio.wav"
            };
            track.Items.Add(audioBlock);
            project.Tracks.Add(track);

            var previewVm = new PreviewViewModel(project);
            previewVm.UpdateFrame(30);

            Assert.Single(previewVm.CurrentTextBlocks);
            Assert.All(previewVm.CurrentTextBlocks, b => Assert.NotEqual(BlockType.Audio, b.Type));
        }
    }
}
