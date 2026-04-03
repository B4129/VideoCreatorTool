using System;
using VideoCreatorWPF.Models;
using VideoCreatorWPF.ViewModels;
using Xunit;

namespace VideoCreator.Tests
{
    public class TimelineViewModelTests
    {
        [Fact]
        public void 初期状態でトラックが1つ以上存在すること()
        {
            var project = new ProjectViewModel();
            project.Tracks.Add(new TimelineTrack { Name = "トラック1" });
            var timelineVm = new TimelineViewModel(project);
            Assert.NotEmpty(timelineVm.Tracks);
        }

        [Fact]
        public void トラックを追加できること()
        {
            var project = new ProjectViewModel();
            project.Tracks.Add(new TimelineTrack { Name = "トラック1" });
            var timelineVm = new TimelineViewModel(project);
            timelineVm.AddTrackInternal();
            Assert.Equal(2, timelineVm.Tracks.Count);
        }

        [Fact]
        public void 再生時にIsPlayingがtrueになること()
        {
            var project = new ProjectViewModel();
            project.Tracks.Add(new TimelineTrack { Name = "トラック1" });
            var timelineVm = new TimelineViewModel(project);
            timelineVm.PlayCommand.Execute(null);
            Assert.True(timelineVm.IsPlaying);
            timelineVm.StopCommand.Execute(null);
        }

        [Fact]
        public void 停止時にIsPlayingがfalseになりCurrentFrameが0になること()
        {
            var project = new ProjectViewModel();
            project.Tracks.Add(new TimelineTrack { Name = "トラック1" });
            var timelineVm = new TimelineViewModel(project);
            timelineVm.PlayCommand.Execute(null);
            timelineVm.StopCommand.Execute(null);
            Assert.False(timelineVm.IsPlaying);
            Assert.Equal(0, timelineVm.CurrentFrame);
        }

        [Fact]
        public void ズームインでPixelsPerFrameが増加すること()
        {
            var project = new ProjectViewModel();
            project.Tracks.Add(new TimelineTrack { Name = "トラック1" });
            var timelineVm = new TimelineViewModel(project);
            var initial = timelineVm.PixelsPerFrame;
            timelineVm.ZoomInCommand.Execute(null);
            Assert.True(timelineVm.PixelsPerFrame > initial);
        }

        [Fact]
        public void ズームアウトでPixelsPerFrameが減少すること()
        {
            var project = new ProjectViewModel();
            project.Tracks.Add(new TimelineTrack { Name = "トラック1" });
            var timelineVm = new TimelineViewModel(project);
            var initial = timelineVm.PixelsPerFrame;
            timelineVm.ZoomOutCommand.Execute(null);
            Assert.True(timelineVm.PixelsPerFrame < initial);
        }
    }
}
