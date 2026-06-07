using AgenticBrowserAI.Models;
using System.Collections.Generic;

namespace AgenticBrowserAI.Database
{
    public class InMemoryDb
    {
        public List<TaskItem> Tasks = new();
        public List<StepLog> Logs = new();

        public void SaveTask(TaskItem task) => Tasks.Add(task);
        public void SaveLog(StepLog log) => Logs.Add(log);
    }
}
