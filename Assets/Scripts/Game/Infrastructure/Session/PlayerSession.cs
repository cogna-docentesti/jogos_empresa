using System;
using UnityEngine;

namespace Game.Infrastructure.Session
{
    public static class PlayerSession
    {
        private const string IdentificationKey = "PlayerSession.Identification";

        [Serializable]
        private sealed class IdentificationData
        {
            public string studentName;
            public string studentRA;
            public string restaurantName;
        }

        public static string StudentName { get; private set; }
        public static string StudentRA { get; private set; }
        public static string RestaurantName { get; private set; }

        public static string SelectedEstablishmentId { get; private set; }
        public static string SelectedEstablishmentName { get; private set; }

        public static void SaveIdentification(string studentName, string studentRA, string restaurantName)
        {
            var data = new IdentificationData
            {
                studentName = studentName?.Trim(),
                studentRA = studentRA?.Trim(),
                restaurantName = restaurantName?.Trim()
            };

            if (!IsComplete(data))
                throw new ArgumentException("All identification fields must be filled in.");

            PlayerPrefs.SetString(IdentificationKey, JsonUtility.ToJson(data));
            PlayerPrefs.Save();
            ApplyIdentification(data);
        }

        public static bool TryLoadIdentification()
        {
            if (!PlayerPrefs.HasKey(IdentificationKey))
                return false;

            try
            {
                var data = JsonUtility.FromJson<IdentificationData>(PlayerPrefs.GetString(IdentificationKey));
                if (!IsComplete(data))
                {
                    Debug.LogWarning("[PlayerSession] Saved identification is incomplete. Registration is required.");
                    return false;
                }

                ApplyIdentification(data);
                return true;
            }
            catch (ArgumentException exception)
            {
                Debug.LogWarning("[PlayerSession] Failed to read saved identification: " + exception.Message);
                return false;
            }
        }

        private static bool IsComplete(IdentificationData data)
        {
            return data != null
                && !string.IsNullOrWhiteSpace(data.studentName)
                && !string.IsNullOrWhiteSpace(data.studentRA)
                && !string.IsNullOrWhiteSpace(data.restaurantName);
        }

        private static void ApplyIdentification(IdentificationData data)
        {
            StudentName = data.studentName.Trim();
            StudentRA = data.studentRA.Trim();
            RestaurantName = data.restaurantName.Trim();
        }

        public static void SaveSelectedEstablishment(string establishmentId, string establishmentName)
        {
            SelectedEstablishmentId = establishmentId;
            SelectedEstablishmentName = establishmentName;
        }

        // Clears runtime state only; resetting a game preserves the saved registration.
        public static void Clear()
        {
            StudentName = null;
            StudentRA = null;
            RestaurantName = null;
            SelectedEstablishmentId = null;
            SelectedEstablishmentName = null;
        }
    }
}
