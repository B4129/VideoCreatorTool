namespace VideoCreatorWPF.Events
{
    public class ProjectCreatedEvent
    {
        public string ProjectName { get; }
        public ProjectCreatedEvent(string projectName)
        {
            ProjectName = projectName;
        }
    }

    public class ProjectLoadedEvent
    {
        public string ProjectPath { get; }
        public ProjectLoadedEvent(string projectPath)
        {
            ProjectPath = projectPath;
        }
    }

    public class ProjectSavedEvent
    {
        public string ProjectPath { get; }
        public ProjectSavedEvent(string projectPath)
        {
            ProjectPath = projectPath;
        }
    }

    public class TimelineSelectionChangedEvent
    {
        public int? SelectedBlockId { get; }
        public TimelineSelectionChangedEvent(int? selectedBlockId)
        {
            SelectedBlockId = selectedBlockId;
        }
    }
}
