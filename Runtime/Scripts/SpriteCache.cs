using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.U2D;
using TMPro;

namespace ABCUnity
{
    class SpriteCache
    {
        SpriteAtlas spriteAtlas;
        private TextMeshPro textPrefab;

        Dictionary<string, Sprite> spriteCache = new Dictionary<string, Sprite>();
        List<GameObject> objectPool = new List<GameObject>();
        private List<TextMeshPro> textPool = new List<TextMeshPro>();

        public Color color { get; set; } = Color.black;

        public SpriteCache(SpriteAtlas spriteAtlas, TextMeshPro textPrefab)
        {
            this.spriteAtlas = spriteAtlas;
            this.textPrefab = textPrefab;
        }

        public TextMeshPro GetTextObject()
        {
            if (textPool.Count > 0)
            {
                var text = textPool[textPool.Count - 1];
                textPool.RemoveAt(textPool.Count - 1);
                return text;
            }
            else
            {
                var text = GameObject.Instantiate(textPrefab);
                text.color = color;
                return text;
            }
        }

        public SpriteRenderer GetSpriteObject(string name)
        {
            var sprite = GetSprite(name);
            if (sprite == null)
                Debug.Log($"Warning: could not locate: {name}");

            if (objectPool.Count > 0)
            {
                var obj = objectPool[objectPool.Count - 1];
                objectPool.RemoveAt(objectPool.Count - 1);

                obj.name = name;
                var spriteRenderer = obj.GetComponent<SpriteRenderer>();
                spriteRenderer.sprite = sprite;

                return spriteRenderer;
            }

            return CreateSpriteObject(sprite, name);
        }

        public void ReturnObject(GameObject obj)
        {
            objectPool.Add(obj);
        }

        SpriteRenderer CreateSpriteObject(Sprite sprite, string name)
        {
            var spriteObj = new GameObject(name);
            var spriteRenderer = spriteObj.AddComponent<SpriteRenderer>();
            spriteRenderer.sprite = sprite;
            spriteRenderer.color = color;

            return spriteRenderer;
        }

        private Sprite GetSprite(string name)
        {
            Sprite sprite;
            if (spriteCache.TryGetValue(name, out sprite))
                return sprite;

            sprite = spriteAtlas.GetSprite(name);
            spriteCache[name] = sprite;

            return sprite;
        }
    }
}
