using System.Collections.Generic;

namespace KomayamaCraft
{
    public static class KomayamaWorkLock
    {
        private static readonly Dictionary<string, string> Owners = new();

        public static bool TryAcquire(string targetId, string workerId)
        {
            if (string.IsNullOrEmpty(targetId) || string.IsNullOrEmpty(workerId))
            {
                return false;
            }

            if (Owners.TryGetValue(targetId, out string owner) &&
                !string.IsNullOrEmpty(owner) &&
                owner != workerId)
            {
                return false;
            }

            Owners[targetId] = workerId;
            return true;
        }

        public static void Release(string targetId, string workerId)
        {
            if (string.IsNullOrEmpty(targetId) ||
                !Owners.TryGetValue(targetId, out string owner) ||
                owner != workerId)
            {
                return;
            }

            Owners.Remove(targetId);
        }

        public static void ReleaseAll(string workerId)
        {
            if (string.IsNullOrEmpty(workerId))
            {
                return;
            }

            var remove = new List<string>();
            foreach (KeyValuePair<string, string> pair in Owners)
            {
                if (pair.Value == workerId)
                {
                    remove.Add(pair.Key);
                }
            }

            for (int i = 0; i < remove.Count; i++)
            {
                Owners.Remove(remove[i]);
            }
        }
    }
}
