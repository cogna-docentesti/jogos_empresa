namespace Game.Infrastructure.Session
{
    public static class PlayerSession
    {
        public static string SelectedEstablishmentId { get; private set; }
        public static string SelectedEstablishmentName { get; private set; }

        public static void SaveSelectedEstablishment(string establishmentId, string establishmentName)
        {
            SelectedEstablishmentId = establishmentId;
            SelectedEstablishmentName = establishmentName;
        }

        public static void Clear()
        {
            SelectedEstablishmentId = null;
            SelectedEstablishmentName = null;
        }
    }
}