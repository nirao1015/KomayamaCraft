using UnityEngine;

namespace Game02
{
    internal static class Game02SpriteResolver
    {
        public static Sprite ResolveByName(string spriteName)
        {
            if (string.IsNullOrEmpty(spriteName))
            {
                return null;
            }

            Sprite[] loaded = Resources.FindObjectsOfTypeAll<Sprite>();
            for (int i = 0; i < loaded.Length; i++)
            {
                Sprite sprite = loaded[i];
                if (sprite != null && sprite.name == spriteName)
                {
                    return sprite;
                }
            }

            return null;
        }
    }
}
