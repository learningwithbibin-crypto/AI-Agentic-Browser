using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace StarAIAgentUI.Models
{
    public enum MessageRole
    {
        User,
        Assistant,
        System
    }

    public class ChatMessage
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public MessageRole Role { get; set; }
        public string Content { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        // Optional: for your task blocks
        public List<TaskItem>? Tasks { get; set; }
    }

    public class TaskItem
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string State { get; set; } // pending, processing, executed, failed
    }

    public class Conversation
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public List<ChatMessage> Messages { get; set; } = new();
    }
}
