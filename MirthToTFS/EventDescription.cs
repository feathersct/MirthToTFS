using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace MirthToTFS
{
    public class EventDescription : IEquatable<EventDescription>
    {
        public string Id { get; set; }
        public string Name { get; set; }


        public EventDescription(string description)
        {
            Regex idRegex = new Regex(@"id=[^,]*");
            Regex nameRegex = new Regex(@"name=[^,]*");
            Match idMatch = idRegex.Match(description);
            Match nameMatch = nameRegex.Match(description);

            if (idMatch.Success)
                this.Id = idMatch.Value.Replace("id=", "");
            if (nameMatch.Success)
                this.Name = nameMatch.Value.Replace("name=", "").Replace("]\n", "");

            //TODO: Get name to just be name, it appears that its grabbing a ] and \n at the end
        }

        public bool Equals(EventDescription other)
        {
            if (Id == other.Id && Name == other.Name)
                return true;

            return false;
        }

        public override int GetHashCode()
        {
            int hashId = Id == null ? 0 : Id.GetHashCode();
            int hashName = Name == null ? 0 : Name.GetHashCode();

            return hashId ^ hashName;
        }
    }
}
