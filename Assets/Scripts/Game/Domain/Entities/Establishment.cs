namespace Game.Domain.Entities
{
public sealed class Establishment
    {
        public string Id             { get; }
        public string Name           { get; }
        public string Description    { get; }
        public string Segment        { get; }   
        public int    RentCost        { get; }   
        public int    TicketCompat    { get; }   
        public int    CompetitionLevel{ get; }   
        public string Channels        { get; }   
        public bool   IsFixed         { get; }
        public bool   IsUnlocked      { get; }

        public Establishment(
            string id, string name, string description,
            string segment, int rentCost, int ticketCompat,
            int competitionLevel, string channels,
            bool isFixed, bool isUnlocked)
        {
            Id              = id;
            Name            = name;
            Description     = description;
            Segment         = segment;
            RentCost        = rentCost;
            TicketCompat    = ticketCompat;
            CompetitionLevel= competitionLevel;
            Channels        = channels;
            IsFixed         = isFixed;
            IsUnlocked      = isUnlocked;
        }
    }

}