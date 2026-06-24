using System.Collections.Generic;

namespace Game.Domain.Entities
{
   public sealed class LocationScreenData
    {
        public string RegionName                   { get; }
        public IReadOnlyList<Establishment> Establishments { get; }

        public LocationScreenData(
            string regionName,
            IReadOnlyList<Establishment> establishments)
        {
            RegionName     = regionName;
            Establishments = establishments;
        }
    }

}