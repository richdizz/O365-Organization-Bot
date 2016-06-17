using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OrgBot.Models
{
    [Serializable]
    public class User
    {
        public User() { }
        public User(string id, string displayName) { this.Id = id; this.DisplayName = displayName; }

        public string Id { get; set; }
        public string DisplayName { get; set; }
        public string IndentedDisplayName { get; set; }
        public User Manager { get; set; }
        public int OrgDepth { get; set; }
        public List<User> DirectReports { get; set; }
        
        public User Clone()
        {
            return new User(this.Id, this.DisplayName);
        }

        public override string ToString()
        {
            return this.DisplayName;
        }
    }
}
